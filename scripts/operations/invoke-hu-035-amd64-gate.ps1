[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^sha256:[0-9a-f]{64}$')]
    [string]$ImageRef,

    [Parameter(Mandatory = $true)]
    [ValidateScript({ -not (Test-Path -LiteralPath $_ -PathType Leaf) })]
    [string]$GateRoot
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$expectedMigration = '20260914210503_AddRecoveryReconciliation'
$bootstrapNetworkName = 'sgol-staging_private'
$bootstrapContainers = @('sgol-tech-ops-postgres', 'sgol-tech-ops-s3-source', 'sgol-tech-ops-s3-destination')
$utf8WithoutBom = [Text.UTF8Encoding]::new($false)

function Test-AbsolutePath([string]$value) {
    if ([IO.Path]::DirectorySeparatorChar -eq '\') {
        return $value -match '^(?:[A-Za-z]:[\\/]|\\\\[^\\/]+[\\/][^\\/]+)'
    }
    return $value.StartsWith('/', [StringComparison]::Ordinal)
}

function Assert-DockerSuccess([string]$message) {
    if ($LASTEXITCODE -ne 0) { throw $message }
}

function Set-ProcessEnvironmentFromFile([string]$path) {
    foreach ($line in Get-Content -LiteralPath $path) {
        if ([string]::IsNullOrWhiteSpace($line) -or $line.StartsWith('#', [StringComparison]::Ordinal)) { continue }
        $parts = $line.Split('=', 2)
        if ($parts.Count -ne 2) { throw 'Synthetic runtime environment contains an invalid line.' }
        [Environment]::SetEnvironmentVariable($parts[0], $parts[1], 'Process')
    }
}

function Replace-ConnectionHost([string]$name, [string]$hostAddress) {
    $value = [Environment]::GetEnvironmentVariable($name, 'Process')
    if ([string]::IsNullOrWhiteSpace($value) -or $value -notmatch 'Host=sgol-tech-ops-postgres(?:;|$)') {
        throw "Synthetic connection setting is invalid: $name"
    }
    [Environment]::SetEnvironmentVariable($name,
        ($value -replace 'Host=sgol-tech-ops-postgres(?=;|$)', "Host=$hostAddress"), 'Process')
}

function Get-PrivateContainerAddress([string]$container, [string]$network) {
    $address = (& docker inspect --format `
        "{{(index .NetworkSettings.Networks `"$network`").IPAddress}}" $container).Trim()
    Assert-DockerSuccess "Could not resolve the isolated address for $container."
    if ($address -notmatch '^(?:10\.|192\.168\.|172\.(?:1[6-9]|2\d|3[01])\.)') {
        throw "$container is not attached to the expected private network."
    }
    return $address
}

function Set-RuntimeStorageEndpoints([string]$path, [string]$sourceAddress,
    [string]$destinationAddress) {
    $endpoints = @{
        'Evidence__Storage__Endpoint' = "http://$sourceAddress`:8333"
        'Backup__Storage__Endpoint' = "http://$destinationAddress`:8333"
        'Replica__Source__Endpoint' = "http://$sourceAddress`:8333"
        'Replica__Destination__Endpoint' = "http://$destinationAddress`:8333"
    }
    $replaced = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    $lines = foreach ($line in Get-Content -LiteralPath $path) {
        $parts = $line.Split('=', 2)
        if ($parts.Count -eq 2 -and $endpoints.ContainsKey($parts[0])) {
            [void]$replaced.Add($parts[0])
            "$($parts[0])=$($endpoints[$parts[0]])"
        }
        else { $line }
    }
    if ($replaced.Count -ne $endpoints.Count) {
        throw 'Synthetic runtime environment is missing one or more storage endpoints.'
    }
    [IO.File]::WriteAllLines($path, $lines, $script:utf8WithoutBom)
}

