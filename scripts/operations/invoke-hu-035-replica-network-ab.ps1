[CmdletBinding()]
param([string]$ExpectedSha, [string]$ImageRef, [string]$OutputDirectory,
    [switch]$ValidateOnly, [switch]$SelfTest)

$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
Set-StrictMode -Version Latest
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$project = Join-Path $repositoryRoot 'tests/Sgol.OperationsIntegrationTests/Sgol.OperationsIntegrationTests.csproj'
$utf8 = [Text.UTF8Encoding]::new($false)

function Assert-AbSha([string]$Expected, [string]$Actual) {
    if ($Expected -cnotmatch '\A[0-9a-f]{40}\z' -or $Expected -cne $Actual) { throw 'AB_SHA_MISMATCH' }
}

function Test-AbTrx([string]$Text, [int]$Total = 1, [switch]$AllowFailure, [hashtable]$Observation) {
    try {
        # Disable DTD/entity resolution even for synthetic artifacts.
        $settings = [Xml.XmlReaderSettings]::new()
        $settings.DtdProcessing = [Xml.DtdProcessing]::Prohibit
        $reader = [Xml.XmlReader]::Create([IO.StringReader]::new($Text), $settings)
        try { $document = [Xml.XmlDocument]::new(); $document.XmlResolver = $null; $document.Load($reader) }
        finally { $reader.Dispose() }
        $counts = $document.SelectSingleNode("/*[local-name()='TestRun']/*[local-name()='ResultSummary']/*[local-name()='Counters']")
        if ($null -eq $counts) { return $false }
        $executed = [int]$counts.GetAttribute('executed')
        $passed = [int]$counts.GetAttribute('passed')
        $failed = [int]$counts.GetAttribute('failed')
        if ($null -ne $Observation) {
            foreach ($key in @('total','executed','passed','failed')) {
                $number = 0
                if ([int]::TryParse($counts.GetAttribute($key), [ref]$number) -and $number -ge 0) { $Observation[$key] = $number }
            }
        }
        return [int]$counts.GetAttribute('total') -eq $Total -and $executed -eq $Total -and
            (($passed -eq $Total -and $failed -eq 0) -or ($AllowFailure -and $passed -eq 0 -and $failed -eq $Total))
    }
    catch { return $false }
}

function Get-AbInterpretation($Variants) {
    if ($Variants.Count -ne 2 -or @($Variants | Where-Object { $_.cleanup -ne 'CONFIRMED' }).Count -ne 0 -or
        @($Variants | Where-Object { $_.outcome -notin @('PASSED_INITIAL','PASSED_AFTER_RETRY','REPLICA_FAILED') }).Count -ne 0) {
        return 'INCONCLUSIVE'
    }
    $a = $Variants[0].outcome; $b = $Variants[1].outcome
    if ($a -eq 'PASSED_INITIAL' -and $b -eq 'PASSED_INITIAL') { return 'NOT_REPRODUCED' }
    if ($a -eq 'PASSED_AFTER_RETRY' -or $b -eq 'PASSED_AFTER_RETRY') { return 'RETRY_DEPENDENT' }
    $target = 'STAGE=REPLICATE_CLEAN:OPERATION=PUT_DESTINATION:TYPE=AmazonS3Exception:HTTP=500:S3CODE=InternalError'
    $aTarget = @($Variants[0].attempts | Where-Object { $_.diagnostic -like "*:$target" }).Count -gt 0
    $bTarget = @($Variants[1].attempts | Where-Object { $_.diagnostic -like "*:$target" }).Count -gt 0
    if ($a -eq 'REPLICA_FAILED' -and $aTarget -and $b -eq 'PASSED_INITIAL') { return 'MIGRATION_EFFECT_SUPPORTED' }
    if ($a -eq 'REPLICA_FAILED' -and $aTarget -and $b -eq 'REPLICA_FAILED' -and $bTarget) { return 'NO_MIGRATION_NOT_SUFFICIENT' }
    if ($a -eq 'PASSED_INITIAL' -and $b -eq 'REPLICA_FAILED' -and $bTarget) { return 'PREDICTION_CONTRADICTED' }
    return 'INCONCLUSIVE'
}

function Throw-AbPreparationFailure([string]$Code, [Nullable[int]]$ExitCode) {
    $failure = [InvalidOperationException]::new('AB_PREPARATION_FAILED')
    $failure.Data['AbCode'] = $Code
    if ($null -ne $ExitCode) { $failure.Data['AbExitCode'] = $ExitCode }
    throw $failure
}

function Set-AbPreparationStage($State,
    [ValidateSet('DIRECTORY_CREATE','NETWORK_CREATE','SOURCE_CONFIG_WRITE','DESTINATION_CONFIG_WRITE',
        'SOURCE_START','DESTINATION_START','SOURCE_BRIDGE_CONNECT','DESTINATION_BRIDGE_CONNECT',
        'SOURCE_PORT_READ','DESTINATION_PORT_READ','SOURCE_READY','DESTINATION_READY','PROVISION_ENVIRONMENT',
        'PROVISION_CREATE','SOURCE_ADMIN_REMOVE','DESTINATION_ADMIN_REMOVE','STORAGE_RESTART',
        'SOURCE_RESTART_READY','DESTINATION_RESTART_READY','PROVISION_VERIFY',
        'SOURCE_BRIDGE_DISCONNECT','DESTINATION_BRIDGE_DISCONNECT','FINAL_NETWORK_CREATE',
        'SOURCE_RENAME','DESTINATION_RENAME','SOURCE_FINAL_CONNECT','DESTINATION_FINAL_CONNECT',
        'SOURCE_BOOTSTRAP_DISCONNECT','DESTINATION_BOOTSTRAP_DISCONNECT',
        'SOURCE_FINAL_INSPECT','DESTINATION_FINAL_INSPECT','BOOTSTRAP_NETWORK_REMOVE')][string]$Stage) {
    $State.preparationStage = $Stage
}

function Get-AbPreparationFailure($State, [Management.Automation.ErrorRecord]$Record) {
    # Never serialize ErrorRecord/Exception or infer a code from its message.
    $type = $Record.Exception.GetType().Name
    if ($type -cnotin @('InvalidOperationException','RuntimeException','IOException','UnauthorizedAccessException',
        'CommandNotFoundException','ParameterBindingException','ArgumentException','XmlException')) { $type = 'UNKNOWN' }
    $code = $Record.Exception.Data['AbCode']
    if ($code -cnotin @('AB_DOCKER_FAILED','AB_STORAGE_NOT_READY','AB_PORT_INVALID','AB_PRIVATE_ADDRESS_INVALID',
        'TEST_EXIT_FAILED','TRX_MISSING','TRX_REJECTED')) { $code = 'UNKNOWN' }
    $exitCode = $Record.Exception.Data['AbExitCode']
    if ($exitCode -isnot [int]) { $exitCode = $null }
    return [ordered]@{ stage=$State.preparationStage; exceptionType=$type; code=$code; exitCode=$exitCode;
        phase=$State.preparationPhase; counters=$State.preparationCounters }
}

