[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$requiredFiles = @(
    'F07_ADENDA_33_CONTRATO_DE_RECONCILIACION_Y_SIMULACRO_DE_RECUPERACION_HU_035.md',
    'F07_ADENDA_34_CONTRATO_DE_GATE_AMD64_AUTOMATIZADO_HU_035.md',
    'F07_ADENDA_35_DIAGNOSTICO_SANITIZADO_DE_REPLICA_HU_035.md',
    'F07_ADENDA_40_DIAGNOSTICO_CAUSAL_SANITIZADO_PUT_HU_035.md',
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
    'Invoke-ConcurrentReconciliation', 'Get-PrivateContainerAddress',
    'Set-RuntimeStorageEndpoints', 'sgol-hu035-$runIdentity-private',
    'VOLUME_ASSIGNMENT_FAILED', 'VOLUME_UPLOAD_FAILED', 'CONDITIONAL_LOOKUP_FAILED',
    'FINAL_ONLY', 'OUTSIDE_FINAL_NETWORK', 'dataNodeRegistration', 'writableCapacity')) {
    if ($amd64Gate.IndexOf($required, [StringComparison]::Ordinal) -lt 0) {
        throw "HU-035 AMD64 gate token is missing: $required"
    }
}
if ($amd64Gate.IndexOf('docker port', [StringComparison]::Ordinal) -ge 0) {
    throw 'HU-035 AMD64 gate must not depend on a published S3 port after private-network isolation.'
}
$syntheticEnvironment = Get-Content -Raw -LiteralPath (
    Join-Path $repositoryRoot 'scripts/operations/new-tech-ops-synthetic-environment.ps1')
