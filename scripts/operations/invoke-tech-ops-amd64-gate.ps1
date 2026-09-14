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
function Test-AbsolutePath([string]$value) {
    if ([IO.Path]::DirectorySeparatorChar -eq '\') {
        return $value -match '^(?:[A-Za-z]:[\\/]|\\\\[^\\/]+[\\/][^\\/]+)'
    }
    return $value.StartsWith('/', [StringComparison]::Ordinal)
}
function Test-NativeX64Host {
    if ($env:OS -eq 'Windows_NT' -and -not [string]::IsNullOrWhiteSpace($env:PROCESSOR_ARCHITECTURE)) {
        return $env:PROCESSOR_ARCHITECTURE -eq 'AMD64' -and
            $env:PROCESSOR_IDENTIFIER -notmatch '^(?i:ARM)'
    }
    return [Runtime.InteropServices.RuntimeInformation]::OSArchitecture -eq [Runtime.InteropServices.Architecture]::X64
}
if (-not (Test-NativeX64Host)) {
    throw 'AMD64_HOST_REQUIRED: the contractual image gate must run natively on an x86-64 host.'
}
if (-not (Test-AbsolutePath $GateRoot)) {
    throw 'GateRoot must be an absolute path outside the repository.'
}
$gatePath = [IO.Path]::GetFullPath($GateRoot)
if ($gatePath.StartsWith($repositoryRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'GateRoot must remain outside the repository.'
}
if (Test-Path -LiteralPath $gatePath) {
    if (-not (Test-Path -LiteralPath $gatePath -PathType Container) -or
        (Get-ChildItem -LiteralPath $gatePath -Force | Select-Object -First 1)) {
        throw 'GateRoot must be a new or empty directory to prevent overwriting evidence.'
    }
}
else {
    New-Item -ItemType Directory -Path $gatePath | Out-Null
}

$inspection = (& docker image inspect $ImageRef | ConvertFrom-Json)[0]
if ($LASTEXITCODE -ne 0 -or $inspection.Architecture -ne 'amd64' -or $inspection.Os -ne 'linux') {
    throw 'The gate requires an already loaded linux/amd64 image identified by immutable image ID.'
}

$runtimeDirectory = Join-Path $gatePath 'runtime-private'
$privateEvidenceDirectory = Join-Path $gatePath 'evidence-private'
$publicEvidenceDirectory = Join-Path $gatePath 'evidence-public'
$startedAt = [DateTimeOffset]::UtcNow
$stage = 'PROVISION'
$result = 'FAILED'
$sourceBucketCount = 0
$destinationBucketCount = 0
$utf8WithoutBom = New-Object Text.UTF8Encoding($false)
try {
    & (Join-Path $PSScriptRoot 'new-tech-ops-synthetic-environment.ps1') `
        -ImageRef $ImageRef `
        -OutputDirectory $runtimeDirectory | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Synthetic TECH-OPS environment provisioning failed.' }

    $summaryPath = Join-Path $runtimeDirectory 'environment-summary.json'
    $runtimeEnvironmentPath = Join-Path $runtimeDirectory 'runtime.env'
    $summary = Get-Content -Raw -LiteralPath $summaryPath | ConvertFrom-Json
    if ($summary.kind -ne 'SGOL_TECH_OPS_SYNTHETIC_ENVIRONMENT' -or $summary.syntheticOnly -ne $true -or
        $summary.imageRef -ne $ImageRef -or $summary.sourceBuckets.Count -ne 2 -or
        $summary.destinationBuckets.Count -ne 3) {
        throw 'Synthetic environment summary is invalid.'
    }
    $sourceBucketCount = $summary.sourceBuckets.Count
    $destinationBucketCount = $summary.destinationBuckets.Count

    $stage = 'VERIFY'
    & (Join-Path $PSScriptRoot 'verify-tech-ops-external.ps1') `
        -ImageRef $ImageRef `
        -RuntimeEnvironmentFile $runtimeEnvironmentPath `
        -BackupManifestUri $summary.backupManifestUri `
        -ReplicaManifestUri $summary.replicaManifestUri `
        -EvidenceDirectory $privateEvidenceDirectory
    if ($LASTEXITCODE -ne 0) { throw 'Integral TECH-OPS external verification failed.' }
    $stage = 'COMPLETE'
    $result = 'SUCCEEDED'
}
finally {
    if (-not (Test-Path -LiteralPath $publicEvidenceDirectory -PathType Container)) {
        New-Item -ItemType Directory -Path $publicEvidenceDirectory | Out-Null
    }
    $publicSummary = [ordered]@{
        kind = 'SGOL_TECH_OPS_AMD64_GATE'
        startedAt = $startedAt
        completedAt = [DateTimeOffset]::UtcNow
        imageRef = $ImageRef
        hostArchitecture = 'x86-64'
        sourceBucketCount = $sourceBucketCount
        destinationBucketCount = $destinationBucketCount
        stage = $stage
        result = $result
    }
    [IO.File]::WriteAllText(
        (Join-Path $publicEvidenceDirectory 'tech-ops-summary.json'),
        ($publicSummary | ConvertTo-Json -Depth 3),
        $utf8WithoutBom)
    $privateInspection = Join-Path $privateEvidenceDirectory 'image-inspect.json'
    if (Test-Path -LiteralPath $privateInspection -PathType Leaf) {
        Copy-Item -LiteralPath $privateInspection -Destination (
            Join-Path $publicEvidenceDirectory 'image-inspect.json')
    }
}
Write-Output "PASS: native AMD64 TECH-OPS gate. PublicEvidence=$publicEvidenceDirectory"
Write-Output "PRIVATE_TECH_OPS_MATERIAL_RETAIN_LOCALLY=$runtimeDirectory,$privateEvidenceDirectory"
