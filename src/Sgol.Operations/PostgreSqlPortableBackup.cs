using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Sgol.Operations;

public interface IPostgreSqlPortableBackup
{
    Task ExecuteAsync(DateTimeOffset scheduledFor, CancellationToken cancellationToken);
}

public sealed partial class PostgreSqlPortableBackup(
    IConfiguration configuration,
    IBackupProcessPipeline processPipeline,
    TimeProvider timeProvider,
    ILogger<PostgreSqlPortableBackup> logger) : IPostgreSqlPortableBackup
{
    public async Task ExecuteAsync(DateTimeOffset scheduledFor, CancellationToken cancellationToken)
    {
        var started = Stopwatch.GetTimestamp();
        string? temporaryPath = null;
        var storageStage = BackupStorageStage.PREVIOUS_MANIFEST_READ;
        try
        {
            var options = BackupOptions.FromConfiguration(configuration);
            options.Validate();
            AssertCredentialSeparation(options);
            var build = OperationBuildIdentity.FromConfiguration(configuration);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(options.ProcessTimeoutSeconds));
            var operationToken = timeout.Token;
            var slot = scheduledFor.ToUniversalTime();
            if (slot.TimeOfDay != new TimeSpan(2, 15, 0))
            {
                throw new OperationsConfigurationException("BACKUP_SLOT_INVALID");
            }

            var key = $"{options.Prefix}/{slot:yyyy/MM/dd}/sgol-{slot:yyyyMMdd'T'HHmmss'Z'}.dump.age";
            using var client = options.Storage.CreateClient();
            var store = new S3OperationStore(client);
            if (await RecoverConfirmedBackupAsync(
                    store,
                    options.Bucket,
                    key,
                    stage => storageStage = stage,
                    operationToken))
            {
                OperationsTelemetry.Record(
                    "postgresql_backup", "recovered", Stopwatch.GetElapsedTime(started), 1, 0);
                OperationsLogs.Completed(logger, "postgresql_backup", slot, "RECOVERED");
                return;
            }

            var directory = Path.Combine(Path.GetTempPath(), "sgol-backup", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(directory,
                    UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            }

            temporaryPath = Path.Combine(directory, "backup.dump.age");
            var process = await processPipeline.CreateEncryptedDumpAsync(options, temporaryPath, operationToken);
            await using var encrypted = new FileStream(
                temporaryPath, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            var (hash, size) = await OperationManifestSerializer.HashAsync(encrypted, operationToken);
            if (size == 0)
            {
                throw new OperationsIntegrityException("BACKUP_EMPTY");
            }

            var serverVersion = await ReadServerVersionAsync(options.ParseConnection(), operationToken);
            AssertClientCompatibility(serverVersion, process.PgDumpVersion);
            storageStage = BackupStorageStage.DUMP_WRITE_AND_VERIFY;
            await store.PutFileVerifiedAsync(
                options.Bucket, key, temporaryPath, hash, "application/octet-stream", operationToken);

            var createdAt = timeProvider.GetUtcNow();
            var manifest = new PostgreSqlBackupManifest(
                1,
                "SGOL_POSTGRESQL_PORTABLE_BACKUP",
                slot,
                createdAt,
                build.Revision,
                build.ImageDigest,
                serverVersion,
                process.PgDumpVersion,
                process.AgeVersion,
                "postgresql-custom",
                "age-x25519",
                OperationManifestSerializer.Fingerprint(options.Recipient),
                size,
                hash,
                key,
                "COMPLETE");
            var bytes = OperationManifestSerializer.Serialize(manifest);
            storageStage = BackupStorageStage.MANIFEST_WRITE_AND_VERIFY;
            await store.PutBytesVerifiedAsync(
                options.Bucket,
                key + ".manifest.json",
                bytes,
                "application/json",
                metadata: null,
                operationToken);
            OperationsTelemetry.Record(
                "postgresql_backup", "succeeded", Stopwatch.GetElapsedTime(started), 1, size);
            OperationsLogs.Completed(logger, "postgresql_backup", slot, "SUCCEEDED");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            OperationsTelemetry.Record(
                "postgresql_backup", "failed", Stopwatch.GetElapsedTime(started), 0, 0);
            OperationsLogs.Failed(logger, "postgresql_backup", "FAILED", "BACKUP_TIMEOUT");
            throw new Sgol.JobInfrastructure.JobExecutionException("BACKUP_TIMEOUT");
        }
        catch (Amazon.S3.AmazonS3Exception exception)
        {
            const string errorCode = "BACKUP_STORAGE_FAILED";
            OperationsTelemetry.Record(
                "postgresql_backup", "failed", Stopwatch.GetElapsedTime(started), 0, 0);
            var numericStatus = (int)exception.StatusCode;
            int? httpStatus = numericStatus is >= 100 and <= 599 ? numericStatus : null;
            BackupStorageFailed(
                logger,
                "postgresql_backup",
                "FAILED",
                errorCode,
                storageStage.ToString(),
                SanitizeDiagnosticToken(exception.GetType().Name),
                httpStatus,
                SanitizeDiagnosticToken(exception.ErrorCode));
            OperationsLogs.Failed(logger, "postgresql_backup", "FAILED", errorCode);
            throw new Sgol.JobInfrastructure.JobExecutionException("BACKUP_STORAGE_FAILED");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            OperationsTelemetry.Record(
                "postgresql_backup", "failed", Stopwatch.GetElapsedTime(started), 0, 0);
            OperationsLogs.Failed(logger, "postgresql_backup", "FAILED", ErrorCode(exception));
            throw new Sgol.JobInfrastructure.JobExecutionException(ErrorCode(exception));
        }
        finally
        {
            if (temporaryPath is not null)
            {
                DeleteRunTemporary(temporaryPath);
            }
        }
    }

    private void AssertCredentialSeparation(BackupOptions options)
    {
        var primaryConnection = configuration.GetConnectionString("Sgol");
        var primaryStorageKey = configuration["Evidence:Storage:AccessKey"];
        var primaryUser = string.IsNullOrWhiteSpace(primaryConnection)
            ? null
            : new NpgsqlConnectionStringBuilder(primaryConnection).Username;
        if (string.Equals(primaryUser, options.ParseConnection().Username, StringComparison.Ordinal) ||
            string.Equals(primaryStorageKey, options.Storage.AccessKey, StringComparison.Ordinal))
        {
            throw new OperationsConfigurationException("BACKUP_CREDENTIAL_REUSE");
        }
    }

    private static async Task<bool> RecoverConfirmedBackupAsync(
        S3OperationStore store,
        string bucket,
        string key,
        Action<BackupStorageStage> setStorageStage,
        CancellationToken cancellationToken)
    {
        setStorageStage(BackupStorageStage.PREVIOUS_MANIFEST_READ);
        var existing = await store.TryReadAsync(bucket, key + ".manifest.json", cancellationToken);
        if (existing is null)
        {
            return false;
        }

        var manifest = OperationManifestSerializer.Deserialize<PostgreSqlBackupManifest>(existing.Value.Content);
        if (manifest is not { SchemaVersion: 1, Kind: "SGOL_POSTGRESQL_PORTABLE_BACKUP", Status: "COMPLETE" } ||
            !string.Equals(manifest.Key, key, StringComparison.Ordinal) ||
            !existing.Value.Content.AsSpan().SequenceEqual(OperationManifestSerializer.Serialize(manifest)))
        {
            throw new OperationsIntegrityException("BACKUP_MANIFEST_INVALID");
        }

        setStorageStage(BackupStorageStage.PREVIOUS_DUMP_VERIFY);
        await store.VerifyObjectAsync(bucket, key, manifest.Sha256, manifest.ByteCount, cancellationToken);

        return true;
    }

    private static async Task<string> ReadServerVersionAsync(
        NpgsqlConnectionStringBuilder connectionString,
        CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(connectionString.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand("SELECT current_setting('server_version')", connection);
        return (string?)await command.ExecuteScalarAsync(cancellationToken) ?? "unknown";
    }

    private static string ErrorCode(Exception exception) => exception switch
    {
        OperationsConfigurationException configured => configured.ErrorCode,
        OperationsIntegrityException integrity => integrity.ErrorCode,
        Amazon.S3.AmazonS3Exception => "BACKUP_STORAGE_FAILED",
        NpgsqlException => "BACKUP_POSTGRESQL_FAILED",
        _ => "BACKUP_FAILED"
    };

    private static string SanitizeDiagnosticToken(string? value)
    {
        const int maximumLength = 64;
        if (string.IsNullOrEmpty(value) || value.Length > maximumLength ||
            !value.All(character => char.IsAsciiLetterOrDigit(character) || character is '.' or '_' or '-'))
        {
            return "UNKNOWN";
        }

        return value;
    }

    [LoggerMessage(3102, LogLevel.Error,
        "Operation {operation} result {result} error {errorClass} stage {stage} exceptionType {exceptionType} httpStatus {httpStatus} s3ErrorCode {s3ErrorCode}",
        EventName = "BackupStorageFailed")]
    private static partial void BackupStorageFailed(
        ILogger logger,
        string operation,
        string result,
        string errorClass,
        string stage,
        string exceptionType,
        int? httpStatus,
        string s3ErrorCode);

    private enum BackupStorageStage
    {
        PREVIOUS_MANIFEST_READ,
        PREVIOUS_DUMP_VERIFY,
        DUMP_WRITE_AND_VERIFY,
        MANIFEST_WRITE_AND_VERIFY
    }

    private static void AssertClientCompatibility(string serverVersion, string pgDumpVersion)
    {
        if (!TryReadMajor(serverVersion, out var serverMajor) || !TryReadMajor(pgDumpVersion, out var clientMajor) ||
            clientMajor < serverMajor)
        {
            throw new OperationsIntegrityException("BACKUP_CLIENT_INCOMPATIBLE");
        }
    }

    private static bool TryReadMajor(string value, out int major)
    {
        var firstDigit = value.AsSpan().IndexOfAnyInRange('0', '9');
        if (firstDigit < 0)
        {
            major = 0;
            return false;
        }

        var digits = value.AsSpan(firstDigit);
        var length = 0;
        while (length < digits.Length && char.IsAsciiDigit(digits[length]))
        {
            length++;
        }

        return int.TryParse(digits[..length], System.Globalization.NumberStyles.None,
            System.Globalization.CultureInfo.InvariantCulture, out major);
    }

    private static void DeleteRunTemporary(string path)
    {
        try
        {
            var directory = Path.GetDirectoryName(path);
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            if (directory is not null && Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: false);
            }
        }
        catch (IOException)
        {
            // The encrypted temporary belongs only to this run; failure to clean never changes backup success.
        }
        catch (UnauthorizedAccessException)
        {
            // The encrypted temporary belongs only to this run; failure to clean never changes backup success.
        }
    }
}
