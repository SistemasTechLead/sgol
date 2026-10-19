using Amazon.S3;
using Amazon.Runtime;
using Microsoft.Extensions.Configuration;
using Npgsql;
using System.Net;
using System.Net.Sockets;

namespace Sgol.Operations;

public sealed record S3EndpointOptions(
    string Endpoint,
    string Region,
    string AccessKey,
    string SecretKey,
    bool AllowInsecureTransport = false,
    int MaximumAttempts = 3,
    int TimeoutSeconds = 60)
{
    public IAmazonS3 CreateClient()
    {
        if (!Uri.TryCreate(Endpoint, UriKind.Absolute, out var endpoint) ||
            !IsTransportAllowed(endpoint, AllowInsecureTransport) ||
            string.IsNullOrWhiteSpace(Region) || string.IsNullOrWhiteSpace(AccessKey) ||
            string.IsNullOrWhiteSpace(SecretKey) || IsPlaceholder(AccessKey) || IsPlaceholder(SecretKey) ||
            MaximumAttempts != 3 || TimeoutSeconds is < 1 or > 300)
        {
            throw new OperationsConfigurationException("S3_CONFIGURATION_INVALID");
        }

        return new AmazonS3Client(
            new BasicAWSCredentials(AccessKey, SecretKey),
            new AmazonS3Config
            {
                ServiceURL = Endpoint,
                AuthenticationRegion = Region,
                ForcePathStyle = true,
                UseHttp = endpoint.Scheme == Uri.UriSchemeHttp,
                RetryMode = RequestRetryMode.Standard,
                MaxErrorRetry = MaximumAttempts - 1,
                Timeout = TimeSpan.FromSeconds(TimeoutSeconds)
            });
    }

    internal static bool IsPlaceholder(string value) =>
        value.Contains("REQUIRED_", StringComparison.OrdinalIgnoreCase) ||
        value.Contains("example.invalid", StringComparison.OrdinalIgnoreCase);

    private static bool IsTransportAllowed(Uri endpoint, bool allowInsecure) =>
        endpoint.Scheme == Uri.UriSchemeHttps ||
        allowInsecure && endpoint.Scheme == Uri.UriSchemeHttp && IsPrivateOrLoopback(endpoint);

    private static bool IsPrivateOrLoopback(Uri endpoint)
    {
        if (endpoint.IsLoopback || string.Equals(endpoint.Host, "localhost", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!IPAddress.TryParse(endpoint.Host, out var address))
        {
            return false;
        }

        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            var bytes = address.GetAddressBytes();
            return bytes[0] == 10 ||
                bytes[0] == 192 && bytes[1] == 168 ||
                bytes[0] == 172 && bytes[1] is >= 16 and <= 31;
        }

        return address.IsIPv6UniqueLocal;
    }
}

public sealed record BackupOptions(
    string PostgreSqlConnectionString,
    S3EndpointOptions Storage,
    string Bucket,
    string Prefix,
    string Recipient,
    string PgDumpPath,
    string AgePath,
    int ProcessTimeoutSeconds)
{
    public static BackupOptions FromConfiguration(IConfiguration configuration) => new(
        RequireSecret(configuration, "Backup:PostgreSql:ConnectionString"),
        new S3EndpointOptions(
            Require(configuration, "Backup:Storage:Endpoint"),
            Require(configuration, "Backup:Storage:Region"),
            RequireSecret(configuration, "Backup:Storage:AccessKey"),
            RequireSecret(configuration, "Backup:Storage:SecretKey"),
            configuration.GetValue<bool>("Backup:Storage:AllowInsecureTransport"),
            RequireInteger(configuration, "Backup:MaximumAttempts"),
            RequireInteger(configuration, "Backup:StorageTimeoutSeconds")),
        Require(configuration, "Backup:Storage:Bucket"),
        Require(configuration, "Backup:Storage:Prefix"),
        Require(configuration, "Backup:Encryption:Recipient"),
        Require(configuration, "Backup:PgDumpPath"),
        Require(configuration, "Backup:AgePath"),
        RequireInteger(configuration, "Backup:ProcessTimeoutSeconds"));

    public NpgsqlConnectionStringBuilder ParseConnection()
    {
        try
        {
            var parsed = new NpgsqlConnectionStringBuilder(PostgreSqlConnectionString);
            if (string.IsNullOrWhiteSpace(parsed.Host) || string.IsNullOrWhiteSpace(parsed.Database) ||
                string.IsNullOrWhiteSpace(parsed.Username) || string.IsNullOrWhiteSpace(parsed.Password))
            {
                throw new OperationsConfigurationException("BACKUP_DATABASE_CONFIGURATION_INVALID");
            }

            return parsed;
        }
        catch (ArgumentException)
        {
            throw new OperationsConfigurationException("BACKUP_DATABASE_CONFIGURATION_INVALID");
        }
    }

    public void Validate()
    {
        _ = ParseConnection();
        if (!BucketContract.IsValid(Bucket) || !PrefixContract.IsValid(Prefix) ||
            !Recipient.StartsWith("age1", StringComparison.Ordinal) || Recipient.Length < 20 ||
            !Path.IsPathFullyQualified(PgDumpPath) || !Path.IsPathFullyQualified(AgePath) ||
            ProcessTimeoutSeconds is < 30 or > 3600)
        {
            throw new OperationsConfigurationException("BACKUP_CONFIGURATION_INVALID");
        }
    }

    internal static string Require(IConfiguration configuration, string key) =>
        configuration[key] is { Length: > 0 } value && !S3EndpointOptions.IsPlaceholder(value)
            ? value
            : throw new OperationsConfigurationException("OPERATIONS_CONFIGURATION_REQUIRED");

    internal static string RequireSecret(IConfiguration configuration, string key) => Require(configuration, key);

    internal static int RequireInteger(IConfiguration configuration, string key) =>
        int.TryParse(configuration[key], System.Globalization.NumberStyles.None,
            System.Globalization.CultureInfo.InvariantCulture, out var value)
            ? value
            : throw new OperationsConfigurationException("OPERATIONS_CONFIGURATION_REQUIRED");
}