function Invoke-ImageOperation([string[]]$Arguments, [string]$runtimePath, [string]$privatePath,
    [int[]]$ExpectedExitCodes = @(0), [hashtable]$EnvironmentOverrides = @{}) {
    $dockerArguments = @('run','--rm','--platform','linux/amd64','--network',$script:networkName,
        '--env-file',$runtimePath,'--mount',"type=bind,source=$privatePath,target=$privatePath")
    $overridePath = $null
    if ($EnvironmentOverrides.Count -ne 0) {
        $overridePath = Join-Path $privatePath ("override-{0}.env" -f [Guid]::NewGuid().ToString('N'))
        $lines = foreach ($name in ($EnvironmentOverrides.Keys | Sort-Object)) {
            $value = [string]$EnvironmentOverrides[$name]
            if ($name -notmatch '^[A-Za-z_][A-Za-z0-9_]*$' -or $value -match "[`r`n]") {
                throw 'HU-035 environment override is invalid.'
            }
            "$name=$value"
        }
        [IO.File]::WriteAllLines($overridePath, $lines, $script:utf8WithoutBom)
        $dockerArguments += @('--env-file', $overridePath)
    }
    $dockerArguments += @($ImageRef,'dotnet','Sgol.Operations.dll') + $Arguments
    try {
        $output = @(& docker @dockerArguments 2>&1)
        $exitCode = $LASTEXITCODE
        if ($ExpectedExitCodes -notcontains $exitCode) {
            throw "HU-035 image operation failed: $($Arguments[0]); exit=$exitCode"
        }
        return [pscustomobject]@{ ExitCode = $exitCode; Output = $output }
    }
    finally {
        if ($null -ne $overridePath -and (Test-Path -LiteralPath $overridePath -PathType Leaf)) {
            Remove-Item -LiteralPath $overridePath -Force
        }
    }
}

function Reset-RestoreDatabase {
    & docker exec $script:containers[0] dropdb --username postgres --force --if-exists sgol_restore *> $null
    Assert-DockerSuccess 'Could not discard the prior isolated restore database.'
    & docker exec $script:containers[0] createdb --username postgres --owner sgol_restore sgol_restore *> $null
    Assert-DockerSuccess 'Could not create a new empty isolated restore database.'
}

