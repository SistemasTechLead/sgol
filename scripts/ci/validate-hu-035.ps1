[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$requiredFiles = @(
    'F07_ADENDA_33_CONTRATO_DE_RECONCILIACION_Y_SIMULACRO_DE_RECUPERACION_HU_035.md',
    'F07_ADENDA_34_CONTRATO_DE_GATE_AMD64_AUTOMATIZADO_HU_035.md',
    'src/Modules/Continuity/Contracts/RecoveryReconciliation.cs',
    'src/Sgol.Operations/FunctionalSnapshotReader.cs',
    'src/Sgol.Operations/FunctionalRecoveryOperations.cs',
    'src/Sgol.Web/Interface/Endpoints/ContinuityApiEndpoints.cs',
    'src/Sgol.Web/Infrastructure/Persistence/Continuity/EfRecoveryReconciliationService.cs',
    'src/Sgol.Web/Infrastructure/Persistence/Migrations/20260914210503_AddRecoveryReconciliation.cs',
    'tests/Sgol.UnitTests/ContinuityContractTests.cs',
    'tests/Sgol.UnitTests/ContinuityApiEndpointTests.cs',
    'tests/Sgol.ArchitectureTests/ContinuityArchitectureTests.cs',
    'tests/Sgol.IntegrationTests/ContinuityPersistenceTests.cs',
    'tests/Sgol.OperationsIntegrationTests/FunctionalRecoveryExternalTests.cs',
    'tests/Sgol.OperationsIntegrationTests/FunctionalRecoveryAmd64GateTests.cs',
    'scripts/operations/verify-hu-035-external.ps1',
    'scripts/operations/invoke-hu-035-amd64-gate.ps1',
    'docs/operations/functional-recovery-reconciliation.md'
)
foreach ($relativePath in $requiredFiles) {
    if (-not (Test-Path -LiteralPath (Join-Path $repositoryRoot $relativePath) -PathType Leaf)) {
        throw "HU-035 required file is missing: $relativePath"
    }
}

$contract = Get-Content -Raw -LiteralPath (Join-Path $repositoryRoot 'src/Modules/Continuity/Contracts/RecoveryReconciliation.cs')
foreach ($required in @(
    'PER-CONTINUIDAD-VER', 'SGOL-CANON-1', 'SGOL-FUNCTIONAL-SNAPSHOT-1',
    'IDENTITY_MISSING', 'LINK_MISSING', 'LINK_CHANGED', 'VERSION_CHANGED', 'EVIDENCE_CORRUPT',
    'AUDIT_MISSING', 'UNEXPECTED_POST_RECOVERY_RECORD',
    'MaximumDifferences = 100_000', 'MaximumRowsPerTable = 1_000_000',
    'MaximumSnapshotBytes = 512L * 1024 * 1024', 'MaximumObjectBytes = 10L * 1024 * 1024 * 1024'
)) {
    if ($contract.IndexOf($required, [StringComparison]::Ordinal) -lt 0) {
        throw "HU-035 contract token is missing: $required"
    }
}
if ([regex]::Matches($contract, '"[a-z_]+"').Value |
    Where-Object { $_ -in @('"branch"','"person"','"employment_version"') } |
    Measure-Object | Select-Object -ExpandProperty Count | Where-Object { $_ -lt 3 }) {
    throw 'HU-035 functional table allowlist is incomplete.'
}

$reader = Get-Content -Raw -LiteralPath (Join-Path $repositoryRoot 'src/Sgol.Operations/FunctionalSnapshotReader.cs')
foreach ($required in @('SET TRANSACTION READ ONLY', "SET LOCAL TIME ZONE 'UTC'", 'pg_export_snapshot()',
    '20260912213000_AddPortableDataProtectionKeyRing', '20260914210503_AddRecoveryReconciliation')) {
    if ($reader.IndexOf($required, [StringComparison]::Ordinal) -lt 0) {
        throw "Snapshot reader contract is missing: $required"
    }
}

$operations = Get-Content -Raw -LiteralPath (Join-Path $repositoryRoot 'src/Sgol.Operations/Program.cs')
foreach ($required in @('complete-functional-reference', 'reconcile-functional-restore',
    'FUNCTIONAL_REFERENCE_READY', 'FUNCTIONAL_RECOVERY_MATCHED', 'pg_try_advisory_lock',
    'pg_advisory_unlock')) {
    if ($operations.IndexOf($required, [StringComparison]::Ordinal) -lt 0) {
        throw "Operations command is missing: $required"
    }
}

$migrationPath = Join-Path $repositoryRoot 'src/Sgol.Web/Infrastructure/Persistence/Migrations/20260914210503_AddRecoveryReconciliation.cs'
$migrationBytes = [IO.File]::ReadAllBytes($migrationPath)
if ($migrationBytes.Length -ge 3 -and $migrationBytes[0] -eq 0xEF -and
    $migrationBytes[1] -eq 0xBB -and $migrationBytes[2] -eq 0xBF) {
    throw 'HU-035 migration contains a UTF-8 BOM.'
}
$migration = [Text.Encoding]::UTF8.GetString($migrationBytes)
if ([regex]::Matches($migration, 'BEFORE UPDATE OR DELETE').Count -ne 3 -or
    $migration.IndexOf("ERRCODE = '55000'", [StringComparison]::Ordinal) -lt 0 -or
    $migration.IndexOf('throw new NotSupportedException', [StringComparison]::Ordinal) -lt 0 -or
    $migration.IndexOf('DropTable', [StringComparison]::Ordinal) -ge 0) {
    throw 'HU-035 migration is not expand-only and append-only.'
}

$endpoint = Get-Content -Raw -LiteralPath (Join-Path $repositoryRoot 'src/Sgol.Web/Interface/Endpoints/ContinuityApiEndpoints.cs')
if ([regex]::Matches($endpoint, 'endpoints\.Map').Count -ne 3 -or
    $endpoint.IndexOf('IdempotencyKeyHeader.Parse', [StringComparison]::Ordinal) -lt 0 -or
    $endpoint -match '(?i)manifestUri|connectionString|signedUrl|secretKey') {
    throw 'HU-035 API surface or minimization contract is invalid.'
}

$sourceFiles = Get-ChildItem -LiteralPath (Join-Path $repositoryRoot 'src') -Recurse -File -Filter '*.cs' |
    Where-Object { $_.FullName -notmatch '[\\/]obj[\\/]|[\\/]bin[\\/]' }
$domainText = Get-ChildItem -LiteralPath (Join-Path $repositoryRoot 'src/Modules/Continuity') -Recurse -File -Filter '*.cs' |
    ForEach-Object { Get-Content -Raw -LiteralPath $_.FullName }
if (($domainText -join "`n") -match 'Amazon\.S3|Npgsql|EntityFrameworkCore|AspNetCore') {
    throw 'Continuity domain depends on infrastructure.'
}
if ($sourceFiles | Select-String -Pattern 'DELETE FROM recovery_reconciliation|DROP TABLE recovery_reconciliation' -CaseSensitive:$false) {
    throw 'Destructive recovery reconciliation SQL detected outside the migration contract.'
}

foreach ($scriptPath in @(
    (Join-Path $repositoryRoot 'scripts/ci/validate-hu-035.ps1'),
    (Join-Path $repositoryRoot 'scripts/operations/verify-hu-035-external.ps1'),
    (Join-Path $repositoryRoot 'scripts/operations/invoke-hu-035-amd64-gate.ps1')
)) {
    $tokens = $null
    $parseErrors = $null
    [void][Management.Automation.Language.Parser]::ParseFile($scriptPath, [ref]$tokens, [ref]$parseErrors)
    if ($parseErrors.Count -ne 0) { throw "PowerShell syntax validation failed: $scriptPath" }
}

$amd64Gate = Get-Content -Raw -LiteralPath (
    Join-Path $repositoryRoot 'scripts/operations/invoke-hu-035-amd64-gate.ps1')
foreach ($required in @('AMD64_LINUX_HOST_REQUIRED', 'SGOL_HU035_AMD64_GATE',
    'FUNCTIONAL_RECOVERY_MATCHED', 'SGOL_TECHNICAL_RESTORE_EVIDENCE',
    'runtime-private', 'evidence-public', 'Remove-Item', 'Reset-RestoreDatabase',
    'Invoke-ConcurrentReconciliation', 'sgol-hu035-$runIdentity-private')) {
    if ($amd64Gate.IndexOf($required, [StringComparison]::Ordinal) -lt 0) {
        throw "HU-035 AMD64 gate token is missing: $required"
    }
}
$amd64Test = Get-Content -Raw -LiteralPath (
    Join-Path $repositoryRoot 'tests/Sgol.OperationsIntegrationTests/FunctionalRecoveryAmd64GateTests.cs')
foreach ($required in @('positive','identity_missing','identity_additional','link_missing','link_altered',
    'version_changed','count_changed','evidence_missing','evidence_corrupt','evidence_inaccessible',
    'audit_missing','audit_altered','reference_corrupt','rpo_exceeded','rto_exceeded','primary_target',
    'replay_conflict','concurrency')) {
    if ($amd64Test.IndexOf('"' + $required + '"', [StringComparison]::Ordinal) -lt 0) {
        throw "HU-035 AMD64 approved case is missing: $required"
    }
}
$workflow = Get-Content -Raw -LiteralPath (Join-Path $repositoryRoot '.github/workflows/pull-request.yml')
if ($workflow.IndexOf('invoke-hu-035-amd64-gate.ps1', [StringComparison]::Ordinal) -lt 0 -or
    $workflow.IndexOf('sgol-hu-035-amd64/evidence-public/**', [StringComparison]::Ordinal) -lt 0) {
    throw 'HU-035 AMD64 gate is not integrated into the pull-request workflow.'
}

$protected = & git -C $repositoryRoot status --short -- Fuentes docs/design/logo.svg docs/design/mapa-pantallas.md
if ($LASTEXITCODE -ne 0) { throw 'Could not verify protected paths.' }
if ($protected) { throw "Protected paths changed: $protected" }

Write-Output 'PASS: HU-035 contract, snapshots, API, migration, operations and protected paths.'
