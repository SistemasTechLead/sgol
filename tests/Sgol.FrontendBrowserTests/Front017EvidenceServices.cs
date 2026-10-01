using System.Security.Cryptography;
using System.Text.Json;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Containers;
using Microsoft.Extensions.Options;
using Sgol.Evidence.Technical;
using Sgol.Web.Infrastructure.Evidence;

namespace Sgol.FrontendBrowserTests;

internal sealed class Front017EvidenceServices : IAsyncDisposable
{
    private const string Quarantine = "sgol-front017-quarantine";
    private const string Clean = "sgol-front017-clean";
    private static readonly string[] Actions = ["Admin", "Read", "Write", "List"];
    private readonly string directory = Path.Combine(Path.GetTempPath(), "sgol-front017-" + Guid.NewGuid().ToString("N"));
    private IContainer? storage;
    private IContainer? scanner;
    private AmazonS3Client? client;
    public EvidenceInspectionPipeline Pipeline { get; private set; } = null!;

    public async Task<IReadOnlyDictionary<string, string>> StartAsync(Uri origin)
    {
        Directory.CreateDirectory(directory);
        var access = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16));
        var secret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var path = Path.Combine(directory, "s3.json");
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(new { identities = new[] { new { name = "synthetic-front017", credentials = new[] { new { accessKey = access, secretKey = secret } }, actions = Actions } } }));
        storage = new ContainerBuilder("chrislusf/seaweedfs:4.45@sha256:fc9f76fa993ad69966ffeb2f65d0318fcae39c6f8e20cf68ef7b3a5cb97769e5")
            .WithPortBinding(8333, true).WithBindMount(path, "/run/sgol/s3.json", AccessMode.ReadOnly)
            .WithCommand("mini", "-dir=/data", "-s3.config=/run/sgol/s3.json")
            .WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(8333)).Build();
        scanner = new ContainerBuilder("clamav/clamav:1.5.4-debian@sha256:be3cb41d9833ce9ffb98f3d3e1483c35c0d87060c2bda3624d75fd28bbf0b3bd")
            .WithPortBinding(3310, true).WithEnvironment("CLAMAV_NO_FRESHCLAMD", "true")
            .WithEnvironment("CLAMD_CONF_StreamMaxLength", "16M").WithEnvironment("CLAMD_CONF_MaxFileSize", "16M")
            .WithEnvironment("CLAMD_CONF_MaxScanSize", "32M").WithEnvironment("CLAMD_CONF_MaxRecursion", "4")
            .WithEnvironment("CLAMD_CONF_MaxFiles", "64").WithEnvironment("CLAMD_CONF_MaxScanTime", "30000")
            .WithEnvironment("CLAMD_CONF_MaxThreads", "2").WithEnvironment("CLAMD_CONF_MaxQueue", "4")
            .WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(3310)).Build();
        await Task.WhenAll(storage.StartAsync(), scanner.StartAsync());
        var endpoint = "http://127.0.0.1:" + storage.GetMappedPublicPort(8333);
        client = new AmazonS3Client(new BasicAWSCredentials(access, secret), new AmazonS3Config { ServiceURL = endpoint, AuthenticationRegion = "us-east-1", ForcePathStyle = true, UseHttp = true, MaxErrorRetry = 0 });
        await client.PutBucketAsync(Quarantine); await client.PutBucketAsync(Clean);
        var allowedOrigin = origin.GetLeftPart(UriPartial.Authority);
        await client.PutCORSConfigurationAsync(new PutCORSConfigurationRequest { BucketName = Quarantine, Configuration = new CORSConfiguration { Rules = [new CORSRule { Id = "sgol-evidence-upload", AllowedOrigins = [allowedOrigin], AllowedMethods = ["PUT"], AllowedHeaders = ["Content-Type", "Content-Length", "If-None-Match", "x-amz-meta-sgol-sha256", "x-amz-meta-sgol-media-type", "x-amz-meta-sgol-size-bytes"], MaxAgeSeconds = 600 }] } });
        var options = new EvidenceStorageOptions { Endpoint = endpoint, Region = "us-east-1", QuarantineBucket = Quarantine, CleanBucket = Clean, AccessKey = access, SecretKey = secret, AllowedUploadOrigins = [allowedOrigin], AllowInsecureTransport = true };
        Pipeline = new(new FileTechnicalValidator(), new CryptographicEvidenceObjectKeyFactory(), new S3PrivateObjectStorage(client, Options.Create(options)),
            new ClamAvScanner(Options.Create(new EvidenceScannerOptions { Host = "127.0.0.1", Port = scanner.GetMappedPublicPort(3310) })));
        return new Dictionary<string, string>
        {
            ["Evidence__Storage__Endpoint"] = endpoint,
            ["Evidence__Storage__Region"] = "us-east-1",
            ["Evidence__Storage__QuarantineBucket"] = Quarantine,
            ["Evidence__Storage__CleanBucket"] = Clean,
            ["Evidence__Storage__AccessKey"] = access,
            ["Evidence__Storage__SecretKey"] = secret,
            ["Evidence__Storage__AllowedUploadOrigins__0"] = allowedOrigin,
            ["Evidence__Storage__AllowInsecureTransport"] = "true",
            ["Evidence__Scanner__Host"] = "127.0.0.1",
            ["Evidence__Scanner__Port"] = scanner.GetMappedPublicPort(3310).ToString(System.Globalization.CultureInfo.InvariantCulture)
        };
    }

    public async ValueTask DisposeAsync()
    {
        client?.Dispose();
        if (scanner is not null) await scanner.DisposeAsync();
        if (storage is not null) await storage.DisposeAsync();
        var resolved = Path.GetFullPath(directory);
        if (!resolved.StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase) ||
            !Path.GetFileName(resolved).StartsWith("sgol-front017-", StringComparison.Ordinal)) throw new InvalidOperationException("Unexpected fixture cleanup path.");
        if (Directory.Exists(resolved)) Directory.Delete(resolved, true);
    }
}