function Invoke-Mutation([string]$caseName, [string]$referenceUri, [string]$evidencePath) {
    [Environment]::SetEnvironmentVariable('SGOL_HU035_PHASE', 'mutate', 'Process')
    [Environment]::SetEnvironmentVariable('SGOL_HU035_CASE', $caseName, 'Process')
    [Environment]::SetEnvironmentVariable('SGOL_HU035_REFERENCE_MANIFEST_URI', $referenceUri, 'Process')
    [Environment]::SetEnvironmentVariable('SGOL_HU035_RESTORE_EVIDENCE_PATH', $evidencePath, 'Process')
    & dotnet test (Join-Path $script:repositoryRoot 'tests/Sgol.OperationsIntegrationTests/Sgol.OperationsIntegrationTests.csproj') `
        --configuration Release --no-build -p:SGOL_HU035_AMD64_TESTS=true --filter 'Category=Hu035Amd64'
    if ($LASTEXITCODE -ne 0) { throw "HU-035 synthetic mutation failed: $caseName" }
}

function Invoke-ConcurrentReconciliation([object]$caseDescriptor, [string]$runtimePath,
    [string]$privatePath, [string]$evidencePath) {
    $arguments = @('run','--rm','--platform','linux/amd64','--network',$script:networkName,
        '--env-file',$runtimePath,'--mount',"type=bind,source=$privatePath,target=$privatePath",
        $script:ImageRef,'dotnet','Sgol.Operations.dll','reconcile-functional-restore','--reconciliation-id',
        [string]$caseDescriptor.reconciliationId,'--reference-manifest',
        [string]$caseDescriptor.referenceManifestUri,'--restore-evidence',$evidencePath)
    $processes = foreach ($ordinal in 1..2) {
        $info = [Diagnostics.ProcessStartInfo]::new('docker')
        $info.UseShellExecute = $false
        $info.RedirectStandardOutput = $true
        $info.RedirectStandardError = $true
        foreach ($argument in $arguments) { [void]$info.ArgumentList.Add($argument) }
        $process = [Diagnostics.Process]::new()
        $process.StartInfo = $info
        if (-not $process.Start()) { throw 'Could not start a concurrent HU-035 reconciliation.' }
        $process
    }
    $matched = 0
    $busy = 0
    foreach ($process in $processes) {
        $stdout = $process.StandardOutput.ReadToEndAsync()
        $stderr = $process.StandardError.ReadToEndAsync()
        $process.WaitForExit()
        $output = @($stdout.GetAwaiter().GetResult(), $stderr.GetAwaiter().GetResult()) -join "`n"
        if ($process.ExitCode -eq 0 -and $output -match 'FUNCTIONAL_RECOVERY_MATCHED') { $matched++ }
        elseif ($process.ExitCode -eq 1 -and $output -match 'LOCK_BUSY') { $busy++ }
        else { throw 'Concurrent HU-035 reconciliation returned an unexpected result.' }
        $process.Dispose()
    }
    if ($matched -ne 1 -or $busy -ne 1) {
        throw 'Concurrent HU-035 reconciliation did not expose exactly one LOCK_BUSY result.'
    }
    Start-Sleep -Milliseconds (Get-Random -Minimum 100 -Maximum 301)
    $retry = Invoke-ImageOperation @('reconcile-functional-restore', '--reconciliation-id',
        [string]$caseDescriptor.reconciliationId, '--reference-manifest',
        [string]$caseDescriptor.referenceManifestUri, '--restore-evidence', $evidencePath) `
        $runtimePath $privatePath
    if ($retry.Output -notcontains 'FUNCTIONAL_RECOVERY_MATCHED') {
        throw 'HU-035 bounded concurrency retry did not replay the immutable result.'
    }
}

if ([Runtime.InteropServices.RuntimeInformation]::OSArchitecture -ne [Runtime.InteropServices.Architecture]::X64 -or
    -not [Runtime.InteropServices.RuntimeInformation]::IsOSPlatform([Runtime.InteropServices.OSPlatform]::Linux)) {
    throw 'AMD64_LINUX_HOST_REQUIRED: HU-035 must run on the native x64 Linux PR runner.'
}
if (-not (Test-AbsolutePath $GateRoot)) { throw 'GateRoot must be an absolute path outside the repository.' }
$gatePath = [IO.Path]::GetFullPath($GateRoot)
if ($gatePath.StartsWith($repositoryRoot + [IO.Path]::DirectorySeparatorChar,
    [StringComparison]::OrdinalIgnoreCase)) {
    throw 'GateRoot must remain outside the repository.'
}
if (Test-Path -LiteralPath $gatePath) {
    if (-not (Test-Path -LiteralPath $gatePath -PathType Container) -or
        (Get-ChildItem -LiteralPath $gatePath -Force | Select-Object -First 1)) {
        throw 'GateRoot must be a new or empty directory.'
    }
}
else { New-Item -ItemType Directory -Path $gatePath | Out-Null }
$identityBytes = [Text.Encoding]::UTF8.GetBytes($gatePath)
$identityHash = [Security.Cryptography.SHA256]::HashData($identityBytes)
$runIdentity = ([Convert]::ToHexString($identityHash)).ToLowerInvariant().Substring(0, 12)
$networkName = "sgol-hu035-$runIdentity-private"
$containers = @("sgol-hu035-$runIdentity-postgres", "sgol-hu035-$runIdentity-s3-source",
    "sgol-hu035-$runIdentity-s3-destination")
$allCleanupContainers = @($bootstrapContainers + $containers)

$inspection = (& docker image inspect $ImageRef | ConvertFrom-Json)[0]
if ($LASTEXITCODE -ne 0 -or $inspection.Architecture -ne 'amd64' -or $inspection.Os -ne 'linux') {
    throw 'HU-035 requires an already loaded immutable linux/amd64 image.'
}

$runtimeDirectory = Join-Path $gatePath 'runtime-private'
$privateDirectory = Join-Path $gatePath 'evidence-private'
$publicDirectory = Join-Path $gatePath 'evidence-public'
$descriptorPath = Join-Path $privateDirectory 'descriptor.json'
$restoreEvidencePath = Join-Path $privateDirectory 'restore-evidence.json'
$publicResultPath = Join-Path $publicDirectory 'hu-035-result.json'
$prepareTrx = Join-Path $publicDirectory 'hu-035-prepare.trx'
$verifyTrx = Join-Path $publicDirectory 'hu-035-verify.trx'
$startedAt = [DateTimeOffset]::UtcNow
$stage = 'PROVISION'
$result = 'FAILED'
$environmentNames = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
New-Item -ItemType Directory -Path $privateDirectory, $publicDirectory | Out-Null

try {
    & (Join-Path $PSScriptRoot 'new-tech-ops-synthetic-environment.ps1') `
        -ImageRef $ImageRef -OutputDirectory $runtimeDirectory | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'HU-035 synthetic infrastructure provisioning failed.' }
    $runtimePath = Join-Path $runtimeDirectory 'runtime.env'
    & docker compose --env-file $runtimePath -f (Join-Path $repositoryRoot 'deploy/staging/compose.yaml') `
        rm --force --stop migrate *> $null
    Assert-DockerSuccess 'Could not remove the bootstrap-only migrate container.'
    & docker network create --driver bridge --internal $networkName *> $null
    Assert-DockerSuccess 'Could not create the run-scoped HU-035 private network.'
    for ($index = 0; $index -lt $bootstrapContainers.Count; $index++) {
        & docker container rename $bootstrapContainers[$index] $containers[$index]
        Assert-DockerSuccess 'Could not scope a HU-035 synthetic container name.'
        & docker network connect --alias $bootstrapContainers[$index] $networkName $containers[$index]
        Assert-DockerSuccess 'Could not attach a HU-035 synthetic container to its run-scoped network.'
        & docker network disconnect $bootstrapNetworkName $containers[$index]
        Assert-DockerSuccess 'Could not detach a HU-035 synthetic container from the bootstrap network.'
    }
    & docker network rm $bootstrapNetworkName *> $null
    Assert-DockerSuccess 'Could not remove the bootstrap network.'
    Set-ProcessEnvironmentFromFile $runtimePath
    $containerPrimaryConnection = [Environment]::GetEnvironmentVariable('ConnectionStrings__Sgol', 'Process')
    foreach ($line in Get-Content -LiteralPath $runtimePath) {
        if ($line -match '^([^#=]+)=') { [void]$environmentNames.Add($Matches[1]) }
    }

    $postgresAddress = Get-PrivateContainerAddress $containers[0] $networkName
    $sourceAddress = Get-PrivateContainerAddress $containers[1] $networkName
    $destinationAddress = Get-PrivateContainerAddress $containers[2] $networkName
    Replace-ConnectionHost 'ConnectionStrings__Sgol' $postgresAddress
    Replace-ConnectionHost 'Backup__PostgreSql__ConnectionString' $postgresAddress
    Replace-ConnectionHost 'Restore__PostgreSql__ConnectionString' $postgresAddress
    Set-RuntimeStorageEndpoints $runtimePath $sourceAddress $destinationAddress
    [Environment]::SetEnvironmentVariable('Evidence__Storage__Endpoint', "http://$sourceAddress`:8333", 'Process')
    [Environment]::SetEnvironmentVariable('Backup__Storage__Endpoint', "http://$destinationAddress`:8333", 'Process')
    [Environment]::SetEnvironmentVariable('Replica__Source__Endpoint', "http://$sourceAddress`:8333", 'Process')
    [Environment]::SetEnvironmentVariable('Replica__Destination__Endpoint', "http://$destinationAddress`:8333", 'Process')

    $pgDumpWrapper = Join-Path $privateDirectory 'pg-dump-wrapper.sh'
    $ageWrapper = Join-Path $privateDirectory 'age-wrapper.sh'
    $wrapperTemplate = @'