function Invoke-AbPair([scriptblock]$Prepare, [scriptblock]$Run, [scriptblock]$Cleanup, [scriptblock]$Publish) {
    $results = [Collections.Generic.List[object]]::new()
    foreach ($variant in @('A','B')) {
        $state = @{ variant=$variant; outcome='PREPARATION_FAILED'; attempts=@(); manifestVerified=$false;
            verifiedObjectCount=0; cleanup='UNCONFIRMED'; resources=@{}; preparationStage='UNKNOWN';
            preparationPhase=$null; preparationCounters=@{}; preparationFailure=$null }
        try {
            & $Prepare $state | Out-Null
            $state.outcome = 'EVIDENCE_INVALID'
            & $Run $state | Out-Null
        }
        catch { # Never emit a dynamic message from preparation, Docker, test runner or evidence parsing.
            if ($state.outcome -eq 'PREPARATION_FAILED' -and $null -eq $state.preparationFailure) {
                $state.preparationFailure = Get-AbPreparationFailure $state $_
            }
        }
        finally {
            try { if ((& $Cleanup $state) -eq $true) { $state.cleanup = 'CONFIRMED' } }
            catch { $state.cleanup = 'UNCONFIRMED' }
            $public = [ordered]@{ variant=$variant; outcome=$state.outcome; attempts=@($state.attempts);
                manifestVerified=$state.manifestVerified; verifiedObjectCount=$state.verifiedObjectCount; cleanup=$state.cleanup;
                preparationFailure=$state.preparationFailure }
            $results.Add($public)
            try { & $Publish $results.ToArray() | Out-Null }
            catch { throw 'AB_PUBLICATION_FAILED' }
        }
        # Even an ordinary replica failure must not prevent B, but uncertain cleanup must.
        if ($state.cleanup -ne 'CONFIRMED') { break }
    }
    return ,$results.ToArray()
}

function Invoke-AbDocker([string[]]$Arguments) {
    $output = @(& docker @Arguments 2>&1)
    $exitCode = $LASTEXITCODE
    if ($exitCode -ne 0) { Throw-AbPreparationFailure 'AB_DOCKER_FAILED' $exitCode }
    return ($output -join "`n").Trim()
}

function Wait-AbPort([int]$Port) {
    $deadline = [DateTimeOffset]::UtcNow.AddSeconds(30)
    do {
        $tcp = [Net.Sockets.TcpClient]::new()
        try {
            $task = $tcp.ConnectAsync('127.0.0.1', $Port)
            if ($task.Wait(1000) -and $tcp.Connected) { return }
        }
        catch { }
        finally { $tcp.Dispose() }
        Start-Sleep -Milliseconds 250
    } while ([DateTimeOffset]::UtcNow -lt $deadline)
    Throw-AbPreparationFailure 'AB_STORAGE_NOT_READY' $null
}

function Set-AbEnvironment([string]$Name, [string]$Value) {
    if (-not $savedEnvironment.ContainsKey($Name)) { $savedEnvironment[$Name] = [Environment]::GetEnvironmentVariable($Name) }
    [Environment]::SetEnvironmentVariable($Name, $Value)
}

function Invoke-AbTest([string]$Filter, [string]$Directory, [string]$Name) {
    # Provisioning output can contain raw SDK failures. Retain neither stdout nor stderr.
    & dotnet test $project --configuration Release --no-build --no-restore `
        -p:SGOL_TECH_OPS_PROVISIONING_TESTS=true -p:SGOL_HU035_AMD64_TESTS=true `
        --filter $Filter --results-directory $Directory --logger "trx;LogFileName=$Name" *> $null
    $exitCode = $LASTEXITCODE
    return $exitCode
}

