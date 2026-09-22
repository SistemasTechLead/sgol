using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Amazon.Runtime;
using Amazon.S3;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Networks;
using Testcontainers.PostgreSql;

namespace Sgol.Cv05Demo;

internal sealed partial class Cv05Infrastructure
{
    private static readonly string[] StoreActions = ["Admin", "Read", "Write", "List", "Tagging"];
    private readonly string networkName = $"sgol-cv05-{Guid.CreateVersion7():N}";
    private readonly string privateDirectory = Path.Combine(Path.GetTempPath(), $"sgol-cv05-{Guid.CreateVersion7():N}");
    private readonly string sourceAccessKey = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16));
    private readonly string sourceSecretKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
    private readonly string destinationAccessKey = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16));
    private readonly string destinationSecretKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
    private INetwork? network;
    private IContainer? sourceStore;
    private IContainer? destinationStore;
    private IContainer? scanner;
    private PostgreSqlContainer? restorePostgres;
    private string primaryUsername = null!;
    private string primaryPassword = null!;
    private readonly string restoreDatabaseName = $"sgol_cv05_restore_{Guid.CreateVersion7():N}";
    private readonly string restoreUsername = $"cv05_restore_{Guid.CreateVersion7():N}";
    private readonly string restorePassword = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
    private string certificatePassword = null!;
    private string certificatePfxBase64 = null!;
    private readonly HashSet<string> transientContainers = new(StringComparer.Ordinal);

    public string NetworkName => networkName;
    public const string SourceStoreAlias = "cv05-source";
    public const string DestinationStoreAlias = "cv05-destination";
    public string PostgreSqlContainerId => postgres?.Id ?? throw new InvalidOperationException();
    public string PrivateDirectory => privateDirectory;
    public string PrimaryNetworkConnectionString =>
        $"Host=cv05-postgres;Port=5432;Database={databaseName};Username={primaryUsername};Password={primaryPassword};SSL Mode=Disable";
    public string RestoreNetworkConnectionString =>
        $"Host=cv05-restore;Port=5432;Database={restoreDatabaseName};Username={restoreUsername};Password={restorePassword};SSL Mode=Disable";
    public string RestoreHostConnectionString => restorePostgres?.GetConnectionString()
        ?? throw new InvalidOperationException();
    public string RuntimeEnvironmentPath => Path.Combine(privateDirectory, "runtime.env");
    public void RegisterTransientContainer(string name)
    {
        if (!name.StartsWith("sgol-cv05-outbox-", StringComparison.Ordinal))
            throw new DemoFailureException("PREFLIGHT", "NONE", "CV05_PRECONDITION_FAILED");
        transientContainers.Add(name);
    }
    public void UnregisterTransientContainer(string name) => transientContainers.Remove(name);

    public async Task WriteRuntimeEnvironmentAsync(string recipient, string ageIdentity, string replicaUri,
        CancellationToken token)
    {
        var sourceAddress = await PrivateContainerAddressAsync(sourceStore!, token);
        var destinationAddress = await PrivateContainerAddressAsync(destinationStore!, token);
        var revision = Environment.GetEnvironmentVariable("CV05_COMMIT") ?? string.Empty;
        if (revision.Length != 40 || revision.Any(character => !Uri.IsHexDigit(character)))
            throw new DemoFailureException("OCI", "NONE", "CV05_PRECONDITION_FAILED");
        var values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["SGOL_SYNTHETIC_ONLY"] = "true",
            ["ASPNETCORE_ENVIRONMENT"] = "CI",
            ["SGOL_REVISION"] = revision,
            ["SGOL_IMAGE_DIGEST"] = ImageId,
            ["ConnectionStrings__Sgol"] = PrimaryNetworkConnectionString,
            ["Evidence__Storage__Endpoint"] = $"http://{sourceAddress}:8333",
            ["Evidence__Storage__Region"] = "us-east-1",
            ["Evidence__Storage__QuarantineBucket"] = DemoContract.SourceQuarantineBucket,
            ["Evidence__Storage__CleanBucket"] = DemoContract.SourceCleanBucket,
            ["Evidence__Storage__AccessKey"] = sourceAccessKey,
            ["Evidence__Storage__SecretKey"] = sourceSecretKey,
            ["Evidence__Storage__AllowInsecureTransport"] = "true",
            ["Evidence__Storage__AllowedUploadOrigins__0"] = BaseAddress.AbsoluteUri.TrimEnd('/'),
            ["Evidence__Scanner__Host"] = "cv05-scanner",
            ["Evidence__Scanner__Port"] = "3310",
            ["Evidence__Scanner__ConnectTimeoutSeconds"] = "3",
            ["Evidence__Scanner__ScanTimeoutSeconds"] = "30",
            ["DataProtection__ApplicationName"] = "SGOL-CV05",
            ["DataProtection__WrappingCertificate"] = certificatePfxBase64,
            ["DataProtection__WrappingCertificatePassword"] = certificatePassword,
            ["Backup__PostgreSql__ConnectionString"] = PrimaryNetworkConnectionString,
            ["Backup__Storage__Endpoint"] = $"http://{destinationAddress}:8333",
            ["Backup__Storage__Region"] = "us-east-1",
            ["Backup__Storage__Bucket"] = DemoContract.ManifestBucket,
            ["Backup__Storage__Prefix"] = "postgresql/v1",
            ["Backup__Storage__AccessKey"] = destinationAccessKey,
            ["Backup__Storage__SecretKey"] = destinationSecretKey,
            ["Backup__Storage__AllowInsecureTransport"] = "true",
            ["Backup__Encryption__Recipient"] = recipient,
            ["Backup__PgDumpPath"] = "/usr/bin/pg_dump",
            ["Backup__AgePath"] = "/usr/bin/age",
            ["Backup__MaximumAttempts"] = "3",
            ["Backup__StorageTimeoutSeconds"] = "60",
            ["Backup__ProcessTimeoutSeconds"] = "900",
            ["Replica__Source__Endpoint"] = $"http://{sourceAddress}:8333",
            ["Replica__Source__Region"] = "us-east-1",
            ["Replica__Source__AccessKey"] = sourceAccessKey,
            ["Replica__Source__SecretKey"] = sourceSecretKey,
            ["Replica__Source__QuarantineBucket"] = DemoContract.SourceQuarantineBucket,
            ["Replica__Source__CleanBucket"] = DemoContract.SourceCleanBucket,
            ["Replica__Source__AllowInsecureTransport"] = "true",
            ["Replica__Destination__Endpoint"] = $"http://{destinationAddress}:8333",
            ["Replica__Destination__Region"] = "us-east-1",
            ["Replica__Destination__AccessKey"] = destinationAccessKey,
            ["Replica__Destination__SecretKey"] = destinationSecretKey,
            ["Replica__Destination__QuarantineBucket"] = DemoContract.DestinationQuarantineBucket,
            ["Replica__Destination__CleanBucket"] = DemoContract.DestinationCleanBucket,
            ["Replica__Destination__ManifestBucket"] = DemoContract.ManifestBucket,
            ["Replica__Destination__ManifestPrefix"] = "objects/v1",
            ["Replica__Destination__AllowInsecureTransport"] = "true",
            ["Replica__MaximumAttempts"] = "3",
            ["Replica__TimeoutSeconds"] = "60",
            ["Replica__BatchSize"] = "500",
            ["Continuity__ReplicaManifestUri"] = replicaUri,
            ["Restore__PostgreSql__ConnectionString"] = RestoreNetworkConnectionString,
            ["Restore__Encryption__Identity"] = ageIdentity,
            ["Restore__ExpectedMigration"] = "20260912213000_AddPortableDataProtectionKeyRing",
        };
        if (values.Any(item => item.Value.Contains('\n') || item.Value.Contains('\r')))
            throw new DemoFailureException("OCI", "NONE", "CV05_PRECONDITION_FAILED");
        await File.WriteAllTextAsync(RuntimeEnvironmentPath,
            string.Join('\n', values.Select(item => $"{item.Key}={item.Value}")) + "\n",
            new System.Text.UTF8Encoding(false), token);
    }

    private async Task<string> PrivateContainerAddressAsync(IContainer container, CancellationToken token)
    {
        var inspection = await NativeProcess.RunAsync("docker", ["container", "inspect", container.Id],
            RepositoryRoot, TimeSpan.FromSeconds(30), token);
        if (inspection.Exit != 0)
            throw new DemoFailureException("STORES", "NONE", "CV05_STORE_FAILED");
        using var document = JsonDocument.Parse(inspection.Stdout);
        var address = document.RootElement[0].GetProperty("NetworkSettings").GetProperty("Networks")
            .GetProperty(networkName).GetProperty("IPAddress").GetString();
        if (!System.Net.IPAddress.TryParse(address, out var parsed) ||
            parsed.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork ||
            parsed.GetAddressBytes() is not { } octets ||
            !(octets[0] == 10 || octets[0] == 192 && octets[1] == 168 ||
              octets[0] == 172 && octets[1] is >= 16 and <= 31))
            throw new DemoFailureException("STORES", "NONE", "CV05_STORE_FAILED");
        return address!;
    }

    private async Task StartRestorePostgreSqlAsync(CancellationToken token)
    {
        restorePostgres = new PostgreSqlBuilder(DemoContract.PostgreSqlImage)
            .WithDatabase(restoreDatabaseName)
            .WithUsername(restoreUsername)
            .WithPassword(restorePassword)
            .WithNetwork(network)
            .WithNetworkAliases("cv05-restore")
            .Build();
        await restorePostgres.StartAsync(token);
        var parsed = new Npgsql.NpgsqlConnectionStringBuilder(restorePostgres.GetConnectionString());
        DemoSafety.ValidateDisposableDatabase(parsed.Host!, parsed.Database!, restoreDatabaseName, parsed.Port);
    }

    private async Task CreateNetworkAsync(CancellationToken token)
    {
        Directory.CreateDirectory(privateDirectory);
        network = new NetworkBuilder().WithName(networkName).Build();
        await network.CreateAsync(token);
    }

    private async Task StartStoresAsync(CancellationToken token)
    {
        var sourceConfiguration = Path.Combine(privateDirectory, "source-s3.json");
        var destinationConfiguration = Path.Combine(privateDirectory, "destination-s3.json");
        await WriteStoreConfigurationAsync(sourceConfiguration, sourceAccessKey, sourceSecretKey, token);
        await WriteStoreConfigurationAsync(destinationConfiguration, destinationAccessKey, destinationSecretKey, token);
        sourceStore = BuildStore(sourceConfiguration, SourceStoreAlias);
        destinationStore = BuildStore(destinationConfiguration, DestinationStoreAlias);
        await sourceStore.StartAsync(token);
        await destinationStore.StartAsync(token);
        using (var source = CreateS3(SourceHostEndpoint, sourceAccessKey, sourceSecretKey))
        {
            await source.PutBucketAsync(DemoContract.SourceQuarantineBucket, token);
            await source.PutBucketAsync(DemoContract.SourceCleanBucket, token);
        }
        using (var destination = CreateS3(DestinationHostEndpoint, destinationAccessKey, destinationSecretKey))
        {
            await destination.PutBucketAsync(DemoContract.DestinationQuarantineBucket, token);
            await destination.PutBucketAsync(DemoContract.DestinationCleanBucket, token);
            await destination.PutBucketAsync(DemoContract.ManifestBucket, token);
        }
    }

    private async Task StartScannerAsync(CancellationToken token)
    {
        scanner = new ContainerBuilder(DemoContract.ClamAvImage)
            .WithNetwork(network)
            .WithNetworkAliases("cv05-scanner")
            .WithPortBinding(3310, true)
            .WithEnvironment("CLAMAV_NO_FRESHCLAMD", "true")
            .WithEnvironment("CLAMD_CONF_StreamMaxLength", "16M")
            .WithEnvironment("CLAMD_CONF_MaxFileSize", "16M")
            .WithEnvironment("CLAMD_CONF_MaxScanSize", "32M")
            .WithEnvironment("CLAMD_CONF_MaxRecursion", "4")
            .WithEnvironment("CLAMD_CONF_MaxFiles", "64")
            .WithEnvironment("CLAMD_CONF_MaxScanTime", "30000")
            .WithEnvironment("CLAMD_CONF_MaxThreads", "2")
            .WithEnvironment("CLAMD_CONF_MaxQueue", "4")
            .WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(3310)).Build();
        await scanner.StartAsync(token);
    }

    private IContainer BuildStore(string configurationPath, string alias) =>
        new ContainerBuilder(DemoContract.SeaweedImage)
            .WithNetwork(network)
            .WithNetworkAliases(alias)
            .WithPortBinding(8333, true)
            .WithBindMount(configurationPath, "/run/sgol/s3.json", AccessMode.ReadOnly)
            .WithCommand("mini", "-dir=/data", "-s3.config=/run/sgol/s3.json")
            .WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(8333)).Build();

    private static Task WriteStoreConfigurationAsync(string path, string accessKey, string secretKey,
        CancellationToken token) => File.WriteAllTextAsync(path, JsonSerializer.Serialize(new
        {
            identities = new[]
            {
                new { name = "cv05-ephemeral", credentials = new[] { new { accessKey, secretKey } },
                    actions = StoreActions },
            },
        }), token);

    private string SourceHostEndpoint => $"http://127.0.0.1:{sourceStore!.GetMappedPublicPort(8333)}";
    private string DestinationHostEndpoint => $"http://127.0.0.1:{destinationStore!.GetMappedPublicPort(8333)}";

    public AmazonS3Client CreateSourceS3() => CreateS3(SourceHostEndpoint, sourceAccessKey, sourceSecretKey);
    public AmazonS3Client CreateDestinationS3() => CreateS3(DestinationHostEndpoint, destinationAccessKey, destinationSecretKey);

    private static AmazonS3Client CreateS3(string endpoint, string accessKey, string secretKey) =>
        new(new BasicAWSCredentials(accessKey, secretKey), new AmazonS3Config
        {
            ServiceURL = endpoint,
            AuthenticationRegion = "us-east-1",
            ForcePathStyle = true,
            UseHttp = true,
            MaxErrorRetry = 0,
        });

    private void AddEvidenceEnvironment(ProcessStartInfo startInfo)
    {
        var environment = startInfo.Environment;
        environment["Evidence__Storage__Endpoint"] = SourceHostEndpoint;
        environment["Evidence__Storage__Region"] = "us-east-1";
        environment["Evidence__Storage__QuarantineBucket"] = DemoContract.SourceQuarantineBucket;
        environment["Evidence__Storage__CleanBucket"] = DemoContract.SourceCleanBucket;
        environment["Evidence__Storage__AccessKey"] = sourceAccessKey;
        environment["Evidence__Storage__SecretKey"] = sourceSecretKey;
        environment["Evidence__Storage__AllowInsecureTransport"] = "true";
        environment["Evidence__Storage__AllowedUploadOrigins__0"] = BaseAddress.AbsoluteUri.TrimEnd('/');
        environment["Evidence__Scanner__Host"] = DemoContract.LoopbackAddress;
        environment["Evidence__Scanner__Port"] = scanner!.GetMappedPublicPort(3310).ToString(
            System.Globalization.CultureInfo.InvariantCulture);
        environment["Evidence__Scanner__ConnectTimeoutSeconds"] = "3";
        environment["Evidence__Scanner__ScanTimeoutSeconds"] = "30";
    }

    private async Task<bool> CleanupStoresAsync()
    {
        var okay = true;
        foreach (var name in transientContainers.ToArray())
        {
            try
            {
                _ = await NativeProcess.RunAsync("docker", ["stop", name], RepositoryRoot,
                    TimeSpan.FromSeconds(30), CancellationToken.None);
                var inspect = await NativeProcess.RunAsync("docker", ["container", "inspect", name], RepositoryRoot,
                    TimeSpan.FromSeconds(30), CancellationToken.None);
                if (inspect.Exit == 0) okay = false;
            }
            catch { okay = false; }
        }
        transientContainers.Clear();
        foreach (var container in new[] { scanner, destinationStore, sourceStore })
        {
            if (container is null) continue;
            try { await container.DisposeAsync(); }
            catch { okay = false; }
        }
        scanner = null;
        destinationStore = null;
        sourceStore = null;
        if (restorePostgres is not null)
        {
            try { await restorePostgres.DisposeAsync(); }
            catch { okay = false; }
            restorePostgres = null;
        }
        if (network is not null)
        {
            try
            {
                await network.DeleteAsync();
                await network.DisposeAsync();
            }
            catch { okay = false; }
            network = null;
        }
        try
        {
            var expectedParent = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar);
            var resolved = Path.GetFullPath(privateDirectory);
            if (Path.GetDirectoryName(resolved)?.TrimEnd(Path.DirectorySeparatorChar) != expectedParent ||
                !Path.GetFileName(resolved).StartsWith("sgol-cv05-", StringComparison.Ordinal))
                return false;
            if (Directory.Exists(resolved)) Directory.Delete(resolved, recursive: true);
            if (Directory.Exists(resolved)) okay = false;
        }
        catch { okay = false; }
        return okay;
    }
}
