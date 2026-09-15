using System.Diagnostics;
using System.Net;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Sgol.Operations;

public interface IObjectReplica
{
    Task ExecuteAsync(DateTimeOffset scheduledFor, CancellationToken cancellationToken);
}

public sealed class ObjectReplica(
    IConfiguration configuration,
    TimeProvider timeProvider,
    ILogger<ObjectReplica> logger) : IObjectReplica
{
    private const string EvidenceHashMetadata = "x-amz-meta-sgol-sha256";
    private const string EvidenceSizeMetadata = "x-amz-meta-sgol-size-bytes";
    private const string EvidenceMediaMetadata = "x-amz-meta-sgol-media-type";

    public async Task ExecuteAsync(DateTimeOffset scheduledFor, CancellationToken cancellationToken)
    {
        var started = Stopwatch.GetTimestamp();
        ReplicaOptions? options = null;
        DateTimeOffset? slot = null;
        var entries = new List<ObjectReplicaManifestEntry>();
        var stage = "CONFIGURATION";
        try
        {
            options = ReplicaOptions.FromConfiguration(configuration);
            options.Validate();
            var build = OperationBuildIdentity.FromConfiguration(configuration);
            slot = scheduledFor.ToUniversalTime();
            if (slot.Value.Minute != 5 || slot.Value.Second != 0)
            {
                throw new OperationsConfigurationException("REPLICA_SLOT_INVALID");
            }

            using var source = options.Source.CreateClient();
            using var destination = options.Destination.CreateClient();
            stage = "RECOVER_MANIFEST";
            if (await RecoverCompleteManifestAsync(destination, options, slot.Value, cancellationToken))
            {
                OperationsTelemetry.Record(
                    "object_replica", "recovered", Stopwatch.GetElapsedTime(started), 0, 0);
                OperationsLogs.Completed(logger, "object_replica", slot.Value, "RECOVERED");
                return;
            }

            stage = "LOAD_PREVIOUS_MANIFEST";
            var previous = await LoadPreviousManifestAsync(destination, options, slot.Value, cancellationToken);
            stage = "REPLICATE_QUARANTINE";
            await ReplicateBucketAsync(
                source, destination, options.SourceQuarantineBucket, options.DestinationQuarantineBucket,
                "quarantine", slot.Value.Hour == 0, options.BatchSize, previous, entries,
                timeProvider.GetUtcNow(), cancellationToken);
            stage = "REPLICATE_CLEAN";
            await ReplicateBucketAsync(
                source, destination, options.SourceCleanBucket, options.DestinationCleanBucket,
                "clean", slot.Value.Hour == 0, options.BatchSize, previous, entries,
                timeProvider.GetUtcNow(), cancellationToken);
            stage = "VERIFY_SOURCE_SET";
            AssertNoSilentSourceDeletion(previous, entries);

            stage = "PUBLISH_MANIFEST";
            await PublishManifestAsync(
                destination, options, build, slot.Value, "COMPLETE", null, entries, cancellationToken);
            OperationsTelemetry.Record(
                "object_replica", "succeeded", Stopwatch.GetElapsedTime(started), entries.Count,
                entries.Sum(item => item.Size));
            OperationsLogs.Completed(logger, "object_replica", slot.Value, "SUCCEEDED");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            var code = exception switch
            {
                OperationsConfigurationException configured => configured.ErrorCode,
                OperationsIntegrityException integrity => integrity.ErrorCode,
                AmazonS3Exception s3 when s3.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized =>
                    "REPLICA_AUTHORIZATION_FAILED",
                _ => "REPLICA_INFRASTRUCTURE_FAILED"
            };
            await TryPublishFailureAsync(options, slot, code, entries);
            OperationsTelemetry.Record("object_replica", "failed", Stopwatch.GetElapsedTime(started), 0, 0);
            OperationsLogs.Failed(logger, "object_replica", "FAILED", code);
            var failure = new Sgol.JobInfrastructure.JobExecutionException(code);
            failure.Data["SGOL_REPLICA_STAGE"] = stage;
            failure.Data["SGOL_REPLICA_EXCEPTION_TYPE"] = exception.GetType().Name;
            failure.Data["SGOL_REPLICA_HTTP_STATUS"] = exception is AmazonS3Exception storageFailure
                ? ((int)storageFailure.StatusCode).ToString(System.Globalization.CultureInfo.InvariantCulture)
                : "NONE";
            failure.Data["SGOL_REPLICA_OPERATION"] =
                exception.Data["SGOL_REPLICA_OPERATION"] as string ?? "UNKNOWN";
            throw failure;
        }
    }

    private static async Task ReplicateBucketAsync(
        IAmazonS3 source,
        IAmazonS3 destination,
        string sourceBucket,
        string destinationBucket,
        string bucketRole,
        bool fullVerification,
        int batchSize,
        ObjectReplicaManifest? previous,
        List<ObjectReplicaManifestEntry> entries,
        DateTimeOffset verifiedAt,
        CancellationToken cancellationToken)
    {
        string? continuation = null;
        do
        {
            var response = await source.ListObjectsV2Async(new ListObjectsV2Request
            {
                BucketName = sourceBucket,
                ContinuationToken = continuation,
                MaxKeys = batchSize
            }, cancellationToken);
            foreach (var item in response.S3Objects ?? [])
            {
                var itemSize = item.Size ?? throw new OperationsIntegrityException("SOURCE_OBJECT_SIZE_MISSING");
                var metadata = await source.GetObjectMetadataAsync(new GetObjectMetadataRequest
                {
                    BucketName = sourceBucket,
                    Key = item.Key
                }, cancellationToken);
                var expectedHash = metadata.Metadata[EvidenceHashMetadata];
                var expectedSize = metadata.Metadata[EvidenceSizeMetadata];
                var expectedMedia = metadata.Metadata[EvidenceMediaMetadata];
                if (!IsSha256(expectedHash) || metadata.ContentLength != itemSize ||
                    !long.TryParse(expectedSize, System.Globalization.NumberStyles.None,
                        System.Globalization.CultureInfo.InvariantCulture, out var declaredSize) ||
                    declaredSize != itemSize ||
                    string.IsNullOrWhiteSpace(expectedMedia))
                {
                    throw new OperationsIntegrityException("SOURCE_OBJECT_INTEGRITY_FAILED");
                }

                var destinationMatches = await HasDestinationMetadataAsync(
                    destination, destinationBucket, item.Key, expectedHash, itemSize, cancellationToken);
                string destinationHash;
                if (!destinationMatches)
                {
                    if (previous?.Objects.Any(previousItem =>
                            previousItem.BucketRole == bucketRole && previousItem.Key == item.Key) == true)
                    {
                        throw new OperationsIntegrityException("DESTINATION_OBJECT_MISSING");
                    }

                    var sourceObject = await ReadObjectHashAsync(source, sourceBucket, item.Key, cancellationToken);
                    if (sourceObject.Size != itemSize ||
                        !string.Equals(sourceObject.Hash, expectedHash, StringComparison.Ordinal))
                    {
                        throw new OperationsIntegrityException("SOURCE_OBJECT_HASH_MISMATCH");
                    }

                    var allowedMetadata = new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["sgol-sha256"] = expectedHash,
                        ["sgol-size-bytes"] = itemSize.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        ["sgol-media-type"] = expectedMedia
                    };
                    using var sourceResponse = await source.GetObjectAsync(
                        new GetObjectRequest { BucketName = sourceBucket, Key = item.Key }, cancellationToken);
                    await new S3OperationStore(destination).PutStreamVerifiedAsync(
                        destinationBucket, item.Key, sourceResponse.ResponseStream, itemSize, expectedHash,
                        metadata.Headers.ContentType ?? "application/octet-stream", allowedMetadata, cancellationToken);
                    destinationHash = sourceObject.Hash;
                }
                else if (fullVerification)
                {
                    destinationHash = (await ReadObjectHashAsync(
                        destination, destinationBucket, item.Key, cancellationToken)).Hash;
                    if (!string.Equals(destinationHash, expectedHash, StringComparison.Ordinal))
                    {
                        throw new OperationsIntegrityException("DESTINATION_OBJECT_CORRUPT");
                    }
                }
                else
                {
                    destinationHash = expectedHash;
                }

                entries.Add(new ObjectReplicaManifestEntry(
                    bucketRole, item.Key, itemSize, expectedHash, destinationHash, verifiedAt, "VERIFIED"));
            }

            continuation = response.IsTruncated == true ? response.NextContinuationToken : null;
        }
        while (continuation is not null);
    }

    private static async Task<bool> HasDestinationMetadataAsync(
        IAmazonS3 destination,
        string bucket,
        string key,
        string expectedHash,
        long expectedSize,
        CancellationToken cancellationToken)
    {
        try
        {
            var listed = await destination.ListObjectsV2Async(new ListObjectsV2Request
            {
                BucketName = bucket,
                Prefix = key,
                MaxKeys = 1
            }, cancellationToken);
            if ((listed.S3Objects ?? []).All(item => !string.Equals(item.Key, key, StringComparison.Ordinal)))
            {
                return false;
            }

            var metadata = await destination.GetObjectMetadataAsync(
                new GetObjectMetadataRequest { BucketName = bucket, Key = key }, cancellationToken);
            if (metadata.ContentLength != expectedSize ||
                !string.Equals(metadata.Metadata[EvidenceHashMetadata], expectedHash, StringComparison.Ordinal))
            {
                throw new OperationsIntegrityException("DESTINATION_OBJECT_CORRUPT");
            }

            return true;
        }
        catch (AmazonS3Exception exception)
        {
            exception.Data["SGOL_REPLICA_OPERATION"] = "CHECK_DESTINATION_METADATA";
            throw;
        }
    }

    private static async Task<ObjectReplicaManifest?> LoadPreviousManifestAsync(
        IAmazonS3 destination,
        ReplicaOptions options,
        DateTimeOffset currentSlot,
        CancellationToken cancellationToken)
    {
        string? continuation = null;
        var manifestKeys = new List<string>();
        do
        {
            var response = await destination.ListObjectsV2Async(new ListObjectsV2Request
            {
                BucketName = options.ManifestBucket,
                Prefix = options.ManifestPrefix + "/",
                ContinuationToken = continuation,
                MaxKeys = options.BatchSize
            }, cancellationToken);
            manifestKeys.AddRange((response.S3Objects ?? [])
                .Select(item => item.Key)
                .Where(key => Path.GetFileName(key).StartsWith("objects-", StringComparison.Ordinal) &&
                    !key.Contains("/failures/", StringComparison.Ordinal) &&
                    key.EndsWith(".manifest.json", StringComparison.Ordinal)));
            continuation = response.IsTruncated == true ? response.NextContinuationToken : null;
        }
        while (continuation is not null);

        var currentKey = $"{options.ManifestPrefix}/{currentSlot:yyyy/MM/dd}/objects-{currentSlot:yyyyMMdd'T'HHmmss'Z'}.manifest.json";
        var previousKey = manifestKeys
            .Where(key => string.CompareOrdinal(key, currentKey) < 0)
            .OrderByDescending(key => key, StringComparer.Ordinal)
            .FirstOrDefault();
        if (previousKey is null)
        {
            return null;
        }

        var previousBytes = (await new S3OperationStore(destination).ReadAsync(
            options.ManifestBucket, previousKey, cancellationToken)).Content;
        var previous = OperationManifestSerializer.Deserialize<ObjectReplicaManifest>(previousBytes);
        if (previous is not { SchemaVersion: 1, Kind: "SGOL_EVIDENCE_OBJECT_REPLICA", Status: "COMPLETE", ErrorClass: null } ||
            !previousBytes.AsSpan().SequenceEqual(OperationManifestSerializer.Serialize(previous)))
        {
            throw new OperationsIntegrityException("PREVIOUS_REPLICA_MANIFEST_INVALID");
        }

        return previous;
    }

    private static async Task<bool> RecoverCompleteManifestAsync(
        IAmazonS3 destination,
        ReplicaOptions options,
        DateTimeOffset slot,
        CancellationToken cancellationToken)
    {
        var key = $"{options.ManifestPrefix}/{slot:yyyy/MM/dd}/objects-{slot:yyyyMMdd'T'HHmmss'Z'}.manifest.json";
        var stored = await new S3OperationStore(destination).TryReadAsync(
            options.ManifestBucket, key, cancellationToken);
        if (stored is null)
        {
            return false;
        }

        var manifest = OperationManifestSerializer.Deserialize<ObjectReplicaManifest>(stored.Value.Content);
        if (manifest is not { SchemaVersion: 1, Kind: "SGOL_EVIDENCE_OBJECT_REPLICA", Status: "COMPLETE", ErrorClass: null } ||
            !stored.Value.Content.AsSpan().SequenceEqual(OperationManifestSerializer.Serialize(manifest)))
        {
            throw new OperationsIntegrityException("REPLICA_MANIFEST_INVALID");
        }

        foreach (var item in manifest.Objects)
        {
            var bucket = item.BucketRole switch
            {
                "quarantine" => options.DestinationQuarantineBucket,
                "clean" => options.DestinationCleanBucket,
                _ => throw new OperationsIntegrityException("REPLICA_BUCKET_ROLE_INVALID")
            };
            var actual = await ReadObjectHashAsync(destination, bucket, item.Key, cancellationToken);
            if (actual.Size != item.Size || !string.Equals(actual.Hash, item.DestinationSha256, StringComparison.Ordinal) ||
                !string.Equals(item.SourceSha256, item.DestinationSha256, StringComparison.Ordinal))
            {
                throw new OperationsIntegrityException("DESTINATION_OBJECT_CORRUPT");
            }
        }

        return true;
    }

    private static void AssertNoSilentSourceDeletion(
        ObjectReplicaManifest? previous,
        IReadOnlyCollection<ObjectReplicaManifestEntry> current)
    {
        if (previous is null)
        {
            return;
        }

        var currentKeys = current.Select(item => (item.BucketRole, item.Key)).ToHashSet();
        if (previous.Objects.Any(item => !currentKeys.Contains((item.BucketRole, item.Key))))
        {
            throw new OperationsIntegrityException("SOURCE_OBJECT_MISSING");
        }
    }

    private async Task TryPublishFailureAsync(
        ReplicaOptions? options,
        DateTimeOffset? slot,
        string errorClass,
        IReadOnlyCollection<ObjectReplicaManifestEntry> entries)
    {
        if (options is null || slot is null)
        {
            return;
        }

        try
        {
            using var destination = options.Destination.CreateClient();
            var build = OperationBuildIdentity.FromConfiguration(configuration);
            await PublishManifestAsync(
                destination, options, build, slot.Value, "FAILED", errorClass, entries, CancellationToken.None);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            OperationsLogs.Failed(logger, "object_replica_failure_manifest", "FAILED", "FAILURE_MANIFEST_UNAVAILABLE");
        }
    }

    private async Task PublishManifestAsync(
        IAmazonS3 destination,
        ReplicaOptions options,
        OperationBuildIdentity build,
        DateTimeOffset slot,
        string status,
        string? errorClass,
        IReadOnlyCollection<ObjectReplicaManifestEntry> entries,
        CancellationToken cancellationToken)
    {
        var manifest = new ObjectReplicaManifest(
            1,
            "SGOL_EVIDENCE_OBJECT_REPLICA",
            slot,
            timeProvider.GetUtcNow(),
            build.Revision,
            build.ImageDigest,
            status,
            errorClass,
            entries.OrderBy(item => item.BucketRole, StringComparer.Ordinal)
                .ThenBy(item => item.Key, StringComparer.Ordinal).ToArray());
        var directory = status == "COMPLETE" ? $"{options.ManifestPrefix}/{slot:yyyy/MM/dd}" :
            $"{options.ManifestPrefix}/failures/{slot:yyyy/MM/dd}";
        var suffix = status == "COMPLETE" ? string.Empty : $"-{timeProvider.GetUtcNow():yyyyMMdd'T'HHmmssfffffff'Z'}";
        var manifestKey = $"{directory}/objects-{slot:yyyyMMdd'T'HHmmss'Z'}{suffix}.manifest.json";
        await new S3OperationStore(destination).PutBytesVerifiedAsync(
            options.ManifestBucket,
            manifestKey,
            OperationManifestSerializer.Serialize(manifest),
            "application/json",
            metadata: null,
            cancellationToken);
    }

    private static async Task<(long Size, string Hash)> ReadObjectHashAsync(
        IAmazonS3 client,
        string bucket,
        string key,
        CancellationToken cancellationToken)
    {
        using var response = await client.GetObjectAsync(
            new GetObjectRequest { BucketName = bucket, Key = key }, cancellationToken);
        var (hash, size) = await OperationManifestSerializer.HashAsync(response.ResponseStream, cancellationToken);
        return (size, hash);
    }

    private static bool IsSha256(string? value) =>
        value is { Length: 64 } && value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');
}
