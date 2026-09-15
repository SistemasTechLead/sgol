[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$')]
    [string]$ReconciliationId,

    [Parameter(Mandatory = $true)]
    [ValidatePattern('^s3://[^/]+/.+\.json$')]
    [string]$ReferenceManifestUri,

    [Parameter(Mandatory = $true)]
    [ValidatePattern('^s3://[^/]+/.+\.json$')]
    [string]$BackupManifestUri,

    [Parameter(Mandatory = $true)]
    [ValidatePattern('^s3://[^/]+/.+\.json$')]
    [string]$ReplicaManifestUri,

    [Parameter(Mandatory = $true)]
    [ValidateScript({ Test-Path -LiteralPath $_ -PathType Leaf })]
    [string]$RestoreEvidencePath,

    [Parameter(Mandatory = $true)]
    [ValidateScript({ Test-Path -LiteralPath $_ -PathType Leaf })]
    [string]$RuntimeEnvironmentFile,

    [ValidateScript({ -not (Test-Path -LiteralPath $_ -PathType Leaf) })]
    [string]$EvidenceDirectory,

    [ValidateScript({ Test-Path -LiteralPath $_ -PathType Leaf })]
    [string]$NegativeCasesFile
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$runtimeEnvironmentPath = (Resolve-Path -LiteralPath $RuntimeEnvironmentFile).Path
$restoreEvidence = (Resolve-Path -LiteralPath $RestoreEvidencePath).Path
foreach ($externalPath in @($runtimeEnvironmentPath, $restoreEvidence)) {
    if ($externalPath.StartsWith($repositoryRoot + [IO.Path]::DirectorySeparatorChar,
        [StringComparison]::OrdinalIgnoreCase)) {
        throw 'HU-035 runtime inputs and evidence must remain outside the repository.'
    }
}
$runtimeEnvironment = Get-Content -LiteralPath $runtimeEnvironmentPath
if ($runtimeEnvironment -notcontains 'SGOL_SYNTHETIC_ONLY=true') {
    throw 'HU-035 accepts only an environment explicitly marked SGOL_SYNTHETIC_ONLY=true.'
}
if ([string]::IsNullOrWhiteSpace($EvidenceDirectory)) {
    $EvidenceDirectory = Join-Path ([IO.Path]::GetTempPath()) ("sgol-hu-035-evidence-" + [guid]::NewGuid().ToString('N'))
}
$evidenceDirectoryPath = [IO.Path]::GetFullPath($EvidenceDirectory)
if ($evidenceDirectoryPath.StartsWith($repositoryRoot + [IO.Path]::DirectorySeparatorChar,
    [StringComparison]::OrdinalIgnoreCase)) {
    throw 'HU-035 evidence output must remain outside the repository.'
}
if (Test-Path -LiteralPath $evidenceDirectoryPath) {
    if ((Get-ChildItem -LiteralPath $evidenceDirectoryPath -Force | Select-Object -First 1)) {
        throw 'EvidenceDirectory must be new or empty; evidence is never overwritten.'
    }
}
else {
    New-Item -ItemType Directory -Path $evidenceDirectoryPath | Out-Null
}

$environmentNames = [System.Collections.Generic.List[string]]::new()
try {
    foreach ($line in $runtimeEnvironment) {
        if ([string]::IsNullOrWhiteSpace($line) -or $line.StartsWith('#', [StringComparison]::Ordinal)) { continue }
        $parts = $line.Split('=', 2)
        if ($parts.Count -ne 2) { throw 'Runtime environment contains an invalid line.' }
        [Environment]::SetEnvironmentVariable($parts[0], $parts[1], 'Process')
        $environmentNames.Add($parts[0])
    }

    & dotnet test (Join-Path $repositoryRoot 'tests/Sgol.IntegrationTests/Sgol.IntegrationTests.csproj') `
        --configuration Release --filter 'FullyQualifiedName~ContinuityPersistenceTests' `
        --logger "trx;LogFileName=$(Join-Path $evidenceDirectoryPath 'hu-035-postgresql.trx')"
    if ($LASTEXITCODE -ne 0) { throw 'HU-035 PostgreSQL append-only and uniqueness tests failed.' }

    $operationsProject = Join-Path $repositoryRoot 'src/Sgol.Operations/Sgol.Operations.csproj'
    $completeOutput = & dotnet run --project $operationsProject --configuration Release --no-build -- `
        complete-functional-reference --reconciliation-id $ReconciliationId --reference $ReferenceManifestUri `
        --backup-manifest $BackupManifestUri --replica-manifest $ReplicaManifestUri 2>&1
    if ($LASTEXITCODE -ne 0 -or $completeOutput -notcontains 'FUNCTIONAL_REFERENCE_READY') {
        throw 'HU-035 reference completion failed.'
    }
    [IO.File]::WriteAllLines((Join-Path $evidenceDirectoryPath 'reference-result.txt'), $completeOutput,
        [Text.UTF8Encoding]::new($false))

    $reconcileOutput = & dotnet run --project $operationsProject --configuration Release --no-build -- `
        reconcile-functional-restore --reconciliation-id $ReconciliationId `
        --reference-manifest $ReferenceManifestUri --restore-evidence $restoreEvidence 2>&1
    if ($LASTEXITCODE -ne 0 -or $reconcileOutput -notcontains 'FUNCTIONAL_RECOVERY_MATCHED') {
        throw 'HU-035 positive functional reconciliation failed.'
    }
    [IO.File]::WriteAllLines((Join-Path $evidenceDirectoryPath 'reconciliation-result.txt'), $reconcileOutput,
        [Text.UTF8Encoding]::new($false))

    $env:SGOL_HU035_EXTERNAL_TESTS = 'true'
    $env:SGOL_HU035_RECONCILIATION_ID = $ReconciliationId
    $env:SGOL_HU035_REFERENCE_MANIFEST_URI = $ReferenceManifestUri
    $env:SGOL_HU035_BACKUP_MANIFEST_URI = $BackupManifestUri
    $env:SGOL_HU035_REPLICA_MANIFEST_URI = $ReplicaManifestUri
    $env:SGOL_HU035_RESTORE_EVIDENCE_PATH = $restoreEvidence
    & dotnet test (Join-Path $repositoryRoot 'tests/Sgol.OperationsIntegrationTests/Sgol.OperationsIntegrationTests.csproj') `
        --configuration Release --filter 'Category=Hu035External' `
        --logger "trx;LogFileName=$(Join-Path $evidenceDirectoryPath 'hu-035-operations.trx')"
    if ($LASTEXITCODE -ne 0) { throw 'HU-035 immutable replay integration test failed.' }

    if (-not [string]::IsNullOrWhiteSpace($NegativeCasesFile)) {
        $negativePath = (Resolve-Path -LiteralPath $NegativeCasesFile).Path
        if ($negativePath.StartsWith($repositoryRoot + [IO.Path]::DirectorySeparatorChar,
            [StringComparison]::OrdinalIgnoreCase)) {
            throw 'Negative case definitions must remain outside the repository.'
        }
        $cases = @(Get-Content -Raw -LiteralPath $negativePath | ConvertFrom-Json)
        foreach ($case in $cases) {
            if ($case.name -notmatch '^[a-z0-9-]{1,48}$' -or
                $case.expectedCode -notmatch '^[A-Z][A-Z0-9_]{0,127}$') {
                throw 'Negative case metadata is invalid.'
            }
            $output = & dotnet run --project $operationsProject --configuration Release --no-build -- `
                reconcile-functional-restore --reconciliation-id ([string]$case.reconciliationId) `
                --reference-manifest ([string]$case.referenceManifestUri) `
                --restore-evidence ([string]$case.restoreEvidencePath) 2>&1
            if ($LASTEXITCODE -eq 0 -or $output -notcontains ([string]$case.expectedCode)) {
                throw "Negative case did not fail closed: $($case.name)"
            }
            [IO.File]::WriteAllLines((Join-Path $evidenceDirectoryPath ("negative-$($case.name).txt")),
                $output, [Text.UTF8Encoding]::new($false))
        }
    }

    Write-Output "PASS: HU-035 synthetic external reconciliation. Evidence: $evidenceDirectoryPath"
}
finally {
    @('SGOL_HU035_EXTERNAL_TESTS','SGOL_HU035_RECONCILIATION_ID',
        'SGOL_HU035_REFERENCE_MANIFEST_URI','SGOL_HU035_BACKUP_MANIFEST_URI',
        'SGOL_HU035_REPLICA_MANIFEST_URI','SGOL_HU035_RESTORE_EVIDENCE_PATH') |
        ForEach-Object { [Environment]::SetEnvironmentVariable($_, $null, 'Process') }
    foreach ($name in $environmentNames) {
        [Environment]::SetEnvironmentVariable($name, $null, 'Process')
    }
}
