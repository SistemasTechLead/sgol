using System.Security.Cryptography;
using System.Text.Json;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Containers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Sgol.JobInfrastructure;
using Sgol.Operations;
using Xunit;

namespace Sgol.OperationsIntegrationTests;

[Trait("Category", "TechOpsExternal")]
public sealed class ObjectReplicaExternalTests : IAsyncLifetime
{
    private static readonly string[] AdminActions = ["Admin", "Read", "Write", "List"];
    private const string SeaweedImage = "chrislusf/seaweedfs:4.45@sha256:fc9f76fa993ad69966ffeb2f65d0318fcae39c6f8e20cf68ef7b3a5cb97769e5";
    private const string SourceQuarantine = "source-quarantine";
    private const string SourceClean = "source-clean";
    private const string DestinationQuarantine = "destination-quarantine";
    private const string DestinationClean = "destination-clean";
    private const string ManifestBucket = "replica-manifests";
    private readonly string temporaryDirectory = Path.Combine(Path.GetTempPath(), $"sgol-ops-{Guid.NewGuid():N}");
    private readonly string sourceKey = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16));
    private readonly string sourceSecret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
    private readonly string destinationKey = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16));
    private readonly string destinationSecret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
    private IContainer? sourceContainer;
    private IContainer? destinationContainer;
    private AmazonS3Client? source;
    private AmazonS3Client? destination;
    private IConfiguration? configuration;

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(temporaryDirectory);
        sourceContainer = await StartStorageAsync("source", sourceKey, sourceSecret);
        destinationContainer = await StartStorageAsync("destination", destinationKey, destinationSecret);
        var sourceEndpoint = $"http://127.0.0.1:{sourceContainer.GetMappedPublicPort(8333)}";
        var destinationEndpoint = $"http://127.0.0.1:{destinationContainer.GetMappedPublicPort(8333)}";
        source = Client(sourceEndpoint, sourceKey, sourceSecret);
        destination = Client(destinationEndpoint, destinationKey, destinationSecret);
        foreach (var bucket in new[] { SourceQuarantine, SourceClean })
        {
            await source.PutBucketAsync(bucket);
        }

        foreach (var bucket in new[] { DestinationQuarantine, DestinationClean, ManifestBucket })
        {
            await destination.PutBucketAsync(bucket);
        }

        configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Replica:Source:Endpoint"] = sourceEndpoint,
            ["Replica:Source:Region"] = "us-east-1",
            ["Replica:Source:AccessKey"] = sourceKey,
            ["Replica:Source:SecretKey"] = sourceSecret,
            ["Replica:Source:AllowInsecureTransport"] = "true",
            ["Replica:Source:QuarantineBucket"] = SourceQuarantine,
            ["Replica:Source:CleanBucket"] = SourceClean,
            ["Replica:Destination:Endpoint"] = destinationEndpoint,
            ["Replica:Destination:Region"] = "us-east-1",
            ["Replica:Destination:AccessKey"] = destinationKey,
            ["Replica:Destination:SecretKey"] = destinationSecret,
            ["Replica:Destination:AllowInsecureTransport"] = "true",
            ["Replica:Destination:QuarantineBucket"] = DestinationQuarantine,
            ["Replica:Destination:CleanBucket"] = DestinationClean,
            ["Replica:Destination:ManifestBucket"] = ManifestBucket,
            ["Replica:Destination:ManifestPrefix"] = "objects/v1",
            ["Replica:MaximumAttempts"] = "3",
            ["Replica:TimeoutSeconds"] = "60",
            ["Replica:BatchSize"] = "500",
            ["SGOL_REVISION"] = new string('a', 40),
            ["SGOL_IMAGE_DIGEST"] = "sha256:" + new string('b', 64)
        }).Build();
    }

    [Fact]
    public async Task ReplicaIsIncrementalHashVerifiedAndNeverPropagatesSourceDeletion()
    {
        var bytes = "synthetic-object"u8.ToArray();
        var hash = Convert.ToHexStringLower(SHA256.HashData(bytes));
        const string objectKey = "synthetic/object.bin";
        var put = new PutObjectRequest
        {
            BucketName = SourceClean,
            Key = objectKey,
            InputStream = new MemoryStream(bytes),
            AutoCloseStream = true,
            ContentType = "application/octet-stream"
        };
        put.Metadata["sgol-sha256"] = hash;
        put.Metadata["sgol-size-bytes"] = bytes.LongLength.ToString(System.Globalization.CultureInfo.InvariantCulture);
        put.Metadata["sgol-media-type"] = "Pdf";
        await source!.PutObjectAsync(put);

        var replica = new ObjectReplica(configuration!, TimeProvider.System, NullLogger<ObjectReplica>.Instance);
        await replica.ExecuteAsync(new DateTimeOffset(2026, 9, 12, 21, 5, 0, TimeSpan.Zero), CancellationToken.None);
        await replica.ExecuteAsync(new DateTimeOffset(2026, 9, 12, 22, 5, 0, TimeSpan.Zero), CancellationToken.None);
        await replica.ExecuteAsync(new DateTimeOffset(2026, 9, 12, 22, 5, 0, TimeSpan.Zero), CancellationToken.None);

        using (var copied = await destination!.GetObjectAsync(DestinationClean, objectKey))
        await using (var content = new MemoryStream())
        {
            await copied.ResponseStream.CopyToAsync(content);
            Assert.Equal(hash, Convert.ToHexStringLower(SHA256.HashData(content.ToArray())));
        }

        var manifests = await destination!.ListObjectsV2Async(new ListObjectsV2Request
        {
            BucketName = ManifestBucket,
            Prefix = "objects/v1/"
        });
        Assert.Equal(2, manifests.S3Objects?.Count);

        var corrupt = new PutObjectRequest
        {
            BucketName = DestinationClean,
            Key = objectKey,
            InputStream = new MemoryStream("corrupt"u8.ToArray()),
            AutoCloseStream = true,
            ContentType = "application/octet-stream"
        };
        corrupt.Metadata["sgol-sha256"] = hash;
        corrupt.Metadata["sgol-size-bytes"] = bytes.LongLength.ToString(System.Globalization.CultureInfo.InvariantCulture);
        corrupt.Metadata["sgol-media-type"] = "Pdf";
        await destination.PutObjectAsync(corrupt);
        var corruption = await Assert.ThrowsAsync<JobExecutionException>(() =>
            replica.ExecuteAsync(new DateTimeOffset(2026, 9, 12, 23, 5, 0, TimeSpan.Zero), CancellationToken.None));
        Assert.Equal("DESTINATION_OBJECT_CORRUPT", corruption.ErrorCode);
        using (var preservedCorruption = await destination.GetObjectAsync(DestinationClean, objectKey))
        await using (var corruptedContent = new MemoryStream())
        {
            await preservedCorruption.ResponseStream.CopyToAsync(corruptedContent);
            Assert.Equal("corrupt"u8.ToArray(), corruptedContent.ToArray());
        }

        put.BucketName = DestinationClean;
        put.InputStream = new MemoryStream(bytes);
        put.AutoCloseStream = true;
        await destination.PutObjectAsync(put);

        await source.DeleteObjectAsync(SourceClean, objectKey);
        var failure = await Assert.ThrowsAsync<JobExecutionException>(() =>
            replica.ExecuteAsync(new DateTimeOffset(2026, 9, 13, 0, 5, 0, TimeSpan.Zero), CancellationToken.None));
        Assert.Equal("SOURCE_OBJECT_MISSING", failure.ErrorCode);

        using var preserved = await destination.GetObjectAsync(DestinationClean, objectKey);
        Assert.NotNull(preserved.ResponseStream);
    }

    public async Task DisposeAsync()
    {
        source?.Dispose();
        destination?.Dispose();
        if (sourceContainer is not null) await sourceContainer.DisposeAsync();
        if (destinationContainer is not null) await destinationContainer.DisposeAsync();
        if (Directory.Exists(temporaryDirectory)) Directory.Delete(temporaryDirectory, recursive: true);
    }

    private async Task<IContainer> StartStorageAsync(string name, string accessKey, string secretKey)
    {
        var configurationPath = Path.Combine(temporaryDirectory, $"{name}-s3.json");
        await File.WriteAllTextAsync(configurationPath, JsonSerializer.Serialize(new
        {
            identities = new[]
            {
                new
                {
                    name,
                    credentials = new[] { new { accessKey, secretKey } },
                    actions = AdminActions
                }
            }
        }));
        var container = new ContainerBuilder(SeaweedImage)
            .WithPortBinding(8333, true)
            .WithBindMount(configurationPath, "/run/sgol/s3.json", AccessMode.ReadOnly)
            .WithCommand("mini", "-dir=/data", "-s3.config=/run/sgol/s3.json")
            .WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(8333))
            .Build();
        await container.StartAsync();
        return container;
    }

    private static AmazonS3Client Client(string endpoint, string accessKey, string secretKey) =>
        new(new BasicAWSCredentials(accessKey, secretKey), new AmazonS3Config
        {
            ServiceURL = endpoint,
            AuthenticationRegion = "us-east-1",
            ForcePathStyle = true,
            UseHttp = true,
            MaxErrorRetry = 0
        });
}
