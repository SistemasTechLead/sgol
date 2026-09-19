using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sgol.Operations;

public sealed record OperationBuildIdentity(string Revision, string ImageDigest)
{
    public static OperationBuildIdentity FromConfiguration(Microsoft.Extensions.Configuration.IConfiguration configuration)
    {
        var revision = BackupOptions.Require(configuration, "SGOL_REVISION");
        var imageDigest = BackupOptions.Require(configuration, "SGOL_IMAGE_DIGEST");
        if (revision.Length != 40 ||
            revision.Any(character => character is not (>= '0' and <= '9' or >= 'a' and <= 'f')) ||
            imageDigest.Length != 71 || !imageDigest.StartsWith("sha256:", StringComparison.Ordinal) ||
            imageDigest[7..].Any(character => character is not (>= '0' and <= '9' or >= 'a' and <= 'f')))
        {
            throw new OperationsConfigurationException("BUILD_IDENTITY_INVALID");
        }

        return new OperationBuildIdentity(revision, imageDigest);
    }
}

public static class OperationManifestSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };

    public static byte[] Serialize<T>(T value)
    {
        using var source = JsonDocument.Parse(JsonSerializer.SerializeToUtf8Bytes(value, Options));
        using var output = new MemoryStream();
        using (var writer = new Utf8JsonWriter(output, new JsonWriterOptions { Indented = false }))
        {
            WriteCanonical(writer, source.RootElement);
        }

        return output.ToArray();
    }

    public static T Deserialize<T>(ReadOnlySpan<byte> value) =>
        JsonSerializer.Deserialize<T>(value, Options)
        ?? throw new OperationsIntegrityException("MANIFEST_INVALID");

    public static async Task<(string Hash, long Size)> HashAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[64 * 1024];
        long size = 0;
        while (true)
        {
            var read = await stream.ReadAsync(buffer, cancellationToken);
            if (read == 0)
            {
                break;
            }

            size += read;
            hash.AppendData(buffer.AsSpan(0, read));
        }

        return (Convert.ToHexStringLower(hash.GetHashAndReset()), size);
    }

    public static string Fingerprint(string value) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)))[..16];

    private static void WriteCanonical(Utf8JsonWriter writer, JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in element.EnumerateObject().OrderBy(item => item.Name, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(property.Name);
                    WriteCanonical(writer, property.Value);
                }

                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray())
                {
                    WriteCanonical(writer, item);
                }

                writer.WriteEndArray();
                break;
            case JsonValueKind.String:
                writer.WriteStringValue(element.GetString());
                break;
            case JsonValueKind.Number:
                writer.WriteRawValue(element.GetRawText(), skipInputValidation: false);
                break;
            case JsonValueKind.True:
                writer.WriteBooleanValue(true);
                break;
            case JsonValueKind.False:
                writer.WriteBooleanValue(false);
                break;
            case JsonValueKind.Null:
                writer.WriteNullValue();
                break;
            default:
                throw new OperationsIntegrityException("MANIFEST_VALUE_INVALID");
        }
    }
}

public sealed record PostgreSqlBackupManifest(
    int SchemaVersion,
    string Kind,
    DateTimeOffset ScheduledFor,
    DateTimeOffset CreatedAt,
    string Revision,
    string ImageDigest,
    string PostgreSqlVersion,
    string PgDumpVersion,
    string AgeVersion,
    string Format,
    string Algorithm,
    string RecipientFingerprint,
    long ByteCount,
    string Sha256,
    string Key,
    string Status);

public sealed record ObjectReplicaManifest(
    int SchemaVersion,
    string Kind,
    DateTimeOffset ScheduledFor,
    DateTimeOffset CreatedAt,
    string Revision,
    string ImageDigest,
    string Status,
    string? ErrorClass,
    IReadOnlyList<ObjectReplicaManifestEntry> Objects);

public sealed record ObjectReplicaManifestEntry(
    string BucketRole,
    string Key,
    long Size,
    string SourceSha256,
    string DestinationSha256,
    DateTimeOffset VerifiedAt,
    string Status);

public sealed record FunctionalRecoveryReferenceManifest(
    int SchemaVersion,
    string Kind,
    Guid ReconciliationId,
    Guid BranchId,
    DateTimeOffset TargetRecoveryAt,
    DateTimeOffset ReferenceCapturedAt,
    string Revision,
    string ImageDigest,
    string Migration,
    string SnapshotKey,
    string SnapshotSha256,
    string SnapshotRootSha256,
    string BackupManifestKey,
    string BackupManifestSha256,
    string ReplicaManifestKey,
    string ReplicaManifestSha256,
    DateTimeOffset ReplicaScheduledFor,
    string Status);

public sealed record FunctionalRecoveryReportManifest(
    int SchemaVersion,
    string Kind,
    Guid ReconciliationId,
    Guid BranchId,
    DateTimeOffset CompletedAt,
    string Status,
    string ReferenceRootSha256,
    string ActualRootSha256,
    int DifferenceCount,
    bool DifferencesTruncated,
    long DatabaseRpoSeconds,
    long ObjectRpoSeconds,
    long ObservedRpoSeconds,
    long ObservedRtoSeconds,
    string ReferenceManifestSha256,
    string RestoreEvidenceSha256,
    IReadOnlyDictionary<string, int> DifferenceSummary);

public sealed class OperationsIntegrityException(string errorCode) : Exception
{
    public string ErrorCode { get; } = errorCode;
}