function New-AbStorage($State) {
    $variant = $State.variant.ToLowerInvariant()
    $prefix = "$runPrefix-$variant"
    $private = Join-Path $privateRoot $variant
    Set-AbPreparationStage $State 'DIRECTORY_CREATE'
    [void][IO.Directory]::CreateDirectory($private)
    $final = "$prefix-private"; $bootstrap = "$prefix-bootstrap"
    $initial = if ($variant -eq 'a') { $bootstrap } else { $final }
    # Record intended resources BEFORE creation so a partial setup remains cleanable.
    $State.resources = @{ containers=@("$prefix-source-bootstrap","$prefix-destination-bootstrap","$prefix-source","$prefix-destination");
        networks=@($initial,$final) | Select-Object -Unique; directory=$private }
    Set-AbPreparationStage $State 'NETWORK_CREATE'
    [void](Invoke-AbDocker -Arguments @('network','create','--driver','bridge','--internal',$initial))
    $operational = @(
        @(
            @{ name='evidence-web'; credentials=@($identities.EVIDENCE); actions=@("Read:$sq","Write:$sq","List:$sq","Tagging:$sq","Read:$sc","Write:$sc","List:$sc","Tagging:$sc") },
            @{ name='replica-source'; credentials=@($identities.REPLICA_SOURCE); actions=@("Read:$sq","List:$sq","Read:$sc","List:$sc") }
        ),
        @(
            @{ name='backup'; credentials=@($identities.BACKUP); actions=@("Read:$mb","Write:$mb","List:$mb","Tagging:$mb") },
            @{ name='replica-destination'; credentials=@($identities.REPLICA_DESTINATION); actions=@("Read:$dq","Write:$dq","List:$dq","Tagging:$dq","Read:$dc","Write:$dc","List:$dc","Tagging:$dc","Read:$mb","Write:$mb","List:$mb","Tagging:$mb") }
        )
    )
    $ports = @(); $containers = @("$prefix-source-bootstrap","$prefix-destination-bootstrap")
    $configPaths = @((Join-Path $private 'source.json'),(Join-Path $private 'destination.json'))
    for ($index=0; $index -lt 2; $index++) {
        $admin = if ($index -eq 0) { 'SOURCE_ADMIN' } else { 'DESTINATION_ADMIN' }
        $config = @{ identities=@(@{ name=$admin; credentials=@($identities[$admin]); actions=@('Admin','Read','Write','List','Tagging') }) + $operational[$index] }
        $side = if ($index -eq 0) { 'SOURCE' } else { 'DESTINATION' }
        Set-AbPreparationStage $State "${side}_CONFIG_WRITE"
        [IO.File]::WriteAllText($configPaths[$index], ($config | ConvertTo-Json -Depth 8 -Compress), $utf8)
        $networkAlias = if ($index -eq 0) { 'sgol-tech-ops-s3-source' } else { 'sgol-tech-ops-s3-destination' }
        Set-AbPreparationStage $State "${side}_START"
        [void](Invoke-AbDocker -Arguments @('run','--detach','--name',$containers[$index],'--network',$initial,'--network-alias',$networkAlias,
            '--publish','127.0.0.1::8333','--mount',"type=bind,source=$($configPaths[$index]),target=/run/sgol/s3.json,readonly",
            $seaweedImage,'mini','-dir=/data','-s3.config=/run/sgol/s3.json'))
    }
    foreach ($container in $containers) {
        $side = if ($container -eq $containers[0]) { 'SOURCE' } else { 'DESTINATION' }
        Set-AbPreparationStage $State "${side}_BRIDGE_CONNECT"
        [void](Invoke-AbDocker -Arguments @('network','connect','bridge',$container))
    }
    foreach ($container in $containers) {
        $side = if ($container -eq $containers[0]) { 'SOURCE' } else { 'DESTINATION' }
        Set-AbPreparationStage $State "${side}_PORT_READ"
        $binding = Invoke-AbDocker -Arguments @('port',$container,'8333/tcp')
        if ($binding -notmatch '\A127\.0\.0\.1:(\d+)\z') { Throw-AbPreparationFailure 'AB_PORT_INVALID' $null }
        $ports += [int]$Matches[1]
        Set-AbPreparationStage $State "${side}_READY"
        Wait-AbPort $ports[-1]
    }
    Set-AbPreparationStage $State 'PROVISION_ENVIRONMENT'
    Set-AbEnvironment 'SGOL_PROVISION_SOURCE_ENDPOINT' "http://127.0.0.1:$($ports[0])"
    Set-AbEnvironment 'SGOL_PROVISION_DESTINATION_ENDPOINT' "http://127.0.0.1:$($ports[1])"
    foreach ($phase in @('create','verify')) {
        if ($phase -eq 'verify') {
            # Same in-place File.WriteAllText update and docker restart as HU-035 provisioning.
            for ($index=0; $index -lt 2; $index++) {
                $side = if ($index -eq 0) { 'SOURCE' } else { 'DESTINATION' }
                Set-AbPreparationStage $State "${side}_ADMIN_REMOVE"
                [IO.File]::WriteAllText($configPaths[$index], (@{ identities=$operational[$index] } | ConvertTo-Json -Depth 8 -Compress), $utf8)
            }
            Set-AbPreparationStage $State 'STORAGE_RESTART'
            [void](Invoke-AbDocker -Arguments (@('restart') + $containers))
            for ($index=0; $index -lt $ports.Count; $index++) {
                $side = if ($index -eq 0) { 'SOURCE' } else { 'DESTINATION' }
                Set-AbPreparationStage $State "${side}_RESTART_READY"
                Wait-AbPort $ports[$index]
            }
        }
        Set-AbPreparationStage $State $(if ($phase -eq 'create') { 'PROVISION_CREATE' } else { 'PROVISION_VERIFY' })
        $State.preparationPhase = $phase
        $State.preparationCounters = @{}
        Set-AbEnvironment 'SGOL_PROVISION_PHASE' $phase
        $code = Invoke-AbTest 'FullyQualifiedName=Sgol.OperationsIntegrationTests.SyntheticEnvironmentProvisioningTests.ProvisionOrVerifySyntheticBucketsAndSeed|FullyQualifiedName=Sgol.OperationsIntegrationTests.SyntheticEnvironmentProvisioningTests.BackupIdentitySupportsConditionalPutGetAndHead' $private "$phase.trx"
        $trx = Join-Path $private "$phase.trx"
        if ($code -ne 0) { Throw-AbPreparationFailure 'TEST_EXIT_FAILED' $code }
        if (-not (Test-Path -LiteralPath $trx)) { Throw-AbPreparationFailure 'TRX_MISSING' $code }
        if (-not (Test-AbTrx ([IO.File]::ReadAllText($trx)) 2 -Observation $State.preparationCounters)) {
            Throw-AbPreparationFailure 'TRX_REJECTED' $code
        }
        $State.preparationPhase = $null
        $State.preparationCounters = @{}
    }
    foreach ($container in $containers) {
        $side = if ($container -eq $containers[0]) { 'SOURCE' } else { 'DESTINATION' }
        Set-AbPreparationStage $State "${side}_BRIDGE_DISCONNECT"
        [void](Invoke-AbDocker -Arguments @('network','disconnect','bridge',$container))
    }
    if ($variant -eq 'a') {
        Set-AbPreparationStage $State 'FINAL_NETWORK_CREATE'
        [void](Invoke-AbDocker -Arguments @('network','create','--driver','bridge','--internal',$final))
    }
    for ($index=0; $index -lt 2; $index++) {
        $name = if ($index -eq 0) { "$prefix-source" } else { "$prefix-destination" }
        $side = if ($index -eq 0) { 'SOURCE' } else { 'DESTINATION' }
        Set-AbPreparationStage $State "${side}_RENAME"
        [void](Invoke-AbDocker -Arguments @('container','rename',$containers[$index],$name))
        if ($variant -eq 'a') {
            $alias = if ($index -eq 0) { 'sgol-tech-ops-s3-source' } else { 'sgol-tech-ops-s3-destination' }
            Set-AbPreparationStage $State "${side}_FINAL_CONNECT"
            [void](Invoke-AbDocker -Arguments @('network','connect','--alias',$alias,$final,$name))
            Set-AbPreparationStage $State "${side}_BOOTSTRAP_DISCONNECT"
            [void](Invoke-AbDocker -Arguments @('network','disconnect',$bootstrap,$name))
        }
        Set-AbPreparationStage $State "${side}_FINAL_INSPECT"
        $inspection = (Invoke-AbDocker -Arguments @('container','inspect',$name) | ConvertFrom-Json)[0]
        $address = $inspection.NetworkSettings.Networks.$final.IPAddress
        if ($address -notmatch '^(10\.|192\.168\.|172\.(1[6-9]|2[0-9]|3[01])\.)') { Throw-AbPreparationFailure 'AB_PRIVATE_ADDRESS_INVALID' $null }
        $side = if ($index -eq 0) { 'Source' } else { 'Destination' }
        Set-AbEnvironment "Replica__${side}__Endpoint" "http://${address}:8333"
    }
    if ($variant -eq 'a') {
        Set-AbPreparationStage $State 'BOOTSTRAP_NETWORK_REMOVE'
        [void](Invoke-AbDocker -Arguments @('network','rm',$bootstrap))
    }
}