#!/bin/sh
exec docker run --rm --platform linux/amd64 --network __NETWORK__ --env-file '__RUNTIME__' \
  --env PGHOST --env PGPORT --env PGDATABASE --env PGUSER --env PGPASSWORD \
  --env PGCONNECT_TIMEOUT --env PGSSLMODE --mount type=bind,source=/tmp,target=/tmp \
  __IMAGE__ __COMMAND__ "$@"
'@
    $pgDumpContent = $wrapperTemplate.Replace('__NETWORK__', $networkName).Replace(
        '__RUNTIME__', $runtimePath).Replace('__IMAGE__', $ImageRef).Replace(
        '__COMMAND__', '/usr/bin/pg_dump')
    $ageContent = $wrapperTemplate.Replace('__NETWORK__', $networkName).Replace(
        '__RUNTIME__', $runtimePath).Replace('__IMAGE__', $ImageRef).Replace(
        '__COMMAND__', '/usr/bin/age')
    [IO.File]::WriteAllText($pgDumpWrapper, $pgDumpContent, $utf8WithoutBom)
    [IO.File]::WriteAllText($ageWrapper, $ageContent, $utf8WithoutBom)
    & chmod 700 $pgDumpWrapper $ageWrapper
    Assert-DockerSuccess 'Could not protect HU-035 process wrappers.'
    [Environment]::SetEnvironmentVariable('Backup__PgDumpPath', $pgDumpWrapper, 'Process')
    [Environment]::SetEnvironmentVariable('Backup__AgePath', $ageWrapper, 'Process')
    [Environment]::SetEnvironmentVariable('SGOL_HU035_DESCRIPTOR_PATH', $descriptorPath, 'Process')
    [Environment]::SetEnvironmentVariable('SGOL_HU035_PUBLIC_RESULT_PATH', $publicResultPath, 'Process')
    [Environment]::SetEnvironmentVariable('SGOL_HU035_PHASE', 'prepare', 'Process')
    [Environment]::SetEnvironmentVariable('SGOL_HU035_AMD64_TESTS', 'true', 'Process')

    $stage = 'REFERENCE'
    & dotnet test (Join-Path $repositoryRoot 'tests/Sgol.OperationsIntegrationTests/Sgol.OperationsIntegrationTests.csproj') `
        --configuration Release --no-restore -p:SGOL_HU035_AMD64_TESTS=true --filter 'Category=Hu035Amd64' `
        --logger "trx;LogFileName=$prepareTrx"
    if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $descriptorPath -PathType Leaf)) {
        throw 'HU-035 reference preparation failed.'
    }
    $descriptor = Get-Content -Raw -LiteralPath $descriptorPath | ConvertFrom-Json
    if ($descriptor.kind -ne 'SGOL_HU035_AMD64_DESCRIPTOR' -or
        $descriptor.expectedMigration -ne $expectedMigration -or $descriptor.cases.Count -ne 18) {
        throw 'HU-035 descriptor is invalid or the approved matrix is incomplete.'
    }

    $stage = 'REFERENCE_READY'
    foreach ($caseDescriptor in $descriptor.cases) {
        $complete = Invoke-ImageOperation @('complete-functional-reference', '--reconciliation-id',
            [string]$caseDescriptor.reconciliationId, '--reference', [string]$caseDescriptor.referenceManifestUri,
            '--backup-manifest', [string]$caseDescriptor.backupManifestUri,
            '--replica-manifest', [string]$caseDescriptor.replicaManifestUri) $runtimePath $privateDirectory
        if ($complete.Output -notcontains 'FUNCTIONAL_REFERENCE_READY') {
            throw "HU-035 reference completion failed: $($caseDescriptor.name)"
        }
        if ($caseDescriptor.name -eq 'positive') {
            $associationReplay = Invoke-ImageOperation @('complete-functional-reference', '--reconciliation-id',
                [string]$caseDescriptor.reconciliationId, '--reference', [string]$caseDescriptor.referenceManifestUri,
                '--backup-manifest', [string]$caseDescriptor.backupManifestUri,
                '--replica-manifest', [string]$caseDescriptor.replicaManifestUri) $runtimePath $privateDirectory
            if ($associationReplay.Output -notcontains 'FUNCTIONAL_REFERENCE_READY') {
                throw 'HU-035 reference association replay failed.'
            }
        }
    }

    $stage = 'MATRIX'
    foreach ($caseDescriptor in $descriptor.cases) {
        Reset-RestoreDatabase
        $caseEvidencePath = Join-Path $privateDirectory "$($caseDescriptor.name)-restore-evidence.json"
        $restore = Invoke-ImageOperation @('verify-postgresql-backup', '--manifest',
            [string]$caseDescriptor.backupManifestUri) $runtimePath $privateDirectory
        $restoreJson = $restore.Output | Where-Object {
            $_ -match '^\{"kind":"SGOL_TECHNICAL_RESTORE_EVIDENCE"'
        } | Select-Object -Last 1
        if ([string]::IsNullOrWhiteSpace($restoreJson)) {
            throw "HU-035 restore evidence is missing: $($caseDescriptor.name)"
        }
        [IO.File]::WriteAllText($caseEvidencePath, $restoreJson, $utf8WithoutBom)
        Invoke-Mutation $caseDescriptor.name $caseDescriptor.referenceManifestUri $caseEvidencePath

        $overrides = @{}
        $expectedExits = @(0)
        if ($caseDescriptor.name -in @('identity_missing','identity_additional','link_missing','link_altered',
            'version_changed','count_changed','evidence_missing','evidence_corrupt','evidence_inaccessible',
            'audit_missing','audit_altered','rpo_exceeded','rto_exceeded')) {
            $expectedExits = @(2)
        }
        elseif ($caseDescriptor.name -in @('reference_corrupt','primary_target')) { $expectedExits = @(1) }
        if ($caseDescriptor.name -eq 'evidence_inaccessible') {
            $overrides['Replica__Destination__Endpoint'] = 'http://127.0.0.1:1'
        }
        elseif ($caseDescriptor.name -eq 'primary_target') {
            $overrides['Restore__PostgreSql__ConnectionString'] = $containerPrimaryConnection
        }

        if ($caseDescriptor.name -eq 'concurrency') {
            Invoke-ConcurrentReconciliation $caseDescriptor $runtimePath $privateDirectory $caseEvidencePath
            continue
        }
        $reconcile = Invoke-ImageOperation @('reconcile-functional-restore', '--reconciliation-id',
            [string]$caseDescriptor.reconciliationId, '--reference-manifest',
            [string]$caseDescriptor.referenceManifestUri, '--restore-evidence', $caseEvidencePath) `
            $runtimePath $privateDirectory $expectedExits $overrides
        if ($expectedExits -contains 2 -and $reconcile.Output -notcontains 'FUNCTIONAL_RECOVERY_NOT_MATCHED') {
            throw "HU-035 negative reconciliation was not visible: $($caseDescriptor.name)"
        }
        if ($caseDescriptor.name -eq 'positive') {
            if ($reconcile.Output -notcontains 'FUNCTIONAL_RECOVERY_MATCHED') {
                throw 'HU-035 positive reconciliation did not match.'
            }
            $replay = Invoke-ImageOperation @('reconcile-functional-restore', '--reconciliation-id',
                [string]$caseDescriptor.reconciliationId, '--reference-manifest',
                [string]$caseDescriptor.referenceManifestUri, '--restore-evidence', $caseEvidencePath) `
                $runtimePath $privateDirectory
            if ($replay.Output -notcontains 'FUNCTIONAL_RECOVERY_MATCHED') {
                throw 'HU-035 identical reconciliation replay failed.'
            }
        }
        elseif ($caseDescriptor.name -eq 'replay_conflict') {
            if ($reconcile.Output -notcontains 'FUNCTIONAL_RECOVERY_MATCHED') {
                throw 'HU-035 conflict prerequisite did not match.'
            }
            Invoke-Mutation 'replay_conflict' $caseDescriptor.referenceManifestUri $caseEvidencePath
            $conflict = Invoke-ImageOperation @('reconcile-functional-restore', '--reconciliation-id',
                [string]$caseDescriptor.reconciliationId, '--reference-manifest',
                [string]$caseDescriptor.referenceManifestUri, '--restore-evidence', $caseEvidencePath) `
                $runtimePath $privateDirectory @(1)
            if (($conflict.Output -join "`n") -notmatch 'RECONCILIATION_IMMUTABLE_CONFLICT') {
                throw 'HU-035 conflicting replay was not rejected explicitly.'
            }
        }
        if ($caseDescriptor.name -in @('evidence_missing','evidence_corrupt')) {
            Invoke-Mutation 'restore_object' $caseDescriptor.referenceManifestUri $caseEvidencePath
        }
    }

    $stage = 'APPROVE'
    [Environment]::SetEnvironmentVariable('SGOL_HU035_PHASE', 'verify', 'Process')
    & dotnet test (Join-Path $repositoryRoot 'tests/Sgol.OperationsIntegrationTests/Sgol.OperationsIntegrationTests.csproj') `
        --configuration Release --no-build -p:SGOL_HU035_AMD64_TESTS=true --filter 'Category=Hu035Amd64' `
        --logger "trx;LogFileName=$verifyTrx"
    if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $publicResultPath -PathType Leaf)) {
        throw 'HU-035 result verification and approval failed.'
    }

    $forbidden = '(?i)(password\s*=|secretkey|accesskey|connectionstrings|runtime-private|s3://|signed[ -]?url|private key)'
    foreach ($file in Get-ChildItem -LiteralPath $publicDirectory -File) {
        if (Select-String -LiteralPath $file.FullName -Pattern $forbidden -Quiet) {
            throw "HU-035 public evidence contains forbidden material: $($file.Name)"
        }
    }
    $stage = 'COMPLETE'
    $result = 'SUCCEEDED'
}
finally {
    $cleanupFailed = $false
    foreach ($container in $allCleanupContainers) {
        & docker container inspect $container *> $null
        if ($LASTEXITCODE -eq 0) {
            & docker container rm --force $container *> $null
            if ($LASTEXITCODE -ne 0) { $cleanupFailed = $true }
        }
    }
    foreach ($network in @($networkName, $bootstrapNetworkName)) {
        & docker network inspect $network *> $null
        if ($LASTEXITCODE -eq 0) {
            & docker network rm $network *> $null
            if ($LASTEXITCODE -ne 0) { $cleanupFailed = $true }
        }
    }
    try {
        if (Test-Path -LiteralPath $runtimeDirectory) { Remove-Item -LiteralPath $runtimeDirectory -Recurse -Force }
        if (Test-Path -LiteralPath $privateDirectory) { Remove-Item -LiteralPath $privateDirectory -Recurse -Force }
    }
    catch { $cleanupFailed = $true }
    if ($cleanupFailed) {
        $stage = 'CLEANUP'
        $result = 'FAILED'
    }
    $summary = [ordered]@{
        kind = 'SGOL_HU035_AMD64_GATE'
        startedAt = $startedAt
        completedAt = [DateTimeOffset]::UtcNow
        imageRef = $ImageRef
        hostArchitecture = 'x86-64'
        syntheticOnly = $true
        stage = $stage
        result = $result
    }
    [IO.File]::WriteAllText((Join-Path $publicDirectory 'hu-035-summary.json'),
        ($summary | ConvertTo-Json -Depth 3), $utf8WithoutBom)
    foreach ($name in $environmentNames) { [Environment]::SetEnvironmentVariable($name, $null, 'Process') }
    foreach ($name in @('SGOL_HU035_DESCRIPTOR_PATH','SGOL_HU035_PUBLIC_RESULT_PATH','SGOL_HU035_PHASE',
        'SGOL_HU035_AMD64_TESTS','SGOL_HU035_CASE','SGOL_HU035_REFERENCE_MANIFEST_URI',
        'SGOL_HU035_RESTORE_EVIDENCE_PATH','Continuity__ReplicaManifestUri')) {
        [Environment]::SetEnvironmentVariable($name, $null, 'Process')
    }
}

if ($result -ne 'SUCCEEDED') { throw "HU-035 AMD64 gate failed at stage $stage." }
Write-Output "PASS: native AMD64 HU-035 functional recovery gate. PublicEvidence=$publicDirectory"
