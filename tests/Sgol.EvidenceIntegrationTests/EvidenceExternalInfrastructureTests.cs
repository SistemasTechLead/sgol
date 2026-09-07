using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Containers;
using Microsoft.Extensions.Options;
using Sgol.Evidence.Contracts;
using Sgol.Evidence.Technical;
using Sgol.Testing;
using Sgol.Web.Infrastructure.Evidence;
using Xunit;

namespace Sgol.EvidenceIntegrationTests;

[Trait("Category", "EvidenceExternal")]
public sealed class EvidenceExternalInfrastructureTests : IAsyncLifetime
{
    private const string SeaweedImage = "chrislusf/seaweedfs:4.45@sha256:fc9f76fa993ad69966ffeb2f65d0318fcae39c6f8e20cf68ef7b3a5cb97769e5";
    private const string ClamAvImage = "clamav/clamav:1.5.4-debian@sha256:be3cb41d9833ce9ffb98f3d3e1483c35c0d87060c2bda3624d75fd28bbf0b3bd";
    private const string QuarantineBucket = "sgol-evidence-quarantine";
    private const string CleanBucket = "sgol-evidence-clean";
    private readonly string temporaryDirectory = Path.Combine(
        Path.GetTempPath(),
        $"sgol-evidence-tests-{Guid.NewGuid():N}");
    private readonly string adminAccessKey = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16));
    private readonly string adminSecretKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
    private readonly string applicationAccessKey = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16));
    private readonly string applicationSecretKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
    private IContainer? storageContainer;
    private IContainer? scannerContainer;
    private AmazonS3Client? applicationClient;
    private EvidenceStorageOptions? storageOptions;

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(temporaryDirectory);
        var configurationPath = Path.Combine(temporaryDirectory, "s3.json");
        var configuration = new
        {
            identities = new object[]
            {
                new
                {
                    name = "provisioner",
                    credentials = new[] { new { accessKey = adminAccessKey, secretKey = adminSecretKey } },
                    actions = new[] { "Admin", "Read", "Write", "List" }
                },
                new
                {
                    name = "sgol-evidence",
                    credentials = new[] { new { accessKey = applicationAccessKey, secretKey = applicationSecretKey } },
                    actions = new[]
                    {
                        $"Read:{QuarantineBucket}", $"Write:{QuarantineBucket}", $"List:{QuarantineBucket}",
                        $"Read:{CleanBucket}", $"Write:{CleanBucket}", $"List:{CleanBucket}"
                    }
                }
            }
        };
        await File.WriteAllTextAsync(configurationPath, JsonSerializer.Serialize(configuration));

        storageContainer = new ContainerBuilder(SeaweedImage)
            .WithPortBinding(8333, true)
            .WithBindMount(configurationPath, "/run/sgol/s3.json", AccessMode.ReadOnly)
            .WithCommand("mini", "-dir=/data", "-s3.config=/run/sgol/s3.json")
            .WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(8333))
            .Build();
        scannerContainer = new ContainerBuilder(ClamAvImage)
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
            .WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(3310))
            .Build();

        await storageContainer.StartAsync();
        await scannerContainer.StartAsync();
        var endpoint = $"http://127.0.0.1:{storageContainer.GetMappedPublicPort(8333)}";
        using (var admin = CreateClient(endpoint, adminAccessKey, adminSecretKey))
        {
            await admin.PutBucketAsync(QuarantineBucket);
            await admin.PutBucketAsync(CleanBucket);
            await admin.PutCORSConfigurationAsync(CorsConfiguration("http://127.0.0.1:5000"));
        }

        applicationClient = CreateClient(endpoint, applicationAccessKey, applicationSecretKey);
        storageOptions = new EvidenceStorageOptions
        {
            Endpoint = endpoint,
            Region = "us-east-1",
            QuarantineBucket = QuarantineBucket,
            CleanBucket = CleanBucket,
            AccessKey = applicationAccessKey,
            SecretKey = applicationSecretKey,
            AllowedUploadOrigins = ["http://127.0.0.1:5000"],
            AllowInsecureTransport = true
        };
    }

    [Fact]
    public async Task PrivateStorageAndRealScannerKeepNonCleanContentInQuarantine()
    {
        var storage = new S3PrivateObjectStorage(applicationClient!, Options.Create(storageOptions!));
        var scanner = new ClamAvScanner(Options.Create(new EvidenceScannerOptions
        {
            Host = "127.0.0.1",
            Port = scannerContainer!.GetMappedPublicPort(3310),
            ConnectTimeoutSeconds = 3,
            ScanTimeoutSeconds = 30
        }));
        var pipeline = new EvidenceInspectionPipeline(
            new FileTechnicalValidator(),
            new CryptographicEvidenceObjectKeyFactory(),
            storage,
            scanner);
        await storage.CheckAvailabilityAsync(CancellationToken.None);
        await scanner.CheckAvailabilityAsync(CancellationToken.None);

        var directBytes = EvidenceCorpus.Png();
        var directKey = new CryptographicEvidenceObjectKeyFactory().Create();
        var directMetadata = new EvidenceObjectMetadata(
            directKey,
            directBytes.Length,
            Convert.ToHexStringLower(SHA256.HashData(directBytes)),
            EvidenceMediaType.Png);
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(10);
        var authorization = await storage.CreateQuarantineUploadAuthorizationAsync(
            directMetadata, expiresAt, CancellationToken.None);
        Assert.Equal(expiresAt, authorization.ExpiresAt);
        Assert.Equal(Uri.UriSchemeHttp, authorization.Url.Scheme);
        Assert.Contains(QuarantineBucket, authorization.Url.AbsoluteUri, StringComparison.Ordinal);
        using (var signedClient = new HttpClient())
        using (var request = new HttpRequestMessage(HttpMethod.Put, authorization.Url))
        {
            request.Content = new ByteArrayContent(directBytes);
            request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(authorization.Headers.ContentType);
            request.Headers.TryAddWithoutValidation("If-None-Match", authorization.Headers.IfNoneMatch);
            request.Headers.TryAddWithoutValidation("x-amz-meta-sgol-sha256", authorization.Headers.Sha256);
            request.Headers.TryAddWithoutValidation("x-amz-meta-sgol-media-type", authorization.Headers.MediaType);
            request.Headers.TryAddWithoutValidation("x-amz-meta-sgol-size-bytes", authorization.Headers.SizeBytes);
            using var response = await signedClient.SendAsync(request);
            Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        }
        Assert.Equal(directMetadata, await storage.GetMetadataAsync(
            EvidenceStorageArea.Quarantine, directKey, CancellationToken.None));
        await storage.DeleteAsync(EvidenceStorageArea.Quarantine, directKey, CancellationToken.None);

        using (var admin = CreateClient(storageOptions!.Endpoint, adminAccessKey, adminSecretKey))
        {
            var cors = await admin.GetCORSConfigurationAsync(QuarantineBucket);
            var rule = Assert.Single(cors.Configuration.Rules);
            Assert.Equal("sgol-evidence-upload", rule.Id);
            Assert.Equal(["http://127.0.0.1:5000"], rule.AllowedOrigins);
            Assert.Equal(["PUT"], rule.AllowedMethods);
            Assert.DoesNotContain("*", rule.AllowedOrigins);
            Assert.Equal(600, rule.MaxAgeSeconds);

            await admin.PutCORSConfigurationAsync(CorsConfiguration("http://127.0.0.1:5001"));
        }
        var mismatchedCorsStorage = new S3PrivateObjectStorage(applicationClient!, Options.Create(storageOptions));
        await Assert.ThrowsAsync<EvidenceStorageUnavailableException>(() =>
            mismatchedCorsStorage.CreateQuarantineUploadAuthorizationAsync(
                directMetadata with { Key = new CryptographicEvidenceObjectKeyFactory().Create() },
                DateTimeOffset.UtcNow.AddMinutes(10),
                CancellationToken.None));

        var receipt = await pipeline.InspectAsync(
            new MemoryStream(EvidenceCorpus.Png()),
            "image/png",
            "synthetic.png",
            CancellationToken.None);

        Assert.Equal(EvidenceScanResult.Limpio, receipt.Result);
        Assert.NotNull(receipt.Metadata);
        await Assert.ThrowsAsync<EvidenceObjectNotFoundException>(() =>
            storage.GetMetadataAsync(EvidenceStorageArea.Quarantine, receipt.Metadata.Key, CancellationToken.None));
        Assert.Equal(receipt.Metadata,
            await storage.GetMetadataAsync(EvidenceStorageArea.Clean, receipt.Metadata.Key, CancellationToken.None));

        using var anonymous = new HttpClient();
        var anonymousRead = await anonymous.GetAsync(
            $"{storageOptions!.Endpoint}/{CleanBucket}/{receipt.Metadata.Key.Value}");
        Assert.Equal(HttpStatusCode.Forbidden, anonymousRead.StatusCode);

        await using var duplicateContent = new MemoryStream(EvidenceCorpus.Jpeg());
        var duplicateValidation = await new FileTechnicalValidator().ValidateAsync(
            duplicateContent, "image/jpeg", "synthetic.jpg", CancellationToken.None);
        Assert.NotNull(duplicateValidation.File);
        await using var duplicateFile = duplicateValidation.File;
        var duplicateMetadata = new EvidenceObjectMetadata(
            new CryptographicEvidenceObjectKeyFactory().Create(),
            duplicateFile.SizeBytes,
            duplicateFile.Sha256,
            duplicateFile.MediaType);
        await storage.PutQuarantineAsync(duplicateMetadata, duplicateFile.Content, CancellationToken.None);
        duplicateFile.Content.Position = 0;
        await Assert.ThrowsAsync<EvidenceObjectAlreadyExistsException>(() =>
            storage.PutQuarantineAsync(duplicateMetadata, duplicateFile.Content, CancellationToken.None));

        var inconsistentKey = new CryptographicEvidenceObjectKeyFactory().Create();
        await applicationClient!.PutObjectAsync(new PutObjectRequest
        {
            BucketName = QuarantineBucket,
            Key = inconsistentKey.Value,
            InputStream = new MemoryStream(EvidenceCorpus.Png()),
            AutoCloseStream = true,
            ContentType = "image/png"
        });
        await Assert.ThrowsAsync<EvidenceObjectIntegrityException>(() =>
            storage.GetMetadataAsync(EvidenceStorageArea.Quarantine, inconsistentKey, CancellationToken.None));

        Assert.Equal("true", Environment.GetEnvironmentVariable("SGOL_EVIDENCE_EICAR_TESTS"));
        var infected = await ScanEicarAsync(scanner);
        Assert.Equal(EvidenceScanResult.Infectado, infected.Result);
    }

    public async Task DisposeAsync()
    {
        applicationClient?.Dispose();
        if (scannerContainer is not null)
        {
            await scannerContainer.DisposeAsync();
        }

        if (storageContainer is not null)
        {
            await storageContainer.DisposeAsync();
        }

        if (Directory.Exists(temporaryDirectory))
        {
            Directory.Delete(temporaryDirectory, recursive: true);
        }
    }

    private static PutCORSConfigurationRequest CorsConfiguration(string origin) => new()
    {
        BucketName = QuarantineBucket,
        Configuration = new CORSConfiguration
        {
            Rules =
            [
                new CORSRule
                {
                    Id = "sgol-evidence-upload",
                    AllowedOrigins = [origin],
                    AllowedMethods = ["PUT"],
                    AllowedHeaders =
                    [
                        "Content-Type", "Content-Length", "If-None-Match",
                        "x-amz-meta-sgol-sha256", "x-amz-meta-sgol-media-type",
                        "x-amz-meta-sgol-size-bytes"
                    ],
                    MaxAgeSeconds = 600
                }
            ]
        }
    };

    private static AmazonS3Client CreateClient(string endpoint, string accessKey, string secretKey) =>
        new(new BasicAWSCredentials(accessKey, secretKey), new AmazonS3Config
        {
            ServiceURL = endpoint,
            AuthenticationRegion = "us-east-1",
            ForcePathStyle = true,
            UseHttp = new Uri(endpoint).Scheme == Uri.UriSchemeHttp,
            MaxErrorRetry = 0
        });

    private async Task<EvidenceScanOutcome> ScanEicarAsync(ClamAvScanner scanner)
    {
        string[] fragments =
        [
            "AP[4\\PZX54(P^)7CC)7}",
            "ANTIVIRUS-TEST-FILE!$H+H*",
            "X5O!P%@",
            "$EICAR-STANDARD-"
        ];
        var bytes = System.Text.Encoding.ASCII.GetBytes(string.Concat(
            fragments[2], fragments[0], fragments[3], fragments[1]));
        var path = Path.Combine(temporaryDirectory, $"eicar-{Guid.NewGuid():N}.tmp");
        try
        {
            await File.WriteAllBytesAsync(path, bytes);
            await using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.None,
                EvidenceFileLimits.BufferBytes,
                FileOptions.Asynchronous | FileOptions.SequentialScan | FileOptions.DeleteOnClose);
            return await scanner.ScanAsync(stream, stream.Length, CancellationToken.None);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
