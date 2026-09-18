[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$script:checks = 0
function Assert-Test([bool]$condition, [string]$label) {
    if (-not $condition) { throw "FAILED: $label" }
    $script:checks++
}
function Read-Ast([string]$path) {
    $tokens = $null; $errors = $null
    $ast = [Management.Automation.Language.Parser]::ParseFile($path, [ref]$tokens, [ref]$errors)
    Assert-Test ($errors.Count -eq 0) "syntax: $([IO.Path]::GetFileName($path))"
    return $ast
}
function Slice([string]$text, [string]$start, [string]$end) {
    $offset = $text.IndexOf($start, [StringComparison]::Ordinal)
    $limit = $text.IndexOf($end, $offset, [StringComparison]::Ordinal)
    Assert-Test ($offset -ge 0 -and $limit -gt $offset) 'real block located'
    return [scriptblock]::Create($text.Substring($offset, $limit - $offset))
}
$provisionAst = Read-Ast (Join-Path $root 'scripts/operations/new-tech-ops-synthetic-environment.ps1')
$gateAst = Read-Ast (Join-Path $root 'scripts/operations/invoke-hu-035-amd64-gate.ps1')
$provisionText = $provisionAst.Extent.Text
foreach ($name in @('Assert-DockerSuccess','Get-PublishedPort','Get-ContainerAddress','Set-ProvisionEnvironment')) {
    $function = $provisionAst.Find({ param($node) $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq $name }, $true)
    . ([scriptblock]::Create($function.Extent.Text))
}
$optionsBlock = Slice $provisionText '$storageNetwork = $composeNetwork' '$postgresImage ='
$storageBlock = Slice $provisionText '$sourceHostPort = New-FreeLoopbackPort' '$summaryPath ='
$networkGuard = $provisionAst.Find({ param($node) $node -is [Management.Automation.Language.IfStatementAst] -and $node.Extent.Text.Contains('$networkProperties =') }, $true)
$networkBlock = [scriptblock]::Create($networkGuard.Extent.Text)
$mainTry = @($gateAst.EndBlock.Statements | Where-Object { $_ -is [Management.Automation.Language.TryStatementAst] })[-1]
$gateBlock = Slice $mainTry.Body.Extent.Text '& docker network create' 'Set-ProcessEnvironmentFromFile $runtimePath'
$cleanupCommand = $mainTry.Finally.Find({ param($node) $node -is [Management.Automation.Language.CommandAst] -and $node.GetCommandName() -eq 'Invoke-Hu035Finalization' }, $true)
$cleanup = [scriptblock]::Create($cleanupCommand.CommandElements[4].ScriptBlock.EndBlock.Extent.Text)
Assert-Test (@($provisionAst.FindAll({ param($node) $node -is [Management.Automation.Language.CommandAst] -and $node.GetCommandName() -eq 'Set-ProvisionEnvironment' -and $node.CommandElements[1].Value -eq 'verify' }, $true)).Count -eq 1) 'single verify environment assignment'

# Only external boundaries are simulated. The selected statements/functions above are real.
function docker {
    $a = @($args | ForEach-Object { [string]$_ })
    $script:LASTEXITCODE = 0
    $script:calls.Add(($a -join '|'))
    if ($a[0] -eq 'port') {
        $port = if ($a[1] -eq $sourceContainer) { 18001 } else { 18002 }
        if ($script:restarted) { $port += 100 }
        return "127.0.0.1:$port"
    }
    if ($a[0] -eq 'restart') { $script:restarted = $true; return }
    if ($a[0] -eq 'run') {
        $name = $a[[Array]::IndexOf($a, '--name') + 1]
        [void]$script:resources.Add($name)
        if ($script:failure -eq 'provision' -and $name -eq $destinationContainer) { $script:LASTEXITCODE = 7 }
        return
    }
    if ($a[0] -eq 'inspect') {
        return (ConvertTo-Json -Depth 6 -Compress -InputObject @(@{ NetworkSettings = @{ Networks = @{ $storageNetwork = @{ IPAddress = '10.23.0.3' } } } }))
    }
    if ($a[0] -eq 'container') {
        switch ($a[1]) {
            'rename' {
                $script:renameCount++
                if ($script:failure -eq "rename$script:renameCount") { $script:LASTEXITCODE = 8; return }
                [void]$script:resources.Remove($a[2]); [void]$script:resources.Add($a[3])
            }
            'inspect' { if (-not $script:resources.Contains($a[2])) { $script:LASTEXITCODE = 1 } }
            'rm' { [void]$script:resources.Remove($a[-1]) }
            default { throw 'Unexpected container command' }
        }
        return
    }
    if ($a[0] -eq 'network') {
        switch ($a[1]) {
            'create' { [void]$script:networks.Add($a[-1]) }
            'inspect' {
                if ($a -contains '--format') { return $script:networkProperties }
                if (-not $script:networks.Contains($a[-1])) { $script:LASTEXITCODE = 1 }
            }
            'rm' { [void]$script:networks.Remove($a[-1]) }
            'connect' { }
            'disconnect' { }
            default { throw 'Unexpected network command' }
        }
        return
    }
    if ($a[0] -ne 'compose') { throw 'Unexpected Docker command' }
}
function dotnet {
    Assert-Test (($args -join '|').Contains('--filter|Category=TechOpsProvisioning')) 'unchanged provisioning filter'
    $script:events.Add("test:$env:SGOL_PROVISION_PHASE`:$env:SGOL_PROVISION_SOURCE_ENDPOINT`:$env:SGOL_PROVISION_DESTINATION_ENDPOINT")
    $script:LASTEXITCODE = 0
}
function New-FreeLoopbackPort { $script:freePort++; return $script:freePort }
function Wait-TcpPort([int]$port, [string]$description) { $script:events.Add("wait:$port") }
function New-S3Configuration($identities, $path) { $script:events.Add('remove-admin') }
function Write-RuntimeEnvironment($sourceAddress, $destinationAddress) {
    Assert-Test ($sourceAddress -eq '10.23.0.3' -and $destinationAddress -eq '10.23.0.3') 'runtime addresses read from selected network'
}
function Reset-Simulation {
    $script:calls = [Collections.Generic.List[string]]::new()
    $script:events = [Collections.Generic.List[string]]::new()
    $script:resources = [Collections.Generic.HashSet[string]]::new()
    $script:networks = [Collections.Generic.HashSet[string]]::new()
    $script:freePort = 18000; $script:restarted = $false; $script:renameCount = 0
    $script:networkProperties = 'true|bridge'; $script:failure = ''
    $script:LASTEXITCODE = 0
}
$savedEnvironment = @{}
Get-ChildItem Env: | ForEach-Object { $savedEnvironment[$_.Name] = $_.Value }
$testDirectory = Join-Path ([IO.Path]::GetTempPath()) ('sgol-private-network-test-' + [guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory($testDirectory)
try {
    $repositoryRoot = $root
    $sourceContainer = 'sgol-tech-ops-s3-source'; $destinationContainer = 'sgol-tech-ops-s3-destination'
    $composeNetwork = 'sgol-staging_private'; $seaweedImage = 'synthetic-image'
    $sourceConfigPath = 'synthetic-source'; $destinationConfigPath = 'synthetic-destination'
    foreach ($StorageNetworkName in @('', 'sgol-hu035-0123456789ab-private')) {
        Reset-Simulation
        . $optionsBlock
        . $networkBlock
        . $storageBlock
        $expectedNetwork = if ($StorageNetworkName) { $StorageNetworkName } else { $composeNetwork }
        $runs = @($script:calls | Where-Object { $_.StartsWith('run|') })
        Assert-Test ($runs.Count -eq 2) 'two storage starts'
        foreach ($pair in @(@($runs[0], $sourceContainer), @($runs[1], $destinationContainer))) {
            Assert-Test ($pair[0].Contains("--network|$expectedNetwork|")) 'primary network from startup'
            Assert-Test ($pair[0].Contains("|mini|-ip=$($pair[1])|-ip.bind=0.0.0.0|")) 'stable advertised alias with unrestricted bind'
            if ($StorageNetworkName) { Assert-Test ($pair[0].Contains("--network-alias|$($pair[1])|")) 'exact consumer alias' }
            else { Assert-Test (-not $pair[0].Contains('--network-alias')) 'default has no new aliases' }
        }
        $p = if ($StorageNetworkName) { 18101 } else { 18001 }
        $q = $p + 1
        Assert-Test (($script:events -join '|') -eq "wait:18001|wait:18002|test:create:http://127.0.0.1:18001:http://127.0.0.1:18002|remove-admin|remove-admin|wait:$p|wait:$q|test:verify:http://127.0.0.1:$p`:http://127.0.0.1:$q") 'waits precede single verify with correct ports'
        Assert-Test (@($script:calls | Where-Object { $_.StartsWith('port|') }).Count -eq $(if ($StorageNetworkName) { 4 } else { 2 })) 'port refresh only in opt-in mode'
        Assert-Test (@($script:calls | Where-Object { $_.StartsWith('restart|') }).Count -eq 1) 'single existing restart'
    }
    foreach ($invalid in @('false|bridge','true|overlay')) {
        Reset-Simulation; $script:networkProperties = $invalid
        $rejected = $false
        try { . $networkBlock } catch { $rejected = $_.Exception.Message -eq 'HU035_STORAGE_NETWORK_MUST_BE_INTERNAL_BRIDGE' }
        Assert-Test $rejected 'reject non-private/non-bridge network'
    }

    # Redirect only the real provisioning call; its storage statements still run above.
    function Join-Path {
        param($Path, $ChildPath)
        if ($ChildPath -eq 'new-tech-ops-synthetic-environment.ps1') { return 'Invoke-SimulatedProvision' }
        Microsoft.PowerShell.Management\Join-Path $Path $ChildPath
    }
    function Invoke-SimulatedProvision {
        param($ImageRef, $OutputDirectory, $StorageNetworkName)
        Assert-Test ($script:networks.Contains($StorageNetworkName)) 'final network exists before provisioning'
        Assert-Test ($StorageNetworkName -eq $networkName) 'gate passes exact final network'
        if ($script:failure -eq 'after-network') { throw 'SIMULATED_PREPARATION_FAILURE' }
        [void]$script:networks.Add($composeNetwork)
        [void]$script:resources.Add('sgol-tech-ops-postgres')
        . $optionsBlock
        . $networkBlock
        . $storageBlock
    }
    $networkName = 'sgol-hu035-0123456789ab-private'; $bootstrapNetworkName = $composeNetwork
    $bootstrapContainers = @('sgol-tech-ops-postgres', $sourceContainer, $destinationContainer)
    $containers = @('sgol-hu035-0123456789ab-postgres','sgol-hu035-0123456789ab-s3-source','sgol-hu035-0123456789ab-s3-destination')
    $allCleanupContainers = @($bootstrapContainers + $containers)
    $ImageRef = 'sha256:' + ('0' * 64)
    $utf8WithoutBom = [Text.UTF8Encoding]::new($false)
    $environmentNames = @(); $startedAt = [DateTimeOffset]::UtcNow
    foreach ($scenario in @('', 'after-network','provision','rename1','rename2','rename3')) {
        Reset-Simulation; $script:failure = $scenario
        $runtimeDirectory = Join-Path $testDirectory 'runtime'
        $privateDirectory = Join-Path $testDirectory 'private'
        $publicDirectory = Join-Path $testDirectory 'public'
        foreach ($directory in @($runtimeDirectory,$privateDirectory,$publicDirectory)) { [void][IO.Directory]::CreateDirectory($directory) }
        $stage = 'PROVISION'; $result = 'FAILED'; $failed = $false
        try { . $gateBlock } catch { $failed = $true } finally { . $cleanup }
        Assert-Test ($failed -eq [bool]$scenario) "expected outcome: $scenario"
        Assert-Test ($script:resources.Count -eq 0 -and $script:networks.Count -eq 0) "all resources cleaned: $scenario"
        Assert-Test (-not $cleanupFailed -and -not (Test-Path $runtimeDirectory) -and -not (Test-Path $privateDirectory)) "private files cleaned: $scenario"
        if (-not $scenario) {
            $migrations = @($script:calls | Where-Object { $_.StartsWith('network|connect|--alias|') -or $_.StartsWith("network|disconnect|$bootstrapNetworkName|") })
            Assert-Test ($migrations.Count -eq 2 -and @($migrations | Where-Object { -not $_.EndsWith($containers[0]) }).Count -eq 0) 'only PostgreSQL migrates'
        }
    }
}
finally {
    foreach ($entry in Get-ChildItem Env:) {
        if ($entry.Name -like 'SGOL_*' -or $entry.Name -eq 'Continuity__ReplicaManifestUri') {
            [Environment]::SetEnvironmentVariable($entry.Name, $savedEnvironment[$entry.Name], 'Process')
        }
    }
    foreach ($name in $savedEnvironment.Keys) {
        if ($name -like 'SGOL_*' -or $name -eq 'Continuity__ReplicaManifestUri') { [Environment]::SetEnvironmentVariable($name, $savedEnvironment[$name], 'Process') }
    }
    $resolved = [IO.Path]::GetFullPath($testDirectory)
    if ($resolved.StartsWith([IO.Path]::GetFullPath([IO.Path]::GetTempPath()), [StringComparison]::OrdinalIgnoreCase) -and [IO.Path]::GetFileName($resolved).StartsWith('sgol-private-network-test-')) {
        Remove-Item -LiteralPath $resolved -Recurse -Force
    }
}
Write-Output "PASS: HU-035 private network, $script:checks pure assertions; no Docker or integration execution."