foreach ($required in @('"-ip=$sourceContainer"', '"-ip=$destinationContainer"', "'-ip.bind=0.0.0.0'")) {
    if ($syntheticEnvironment.IndexOf($required, [StringComparison]::Ordinal) -lt 0) {
        throw "HU-035 storage advertised-address invariant is missing: $required"
    }
}
if ($syntheticEnvironment.IndexOf("'GRANT SET ON PARAMETER session_replication_role TO sgol_restore'",
        [StringComparison]::Ordinal) -lt 0 -or
    $syntheticEnvironment.IndexOf('SUPERUSER', [StringComparison]::OrdinalIgnoreCase) -ge 0) {
    throw 'HU-035 negative matrix requires only the exact synthetic mutation parameter grant.'
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

$continuityPersistence = Get-Content -Raw -LiteralPath (
    Join-Path $repositoryRoot 'src/Sgol.Web/Infrastructure/Persistence/Continuity/EfRecoveryReconciliationService.cs')
$operationsJobs = Get-Content -Raw -LiteralPath (Join-Path $repositoryRoot 'src/Sgol.Operations/OperationsJobs.cs')
foreach ($required in @(
    'JsonSerializer.Serialize(new { reconciliationId = id })',
    'TryReadReconciliationId(context.Checkpoint, out var reconciliationId)',
    'root.EnumerateObject().Count() == 1',
    'root.TryGetProperty("reconciliationId", out var value)'
)) {
    if ($continuityPersistence.IndexOf($required, [StringComparison]::Ordinal) -lt 0 -and
        $operationsJobs.IndexOf($required, [StringComparison]::Ordinal) -lt 0) {
        throw "HU-035 recovery reference checkpoint contract is missing: $required"
    }
}

$objectReplica = Get-Content -Raw -LiteralPath (Join-Path $repositoryRoot 'src/Sgol.Operations/ObjectReplica.cs')
$replicaOperations = @(
    'LIST_SOURCE_OBJECTS',
    'HEAD_SOURCE_METADATA',
    'READ_SOURCE_HASH',
    'GET_SOURCE_STREAM',
    'CHECK_DESTINATION_METADATA',
    'WRITE_AND_VERIFY_DESTINATION',
    'READ_DESTINATION_HASH'
)
$assignedReplicaOperations = [regex]::Matches(
    $objectReplica,
    'diagnostic\.Operation = "([A-Z_]+)";') |
    ForEach-Object { $_.Groups[1].Value } |
    Sort-Object -Unique
if ([string]::Join('|', $assignedReplicaOperations) -ne
    [string]::Join('|', ($replicaOperations | Sort-Object))) {
    throw 'HU-035 replica diagnostic operation assignments are not the closed seven-operation set.'
}

$operationAdjacency = @(
    'diagnostic\.Operation = "LIST_SOURCE_OBJECTS";\s*var response = await source\.ListObjectsV2Async',
    'diagnostic\.Operation = "HEAD_SOURCE_METADATA";\s*var metadata = await source\.GetObjectMetadataAsync',
    'diagnostic\.Operation = "CHECK_DESTINATION_METADATA";\s*var destinationMatches = await HasDestinationMetadataAsync',
    'diagnostic\.Operation = "READ_SOURCE_HASH";\s*var sourceObject = await ReadObjectHashAsync\(source,',
    'diagnostic\.Operation = "GET_SOURCE_STREAM";\s*using var sourceResponse = await source\.GetObjectAsync',
    'diagnostic\.Operation = "WRITE_AND_VERIFY_DESTINATION";\s*try\s*\{\s*await destinationStore\.PutStreamVerifiedAsync',
    'diagnostic\.Operation = "READ_DESTINATION_HASH";\s*destinationHash = \(await ReadObjectHashAsync\(\s*destination,',
    'diagnostic\.Operation = "READ_DESTINATION_HASH";\s*var actual = await ReadObjectHashAsync\(destination,'
)
foreach ($pattern in $operationAdjacency) {
    if (-not [regex]::IsMatch($objectReplica, $pattern)) {
        throw "HU-035 replica diagnostic marker is not adjacent to its approved operation: $pattern"
    }
}
if ([regex]::Matches($objectReplica,
        'diagnostic\.Operation = "WRITE_AND_VERIFY_DESTINATION";').Count -ne 1) {
    throw 'WRITE_AND_VERIFY_DESTINATION must identify only PutStreamVerifiedAsync.'
}
if ([regex]::Matches($objectReplica,
        'diagnostic\.Operation = "READ_DESTINATION_HASH";').Count -ne 2) {
    throw 'READ_DESTINATION_HASH must identify both complete destination hash reads.'
}

$catchStart = $objectReplica.IndexOf(
    'catch (Exception exception) when (exception is not OperationCanceledException)',
    [StringComparison]::Ordinal)
$failureManifest = $objectReplica.IndexOf(
    'await TryPublishFailureAsync(options, slot, code, entries);',
    [StringComparison]::Ordinal)
if ($catchStart -lt 0 -or $failureManifest -lt 0 -or $failureManifest -le $catchStart) {
    throw 'HU-035 replica failure catch or failure-manifest call is missing.'
}
$captureCall = $objectReplica.IndexOf('var failure = CaptureFailure(exception, stage, diagnostic);', $catchStart, [StringComparison]::Ordinal)
if ($captureCall -lt $catchStart -or $captureCall -ge $failureManifest) {
    throw 'HU-035 must capture the failure before publishing its failure manifest.'
}
$captureBody = [regex]::Match($objectReplica,
    '(?s)private static Sgol\.JobInfrastructure\.JobExecutionException CaptureFailure\(.*?(?=private static string ClassifyFailure)').Value
foreach ($captured in @(
    'var capturedStage = NormalizeStage(stage);',
    'var capturedOperation = NormalizeOperation(diagnostic.Operation);',
    'var capturedExceptionType = SanitizeDiagnosticToken(exception.GetType().Name);',
    'var capturedHttpStatus =',
    'var capturedS3ErrorCode ='
)) {
    if ($captureBody.IndexOf($captured, [StringComparison]::Ordinal) -lt 0) {
        throw "HU-035 replica diagnostic is not captured before failure-manifest publication: $captured"
    }
}
$store = Get-Content -Raw -LiteralPath (Join-Path $repositoryRoot 'src/Sgol.Operations/S3OperationStore.cs')
$innerOperations = @('PUT_DESTINATION', 'GET_DESTINATION_VERIFY', 'HEAD_DESTINATION_METADATA')
$markedOperations = [regex]::Matches($store, 'MarkReplicaWriteOperation\("([A-Z_]+)"\)') |
    ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique
if ([string]::Join('|', $markedOperations) -ne [string]::Join('|', (($innerOperations + 'UNKNOWN') | Sort-Object))) {
    throw 'HU-035 internal write operations are not the closed three-operation set.'
}
foreach ($operation in ($replicaOperations + $innerOperations)) {
    $mapping = '"' + $operation + '" => "' + $operation + '"'
    if (-not $objectReplica.Contains($mapping) -or -not $amd64Test.Contains($mapping)) {
        throw "HU-035 operation does not propagate through both normalizers: $operation"
    }
}
foreach ($pattern in @(
    'MarkReplicaWriteOperation\("PUT_DESTINATION"\);\s*await client\.PutObjectAsync\(request, cancellationToken\);\s*MarkReplicaWriteOperation\("UNKNOWN"\);',
    'MarkReplicaWriteOperation\("GET_DESTINATION_VERIFY"\);\s*using var response = await client\.GetObjectAsync',
    'response.ResponseStream, cancellationToken\);\s*MarkReplicaWriteOperation\("UNKNOWN"\);\s*if \(actualSize',
    'MarkReplicaWriteOperation\("HEAD_DESTINATION_METADATA"\);\s*var metadata = await client\.GetObjectMetadataAsync',
    'new GetObjectMetadataRequest \{ BucketName = bucket, Key = key \}, cancellationToken\);\s*MarkReplicaWriteOperation\("UNKNOWN"\);',
    'exception.StatusCode == HttpStatusCode.NotFound\)\s*\{\s*MarkReplicaWriteOperation\("UNKNOWN"\);\s*return false;',
    'exception.StatusCode is HttpStatusCode.Conflict or HttpStatusCode.PreconditionFailed\)\s*\{\s*if \(!await ExistsWithHashAsync\(bucket, key, sha256, size, cancellationToken\)\)',
    'if \(CaptureReplicaWriteOperation\)\s*ReplicaWriteOperation = operation;'
)) {
    if (-not [regex]::IsMatch($store, $pattern)) { throw "HU-035 storage diagnostic invariant missing: $pattern" }
}
if ([regex]::Matches($objectReplica, 'CaptureReplicaWriteOperation = true').Count -ne 1 -or
    -not [regex]::IsMatch($objectReplica, 'catch\s*\{\s*diagnostic.Operation = NormalizeOperation\(destinationStore.ReplicaWriteOperation\);\s*throw;') -or
    -not $objectReplica.Contains('catch (Exception exception) when (exception is not OperationCanceledException)') -or
    -not $captureBody.Contains('return CreateFailure(ClassifyFailure(exception), capturedStage, capturedOperation,')) {
    throw 'HU-035 write diagnostics must remain opt-in and preserve exception/cancellation propagation.'
}
foreach ($requestToken in @('InputStream = input', 'AutoCloseStream = false', 'IfNoneMatch = "*"',
    'request.Headers.ContentLength = size;', 'request.Metadata["sha256"] = sha256;',
    'request.Metadata[item.Key] = item.Value;', 'await VerifyObjectAsync(bucket, key, expectedHash, expectedSize, cancellationToken);')) {
    if (-not $store.Contains($requestToken)) { throw "HU-035 storage request changed: $requestToken" }
}
foreach ($forbidden in @('exception.Message', 'exception.StackTrace', 'exception.InnerException',
    'exception.ToString()')) {
    if ($objectReplica.IndexOf($forbidden, [StringComparison]::Ordinal) -ge 0) {
        throw "HU-035 replica diagnostic exposes forbidden exception data: $forbidden"
    }
}
foreach ($required in @(
    'BucketName = sourceBucket',
    'ContinuationToken = continuation',
    'MaxKeys = batchSize',
    'new GetObjectMetadataRequest',
    'new GetObjectRequest { BucketName = sourceBucket, Key = item.Key }',
    'destinationBucket, item.Key, sourceResponse.ResponseStream, itemSize, expectedHash',
    'metadata.Headers.ContentType ?? "application/octet-stream", allowedMetadata, cancellationToken'
)) {
    if ($objectReplica.IndexOf($required, [StringComparison]::Ordinal) -lt 0) {
        throw "HU-035 replica request contract changed or is missing: $required"
    }
}
foreach ($required in @(
    'const int maximumAttempts = 3;',
    'exception.ErrorCode == "REPLICA_INFRASTRUCTURE_FAILED" && attempt < maximumAttempts',
    'S3CODE={s3ErrorCode}',
    'Category", "Hu035ReplicaDiagnostics',
    'HU035_REFERENCE_DISPATCH_FAILED',
    'OUTBOX_RESULT={NormalizeOutboxResult(outboxResult)}',
    'OUTBOX_ERROR={NormalizeReferenceOutboxError(outboxError)}',
    'JOB_STATUS={NormalizeReferenceJobStatus(jobStatus)}',
    'JOB_ERROR={NormalizeReferenceJobError(jobError)}',
    'HU035_REFERENCE_CAPTURE_FAILED',
    'RECONCILIATION_STATUS={NormalizeReferenceReconciliationStatus(reconciliationStatus)}',
    'RECONCILIATION_ERROR={NormalizeReferenceReconciliationError(reconciliationError)}'
)) {
    if ($amd64Test.IndexOf($required, [StringComparison]::Ordinal) -lt 0) {
        throw "HU-035 replica diagnostic gate contract is missing: $required"
    }
}
if ($amd64Test.IndexOf('HTTP={httpStatus}", exception', [StringComparison]::Ordinal) -ge 0) {
    throw 'HU-035 replica diagnostic gate must not attach the original exception.'
}
$functionalRecovery = Get-Content -Raw -LiteralPath (Join-Path $repositoryRoot 'src/Sgol.Operations/FunctionalRecoveryOperations.cs')
$operationsJobs = Get-Content -Raw -LiteralPath (Join-Path $repositoryRoot 'src/Sgol.Operations/OperationsJobs.cs')
foreach ($required in @(
    'CAPTURE_SNAPSHOT',
    'EXPORT_BACKUP',
    'PUT_BACKUP',
    'PUT_BACKUP_MANIFEST',
    'READ_REPLICA_MANIFEST',
    'PUT_REFERENCE_SNAPSHOT',
    'PUT_REFERENCE_MANIFEST',
    'S3_INTERNAL_ERROR',
    'S3_ERROR',
    'POSTGRESQL_ERROR',
    'EXTERNAL_PROCESS_ERROR',
    'IO_ERROR',
    'UNEXPECTED',
    'OperationsReferenceCaptureException')) {
    if ($functionalRecovery.IndexOf($required, [StringComparison]::Ordinal) -lt 0) {
        throw "HU-035 reference capture classifier token is missing: $required"
    }
}
if ($functionalRecovery.IndexOf('new OperationsReferenceCaptureException($"REFERENCE_{stage}_{classification}")',
        [StringComparison]::Ordinal) -lt 0 -or
    $operationsJobs.IndexOf('OperationsReferenceCaptureException reference => reference.ErrorCode',
        [StringComparison]::Ordinal) -lt 0) {
    throw 'HU-035 reference capture failure is not persisted with its closed stage and class.'
}
if ($functionalRecovery.IndexOf('internal sealed class OperationsReferenceCaptureException(string errorCode) : Exception(errorCode)',
        [StringComparison]::Ordinal) -lt 0 -or
    $functionalRecovery.IndexOf('OperationsReferenceCaptureException(string errorCode) : Exception(errorCode,',
        [StringComparison]::Ordinal) -ge 0) {
    throw 'HU-035 reference capture classifier must not retain an original exception.'
}
$snapshotReader = Get-Content -Raw -LiteralPath (Join-Path $repositoryRoot 'src/Sgol.Operations/FunctionalSnapshotReader.cs')
if ($snapshotReader.IndexOf('SELECT \"MigrationId\" FROM \"__EFMigrationsHistory\" ORDER BY \"MigrationId\" DESC LIMIT 1',
        [StringComparison]::Ordinal) -lt 0 -or
    $snapshotReader.IndexOf('SELECT migration_id FROM', [StringComparison]::Ordinal) -ge 0) {
    throw 'HU-035 snapshot reader must query the exact EF migrations history identifier.'
}
if ($amd64Test.IndexOf('FunctionalSnapshotReader.CaptureReferenceAsync(', [StringComparison]::Ordinal) -lt 0 -or
    $amd64Test.IndexOf('FunctionalSnapshotSchema.Tables.Length', [StringComparison]::Ordinal) -lt 0) {
    throw 'HU-035 PostgreSQL fixture must execute the real functional snapshot reader.'
}
$gate = Get-Content -Raw -LiteralPath (Join-Path $repositoryRoot 'scripts/operations/invoke-hu-035-amd64-gate.ps1')
if ($gate.IndexOf("`$ageContent = `$wrapperTemplate.Replace('docker run --rm --platform',", [StringComparison]::Ordinal) -lt 0 -or
    $gate.IndexOf("'docker run --rm --interactive --platform'", [StringComparison]::Ordinal) -lt 0 -or
    ([regex]::Matches($gate, '--interactive')).Count -ne 1) {
    throw 'HU-035 age wrapper must keep stdin attached without changing the pg_dump wrapper.'
}
if ($gate.IndexOf('function Format-Hu035ReconcileFailure', [StringComparison]::Ordinal) -lt 0 -or
    $gate.IndexOf('HU035_RECONCILE_FAILED:CASE=', [StringComparison]::Ordinal) -lt 0 -or
    $gate.IndexOf("if (`$approvedErrors -contains `$candidate)", [StringComparison]::Ordinal) -lt 0 -or
    $gate.IndexOf("if (`$Arguments[0] -eq 'reconcile-functional-restore')", [StringComparison]::Ordinal) -lt 0) {
    throw 'HU-035 unexpected reconciliation failures must use the approved closed diagnostic.'
}
if ($gate.IndexOf('function Format-Hu035RestoreVerifyFailure', [StringComparison]::Ordinal) -lt 0 -or
    $gate.IndexOf('HU035_RESTORE_VERIFY_FAILED:CASE=', [StringComparison]::Ordinal) -lt 0 -or
    $gate.IndexOf("if (`$Arguments[0] -eq 'verify-postgresql-backup')", [StringComparison]::Ordinal) -lt 0) {
    throw 'HU-035 unexpected restore verification failures must use the approved closed diagnostic.'
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
