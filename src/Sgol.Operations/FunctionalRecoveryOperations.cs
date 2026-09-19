using System.Text.Json;
using System.Diagnostics;
using System.Net;
using System.ComponentModel;
using Amazon.Runtime;
using Amazon.S3;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Sgol.Continuity.Contracts;
using Sgol.Organization.Contracts;

namespace Sgol.Operations;

public sealed class FunctionalRecoveryOperations(
    IConfiguration configuration,
    IBackupProcessPipeline processPipeline,
    TimeProvider timeProvider) : IFunctionalRecoveryOperations
{
    private const string ContractBaselineMigration = "20260912213000_AddPortableDataProtectionKeyRing";

    public async Task<FunctionalReferenceReceipt> CaptureReferenceAsync(
        Guid reconciliationId,
        CancellationToken cancellationToken)
    {
        if (reconciliationId == Guid.Empty) throw new OperationsConfigurationException("RECONCILIATION_ID_INVALID");
        var options = BackupOptions.FromConfiguration(configuration);
        options.Validate();
        if (!SameDatabase(options.ParseConnection().ConnectionString,
                BackupOptions.RequireSecret(configuration, "ConnectionStrings:Sgol")))
            throw new OperationsConfigurationException("REFERENCE_BACKUP_SNAPSHOT_MISMATCH");
        var replica = ReplicaOptions.FromConfiguration(configuration);
        replica.Validate();
        var build = OperationBuildIdentity.FromConfiguration(configuration);
        var replicaUri = BackupOptions.Require(configuration, "Continuity:ReplicaManifestUri");
        var (replicaBucket, replicaKey) = ParseS3Uri(replicaUri);
        if (replicaBucket != replica.ManifestBucket || !replicaKey.StartsWith(replica.ManifestPrefix + "/", StringComparison.Ordinal))
            throw new OperationsConfigurationException("REPLICA_MANIFEST_URI_INVALID");

        var directory = Path.Combine(Path.GetTempPath(), "sgol-continuity", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var encryptedPath = Path.Combine(directory, "backup.dump.age");
        BackupProcessResult? process = null;
        var stage = "CAPTURE_SNAPSHOT";
        try
        {
            var exported = await FunctionalSnapshotReader.CaptureReferenceAsync(
                options.ParseConnection().ConnectionString, reconciliationId, BranchScope.LorettaId,
                build.Revision, build.ImageDigest,
                async (snapshotId, token) =>
                {
                    stage = "EXPORT_BACKUP";
                    process = await processPipeline.CreateEncryptedDumpFromSnapshotAsync(
                        options, encryptedPath, snapshotId, token);
                    stage = "CAPTURE_SNAPSHOT";
                }, cancellationToken);
            if (process is null) throw new OperationsIntegrityException("REFERENCE_BACKUP_SNAPSHOT_MISMATCH");
            stage = "PUT_BACKUP";
            await using var encrypted = new FileStream(encryptedPath, FileMode.Open, FileAccess.Read, FileShare.Read,
                64 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
            var (backupHash, backupSize) = await OperationManifestSerializer.HashAsync(encrypted, cancellationToken);
            if (backupSize <= 0) throw new OperationsIntegrityException("BACKUP_EMPTY");
            var prefix = $"{options.Prefix}/continuity/v1/{reconciliationId:D}";
            var backupKey = prefix + "/postgresql.dump.age";
            using var backupClient = options.Storage.CreateClient();
            var backupStore = new S3OperationStore(backupClient);
            await backupStore.PutFileVerifiedAsync(options.Bucket, backupKey, encryptedPath, backupHash,
                "application/octet-stream", cancellationToken);
            stage = "PUT_BACKUP_MANIFEST";
            var serverVersion = await ReadServerVersionAsync(options.ParseConnection(), cancellationToken);
            var backupManifest = new PostgreSqlBackupManifest(1, "SGOL_POSTGRESQL_PORTABLE_BACKUP",
                exported.Snapshot.CapturedAt, timeProvider.GetUtcNow(), build.Revision, build.ImageDigest,
                serverVersion, process.PgDumpVersion, process.AgeVersion, "postgresql-custom", "age-x25519",
                OperationManifestSerializer.Fingerprint(options.Recipient), backupSize, backupHash, backupKey, "COMPLETE");
            var backupManifestBytes = OperationManifestSerializer.Serialize(backupManifest);
            var backupManifestKey = backupKey + ".manifest.json";
            await backupStore.PutBytesVerifiedAsync(options.Bucket, backupManifestKey, backupManifestBytes,
                "application/json", null, cancellationToken);
            var backupManifestHash = Hash(backupManifestBytes);

            using var replicaClient = replica.Destination.CreateClient();
            var replicaStore = new S3OperationStore(replicaClient);
            stage = "READ_REPLICA_MANIFEST";
            var replicaObject = await replicaStore.TryReadAsync(replicaBucket, replicaKey, cancellationToken)
                ?? throw new OperationsIntegrityException("REPLICA_MANIFEST_INVALID");
            var replicaManifest = OperationManifestSerializer.Deserialize<ObjectReplicaManifest>(replicaObject.Content);
            if (replicaManifest is not { SchemaVersion: 1, Kind: "SGOL_EVIDENCE_OBJECT_REPLICA", Status: "COMPLETE", ErrorClass: null } ||
                replicaManifest.ScheduledFor > exported.Snapshot.CapturedAt ||
                replicaManifest.Objects.Any(item => item.Status != "VERIFIED" || item.SourceSha256 != item.DestinationSha256))
                throw new OperationsIntegrityException("REPLICA_MANIFEST_INVALID");

            var snapshotBytes = OperationManifestSerializer.Serialize(exported.Snapshot);
            var snapshotKey = prefix + "/reference.snapshot.json";
            stage = "PUT_REFERENCE_SNAPSHOT";
            await backupStore.PutBytesVerifiedAsync(options.Bucket, snapshotKey, snapshotBytes, "application/json", null,
                cancellationToken);
            var reference = new FunctionalRecoveryReferenceManifest(1, "SGOL_FUNCTIONAL_RECOVERY_REFERENCE",
                reconciliationId, BranchScope.LorettaId, exported.Snapshot.CapturedAt, exported.Snapshot.CapturedAt,
                build.Revision, build.ImageDigest,
                ContractBaselineMigration, snapshotKey, Hash(snapshotBytes), exported.Snapshot.RootSha256, backupManifestKey,
                backupManifestHash, replicaKey, replicaObject.Sha256, replicaManifest.ScheduledFor, "COMPLETE");
            var referenceBytes = OperationManifestSerializer.Serialize(reference);
            var referenceKey = prefix + "/reference.manifest.json";
            stage = "PUT_REFERENCE_MANIFEST";
            await backupStore.PutBytesVerifiedAsync(options.Bucket, referenceKey, referenceBytes, "application/json", null,
                cancellationToken);
            return new(exported.Snapshot.CapturedAt, $"s3://{options.Bucket}/{referenceKey}", Hash(referenceBytes),
                exported.Snapshot.RootSha256);
        }
        catch (OperationsIntegrityException exception) when (exception.ErrorCode == "IMMUTABLE_OBJECT_CONFLICT")
        {
            throw new OperationsIntegrityException("RECONCILIATION_IMMUTABLE_CONFLICT");
        }
        catch (Exception exception) when (exception is not OperationCanceledException and
            not OperationsConfigurationException and not OperationsIntegrityException and
            not RecoveryContractException and not OperationsReferenceCaptureException)
        {
            throw CreateReferenceCaptureFailure(stage, exception);
        }
        finally
        {
            DeletePrivateTemporary(directory, encryptedPath);
        }
    }

    public async Task<FunctionalReconciliationReceipt> ReconcileAsync(Guid reconciliationId,
        string referenceManifestUri, string restoreEvidencePath, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        if (reconciliationId == Guid.Empty || !File.Exists(restoreEvidencePath))
            throw new OperationsConfigurationException("RESTORE_EVIDENCE_INVALID");
        var options = BackupOptions.FromConfiguration(configuration);
        options.Validate();
        var replica = ReplicaOptions.FromConfiguration(configuration);
        replica.Validate();
        var build = OperationBuildIdentity.FromConfiguration(configuration);
        var (bucket, referenceKey) = ParseS3Uri(referenceManifestUri);
        if (bucket != options.Bucket) throw new OperationsConfigurationException("REFERENCE_MANIFEST_URI_INVALID");
        using var backupClient = options.Storage.CreateClient();
        var backupStore = new S3OperationStore(backupClient);
        var referenceObject = await backupStore.TryReadAsync(bucket, referenceKey, cancellationToken)
            ?? throw new OperationsIntegrityException("REFERENCE_MISSING");
        EnsureArtifactSize(referenceObject.Content);
        var reference = OperationManifestSerializer.Deserialize<FunctionalRecoveryReferenceManifest>(referenceObject.Content);
        if (reference is not { SchemaVersion: 1, Kind: "SGOL_FUNCTIONAL_RECOVERY_REFERENCE", Status: "COMPLETE" } ||
            reference.ReconciliationId != reconciliationId || reference.BranchId != BranchScope.LorettaId ||
            reference.TargetRecoveryAt != reference.ReferenceCapturedAt ||
            reference.Migration != ContractBaselineMigration || reference.Revision != build.Revision ||
            reference.ImageDigest != build.ImageDigest ||
            !referenceObject.Content.AsSpan().SequenceEqual(OperationManifestSerializer.Serialize(reference)))
            throw new OperationsIntegrityException("REFERENCE_CORRUPT");
        var snapshotObject = await backupStore.TryReadAsync(bucket, reference.SnapshotKey, cancellationToken)
            ?? throw new OperationsIntegrityException("REFERENCE_CORRUPT");
        EnsureArtifactSize(snapshotObject.Content);
        if (snapshotObject.Sha256 != reference.SnapshotSha256) throw new OperationsIntegrityException("REFERENCE_CORRUPT");
        var expected = OperationManifestSerializer.Deserialize<FunctionalSnapshot>(snapshotObject.Content);
        if (expected.RootSha256 != reference.SnapshotRootSha256 ||
            !snapshotObject.Content.AsSpan().SequenceEqual(OperationManifestSerializer.Serialize(expected)))
            throw new OperationsIntegrityException("REFERENCE_CORRUPT");
        FunctionalSnapshotContract.ValidateIntegrity(expected);
        var backupManifestObject = await backupStore.TryReadAsync(bucket, reference.BackupManifestKey, cancellationToken)
            ?? throw new OperationsIntegrityException("BACKUP_MANIFEST_INVALID");
        EnsureArtifactSize(backupManifestObject.Content);
        if (backupManifestObject.Sha256 != reference.BackupManifestSha256)
            throw new OperationsIntegrityException("BACKUP_MANIFEST_INVALID");
        var backupManifest = OperationManifestSerializer.Deserialize<PostgreSqlBackupManifest>(backupManifestObject.Content);
        if (backupManifest is not { SchemaVersion: 1, Kind: "SGOL_POSTGRESQL_PORTABLE_BACKUP", Status: "COMPLETE" } ||
            backupManifest.ScheduledFor != reference.TargetRecoveryAt)
            throw new OperationsIntegrityException("REFERENCE_BACKUP_SNAPSHOT_MISMATCH");
        var restoreEvidenceBytes = await File.ReadAllBytesAsync(restoreEvidencePath, cancellationToken);
        using var restoreEvidence = JsonDocument.Parse(restoreEvidenceBytes);
        var startedAt = ValidateRestoreEvidence(restoreEvidence.RootElement, reference, backupManifest);
        var restoreEvidenceHash = Hash(restoreEvidenceBytes);
        var prefix = $"{options.Prefix}/continuity/v1/{reconciliationId:D}";
        var existingReport = await backupStore.TryReadAsync(bucket, prefix + "/report.manifest.json", cancellationToken);
        if (existingReport is not null)
        {
            EnsureArtifactSize(existingReport.Value.Content);
            var prior = OperationManifestSerializer.Deserialize<FunctionalRecoveryReportManifest>(existingReport.Value.Content);
            if (prior is not { SchemaVersion: 1, Kind: "SGOL_FUNCTIONAL_RECOVERY_REPORT" } ||
                prior.ReconciliationId != reconciliationId || prior.ReferenceManifestSha256 != referenceObject.Sha256 ||
                prior.RestoreEvidenceSha256 != restoreEvidenceHash ||
                !existingReport.Value.Content.AsSpan().SequenceEqual(OperationManifestSerializer.Serialize(prior)))
                throw new OperationsIntegrityException("RECONCILIATION_IMMUTABLE_CONFLICT");
            var priorResult = new FunctionalReconciliationResult(prior.Status, prior.ReferenceRootSha256,
                prior.ActualRootSha256, prior.DifferenceCount, prior.DifferencesTruncated, []);
            var priorObjectives = new RecoveryObjectives(prior.DatabaseRpoSeconds, prior.ObjectRpoSeconds,
                prior.ObservedRpoSeconds, prior.ObservedRtoSeconds);
            stopwatch.Stop();
            FunctionalRecoveryTelemetry.Record(priorResult, priorObjectives, reference.TargetRecoveryAt,
                prior.CompletedAt, stopwatch.Elapsed.TotalSeconds);
            return new(prior.CompletedAt, existingReport.Value.Sha256, priorResult, priorObjectives);
        }

        var restoreConnection = BackupOptions.RequireSecret(configuration, "Restore:PostgreSql:ConnectionString");
        var primaryConnection = BackupOptions.RequireSecret(configuration, "ConnectionStrings:Sgol");
        if (!string.Equals(BackupOptions.Require(configuration, "SGOL_SYNTHETIC_ONLY"), "true",
                StringComparison.Ordinal) || SameDatabase(restoreConnection, primaryConnection))
            throw new OperationsConfigurationException("RESTORE_PRIMARY_TARGET_REJECTED");
        await VerifyRestoredSecurityAsync(restoreConnection, cancellationToken);
        var actual = await FunctionalSnapshotReader.CaptureActualAsync(restoreConnection, reconciliationId,
            BranchScope.LorettaId, reference.TargetRecoveryAt, build.Revision, build.ImageDigest, cancellationToken);
        var result = FunctionalSnapshotReconciler.Compare(expected, actual);

        var objectDifferences = await VerifyReplicaObjectsAsync(replica, reference, expected, cancellationToken);
        if (objectDifferences.Count != 0)
        {
            var combined = result.Differences.Concat(objectDifferences.Select((item, index) => item with
            {
                Ordinal = result.Differences.Count + index + 1
            })).Take(FunctionalSnapshotContract.MaximumDifferences).ToArray();
            var truncated = result.TotalDifferences + objectDifferences.Count > FunctionalSnapshotContract.MaximumDifferences;
            result = new(truncated ? RecoveryReconciliationStatuses.Failed : RecoveryReconciliationStatuses.Different,
                result.ExpectedRootSha256,
                result.ActualRootSha256, result.TotalDifferences + objectDifferences.Count,
                result.Truncated || truncated,
                combined);
        }

        var completedAt = timeProvider.GetUtcNow();
        var objectives = RecoveryObjectiveCalculator.Calculate(reference.TargetRecoveryAt,
            reference.TargetRecoveryAt, reference.ReplicaScheduledFor, startedAt, completedAt);
        var status = result.Status == RecoveryReconciliationStatuses.Matched && objectives.MeetsRpo && objectives.MeetsRto
            ? RecoveryReconciliationStatuses.Matched : result.Status == RecoveryReconciliationStatuses.Different
                ? RecoveryReconciliationStatuses.Different : RecoveryReconciliationStatuses.Failed;
        var report = new FunctionalRecoveryReportManifest(1, "SGOL_FUNCTIONAL_RECOVERY_REPORT", reconciliationId,
            BranchScope.LorettaId, completedAt, status, result.ExpectedRootSha256, result.ActualRootSha256,
            result.TotalDifferences, result.Truncated, objectives.DatabaseRpoSeconds, objectives.ObjectRpoSeconds,
            objectives.ObservedRpoSeconds, objectives.ObservedRtoSeconds,
            referenceObject.Sha256, restoreEvidenceHash, result.Differences.GroupBy(item => item.Kind,
                StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal));
        var actualBytes = OperationManifestSerializer.Serialize(actual);
        var reportBytes = OperationManifestSerializer.Serialize(report);
        try
        {
            await backupStore.PutBytesVerifiedAsync(bucket, prefix + "/actual.snapshot.json", actualBytes,
                "application/json", null, cancellationToken);
            await backupStore.PutBytesVerifiedAsync(bucket, prefix + "/report.manifest.json", reportBytes,
                "application/json", null, cancellationToken);
        }
        catch (OperationsIntegrityException exception) when (exception.ErrorCode == "IMMUTABLE_OBJECT_CONFLICT")
        {
            throw new OperationsIntegrityException("RECONCILIATION_IMMUTABLE_CONFLICT");
        }
        stopwatch.Stop();
        FunctionalRecoveryTelemetry.Record(result, objectives, reference.TargetRecoveryAt, completedAt,
            stopwatch.Elapsed.TotalSeconds);
        return new(completedAt, Hash(reportBytes), result, objectives);
    }

    public async Task<FunctionalReferenceReceipt> CompleteReferenceAsync(Guid reconciliationId,
        string referenceManifestUri, string backupManifestUri, string replicaManifestUri,
        CancellationToken cancellationToken = default)
    {
        var options = BackupOptions.FromConfiguration(configuration);
        options.Validate();
        var replica = ReplicaOptions.FromConfiguration(configuration);
        replica.Validate();
        var (referenceBucket, referenceKey) = ParseS3Uri(referenceManifestUri);
        var (backupBucket, backupKey) = ParseS3Uri(backupManifestUri);
        var (replicaBucket, replicaKey) = ParseS3Uri(replicaManifestUri);
        if (referenceBucket != options.Bucket || backupBucket != options.Bucket ||
            replicaBucket != replica.ManifestBucket)
            throw new OperationsConfigurationException("REFERENCE_ARTIFACT_LOCATION_INVALID");

        using var primaryClient = options.Storage.CreateClient();
        var primaryStore = new S3OperationStore(primaryClient);
        var referenceObject = await primaryStore.TryReadAsync(referenceBucket, referenceKey, cancellationToken)
            ?? throw new OperationsIntegrityException("REFERENCE_MISSING");
        EnsureArtifactSize(referenceObject.Content);
        var reference = OperationManifestSerializer.Deserialize<FunctionalRecoveryReferenceManifest>(referenceObject.Content);
        if (reference is not { SchemaVersion: 1, Kind: "SGOL_FUNCTIONAL_RECOVERY_REFERENCE", Status: "COMPLETE" } ||
            reference.ReconciliationId != reconciliationId || reference.BackupManifestKey != backupKey ||
            reference.ReplicaManifestKey != replicaKey || reference.TargetRecoveryAt != reference.ReferenceCapturedAt ||
            !referenceObject.Content.AsSpan().SequenceEqual(OperationManifestSerializer.Serialize(reference)))
            throw new OperationsIntegrityException("REFERENCE_CORRUPT");
        var backupObject = await primaryStore.TryReadAsync(backupBucket, backupKey, cancellationToken)
            ?? throw new OperationsIntegrityException("BACKUP_MANIFEST_INVALID");
        EnsureArtifactSize(backupObject.Content);
        if (backupObject.Sha256 != reference.BackupManifestSha256)
            throw new OperationsIntegrityException("BACKUP_MANIFEST_INVALID");
        var backup = OperationManifestSerializer.Deserialize<PostgreSqlBackupManifest>(backupObject.Content);
        if (backup is not { SchemaVersion: 1, Kind: "SGOL_POSTGRESQL_PORTABLE_BACKUP", Status: "COMPLETE" } ||
            backup.ScheduledFor != reference.TargetRecoveryAt ||
            !backupObject.Content.AsSpan().SequenceEqual(OperationManifestSerializer.Serialize(backup)))
            throw new OperationsIntegrityException("REFERENCE_BACKUP_SNAPSHOT_MISMATCH");
        using var replicaClient = replica.Destination.CreateClient();
        var replicaStore = new S3OperationStore(replicaClient);
        var replicaObject = await replicaStore.TryReadAsync(replicaBucket, replicaKey, cancellationToken)
            ?? throw new OperationsIntegrityException("REPLICA_MANIFEST_INVALID");
        EnsureArtifactSize(replicaObject.Content);
        if (replicaObject.Sha256 != reference.ReplicaManifestSha256)
            throw new OperationsIntegrityException("REPLICA_MANIFEST_INVALID");
        var replicaManifest = OperationManifestSerializer.Deserialize<ObjectReplicaManifest>(replicaObject.Content);
        if (replicaManifest is not { SchemaVersion: 1, Kind: "SGOL_EVIDENCE_OBJECT_REPLICA", Status: "COMPLETE", ErrorClass: null } ||
            replicaManifest.Objects.Any(item => item.Status != "VERIFIED" || item.SourceSha256 != item.DestinationSha256) ||
            !replicaObject.Content.AsSpan().SequenceEqual(OperationManifestSerializer.Serialize(replicaManifest)))
            throw new OperationsIntegrityException("REPLICA_MANIFEST_INVALID");
        return new(reference.TargetRecoveryAt, referenceManifestUri, referenceObject.Sha256,
            reference.SnapshotRootSha256);
    }

    private static async Task<IReadOnlyList<RecoveryDifference>> VerifyReplicaObjectsAsync(
        ReplicaOptions options, FunctionalRecoveryReferenceManifest reference, FunctionalSnapshot expected,
        CancellationToken cancellationToken)
    {
        using var client = options.Destination.CreateClient();
        var store = new S3OperationStore(client);
        var manifestRead = await TryReadReplicaManifestAsync(store, options.ManifestBucket,
            reference.ReplicaManifestKey, cancellationToken);
        if (manifestRead.Inaccessible)
        {
            return
            [
                new(1, "evidence", "file_object", FunctionalSnapshotContract.Hash(reference.ReplicaManifestKey),
                    null, RecoveryDifferenceKinds.EvidenceInaccessible, reference.ReplicaManifestSha256, null)
            ];
        }
        var manifestObject = manifestRead.Manifest
            ?? throw new OperationsIntegrityException("REPLICA_MANIFEST_INVALID");
        EnsureArtifactSize(manifestObject.Content);
        if (manifestObject.Sha256 != reference.ReplicaManifestSha256)
            throw new OperationsIntegrityException("REPLICA_MANIFEST_INVALID");
        var manifest = OperationManifestSerializer.Deserialize<ObjectReplicaManifest>(manifestObject.Content);
        var differences = new List<RecoveryDifference>();
        var requiredFiles = expected.Tables.Single(table => table.Name == "file_object").Records;
        foreach (var required in requiredFiles)
        {
            var fields = required.Fields.ToDictionary(field => field.Name, field => field.Sha256, StringComparer.Ordinal);
            var present = manifest.Objects.Any(item =>
                fields.GetValueOrDefault("object_key") == FunctionalSnapshotContract.HashValue(item.Key) &&
                fields.GetValueOrDefault("sha256") == FunctionalSnapshotContract.HashValue(item.SourceSha256) &&
                fields.GetValueOrDefault("size_bytes") == FunctionalSnapshotContract.HashValue(item.Size) &&
                fields.GetValueOrDefault("bucket_class") ==
                    FunctionalSnapshotContract.HashValue(item.BucketRole.ToUpperInvariant()));
            if (!present)
                differences.Add(new(differences.Count + 1, "evidence", "file_object", required.StableKey,
                    null, RecoveryDifferenceKinds.EvidenceMissing, required.RowSha256, null));
        }
        foreach (var item in manifest.Objects)
        {
            if (item.Size is < 1 or > FunctionalSnapshotContract.MaximumObjectBytes)
                throw new OperationsIntegrityException("CARDINALITY_LIMIT_EXCEEDED");
            var targetBucket = item.BucketRole switch
            {
                "quarantine" => options.DestinationQuarantineBucket,
                "clean" => options.DestinationCleanBucket,
                _ => throw new OperationsIntegrityException("REPLICA_MANIFEST_INVALID")
            };
            try
            {
                await store.VerifyObjectAsync(targetBucket, item.Key, item.DestinationSha256, item.Size, cancellationToken);
            }
            catch (AmazonS3Exception exception) when (exception.StatusCode == HttpStatusCode.NotFound)
            {
                differences.Add(new(differences.Count + 1, "evidence", "file_object",
                    FunctionalSnapshotContract.Hash(item.Key), null, RecoveryDifferenceKinds.EvidenceMissing,
                    item.SourceSha256, null));
            }
            catch (AmazonS3Exception)
            {
                differences.Add(new(differences.Count + 1, "evidence", "file_object",
                    FunctionalSnapshotContract.Hash(item.Key), null, RecoveryDifferenceKinds.EvidenceInaccessible,
                    item.SourceSha256, null));
            }
            catch (AmazonServiceException)
            {
                differences.Add(new(differences.Count + 1, "evidence", "file_object",
                    FunctionalSnapshotContract.Hash(item.Key), null, RecoveryDifferenceKinds.EvidenceInaccessible,
                    item.SourceSha256, null));
            }
            catch (HttpRequestException)
            {
                differences.Add(new(differences.Count + 1, "evidence", "file_object",
                    FunctionalSnapshotContract.Hash(item.Key), null, RecoveryDifferenceKinds.EvidenceInaccessible,
                    item.SourceSha256, null));
            }
            catch (OperationsIntegrityException)
            {
                differences.Add(new(differences.Count + 1, "evidence", "file_object",
                    FunctionalSnapshotContract.Hash(item.Key), "sha256", RecoveryDifferenceKinds.EvidenceCorrupt,
                    item.SourceSha256, null));
            }
        }
        return differences;
    }

    internal static async Task<((byte[] Content, string Sha256)? Manifest, bool Inaccessible)>
        TryReadReplicaManifestAsync(S3OperationStore store, string bucket, string key,
            CancellationToken cancellationToken)
    {
        try
        {
            return (await store.TryReadAsync(bucket, key, cancellationToken), false);
        }
        catch (AmazonS3Exception exception) when (exception.StatusCode != HttpStatusCode.NotFound)
        {
            return (null, true);
        }
        catch (AmazonServiceException)
        {
            return (null, true);
        }
        catch (HttpRequestException)
        {
            return (null, true);
        }
    }

    private static DateTimeOffset RequiredUtc(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String ||
            !value.TryGetDateTimeOffset(out var result) || result.Offset != TimeSpan.Zero)
            throw new OperationsIntegrityException("RESTORE_EVIDENCE_INVALID");
        return result;
    }

    private static DateTimeOffset ValidateRestoreEvidence(JsonElement root,
        FunctionalRecoveryReferenceManifest reference, PostgreSqlBackupManifest backup)
    {
        if (!root.TryGetProperty("kind", out var kind) || kind.GetString() != "SGOL_TECHNICAL_RESTORE_EVIDENCE" ||
            !root.TryGetProperty("result", out var result) || result.GetString() != "BACKUP_RESTORE_VERIFIED" ||
            !root.TryGetProperty("imageDigest", out var image) || image.GetString() != reference.ImageDigest ||
            !root.TryGetProperty("backupSha256", out var backupHash) || backupHash.GetString() != backup.Sha256 ||
            !root.TryGetProperty("manifestSha256", out var manifestHash) ||
            manifestHash.GetString() != reference.BackupManifestSha256 ||
            !root.TryGetProperty("migration", out var migration) || migration.GetString() != reference.Migration ||
            !root.TryGetProperty("isolatedTarget", out var isolated) || isolated.ValueKind != JsonValueKind.True ||
            !root.TryGetProperty("durationMilliseconds", out var duration) || !duration.TryGetInt64(out var elapsed) ||
            elapsed < 0)
            throw new OperationsIntegrityException("RESTORE_EVIDENCE_INVALID");
        var startedAt = RequiredUtc(root, "startedAt");
        var completedAt = RequiredUtc(root, "completedAt");
        if (completedAt < startedAt) throw new OperationsIntegrityException("RESTORE_EVIDENCE_INVALID");
        return startedAt;
    }

    private static async Task<string> ReadServerVersionAsync(NpgsqlConnectionStringBuilder builder,
        CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(builder.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand("SELECT current_setting('server_version')", connection);
        return (string?)await command.ExecuteScalarAsync(cancellationToken) ?? "unknown";
    }

    private static async Task VerifyRestoredSecurityAsync(string connectionString,
        CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        const string sql = """
            SELECT
                EXISTS (SELECT 1 FROM data_protection_key WHERE length(xml) > 0),
                EXISTS (SELECT 1 FROM identity_credential WHERE length(password_hash) > 0),
                EXISTS (
                    SELECT 1
                    FROM app_user u
                    JOIN person p ON p.id = u.person_id
                    JOIN employment_version e ON e.person_id = p.id
                    JOIN role_assignment_version r ON r.user_id = u.id
                    WHERE u.status = 'ACTIVA' AND length(u.security_stamp) > 0
                      AND e.branch_id = @branch_id AND e.status = 'ACTIVA' AND e.valid_to IS NULL
                      AND r.branch_id = @branch_id AND r.status = 'ACTIVO' AND r.valid_to IS NULL
                      AND r.role_code = 'DIRECCION')
            """;
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("branch_id", BranchScope.LorettaId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken) || !reader.GetBoolean(0) || !reader.GetBoolean(1) ||
            !reader.GetBoolean(2))
            throw new OperationsIntegrityException("RESTORE_EVIDENCE_INVALID");
    }

    private static (string Bucket, string Key) ParseS3Uri(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme != "s3" ||
            string.IsNullOrWhiteSpace(uri.Host) || uri.AbsolutePath.Length <= 1 || uri.Query.Length != 0 || uri.Fragment.Length != 0)
            throw new OperationsConfigurationException("S3_URI_INVALID");
        return (uri.Host, Uri.UnescapeDataString(uri.AbsolutePath[1..]));
    }

    private static bool SameDatabase(string first, string second)
    {
        var left = new NpgsqlConnectionStringBuilder(first);
        var right = new NpgsqlConnectionStringBuilder(second);
        return string.Equals(left.Host, right.Host, StringComparison.OrdinalIgnoreCase) && left.Port == right.Port &&
            string.Equals(left.Database, right.Database, StringComparison.Ordinal);
    }

    private static string Hash(byte[] value) =>
        Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(value));

    private static OperationsReferenceCaptureException CreateReferenceCaptureFailure(
        string stage, Exception exception)
    {
        var classification = exception switch
        {
            AmazonS3Exception s3 when s3.StatusCode == HttpStatusCode.InternalServerError &&
                string.Equals(s3.ErrorCode, "InternalError", StringComparison.Ordinal) => "S3_INTERNAL_ERROR",
            AmazonS3Exception => "S3_ERROR",
            NpgsqlException => "POSTGRESQL_ERROR",
            Win32Exception => "EXTERNAL_PROCESS_ERROR",
            IOException or UnauthorizedAccessException => "IO_ERROR",
            _ => "UNEXPECTED"
        };
        return new OperationsReferenceCaptureException($"REFERENCE_{stage}_{classification}");
    }

    private static void EnsureArtifactSize(byte[] value)
    {
        if (value.LongLength > FunctionalSnapshotContract.MaximumSnapshotBytes)
            throw new OperationsIntegrityException("CARDINALITY_LIMIT_EXCEEDED");
    }

    private static void DeletePrivateTemporary(string directory, string file)
    {
        try { if (File.Exists(file)) File.Delete(file); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { }
        try { if (Directory.Exists(directory)) Directory.Delete(directory, false); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { }
    }
}

internal sealed class OperationsReferenceCaptureException(string errorCode) : Exception(errorCode)
{
    public string ErrorCode { get; } = errorCode;
}