function Remove-AbStorage($State, [scriptblock]$Docker = { param($arguments) Invoke-AbDocker $arguments }) {
    $clean = $true
    try {
        $present = @((& $Docker -Arguments @('container','ls','--all','--format','{{.Names}}')) -split "`n")
        foreach ($name in $State.resources.containers) {
            if ($present -contains $name) { try { & $Docker -Arguments @('container','rm','--force','--volumes',$name) | Out-Null } catch { $clean=$false } }
        }
        $remaining = @((& $Docker -Arguments @('container','ls','--all','--format','{{.Names}}')) -split "`n")
        if (@($State.resources.containers | Where-Object { $remaining -contains $_ }).Count) { $clean=$false }
        $networks = @((& $Docker -Arguments @('network','ls','--format','{{.Name}}')) -split "`n")
        foreach ($name in $State.resources.networks) {
            if ($networks -contains $name) { try { & $Docker -Arguments @('network','rm',$name) | Out-Null } catch { $clean=$false } }
        }
        $remainingNetworks = @((& $Docker -Arguments @('network','ls','--format','{{.Name}}')) -split "`n")
        if (@($State.resources.networks | Where-Object { $remainingNetworks -contains $_ }).Count) { $clean=$false }
    }
    catch { $clean=$false }
    try {
        $path = [IO.Path]::GetFullPath($State.resources.directory)
        $allowed = [IO.Path]::GetFullPath($privateRoot) + [IO.Path]::DirectorySeparatorChar
        if (-not $path.StartsWith($allowed, [StringComparison]::Ordinal) -or
            [IO.Path]::GetFileName($path) -notin @('a','b')) { throw 'AB_CLEANUP_PATH_REJECTED' }
        if (Test-Path -LiteralPath $path) { Remove-Item -LiteralPath $path -Recurse -Force }
        if (Test-Path -LiteralPath $path) { $clean=$false }
    }
    catch { $clean=$false }
    return $clean
}

function Read-AbResult($State) {
    $variant = $State.variant
    $resultPath = Join-Path $publicRoot "$variant-result.json"
    Set-AbEnvironment 'SGOL_HU035_NETWORK_VARIANT' $variant
    Set-AbEnvironment 'SGOL_HU035_NETWORK_RESULT' $resultPath
    $exitCode = Invoke-AbTest 'FullyQualifiedName=Sgol.OperationsIntegrationTests.FunctionalRecoveryAmd64GateTests.FirstReplicaOnPreparedNetworkHasExactCompleteManifest' $publicRoot "$variant.trx"
    $trx = Join-Path $publicRoot "$variant.trx"
    if (-not (Test-Path -LiteralPath $resultPath) -or -not (Test-Path -LiteralPath $trx) -or
        -not (Test-AbTrx ([IO.File]::ReadAllText($trx)) -AllowFailure)) { throw 'AB_EVIDENCE_INVALID' }
    $result = [IO.File]::ReadAllText($resultPath) | ConvertFrom-Json
    if ($result.schemaVersion -ne 1 -or $result.variant -cne $variant -or $result.revision -cne $ExpectedSha -or
        $result.imageDigest -cne $ImageRef -or [DateTimeOffset]$result.scheduledFor -ne $slot -or
        $result.outcome -notin @('PASSED_INITIAL','PASSED_AFTER_RETRY','REPLICA_FAILED','MANIFEST_INVALID','MANIFEST_READ_FAILED','CANCELLED') -or
        @($result.attempts).Count -lt 1 -or @($result.attempts).Count -gt 3) { throw 'AB_EVIDENCE_INVALID' }
    $number = 0
    foreach ($attempt in $result.attempts) {
        $number++
        if ($attempt.attempt -ne $number -or $attempt.outcome -notin @('FAILED','SUCCEEDED','CANCELLED') -or
            [DateTimeOffset]$attempt.endedAt -lt [DateTimeOffset]$attempt.startedAt -or
            ($null -ne $attempt.diagnostic -and $attempt.diagnostic -cnotmatch '\A(?:UNEXPECTED_REPLICA_FAILURE|HU035_REPLICA_PREPARATION_FAILED:[A-Z_]+:STAGE=[A-Z_]+:OPERATION=[A-Z_]+:TYPE=[A-Za-z0-9_.-]{1,64}:HTTP=(?:[1-5][0-9]{2}|NONE):S3CODE=[A-Za-z0-9_.-]{1,64})\z')) { throw 'AB_EVIDENCE_INVALID' }
    }
    $passed = $result.outcome -in @('PASSED_INITIAL','PASSED_AFTER_RETRY')
    if ($passed -ne ($exitCode -eq 0) -or ($passed -and (-not $result.manifestVerified -or $result.verifiedObjectCount -ne 1 -or
        -not (Test-AbTrx ([IO.File]::ReadAllText($trx))) -or $result.attempts[-1].outcome -ne 'SUCCEEDED' -or
        ($result.outcome -eq 'PASSED_INITIAL' -and $number -ne 1) -or ($result.outcome -eq 'PASSED_AFTER_RETRY' -and $number -lt 2)))) { throw 'AB_EVIDENCE_INVALID' }
    $State.outcome=$result.outcome; $State.attempts=@($result.attempts)
    $State.manifestVerified=$result.manifestVerified; $State.verifiedObjectCount=$result.verifiedObjectCount
}

