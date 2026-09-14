using System.Net;
using System.Text.Json;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Sgol.Operations;
using Sgol.Web.Infrastructure.Persistence;

namespace Sgol.Operations
{
    public static class Program
    {
        public static Task<int> Main(string[] args) => OperationsProgram.RunAsync(args);
    }

    public static class OperationsProgram
    {
        public static async Task<int> RunAsync(
            string[] args,
            IConfiguration? suppliedConfiguration = null,
            HttpMessageHandler? httpHandler = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                return args switch
                {
                    ["probe-http", "--url", var url] => await ProbeAsync(url, httpHandler, cancellationToken),
                    ["migrate", "--expected-migration", var migration] =>
                        await MigrateAsync(migration, suppliedConfiguration, cancellationToken),
                    ["verify-postgresql-backup", "--manifest", var manifest] =>
                        await VerifyBackupAsync(manifest, suppliedConfiguration, cancellationToken),
                    ["verify-object-replica", "--manifest", var replicaManifest] =>
                        await VerifyReplicaAsync(replicaManifest, suppliedConfiguration, cancellationToken),
                    _ => 64
                };
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                Console.Error.WriteLine(ErrorCode(exception));
                return 1;
            }
        }

        private static async Task<int> ProbeAsync(
            string url,
            HttpMessageHandler? handler,
            CancellationToken cancellationToken)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var target) || target.Scheme != Uri.UriSchemeHttp ||
                target.Host is not ("127.0.0.1" or "localhost") ||
                target.AbsolutePath is not ("/health/live" or "/health/ready") ||
                target.Query.Length != 0 || target.Fragment.Length != 0)
            {
                return 64;
            }

            using var client = handler is null ? new HttpClient() : new HttpClient(handler, disposeHandler: false);
            client.Timeout = TimeSpan.FromSeconds(4);
            using var response = await client.GetAsync(target, cancellationToken);
            return response.StatusCode == HttpStatusCode.OK ? 0 : 1;
        }

        private static async Task<int> MigrateAsync(
            string expectedMigration,
            IConfiguration? configuration,
            CancellationToken cancellationToken)
        {
            if (!IsMigrationId(expectedMigration))
            {
                return 64;
            }

            using var host = CreateHost(configuration);
            await using var scope = host.Services.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<SgolDbContext>();
            await dbContext.Database.MigrateAsync(cancellationToken);
            var applied = await dbContext.Database.GetAppliedMigrationsAsync(cancellationToken);
            if (!applied.Contains(expectedMigration, StringComparer.Ordinal))
            {
                throw new OperationsIntegrityException("EXPECTED_MIGRATION_NOT_APPLIED");
            }

            Console.WriteLine("MIGRATION_APPLIED");
            return 0;
        }

        private static async Task<int> VerifyBackupAsync(
            string manifestUri,
            IConfiguration? suppliedConfiguration,
            CancellationToken cancellationToken)
        {
            var startedAt = DateTimeOffset.UtcNow;
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var configuration = suppliedConfiguration ?? BuildConfiguration();
            var options = BackupOptions.FromConfiguration(configuration);
            options.Validate();
            var (bucket, manifestKey) = ParseS3Uri(manifestUri);
            if (!string.Equals(bucket, options.Bucket, StringComparison.Ordinal) ||
                !manifestKey.StartsWith(options.Prefix + "/", StringComparison.Ordinal) ||
                !manifestKey.EndsWith(".manifest.json", StringComparison.Ordinal))
            {
                throw new OperationsConfigurationException("BACKUP_MANIFEST_URI_INVALID");
            }

            using var client = options.Storage.CreateClient();
            var store = new S3OperationStore(client);
            var manifestObject = await store.ReadAsync(bucket, manifestKey, cancellationToken);
            var manifestContent = manifestObject.Content;
            var manifest = OperationManifestSerializer.Deserialize<PostgreSqlBackupManifest>(manifestContent);
            if (manifest is not { SchemaVersion: 1, Kind: "SGOL_POSTGRESQL_PORTABLE_BACKUP", Status: "COMPLETE" } ||
                manifest.Key + ".manifest.json" != manifestKey || !IsSha256(manifest.Sha256) || manifest.ByteCount <= 0 ||
                !manifestContent.AsSpan().SequenceEqual(OperationManifestSerializer.Serialize(manifest)) ||
                !string.Equals(manifest.RecipientFingerprint,
                    OperationManifestSerializer.Fingerprint(options.Recipient), StringComparison.Ordinal))
            {
                throw new OperationsIntegrityException("BACKUP_MANIFEST_INVALID");
            }

            var directory = Path.Combine(Path.GetTempPath(), "sgol-restore", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(directory,
                    UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            }

            var encryptedPath = Path.Combine(directory, "backup.dump.age");
            var identityPath = Path.Combine(directory, "identity.txt");
            try
            {
                await store.DownloadVerifiedAsync(
                    bucket, manifest.Key, encryptedPath, manifest.Sha256, manifest.ByteCount, cancellationToken);
                var identity = BackupOptions.RequireSecret(configuration, "Restore:Encryption:Identity");
                await File.WriteAllTextAsync(identityPath, identity, cancellationToken);
                if (!OperatingSystem.IsWindows())
                {
                    File.SetUnixFileMode(identityPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
                }

                var restoreText = BackupOptions.RequireSecret(configuration, "Restore:PostgreSql:ConnectionString");
                var primaryText = BackupOptions.RequireSecret(configuration, "ConnectionStrings:Sgol");
                if (IsSameDatabase(restoreText, primaryText))
                {
                    throw new OperationsConfigurationException("RESTORE_PRIMARY_TARGET_REJECTED");
                }

                var destination = new NpgsqlConnectionStringBuilder(restoreText);
                await AssertEmptyTargetAsync(destination, cancellationToken);
                var pipeline = new BackupProcessPipeline();
                await pipeline.VerifyAndRestoreAsync(
                    options.AgePath,
                    "/usr/bin/pg_restore",
                    encryptedPath,
                    identityPath,
                    destination,
                    cancellationToken);
                var expectedMigration = BackupOptions.Require(configuration, "Restore:ExpectedMigration");
                await AssertRestoredTargetAsync(destination, expectedMigration, cancellationToken);
                stopwatch.Stop();
                Console.WriteLine(JsonSerializer.Serialize(new
                {
                    kind = "SGOL_TECHNICAL_RESTORE_EVIDENCE",
                    startedAt,
                    completedAt = DateTimeOffset.UtcNow,
                    durationMilliseconds = stopwatch.ElapsedMilliseconds,
                    imageDigest = manifest.ImageDigest,
                    backupSha256 = manifest.Sha256,
                    manifestSha256 = manifestObject.Sha256,
                    result = "BACKUP_RESTORE_VERIFIED"
                }));
                return 0;
            }
            finally
            {
                DeleteRestoreTemporary(directory, encryptedPath, identityPath);
            }
        }

        private static async Task<int> VerifyReplicaAsync(
            string manifestUri,
            IConfiguration? suppliedConfiguration,
            CancellationToken cancellationToken)
        {
            var configuration = suppliedConfiguration ?? BuildConfiguration();
            var options = ReplicaOptions.FromConfiguration(configuration);
            options.Validate();
            var (bucket, key) = ParseS3Uri(manifestUri);
            if (!string.Equals(bucket, options.ManifestBucket, StringComparison.Ordinal) ||
                !key.StartsWith(options.ManifestPrefix + "/", StringComparison.Ordinal))
            {
                throw new OperationsConfigurationException("REPLICA_MANIFEST_URI_INVALID");
            }

            using var destination = options.Destination.CreateClient();
            var store = new S3OperationStore(destination);
            var manifestObject = await store.ReadAsync(bucket, key, cancellationToken);
            var manifest = OperationManifestSerializer.Deserialize<ObjectReplicaManifest>(manifestObject.Content);
            if (manifest is not { SchemaVersion: 1, Kind: "SGOL_EVIDENCE_OBJECT_REPLICA", Status: "COMPLETE", ErrorClass: null } ||
                !manifestObject.Content.AsSpan().SequenceEqual(OperationManifestSerializer.Serialize(manifest)))
            {
                throw new OperationsIntegrityException("REPLICA_MANIFEST_INVALID");
            }

            var identities = new HashSet<(string BucketRole, string Key)>();
            foreach (var item in manifest.Objects)
            {
                if (string.IsNullOrWhiteSpace(item.Key) || !identities.Add((item.BucketRole, item.Key)))
                {
                    throw new OperationsIntegrityException("REPLICA_MANIFEST_INVALID");
                }

                var targetBucket = item.BucketRole switch
                {
                    "quarantine" => options.DestinationQuarantineBucket,
                    "clean" => options.DestinationCleanBucket,
                    _ => throw new OperationsIntegrityException("REPLICA_BUCKET_ROLE_INVALID")
                };
                var actual = await store.ReadAsync(targetBucket, item.Key, cancellationToken);
                if (actual.Content.LongLength != item.Size ||
                    !IsSha256(item.SourceSha256) || !IsSha256(item.DestinationSha256) ||
                    item.Status != "VERIFIED" ||
                    !string.Equals(actual.Sha256, item.DestinationSha256, StringComparison.Ordinal) ||
                    !string.Equals(item.SourceSha256, item.DestinationSha256, StringComparison.Ordinal))
                {
                    throw new OperationsIntegrityException("DESTINATION_OBJECT_CORRUPT");
                }
            }

            Console.WriteLine("OBJECT_REPLICA_VERIFIED");
            return 0;
        }

        private static IHost CreateHost(IConfiguration? configuration)
        {
            var builder = Host.CreateApplicationBuilder();
            if (configuration is not null)
            {
                builder.Configuration.Sources.Clear();
                builder.Configuration.AddConfiguration(configuration);
            }

            var connectionString = BackupOptions.RequireSecret(builder.Configuration, "ConnectionStrings:Sgol");
            _ = new NpgsqlConnectionStringBuilder(connectionString);
            builder.Services.AddDbContext<SgolDbContext>(options => options.UseNpgsql(connectionString));
            return builder.Build();
        }

        private static IConfiguration BuildConfiguration() =>
            new ConfigurationBuilder().AddEnvironmentVariables().Build();

        private static async Task AssertEmptyTargetAsync(
            NpgsqlConnectionStringBuilder connectionString,
            CancellationToken cancellationToken)
        {
            await using var connection = new NpgsqlConnection(connectionString.ConnectionString);
            await connection.OpenAsync(cancellationToken);
            const string sql = "SELECT count(*) FROM pg_catalog.pg_tables WHERE schemaname NOT IN ('pg_catalog','information_schema')";
            await using var command = new NpgsqlCommand(sql, connection);
            var count = (long)(await command.ExecuteScalarAsync(cancellationToken) ?? -1L);
            if (count != 0)
            {
                throw new OperationsIntegrityException("RESTORE_TARGET_NOT_EMPTY");
            }
        }

        private static async Task AssertRestoredTargetAsync(
            NpgsqlConnectionStringBuilder connectionString,
            string expectedMigration,
            CancellationToken cancellationToken)
        {
            await using var connection = new NpgsqlConnection(connectionString.ConnectionString);
            await connection.OpenAsync(cancellationToken);
            const string sql = """
                SELECT
                    (SELECT count(*) FROM pg_catalog.pg_tables WHERE schemaname = 'public'),
                    EXISTS (SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = @expected_migration),
                    (SELECT count(*) FROM pg_catalog.pg_constraint WHERE NOT convalidated),
                    (SELECT count(*) FROM data_protection_key)
                """;
            await using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddWithValue("expected_migration", expectedMigration);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken) || reader.GetInt64(0) <= 0 ||
                !reader.GetBoolean(1) || reader.GetInt64(2) != 0 || reader.GetInt64(3) < 0)
            {
                throw new OperationsIntegrityException("RESTORE_STRUCTURAL_VERIFICATION_FAILED");
            }
        }

        private static (string Bucket, string Key) ParseS3Uri(string value)
        {
            if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme != "s3" ||
                string.IsNullOrWhiteSpace(uri.Host) || uri.AbsolutePath.Length <= 1 ||
                !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
            {
                throw new OperationsConfigurationException("S3_URI_INVALID");
            }

            return (uri.Host, Uri.UnescapeDataString(uri.AbsolutePath[1..]));
        }

        private static bool IsMigrationId(string value) =>
            value.Length is >= 15 and <= 160 && value[..14].All(char.IsAsciiDigit) &&
            value[14] == '_' && value[15..].All(character => char.IsAsciiLetterOrDigit(character) || character == '_');

        private static bool IsSha256(string value) =>
            value.Length == 64 && value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

        private static bool IsSameDatabase(string first, string second)
        {
            var left = new NpgsqlConnectionStringBuilder(first);
            var right = new NpgsqlConnectionStringBuilder(second);
            return string.Equals(left.Host, right.Host, StringComparison.OrdinalIgnoreCase) &&
                left.Port == right.Port &&
                string.Equals(left.Database, right.Database, StringComparison.Ordinal);
        }

        private static string ErrorCode(Exception exception) => exception switch
        {
            OperationsConfigurationException configured => configured.ErrorCode,
            OperationsIntegrityException integrity => integrity.ErrorCode,
            AmazonS3Exception => "S3_OPERATION_FAILED",
            NpgsqlException => "POSTGRESQL_OPERATION_FAILED",
            _ => "OPERATIONS_COMMAND_FAILED"
        };

        private static void DeleteRestoreTemporary(string directory, params string[] files)
        {
            foreach (var file in files)
            {
                try
                {
                    if (File.Exists(file))
                    {
                        File.Delete(file);
                    }
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    // Only private temporaries created by this invocation are eligible for cleanup.
                }
            }

            try
            {
                Directory.Delete(directory, recursive: false);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // Only the private empty directory created by this invocation is eligible for cleanup.
            }
        }
    }
}
