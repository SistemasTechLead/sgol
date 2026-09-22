using System.Net;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Sgol.JobInfrastructure;
using Sgol.Operations;
using Sgol.Web.Infrastructure.Persistence;
using Sgol.Web.Infrastructure.Persistence.Continuity;
using Xunit;

namespace Sgol.UnitTests;

public sealed class PortableOperationsTests
{
    [Fact]
    public void WebCompositionRegistersRecoveryReferenceOutboxHandlerOnce()
    {
        var services = new ServiceCollection();
        services.AddSgolPersistence(Configuration(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Sgol"] = "Host=localhost;Database=sgol_cv05_synthetic;Username=test",
            ["ASPNETCORE_ENVIRONMENT"] = "CI",
        }));

        Assert.Single(services, item => item.ServiceType == typeof(IOutboxHandler) &&
            item.ImplementationType == typeof(RecoveryReferenceRequestedOutboxHandler));
    }

    [Fact]
    public void OperationalJobsUseStableNamesAndPreventOverlappingSlots()
    {
        var backup = new PostgreSqlPortableBackupJob(null!);
        var replica = new ReplicateEvidenceObjectsJob(null!);
        var recovery = new CaptureRecoveryReferenceJob(null!, null!, TimeProvider.System);

        Assert.Equal("POSTGRESQL_PORTABLE_BACKUP", backup.Name);
        Assert.True(backup.PreventOverlappingSlots);
        Assert.Equal("REPLICATE_EVIDENCE_OBJECTS", replica.Name);
        Assert.True(replica.PreventOverlappingSlots);
        Assert.Equal("CAPTURE_RECOVERY_REFERENCE", recovery.Name);
        Assert.True(recovery.PreventOverlappingSlots);
        Assert.Equal(
            ScheduledJobRunner.ComputeLockKey(backup.Name),
            ScheduledJobRunner.ComputeLockKey(backup.Name));
        Assert.NotEqual(
            ScheduledJobRunner.ComputeLockKey(backup.Name),
            ScheduledJobRunner.ComputeLockKey(replica.Name));
        Assert.NotEqual(
            ScheduledJobRunner.ComputeLockKey(replica.Name),
            ScheduledJobRunner.ComputeLockKey(recovery.Name));
    }

    [Fact]
    public void BackupConfigurationRejectsPlaceholdersAndInvalidSlotsBeforeEffects()
    {
        var configuration = Configuration(new Dictionary<string, string?>
        {
            ["Backup:PostgreSql:ConnectionString"] = "REQUIRED_EXTERNAL_SECRET"
        });

        var exception = Assert.Throws<OperationsConfigurationException>(
            () => BackupOptions.FromConfiguration(configuration));

        Assert.Equal("OPERATIONS_CONFIGURATION_REQUIRED", exception.ErrorCode);
    }

    [Fact]
    public void ReplicaConfigurationRejectsCredentialReuseAndSameBuckets()
    {
        var options = new ReplicaOptions(
            new S3EndpointOptions("https://primary.invalid.example", "region-1", "source-key", "source-secret"),
            new S3EndpointOptions("https://backup.invalid.example", "region-2", "source-key", "destination-secret"),
            "quarantine", "clean", "quarantine", "clean-copy", "manifests", "objects/v1");

        var exception = Assert.Throws<OperationsConfigurationException>(options.Validate);

        Assert.Equal("REPLICA_CONFIGURATION_INVALID", exception.ErrorCode);
    }

    [Theory]
    [InlineData("http://172.15.0.1:9000", false)]
    [InlineData("http://172.16.0.1:9000", true)]
    [InlineData("http://172.31.255.254:9000", true)]
    [InlineData("http://172.32.0.1:9000", false)]
    public void InsecureS3TransportIsRestrictedToPrivateNetworks(string endpoint, bool allowed)
    {
        var options = new S3EndpointOptions(endpoint, "us-east-1", "access", "secret", true);

        if (allowed)
        {
            using var client = options.CreateClient();
            Assert.NotNull(client);
        }
        else
        {
            var exception = Assert.Throws<OperationsConfigurationException>(options.CreateClient);
            Assert.Equal("S3_CONFIGURATION_INVALID", exception.ErrorCode);
        }
    }

    [Fact]
    public async Task InvalidBackupSlotDoesNotStartDumpOrCreateStorageClient()
    {
        var pipeline = new RecordingBackupPipeline();
        var configuration = Configuration(new Dictionary<string, string?>
        {
            ["Backup:PostgreSql:ConnectionString"] = "Host=postgres.invalid;Database=sgol;Username=backup;Password=synthetic",
            ["Backup:Storage:Endpoint"] = "https://backup.invalid.example",
            ["Backup:Storage:Region"] = "us-east-1",
            ["Backup:Storage:AccessKey"] = "backup-access",
            ["Backup:Storage:SecretKey"] = "backup-secret",
            ["Backup:Storage:Bucket"] = "backup-bucket",
            ["Backup:Storage:Prefix"] = "postgresql/v1",
            ["Backup:Encryption:Recipient"] = "age1qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqd3r5j",
            ["Backup:PgDumpPath"] = Path.GetFullPath("pg_dump"),
            ["Backup:AgePath"] = Path.GetFullPath("age"),
            ["Backup:MaximumAttempts"] = "3",
            ["Backup:StorageTimeoutSeconds"] = "60",
            ["Backup:ProcessTimeoutSeconds"] = "900",
            ["SGOL_REVISION"] = new string('a', 40),
            ["SGOL_IMAGE_DIGEST"] = "sha256:" + new string('b', 64)
        });
        var backup = new PostgreSqlPortableBackup(
            configuration, pipeline, TimeProvider.System, NullLogger<PostgreSqlPortableBackup>.Instance);

        var exception = await Assert.ThrowsAsync<JobExecutionException>(() =>
            backup.ExecuteAsync(new DateTimeOffset(2026, 9, 12, 2, 14, 0, TimeSpan.Zero), CancellationToken.None));

        Assert.Equal("BACKUP_SLOT_INVALID", exception.ErrorCode);
        Assert.Equal(0, pipeline.Calls);
    }

    [Fact]
    public async Task ManifestSerializationAndHashAreDeterministic()
    {
        var manifest = new PostgreSqlBackupManifest(
            1, "SGOL_POSTGRESQL_PORTABLE_BACKUP", DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch,
            "revision", "sha256:digest", "16.15", "pg_dump 16.15", "age 1.1.1",
            "postgresql-custom", "age-x25519", "0123456789abcdef", 3, new string('a', 64),
            "postgresql/v1/1970/01/01/sgol-19700101T000000Z.dump.age", "COMPLETE");

        var first = OperationManifestSerializer.Serialize(manifest);
        var second = OperationManifestSerializer.Serialize(manifest);
        await using var content = new MemoryStream(Encoding.UTF8.GetBytes("abc"));
        var hash = await OperationManifestSerializer.HashAsync(content, CancellationToken.None);

        Assert.Equal(first, second);
        Assert.StartsWith("{\"ageVersion\":", Encoding.UTF8.GetString(first), StringComparison.Ordinal);
        Assert.Equal(manifest, OperationManifestSerializer.Deserialize<PostgreSqlBackupManifest>(first));
        Assert.Equal("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", hash.Hash);
        Assert.Equal(3, hash.Size);
    }

    [Fact]
    public async Task ProbeAllowsOnlyLoopbackHealthAndReturnsNonZeroForFailure()
    {
        var success = await OperationsProgram.RunAsync(
            ["probe-http", "--url", "http://127.0.0.1:8080/health/live"],
            httpHandler: new StaticResponseHandler(HttpStatusCode.OK));
        var unavailable = await OperationsProgram.RunAsync(
            ["probe-http", "--url", "http://localhost:8080/health/ready"],
            httpHandler: new StaticResponseHandler(HttpStatusCode.ServiceUnavailable));
        var external = await OperationsProgram.RunAsync(
            ["probe-http", "--url", "https://example.com/health/live"],
            httpHandler: new StaticResponseHandler(HttpStatusCode.OK));

        Assert.Equal(0, success);
        Assert.Equal(1, unavailable);
        Assert.Equal(64, external);
    }

    [Fact]
    public async Task InvalidMigrationCommandFailsBeforeConfigurationOrDatabase()
    {
        var result = await OperationsProgram.RunAsync(
            ["migrate", "--expected-migration", "latest"],
            Configuration([]));

        Assert.Equal(64, result);
    }

    [Fact]
    public async Task FunctionalRecoveryCommandsRejectNonCanonicalIdentityBeforeEffects()
    {
        var complete = await OperationsProgram.RunAsync(
            ["complete-functional-reference", "--reconciliation-id", "invalid", "--reference", "s3://a/r.json",
                "--backup-manifest", "s3://a/b.json", "--replica-manifest", "s3://b/r.json"]);
        var reconcile = await OperationsProgram.RunAsync(
            ["reconcile-functional-restore", "--reconciliation-id", "invalid", "--reference-manifest",
                "s3://a/r.json", "--restore-evidence", "missing.json"]);

        Assert.Equal(64, complete);
        Assert.Equal(64, reconcile);
    }

    [Fact]
    public void PartialPortableDataProtectionConfigurationFailsClosed()
    {
        var services = new ServiceCollection();
        var configuration = Configuration(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Sgol"] = "Host=postgres.invalid;Database=sgol;Username=app;Password=synthetic",
            ["DataProtection:ApplicationName"] = "SGOL"
        });

        var exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddSgolPersistence(configuration));

        Assert.Equal("Data Protection configuration is incomplete.", exception.Message);
    }

    [Fact]
    public void ProductionWithoutPortableDataProtectionConfigurationFailsClosed()
    {
        var services = new ServiceCollection();
        var configuration = Configuration(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Sgol"] = "Host=postgres.invalid;Database=sgol;Username=app;Password=synthetic",
            ["ASPNETCORE_ENVIRONMENT"] = "Production"
        });

        var exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddSgolPersistence(configuration));

        Assert.Equal("Portable Data Protection configuration is required.", exception.Message);
    }

    private static IConfiguration Configuration(IEnumerable<KeyValuePair<string, string?>> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    private sealed class StaticResponseHandler(HttpStatusCode statusCode) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(statusCode));
    }

    private sealed class RecordingBackupPipeline : IBackupProcessPipeline
    {
        public int Calls { get; private set; }

        public Task<BackupProcessResult> CreateEncryptedDumpAsync(
            BackupOptions options,
            string outputPath,
            CancellationToken cancellationToken)
        {
            Calls++;
            throw new InvalidOperationException("The invalid slot must fail before invoking pg_dump.");
        }

        public Task<RestoreProcessResult> VerifyAndRestoreAsync(
            string agePath,
            string pgRestorePath,
            string encryptedPath,
            string identity,
            NpgsqlConnectionStringBuilder destination,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