function Test-AbPure {
    $checks = 0
    $assertions = @{ count=0 }
    function Assert-Ab($Condition) {
        if (-not $Condition) { throw 'AB_SELF_TEST_FAILED' }
        $assertions.count++
    }
    $sha = 'a' * 40
    Assert-AbSha $sha $sha; $checks++
    foreach ($bad in @(('a'*39), ('A'*40), "$sha`n", '$(echo unsafe)', ('b'*40))) {
        $rejected=$false; try { Assert-AbSha $bad $sha } catch { $rejected=$true }; Assert-Ab $rejected; $checks++
    }
    foreach ($case in @(@(1,1,1,0,$true),@(0,0,0,0,$false),@(1,1,0,1,$false),@(1,0,0,0,$false),@(2,2,2,0,$false))) {
        $xml='<TestRun><ResultSummary><Counters total="{0}" executed="{1}" passed="{2}" failed="{3}" /></ResultSummary></TestRun>' -f $case[0..3]
        Assert-Ab ((Test-AbTrx $xml) -eq $case[4]); $checks++
    }
    Assert-Ab (-not (Test-AbTrx '')); Assert-Ab (-not (Test-AbTrx '<invalid')); $checks+=2
    Assert-Ab (Test-AbTrx '<TestRun><ResultSummary><Counters total="1" executed="1" passed="0" failed="1" /></ResultSummary></TestRun>' -AllowFailure); $checks++
    foreach ($mode in @('success','replicaFailure','prepareFailure','cleanupFailure','cleanupThrow')) {
        $calls=[Collections.Generic.List[string]]::new()
        $prepare={param($s) $calls.Add("prepare-$($s.variant)"); if ($mode -eq 'prepareFailure' -and $s.variant -eq 'A') { throw 'SENSITIVE_SENTINEL' }}
        $run={param($s) $calls.Add("run-$($s.variant)"); $s.outcome='PASSED_INITIAL'; if ($mode -eq 'replicaFailure' -and $s.variant -eq 'A') { $s.outcome='REPLICA_FAILED'; throw 'SENSITIVE_SENTINEL' }}
        $cleanup={param($s) $calls.Add("cleanup-$($s.variant)"); if ($mode -eq 'cleanupThrow') { throw 'SENSITIVE_SENTINEL' }; return $mode -ne 'cleanupFailure'}
        $publish={param($r) $calls.Add('publish'); Assert-Ab (-not (($r | ConvertTo-Json -Depth 8) -match 'SENSITIVE_SENTINEL'))}
        $results=Invoke-AbPair $prepare $run $cleanup $publish
        Assert-Ab (@($calls | Where-Object { $_ -eq 'cleanup-A' }).Count -eq 1)
        $blocked=$mode -in @('cleanupFailure','cleanupThrow')
        Assert-Ab ($results.Count -eq $(if ($blocked) {1} else {2}))
        Assert-Ab (@($calls | Where-Object { $_ -eq 'cleanup-B' }).Count -eq $(if ($blocked) {0} else {1}))
        Assert-Ab ($calls.IndexOf('cleanup-A') -lt $calls.IndexOf('publish'))
        if (-not $blocked) { Assert-Ab ($calls.IndexOf('cleanup-A') -lt $calls.IndexOf('prepare-B')) }
        if ($blocked -or $mode -eq 'prepareFailure') { Assert-Ab ((Get-AbInterpretation $results) -eq 'INCONCLUSIVE') }
        $checks+=6
    }
    $target='HU035_REPLICA_PREPARATION_FAILED:REPLICA_INFRASTRUCTURE_FAILED:STAGE=REPLICATE_CLEAN:OPERATION=PUT_DESTINATION:TYPE=AmazonS3Exception:HTTP=500:S3CODE=InternalError'
    foreach ($case in @(@('PASSED_INITIAL','PASSED_INITIAL','NOT_REPRODUCED'),@('REPLICA_FAILED','PASSED_INITIAL','MIGRATION_EFFECT_SUPPORTED'),
        @('REPLICA_FAILED','REPLICA_FAILED','NO_MIGRATION_NOT_SUFFICIENT'),@('PASSED_INITIAL','REPLICA_FAILED','PREDICTION_CONTRADICTED'),
        @('PASSED_AFTER_RETRY','PASSED_INITIAL','RETRY_DEPENDENT'),@('MANIFEST_INVALID','PASSED_INITIAL','INCONCLUSIVE'))) {
        $pair=@(@{outcome=$case[0];cleanup='CONFIRMED';attempts=@(@{diagnostic=$target})},@{outcome=$case[1];cleanup='CONFIRMED';attempts=@(@{diagnostic=$target})})
        Assert-Ab ((Get-AbInterpretation $pair) -eq $case[2]); $checks++
    }
    # Execute the real preparation/cleanup with a local command double; no Docker process starts.
    $fixtureRoot=Join-Path ([IO.Path]::GetTempPath()) ('sgol-ab-pure-'+[Guid]::NewGuid().ToString('N'))
    $privateRoot=Join-Path $fixtureRoot 'private'
    [void][IO.Directory]::CreateDirectory($privateRoot)
    $runPrefix='sgol-ab-pure'; $seaweedImage='synthetic-image'
    $sq='source-quarantine'; $sc='source-clean'; $dq='destination-quarantine'; $dc='destination-clean'; $mb='manifests'
    $identities=@{}; $savedEnvironment=@{}
    foreach ($name in @('SOURCE_ADMIN','DESTINATION_ADMIN','EVIDENCE','REPLICA_SOURCE','BACKUP','REPLICA_DESTINATION')) {
        $identities[$name]=@{accessKey='SENSITIVE_SENTINEL';secretKey='SENSITIVE_SENTINEL'}
    }
    $simulatedContainers=[Collections.Generic.HashSet[string]]::new(); [void]$simulatedContainers.Add('unrelated-container')
    $simulatedNetworks=[Collections.Generic.HashSet[string]]::new(); [void]$simulatedNetworks.Add('unrelated-network')
    $commands=[Collections.Generic.List[string]]::new(); $cleanupSimulation='normal'
    $preparationMode='success'; $failurePhase='create'
    function docker {
        $Arguments = [string[]]$args
        $script:LASTEXITCODE = 0
        $commands.Add($Arguments -join ' ')
        if ($preparationMode -eq 'dockerFailure' -and $Arguments[0] -eq 'network' -and $Arguments[1] -eq 'create') {
            $script:LASTEXITCODE = 125
            return 'SENSITIVE_SENTINEL'
        }
        if ($Arguments[0] -eq 'run') { [void]$simulatedContainers.Add($Arguments[[Array]::IndexOf($Arguments,'--name')+1]); return '' }
        if ($Arguments[0] -eq 'port') { return '127.0.0.1:12345' }
        if ($Arguments[0] -eq 'restart') { return '' }
        if ($Arguments[0] -eq 'container') {
            switch ($Arguments[1]) {
                'rename' { [void]$simulatedContainers.Remove($Arguments[2]); [void]$simulatedContainers.Add($Arguments[3]); return '' }
                'ls' { return $simulatedContainers -join "`n" }
                'rm' { if ($cleanupSimulation -eq 'throw') { throw 'SENSITIVE_SENTINEL' }; if ($cleanupSimulation -ne 'sticky') { [void]$simulatedContainers.Remove($Arguments[-1]) }; return '' }
                'inspect' {
                    $final=if ($Arguments[2].Contains('-a-')) {'sgol-ab-pure-a-private'} else {'sgol-ab-pure-b-private'}
                    return ConvertTo-Json -Depth 5 -InputObject @(@{NetworkSettings=@{Networks=@{$final=@{IPAddress='172.28.0.2'}}}})
                }
            }
        }
        if ($Arguments[0] -eq 'network') {
            switch ($Arguments[1]) {
                'create' { [void]$simulatedNetworks.Add($Arguments[-1]); return '' }
                'rm' { [void]$simulatedNetworks.Remove($Arguments[2]); return '' }
                'ls' { return $simulatedNetworks -join "`n" }
                'connect' { return '' }
                'disconnect' { return '' }
            }
        }
        throw 'AB_UNEXPECTED_SIMULATED_COMMAND'
    }
    function Wait-AbPort([int]$Port) { Assert-Ab ($Port -eq 12345) }
    $testCalls=[Collections.Generic.List[string]]::new()
    function dotnet {
        $arguments = [string[]]$args
        Assert-Ab ($arguments[0] -ceq 'test')
        Assert-Ab ($arguments[1] -ceq $project)
        Assert-Ab ($arguments -contains '--no-build' -and $arguments -contains '--no-restore')
        # PowerShell binds -p:value as two tokens for a function double (one for a native executable).
        Assert-Ab ((@($arguments | Where-Object { $_ -ceq '-p:' }).Count -eq 2) -and
            $arguments -contains 'SGOL_TECH_OPS_PROVISIONING_TESTS=true' -and $arguments -contains 'SGOL_HU035_AMD64_TESTS=true')
        Assert-Ab ($arguments[[Array]::IndexOf($arguments,'--configuration')+1] -ceq 'Release')
        $filter = $arguments[[Array]::IndexOf($arguments,'--filter')+1]
        Assert-Ab ($filter -ceq 'FullyQualifiedName=Sgol.OperationsIntegrationTests.SyntheticEnvironmentProvisioningTests.ProvisionOrVerifySyntheticBucketsAndSeed|FullyQualifiedName=Sgol.OperationsIntegrationTests.SyntheticEnvironmentProvisioningTests.BackupIdentitySupportsConditionalPutGetAndHead')
        $directory = $arguments[[Array]::IndexOf($arguments,'--results-directory')+1]
        $logger = $arguments[[Array]::IndexOf($arguments,'--logger')+1]
        Assert-Ab ($logger -ceq "trx;LogFileName=$env:SGOL_PROVISION_PHASE.trx")
        $testCalls.Add($env:SGOL_PROVISION_PHASE)
        $script:LASTEXITCODE = 0
        if ($env:SGOL_PROVISION_PHASE -eq $failurePhase) {
            if ($preparationMode -eq 'testFailure') { $script:LASTEXITCODE = 17; return 'SENSITIVE_SENTINEL' }
            if ($preparationMode -eq 'missingTrx') { return }
        }
        $xml = '<TestRun><ResultSummary><Counters total="2" executed="2" passed="2" failed="0" /></ResultSummary></TestRun>'
        if ($env:SGOL_PROVISION_PHASE -eq $failurePhase) {
            if ($preparationMode -eq 'invalidTrx') { $xml = '<broken SENSITIVE_SENTINEL' }
            if ($preparationMode -eq 'rejectedTrx') { $xml = '<TestRun><ResultSummary><Counters total="2" executed="1" passed="1" failed="0" secret="SENSITIVE_SENTINEL" /></ResultSummary></TestRun>' }
        }
        [IO.File]::WriteAllText((Join-Path $directory "$env:SGOL_PROVISION_PHASE.trx"), $xml)
    }
    try {
        $previousConfigs=$null
        foreach ($variant in @('A','B')) {
            $commands.Clear(); $state=@{variant=$variant;resources=@{}}
            New-AbStorage $state
            $configs=@([IO.File]::ReadAllText((Join-Path $state.resources.directory 'source.json')),
                [IO.File]::ReadAllText((Join-Path $state.resources.directory 'destination.json')))
            if ($null -ne $previousConfigs) { Assert-Ab (($configs -join "`n") -ceq ($previousConfigs -join "`n")) }
            $previousConfigs=$configs
            Assert-Ab (@($commands | Where-Object { $_ -like 'restart *' }).Count -eq 1)
            Assert-Ab (@($commands | Where-Object { $_ -like 'run *' }).Count -eq 2)
            Assert-Ab (@($commands | Where-Object { $_ -like 'network disconnect bridge *' }).Count -eq 2)
            Assert-Ab (@($commands | Where-Object { $_ -like 'network connect --alias *' }).Count -eq $(if ($variant -eq 'A') {2} else {0}))
            Assert-Ab (@($commands | Where-Object { $_ -like 'network disconnect *-bootstrap *' }).Count -eq $(if ($variant -eq 'A') {2} else {0}))
            Assert-Ab (Remove-AbStorage $state)
            Assert-Ab (-not (Test-Path -LiteralPath $state.resources.directory))
            Assert-Ab ($simulatedContainers.SetEquals([string[]]@('unrelated-container')))
            Assert-Ab ($simulatedNetworks.SetEquals([string[]]@('unrelated-network')))
            $checks+=9
        }
        foreach ($failure in @('sticky','throw')) {
            $state=@{variant='A';resources=@{}}
            New-AbStorage $state
            $cleanupSimulation=$failure
            Assert-Ab (-not (Remove-AbStorage $state)); $checks++
            $cleanupSimulation='normal'
            Assert-Ab (Remove-AbStorage $state); $checks++
        }
        # Exercise real preparation, both command boundaries, capture and original cleanup together.
        foreach ($failurePhase in @('create','verify')) {
            foreach ($preparationMode in @('testFailure','missingTrx','invalidTrx','rejectedTrx','dockerFailure')) {
                $testCalls.Clear()
                $cleanupCalls=[Collections.Generic.List[string]]::new()
                $prepare=${function:New-AbStorage}
                $run={param($s) throw 'UNEXPECTED_REPLICA_EXECUTION'}
                $cleanup={param($s)
                    # Overwrite the process status and provoke a later failure: neither replaces the first diagnostic.
                    $script:LASTEXITCODE=99
                    $cleanupCalls.Add($s.variant)
                    $done=Remove-AbStorage $s
                    Assert-Ab (-not (Test-Path -LiteralPath $s.resources.directory))
                    return $done
                }
                $publish={param($r) Assert-Ab (-not (($r | ConvertTo-Json -Depth 8) -match 'SENSITIVE_SENTINEL'))}
                $results=Invoke-AbPair $prepare $run $cleanup $publish
                Assert-Ab ($results.Count -eq 2)
                Assert-Ab (($cleanupCalls -join ',') -ceq 'A,B')
                Assert-Ab ($simulatedContainers.SetEquals([string[]]@('unrelated-container')))
                Assert-Ab ($simulatedNetworks.SetEquals([string[]]@('unrelated-network')))
                foreach ($result in $results) {
                    Assert-Ab ($result.outcome -ceq 'PREPARATION_FAILED' -and $result.cleanup -ceq 'CONFIRMED')
                    $diagnostic=$result.preparationFailure
                    Assert-Ab ((@($diagnostic.Keys | Sort-Object) -join ',') -ceq 'code,counters,exceptionType,exitCode,phase,stage')
                    Assert-Ab ($diagnostic.exceptionType -ceq 'InvalidOperationException')
                    $expectedStage=if ($preparationMode -eq 'dockerFailure') {'NETWORK_CREATE'} elseif ($failurePhase -eq 'create') {'PROVISION_CREATE'} else {'PROVISION_VERIFY'}
                    Assert-Ab ($diagnostic.stage -ceq $expectedStage)
                    $expectedCode=switch ($preparationMode) { 'testFailure' {'TEST_EXIT_FAILED'} 'missingTrx' {'TRX_MISSING'} 'dockerFailure' {'AB_DOCKER_FAILED'} default {'TRX_REJECTED'} }
                    Assert-Ab ($diagnostic.code -ceq $expectedCode)
                    $expectedExit=if ($preparationMode -eq 'dockerFailure') {125} elseif ($preparationMode -eq 'testFailure') {17} else {0}
                    Assert-Ab ($diagnostic.exitCode -eq $expectedExit)
                    if ($preparationMode -eq 'dockerFailure') { Assert-Ab ($null -eq $diagnostic.phase) }
                    else { Assert-Ab ($diagnostic.phase -ceq $failurePhase) }
                    if ($preparationMode -eq 'rejectedTrx') {
                        Assert-Ab ((@($diagnostic.counters.Keys | Sort-Object) -join ',') -ceq 'executed,failed,passed,total')
                        Assert-Ab ($diagnostic.counters.total -eq 2 -and $diagnostic.counters.executed -eq 1 -and $diagnostic.counters.passed -eq 1 -and $diagnostic.counters.failed -eq 0)
                    }
                    else { Assert-Ab ($diagnostic.counters.Count -eq 0) }
                }
                $expectedCalls=if ($preparationMode -eq 'dockerFailure') {''} elseif ($failurePhase -eq 'create') {'create,create'} else {'create,verify,create,verify'}
                Assert-Ab (($testCalls -join ',') -ceq $expectedCalls)
            }
        }
        $preparationMode='testFailure'; $failurePhase='create'
        $cleanup={param($s) [void](Remove-AbStorage $s); throw 'SENSITIVE_SENTINEL'}
        $results=Invoke-AbPair ${function:New-AbStorage} $run $cleanup $publish
        Assert-Ab ($results.Count -eq 1 -and $results[0].cleanup -ceq 'UNCONFIRMED')
        Assert-Ab ($results[0].preparationFailure.code -ceq 'TEST_EXIT_FAILED' -and $results[0].preparationFailure.exitCode -eq 17)
        $preparationMode='success'
        # Validate the actual result reader, including native exit status and TRX consistency.
        $publicRoot=Join-Path $fixtureRoot 'public'
        [void][IO.Directory]::CreateDirectory($publicRoot)
        $ExpectedSha='a'*40; $ImageRef='sha256:'+('b'*64)
        $slot=[DateTimeOffset]::new(2026,9,17,12,5,0,[TimeSpan]::Zero)
        function Invoke-AbTest([string]$Filter, [string]$Directory, [string]$Name) {
            Assert-Ab ($Filter -ceq 'FullyQualifiedName=Sgol.OperationsIntegrationTests.FunctionalRecoveryAmd64GateTests.FirstReplicaOnPreparedNetworkHasExactCompleteManifest')
            $passed=$resultMode -notin @('failed','cancelled')
            $count=if ($resultMode -eq 'retry') {2} else {1}
            $attempts=@(for ($index=1; $index -le $count; $index++) {
                @{attempt=$index;startedAt=$slot;endedAt=$slot;outcome=$(if ($passed -and $index -eq $count) {'SUCCEEDED'} else {'FAILED'});diagnostic=$null}
            })
            $outcome=if ($resultMode -eq 'retry') {'PASSED_AFTER_RETRY'} elseif ($resultMode -eq 'failed') {'REPLICA_FAILED'} elseif ($resultMode -eq 'cancelled') {'CANCELLED'} else {'PASSED_INITIAL'}
            $result=@{schemaVersion=1;variant='A';revision=$ExpectedSha;imageDigest=$ImageRef;scheduledFor=$slot;
                outcome=$outcome;manifestVerified=$passed;verifiedObjectCount=$(if ($passed) {1} else {0});attempts=$attempts}
            if ($resultMode -eq 'zeroObjects') {$result.verifiedObjectCount=0}
            if ($resultMode -eq 'wrongSlot') {$result.scheduledFor=$slot.AddHours(1)}
            if ($resultMode -eq 'wrongSha') {$result.revision='c'*40}
            if ($resultMode -eq 'wrongImage') {$result.imageDigest='sha256:'+('c'*64)}
            if ($resultMode -eq 'unsafe') {$attempts[0].diagnostic='SENSITIVE_SENTINEL endpoint=https://private.invalid'}
            [IO.File]::WriteAllText((Join-Path $Directory 'A-result.json'),($result | ConvertTo-Json -Depth 8))
            $trx=if ($passed) {'<TestRun><ResultSummary><Counters total="1" executed="1" passed="1" failed="0" /></ResultSummary></TestRun>'} else {'<TestRun><ResultSummary><Counters total="1" executed="1" passed="0" failed="1" /></ResultSummary></TestRun>'}
            if ($resultMode -eq 'zeroTests') {$trx='<TestRun><ResultSummary><Counters total="0" executed="0" passed="0" failed="0" /></ResultSummary></TestRun>'}
            if ($resultMode -eq 'invalidTrx') {$trx='<broken'}
            if ($resultMode -ne 'missingTrx') {[IO.File]::WriteAllText((Join-Path $Directory $Name),$trx)}
            return $(if ($passed) {0} else {1})
        }
        foreach ($resultMode in @('initial','retry','failed','cancelled','zeroObjects','wrongSlot','wrongSha','wrongImage','zeroTests','invalidTrx','missingTrx','unsafe')) {
            $readState=@{variant='A';outcome='EVIDENCE_INVALID';attempts=@();manifestVerified=$false;verifiedObjectCount=0}
            $accepted=$true
            try { Read-AbResult $readState } catch { $accepted=$false }
            Assert-Ab ($accepted -eq ($resultMode -in @('initial','retry','failed','cancelled')))
            if ($accepted) {
                Assert-Ab (($readState.attempts | ConvertTo-Json -Depth 8) -notmatch 'SENSITIVE_SENTINEL')
                if ($resultMode -eq 'retry') { Assert-Ab ($readState.outcome -eq 'PASSED_AFTER_RETRY') }
            }
            foreach ($leaf in @('A.trx','A-result.json')) {
                $file=Join-Path $publicRoot $leaf
                if (Test-Path -LiteralPath $file) { [IO.File]::Delete($file) }
            }
            $checks++
        }
        [IO.Directory]::Delete($publicRoot)
    }
    finally {
        foreach ($name in $savedEnvironment.Keys) { [Environment]::SetEnvironmentVariable($name,$savedEnvironment[$name]) }
        # Only these now-empty, explicitly created test directories; no recursive fallback.
        if (@([IO.Directory]::EnumerateFileSystemEntries($privateRoot)).Count -eq 0) {
            [IO.Directory]::Delete($privateRoot)
            [IO.Directory]::Delete($fixtureRoot)
        }
    }
    # Test the actual job guards, not an independent routing table.
    $workflow=[IO.File]::ReadAllText((Join-Path $repositoryRoot '.github/workflows/pull-request.yml'))
    foreach ($job in @('verify','hu035-network-ab')) {
        $match=[regex]::Match($workflow, '(?m)^  '+[regex]::Escape($job)+':\r?\n    if: github\.event_name == ''(?<event>[a-z_]+)''\r?$')
        Assert-Ab $match.Success
        $expected=if ($job -eq 'verify') {'pull_request'} else {'workflow_dispatch'}
        Assert-Ab ($match.Groups['event'].Value -ceq $expected)
        foreach ($event in @('pull_request','workflow_dispatch','push','create')) {
            Assert-Ab (($event -ceq $match.Groups['event'].Value) -eq ($event -ceq $expected)); $checks++
        }
    }
    Assert-Ab ($workflow.Contains('group: hu035-network-ab-manual'))
    Assert-Ab ($workflow.Contains('cancel-in-progress: false'))
    Assert-Ab (-not $workflow.Contains('${{ inputs.expected_sha }}' + '"'))
    # Preserve every original PR step verbatim, independent of the added event/job.
    $baseline=(& git -C $repositoryRoot show HEAD:.github/workflows/pull-request.yml) -join "`n"
    $baselineEnd=$baseline.IndexOf("`n  hu035-network-ab:")
    if ($baselineEnd -lt 0) { $baselineEnd=$baseline.Length }
    $existing=$baseline.Substring($baseline.IndexOf('    steps:'),$baselineEnd-$baseline.IndexOf('    steps:')).TrimEnd()
    $current=$workflow.Substring($workflow.IndexOf('    steps:'),$workflow.IndexOf("`n  hu035-network-ab:")-$workflow.IndexOf('    steps:')).Replace("`r`n","`n").TrimEnd()
    Assert-Ab ($current -ceq $existing)
    Write-Output "PASS: HU035 network A/B pure orchestration ($($assertions.count) assertions)."
}