public sealed record ReplicaOptions(
    S3EndpointOptions Source,
    S3EndpointOptions Destination,
    string SourceQuarantineBucket,
    string SourceCleanBucket,
    string DestinationQuarantineBucket,
    string DestinationCleanBucket,
    string ManifestBucket,
    string ManifestPrefix,
    int BatchSize = 500)
{
    public static ReplicaOptions FromConfiguration(IConfiguration configuration)
    {
        var attempts = BackupOptions.RequireInteger(configuration, "Replica:MaximumAttempts");
        var timeout = BackupOptions.RequireInteger(configuration, "Replica:TimeoutSeconds");
        return new(
        Endpoint(configuration, "Replica:Source", attempts, timeout),
        Endpoint(configuration, "Replica:Destination", attempts, timeout),
        BackupOptions.Require(configuration, "Replica:Source:QuarantineBucket"),
        BackupOptions.Require(configuration, "Replica:Source:CleanBucket"),
        BackupOptions.Require(configuration, "Replica:Destination:QuarantineBucket"),
        BackupOptions.Require(configuration, "Replica:Destination:CleanBucket"),
        BackupOptions.Require(configuration, "Replica:Destination:ManifestBucket"),
        BackupOptions.Require(configuration, "Replica:Destination:ManifestPrefix"),
        BackupOptions.RequireInteger(configuration, "Replica:BatchSize"));
    }

    public void Validate()
    {
        var buckets = new[]
        {
            SourceQuarantineBucket, SourceCleanBucket, DestinationQuarantineBucket,
            DestinationCleanBucket, ManifestBucket
        };
        if (buckets.Any(bucket => !BucketContract.IsValid(bucket)) ||
            string.Equals(Source.AccessKey, Destination.AccessKey, StringComparison.Ordinal) ||
            string.Equals(SourceQuarantineBucket, DestinationQuarantineBucket, StringComparison.Ordinal) ||
            string.Equals(SourceCleanBucket, DestinationCleanBucket, StringComparison.Ordinal) ||
            !PrefixContract.IsValid(ManifestPrefix) || BatchSize is < 1 or > 1000)
        {
            throw new OperationsConfigurationException("REPLICA_CONFIGURATION_INVALID");
        }
    }

    private static S3EndpointOptions Endpoint(
        IConfiguration configuration,
        string section,
        int maximumAttempts,
        int timeoutSeconds) => new(
        BackupOptions.Require(configuration, $"{section}:Endpoint"),
        BackupOptions.Require(configuration, $"{section}:Region"),
        BackupOptions.RequireSecret(configuration, $"{section}:AccessKey"),
        BackupOptions.RequireSecret(configuration, $"{section}:SecretKey"),
        configuration.GetValue<bool>($"{section}:AllowInsecureTransport"),
        maximumAttempts,
        timeoutSeconds);
}

public sealed class OperationsConfigurationException(string errorCode) : Exception
{
    public string ErrorCode { get; } = errorCode;
}

internal static class BucketContract
{
    internal static bool IsValid(string value) =>
        value.Length is >= 3 and <= 63 &&
        value[0] is >= 'a' and <= 'z' or >= '0' and <= '9' &&
        value[^1] is >= 'a' and <= 'z' or >= '0' and <= '9' &&
        value.All(character => character is >= 'a' and <= 'z' or >= '0' and <= '9' or '-' or '.');
}

internal static class PrefixContract
{
    internal static bool IsValid(string value) =>
        value.Length is > 0 and <= 128 && !value.StartsWith('/') && !value.EndsWith('/') &&
        !value.Contains("..", StringComparison.Ordinal) &&
        value.All(character => char.IsAsciiLetterOrDigit(character) || character is '/' or '-' or '_' or '.');
}