if ($SelfTest) { Test-AbPure; return }
$actual = (& git -C $repositoryRoot rev-parse HEAD).Trim()
Assert-AbSha $ExpectedSha $actual
if (-not $IsLinux -or [Runtime.InteropServices.RuntimeInformation]::OSArchitecture -ne [Runtime.InteropServices.Architecture]::X64) { throw 'AB_NATIVE_AMD64_REQUIRED' }
$architecture=Invoke-AbDocker -Arguments @('info','--format','{{.Architecture}}')
if ($architecture -notin @('x86_64','amd64')) { throw 'AB_DOCKER_AMD64_REQUIRED' }
if ($ValidateOnly) { Write-Output 'PASS: exact SHA and native Linux AMD64.'; return }
if ($ImageRef -cnotmatch '\Asha256:[0-9a-f]{64}\z') { throw 'AB_IMAGE_INVALID' }
$image=Invoke-AbDocker -Arguments @('image','inspect','--format','{{.Architecture}}|{{index .Config.Labels "org.opencontainers.image.revision"}}',$ImageRef)
if ($image -cne "amd64|$ExpectedSha") { throw 'AB_IMAGE_IDENTITY_MISMATCH' }
$root=[IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $root) { throw 'AB_OUTPUT_MUST_BE_NEW' }
$temporaryRoot = if ($env:RUNNER_TEMP) { [IO.Path]::GetFullPath($env:RUNNER_TEMP) } else { [IO.Path]::GetTempPath() }
if (-not $root.StartsWith($temporaryRoot.TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar, [StringComparison]::Ordinal)) { throw 'AB_OUTPUT_MUST_BE_TEMPORARY' }
$publicRoot=Join-Path $root 'public'; $privateRoot=Join-Path $root 'private'
[void][IO.Directory]::CreateDirectory($publicRoot); [void][IO.Directory]::CreateDirectory($privateRoot)
$runPrefix='sgol-ab-'+[Guid]::NewGuid().ToString('N')
$seaweedImage='chrislusf/seaweedfs:4.45@sha256:fc9f76fa993ad69966ffeb2f65d0318fcae39c6f8e20cf68ef7b3a5cb97769e5'
$sq='sgol-staging-evidence-quarantine'; $sc='sgol-staging-evidence-clean'
$dq='sgol-staging-evidence-quarantine-replica'; $dc='sgol-staging-evidence-clean-replica'; $mb='sgol-staging-portable-backups'
$savedEnvironment=@{}; $identities=@{}
$now=[DateTimeOffset]::UtcNow
$slot=[DateTimeOffset]::new($now.Year,$now.Month,$now.Day,$now.Hour,5,0,[TimeSpan]::Zero)
if ($slot -gt $now) { $slot=$slot.AddHours(-1) }; $slot=$slot.AddHours(-2)
try {
    foreach ($name in @('SOURCE_ADMIN','DESTINATION_ADMIN','EVIDENCE','REPLICA_SOURCE','BACKUP','REPLICA_DESTINATION')) {
        $identities[$name]=@{ accessKey=[Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(16)).ToLowerInvariant(); secretKey=[Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(32)).ToLowerInvariant() }
        Set-AbEnvironment "SGOL_PROVISION_${name}_ACCESS_KEY" $identities[$name].accessKey
        Set-AbEnvironment "SGOL_PROVISION_${name}_SECRET_KEY" $identities[$name].secretKey
    }
    $values=@{ SGOL_PROVISION_SOURCE_QUARANTINE_BUCKET=$sq; SGOL_PROVISION_SOURCE_CLEAN_BUCKET=$sc;
        SGOL_PROVISION_DESTINATION_QUARANTINE_BUCKET=$dq; SGOL_PROVISION_DESTINATION_CLEAN_BUCKET=$dc; SGOL_PROVISION_MANIFEST_BUCKET=$mb;
        Replica__Source__QuarantineBucket=$sq; Replica__Source__CleanBucket=$sc; Replica__Destination__QuarantineBucket=$dq;
        Replica__Destination__CleanBucket=$dc; Replica__Destination__ManifestBucket=$mb; Replica__Destination__ManifestPrefix='objects/v1';
        Replica__MaximumAttempts='3'; Replica__TimeoutSeconds='60'; Replica__BatchSize='500'; SGOL_REVISION=$ExpectedSha; SGOL_IMAGE_DIGEST=$ImageRef;
        SGOL_HU035_NETWORK_AB='true'; SGOL_HU035_NETWORK_SLOT=$slot.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'") }
    foreach ($side in @('Source','Destination')) {
        $identity=if ($side -eq 'Source') {'REPLICA_SOURCE'} else {'REPLICA_DESTINATION'}
        $values["Replica__${side}__Region"]='us-east-1'; $values["Replica__${side}__AllowInsecureTransport"]='true'
        $values["Replica__${side}__AccessKey"]=$identities[$identity].accessKey; $values["Replica__${side}__SecretKey"]=$identities[$identity].secretKey
    }
    foreach ($item in $values.GetEnumerator()) { Set-AbEnvironment $item.Key $item.Value }
    $publish={ param($variants)
        $interpretation=Get-AbInterpretation $variants
        $summary=[ordered]@{ schemaVersion=1; revision=$ExpectedSha; imageDigest=$ImageRef; scheduledFor=$slot;
            experimentStatus=$(if ($interpretation -eq 'INCONCLUSIVE') {'INCONCLUSIVE'} else {'EXECUTED'});
            interpretation=$interpretation; hu035Accepted=$false;
            bNotRunReason=$(if ($variants.Count -eq 1 -and $variants[0].cleanup -ne 'CONFIRMED') {'A_CLEANUP_UNCONFIRMED'} else {$null});
            variants=@($variants) }
        [IO.File]::WriteAllText((Join-Path $publicRoot 'network-ab-summary.json'), ($summary | ConvertTo-Json -Depth 12), $utf8)
    }
    $variants=Invoke-AbPair ${function:New-AbStorage} ${function:Read-AbResult} ${function:Remove-AbStorage} $publish
    if (@($variants).Count -ne 2 -or @($variants | Where-Object { $_.cleanup -ne 'CONFIRMED' -or $_.outcome -notin @('PASSED_INITIAL','PASSED_AFTER_RETRY') }).Count) {
        throw 'AB_REPLICAS_NOT_BOTH_APPROVED_OR_EXPERIMENT_INCONCLUSIVE'
    }
    Write-Output 'PASS: both replica manifests verified; this does not accept HU-035.'
}
finally {
    foreach ($name in $savedEnvironment.Keys) { [Environment]::SetEnvironmentVariable($name,$savedEnvironment[$name]) }
}
