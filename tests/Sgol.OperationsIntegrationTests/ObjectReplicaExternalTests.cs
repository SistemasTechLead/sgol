using System.Security.Cryptography;
using System.Net;
using System.Reflection;
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

// Test-only delegation: no S3 request construction, buffering or retry logic.
public class ReplicaStreamDiagnosticClient : DispatchProxy
{
    public IAmazonS3 Target { get; set; } = null!;
    public string Operation { get; private set; } = "UNKNOWN";
    public string LastCompleted { get; private set; } = "UNKNOWN";

    public static IAmazonS3 Wrap(IAmazonS3 target)
    {
        var client = Create<IAmazonS3, ReplicaStreamDiagnosticClient>();
        ((ReplicaStreamDiagnosticClient)client).Target = target;
        return client;
    }

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => targetMethod?.Name switch
    {
        nameof(IAmazonS3.PutObjectAsync) => Call("PUT_DESTINATION", () =>
            Target.PutObjectAsync((PutObjectRequest)args![0]!, (CancellationToken)args[1]!)),
        nameof(IAmazonS3.GetObjectAsync) => Call("GET_DESTINATION_VERIFY", () =>
            Target.GetObjectAsync((GetObjectRequest)args![0]!, (CancellationToken)args[1]!)),
        nameof(IAmazonS3.GetObjectMetadataAsync) => Call("HEAD_DESTINATION_METADATA", () =>
            Target.GetObjectMetadataAsync((GetObjectMetadataRequest)args![0]!, (CancellationToken)args[1]!)),
        _ => throw new InvalidOperationException("UNSUPPORTED_PROBE_OPERATION")
    };

    private async Task<T> Call<T>(string operation, Func<Task<T>> action)
    {
        Operation = operation;
        var result = await action();
        LastCompleted = operation;
        Operation = "UNKNOWN";
        return result;
    }

    public static string Diagnostic(string operation, Exception error)
    {
        var safeOperation = operation is "PROVISION" or "GET_SOURCE_STREAM" or "PUT_DESTINATION"
            or "GET_DESTINATION_VERIFY" or "HEAD_DESTINATION_METADATA" or "CLEANUP" ? operation : "UNKNOWN";
        var status = error is AmazonS3Exception s3 && (int)s3.StatusCode is >= 100 and <= 599
            ? ((int)s3.StatusCode).ToString(System.Globalization.CultureInfo.InvariantCulture) : "NONE";
        return $"OPERATION={safeOperation}:TYPE={Token(error.GetType().Name)}:HTTP={status}:S3CODE={Token((error as AmazonS3Exception)?.ErrorCode)}";
    }

    public static string Token(string? value) => value is { Length: > 0 and <= 64 } && value.All(character =>
        character is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '.' or '-' or '_')
        ? value : "UNKNOWN";
}

// No fixture lifecycle: selecting the pure category cannot start containers.
public sealed class ReplicaStreamFocusedTests
{
    private static readonly string[] ProvisionerActions = ["Admin", "Read", "Write", "List", "Tagging"];

    [Fact]
    [Trait("Category", "Hu035ReplicaStreamPure")]
    public async Task RealStorePreservesStreamRequestMetadataAndVerificationOrder()
    {
        var bytes = SyntheticEvidenceFixture.Content;
        await using var stream = new MemoryStream(bytes, writable: false);
        using var cancellation = new CancellationTokenSource();
        var metadata = new Dictionary<string, string>
        {
            ["sgol-sha256"] = SyntheticEvidenceFixture.Sha256,
            ["sgol-size-bytes"] = bytes.LongLength.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["sgol-media-type"] = SyntheticEvidenceFixture.MetadataMediaType
        };
        var calls = new List<string>();
        var fake = DispatchProxy.Create<IAmazonS3, ReplicaStreamStub>();
        ((ReplicaStreamStub)fake).Handler = (method, args) =>
        {
            calls.Add(method.Name);
            Assert.Equal(cancellation.Token, (CancellationToken)args[1]!);
            if (args[0] is PutObjectRequest put)
            {
                Assert.Same(stream, put.InputStream);
                Assert.Equal(0, stream.Position);
                Assert.Equal("*", put.IfNoneMatch);
                Assert.False(put.AutoCloseStream);
                Assert.Equal(bytes.LongLength, put.Headers.ContentLength);
                Assert.Equal(SyntheticEvidenceFixture.ContentType, put.ContentType);
                Assert.Equal(SyntheticEvidenceFixture.Sha256, put.Metadata["sha256"]);
                foreach (var item in metadata) Assert.Equal(item.Value, put.Metadata[item.Key]);
                return Task.FromResult(new PutObjectResponse());
            }
            if (args[0] is GetObjectRequest)
                return Task.FromResult(new GetObjectResponse { ResponseStream = new MemoryStream(bytes, writable: false) });
            var head = new GetObjectMetadataResponse { ContentLength = bytes.LongLength };
            head.Metadata["x-amz-meta-sha256"] = SyntheticEvidenceFixture.Sha256;
            return Task.FromResult(head);
        };
        var client = ReplicaStreamDiagnosticClient.Wrap(fake);
        await new S3OperationStore(client).PutStreamVerifiedAsync("synthetic", "synthetic", stream,
            bytes.LongLength, SyntheticEvidenceFixture.Sha256, SyntheticEvidenceFixture.ContentType, metadata, cancellation.Token);
        Assert.Equal(new[] { nameof(IAmazonS3.PutObjectAsync), nameof(IAmazonS3.GetObjectAsync), nameof(IAmazonS3.GetObjectMetadataAsync) }, calls);
        Assert.Equal("UNKNOWN", ((ReplicaStreamDiagnosticClient)client).Operation);
    }

    [Theory]
    [Trait("Category", "Hu035ReplicaStreamPure")]
    [InlineData("PUT_DESTINATION")]
    [InlineData("GET_DESTINATION_VERIFY")]
    [InlineData("HEAD_DESTINATION_METADATA")]
    public async Task AdapterDelegatesOnceAndPreservesArgumentsAndAsyncFailure(string operation)
    {
        using var cancellation = new CancellationTokenSource();
        await using var stream = new MemoryStream("synthetic"u8.ToArray());
        object request = operation switch
        {
            "PUT_DESTINATION" => new PutObjectRequest { InputStream = stream },
            "GET_DESTINATION_VERIFY" => new GetObjectRequest(),
            _ => new GetObjectMetadataRequest()
        };
        var fake = DispatchProxy.Create<IAmazonS3, ReplicaStreamStub>();
        var stub = (ReplicaStreamStub)fake;
        var client = ReplicaStreamDiagnosticClient.Wrap(fake);
        var diagnostic = (ReplicaStreamDiagnosticClient)client;
        for (var failure = 0; failure < 2; failure++)
        {
            var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var exception = new AmazonS3Exception("private-sentinel") { StatusCode = HttpStatusCode.InternalServerError };
            var response = operation switch
            {
                "PUT_DESTINATION" => (object)new PutObjectResponse(),
                "GET_DESTINATION_VERIFY" => new GetObjectResponse { ResponseStream = stream },
                _ => new GetObjectMetadataResponse()
            };
            stub.Handler = (_, args) =>
            {
                Assert.Same(request, args[0]);
                Assert.Equal(cancellation.Token, (CancellationToken)args[1]!);
                if (request is PutObjectRequest put) Assert.Same(stream, put.InputStream);
                return operation switch
                {
                    "PUT_DESTINATION" => After(completion.Task, (PutObjectResponse)response),
                    "GET_DESTINATION_VERIFY" => After(completion.Task, (GetObjectResponse)response),
                    _ => After(completion.Task, (GetObjectMetadataResponse)response)
                };
            };
            async Task<object> Invoke() => operation switch
            {
                "PUT_DESTINATION" => await client.PutObjectAsync((PutObjectRequest)request, cancellation.Token),
                "GET_DESTINATION_VERIFY" => await client.GetObjectAsync((GetObjectRequest)request, cancellation.Token),
                _ => await client.GetObjectMetadataAsync((GetObjectMetadataRequest)request, cancellation.Token)
            };
            var pending = Invoke();
            Assert.Equal(operation, diagnostic.Operation);
            Assert.False(pending.IsCompleted);
            if (failure == 0)
            {
                completion.SetResult(true);
                Assert.Same(response, await pending);
                Assert.Equal("UNKNOWN", diagnostic.Operation);
                Assert.Equal(operation, diagnostic.LastCompleted);
            }
            else
            {
                completion.SetException(exception);
                Assert.Same(exception, await Assert.ThrowsAsync<AmazonS3Exception>(async () => await pending));
                Assert.Equal(operation, diagnostic.Operation);
            }
            Assert.Equal(failure + 1, stub.Calls);
        }
    }

    private static async Task<T> After<T>(Task completion, T result)
    {
        await completion;
        return result;
    }

    [Theory]
    [Trait("Category", "Hu035ReplicaStreamPure")]
    [InlineData(409)]
    [InlineData(412)]
    public async Task RealStoreKeepsConflictThenMetadataSequence(int status)
    {
        var fake = DispatchProxy.Create<IAmazonS3, ReplicaStreamStub>();
        var calls = new List<string>();
        var client = ReplicaStreamDiagnosticClient.Wrap(fake);
        var diagnostic = (ReplicaStreamDiagnosticClient)client;
        var headFailure = new AmazonS3Exception("private-sentinel") { StatusCode = HttpStatusCode.InternalServerError };
        ((ReplicaStreamStub)fake).Handler = (method, _) =>
        {
            calls.Add(method.Name);
            if (method.Name == nameof(IAmazonS3.PutObjectAsync))
            {
                return Task.FromException<PutObjectResponse>(new AmazonS3Exception("conflict") { StatusCode = (HttpStatusCode)status });
            }
            Assert.Equal("HEAD_DESTINATION_METADATA", diagnostic.Operation);
            return Task.FromException<GetObjectMetadataResponse>(headFailure);
        };
        await using var stream = new MemoryStream("synthetic"u8.ToArray());
        var actual = await Assert.ThrowsAsync<AmazonS3Exception>(() => new S3OperationStore(client)
            .PutStreamVerifiedAsync("synthetic", "synthetic", stream, stream.Length, "synthetic", "application/octet-stream", null, CancellationToken.None));
        Assert.Same(headFailure, actual);
        Assert.Equal(new[] { nameof(IAmazonS3.PutObjectAsync), nameof(IAmazonS3.GetObjectMetadataAsync) }, calls);
        Assert.Equal("HEAD_DESTINATION_METADATA", diagnostic.Operation);
    }

    [Fact]
    [Trait("Category", "Hu035ReplicaStreamPure")]
    public void DiagnosticRejectsSensitiveTokensAndInvalidHttp()
    {
        foreach (var token in new string?[] { null, "", "Internal\nError", "AccessKey=synthetic", "https://private.invalid", new('a', 65) })
            Assert.Equal("UNKNOWN", ReplicaStreamDiagnosticClient.Token(token));
        Assert.Equal("InternalError", ReplicaStreamDiagnosticClient.Token("InternalError"));
        foreach (var status in new[] { 0, 99, 100, 500, 599, 600 })
        {
            var exception = new AmazonS3Exception("private-sentinel") { StatusCode = (HttpStatusCode)status, ErrorCode = "AccessKey=synthetic" };
            var expectedHttp = status is >= 100 and <= 599 ? status.ToString(System.Globalization.CultureInfo.InvariantCulture) : "NONE";
            Assert.Equal($"OPERATION=UNKNOWN:TYPE=AmazonS3Exception:HTTP={expectedHttp}:S3CODE=UNKNOWN",
                ReplicaStreamDiagnosticClient.Diagnostic("private-sentinel", exception));
        }
    }

    [Fact]
    [Trait("Category", "Hu035ReplicaStreamExternal")]
    public async Task ReplicaResponseStreamIsConditionallyWrittenAndVerified()
    {
        // Host-published ports reproduce requests/identities, NOT the private Worker network or native AMD64 CI.
        const string image = "chrislusf/seaweedfs:4.45@sha256:fc9f76fa993ad69966ffeb2f65d0318fcae39c6f8e20cf68ef7b3a5cb97769e5";
        const string sourceBucket = "source-clean";
        const string destinationBucket = "destination-clean";
        var directory = Path.Combine(Path.GetTempPath(), $"sgol-stream-probe-{Guid.NewGuid():N}");
        var containers = new List<IContainer>();
        var files = new List<string>();
        var restrictedConfigurations = new List<(string Path, string Content)>();
        string? firstFailure = null;
        var cleanupFailures = new List<string>();
        var operation = "PROVISION";
        ReplicaStreamDiagnosticClient? diagnostic = null;
        try
        {
            Directory.CreateDirectory(directory);
            async Task<S3EndpointOptions> Provision(bool isSource)
            {
                var adminKey = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16));
                var adminSecret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
                var key = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16));
                var secret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
                var buckets = isSource ? new[] { "source-quarantine", sourceBucket }
                    : new[] { "destination-quarantine", destinationBucket, "replica-manifests" };
                var verbs = isSource ? new[] { "Read", "List" } : new[] { "Read", "Write", "List", "Tagging" };
                var path = Path.Combine(directory, isSource ? "source.json" : "destination.json");
                files.Add(path);
                var operationalIdentity = new
                {
                    name = isSource ? "replica-source" : "replica-destination",
                    credentials = new[] { new { accessKey = key, secretKey = secret } },
                    actions = buckets.SelectMany(bucket => verbs.Select(verb => $"{verb}:{bucket}")).ToArray()
                };
                restrictedConfigurations.Add((path, JsonSerializer.Serialize(new { identities = new[] { operationalIdentity } })));
                await File.WriteAllTextAsync(path, JsonSerializer.Serialize(new
                {
                    identities = new[]
                    {
                        new { name = "provisioner", credentials = new[] { new { accessKey = adminKey, secretKey = adminSecret } }, actions = ProvisionerActions },
                        operationalIdentity
                    }
                }));
                var container = new ContainerBuilder(image).WithPortBinding(8333, true)
                    .WithBindMount(path, "/run/sgol/s3.json", AccessMode.ReadOnly)
                    .WithCommand("mini", "-dir=/data", "-s3.config=/run/sgol/s3.json")
                    .WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(8333)).Build();
                containers.Add(container);
                await container.StartAsync();
                var endpoint = $"http://127.0.0.1:{container.GetMappedPublicPort(8333)}";
                using var admin = new S3EndpointOptions(endpoint, "us-east-1", adminKey, adminSecret, true, 3, 60).CreateClient();
                foreach (var bucket in buckets) await admin.PutBucketAsync(bucket);
                if (isSource)
                {
                    var seed = new PutObjectRequest { BucketName = sourceBucket, Key = "synthetic/stream-probe.bin", InputStream = new MemoryStream(SyntheticEvidenceFixture.Content), ContentType = SyntheticEvidenceFixture.ContentType };
                    seed.Metadata["sgol-sha256"] = SyntheticEvidenceFixture.Sha256;
                    seed.Metadata["sgol-size-bytes"] = SyntheticEvidenceFixture.Content.LongLength.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    seed.Metadata["sgol-media-type"] = SyntheticEvidenceFixture.MetadataMediaType;
                    await admin.PutObjectAsync(seed);
                }
                return new S3EndpointOptions(endpoint, "us-east-1", key, secret, true, 3, 60);
            }
            var sourceOptions = await Provision(true);
            var destinationOptions = await Provision(false);
            // Same in-place WriteAllText/UTF-8-without-BOM update as HU-035 provisioning;
            // do not replace the mounted file or regenerate operational identities.
            foreach (var restricted in restrictedConfigurations)
                File.WriteAllText(restricted.Path, restricted.Content, new System.Text.UTF8Encoding(false));
            foreach (var container in containers)
            {
                // One stop/start cycle, including the existing TCP readiness strategy.
                using var restartDeadline = new CancellationTokenSource(TimeSpan.FromSeconds(60));
                await container.StopAsync(restartDeadline.Token);
                await container.StartAsync(restartDeadline.Token);
            }
            sourceOptions = sourceOptions with
            {
                Endpoint = new UriBuilder(Uri.UriSchemeHttp, containers[0].Hostname,
                    containers[0].GetMappedPublicPort(8333)).Uri.AbsoluteUri
            };
            destinationOptions = destinationOptions with
            {
                Endpoint = new UriBuilder(Uri.UriSchemeHttp, containers[1].Hostname,
                    containers[1].GetMappedPublicPort(8333)).Uri.AbsoluteUri
            };
            using var source = sourceOptions.CreateClient();
            using var destination = destinationOptions.CreateClient();
            var observed = ReplicaStreamDiagnosticClient.Wrap(destination);
            diagnostic = (ReplicaStreamDiagnosticClient)observed;
            operation = "GET_SOURCE_STREAM";
            using var response = await source.GetObjectAsync(new GetObjectRequest { BucketName = sourceBucket, Key = "synthetic/stream-probe.bin" }, CancellationToken.None);
            var metadata = new Dictionary<string, string>
            {
                ["sgol-sha256"] = response.Metadata["x-amz-meta-sgol-sha256"],
                ["sgol-size-bytes"] = response.Metadata["x-amz-meta-sgol-size-bytes"],
                ["sgol-media-type"] = response.Metadata["x-amz-meta-sgol-media-type"]
            };
            operation = "UNKNOWN";
            await new S3OperationStore(observed).PutStreamVerifiedAsync(destinationBucket, "synthetic/stream-probe.bin",
                response.ResponseStream, response.ContentLength, metadata["sgol-sha256"], response.Headers.ContentType ?? "application/octet-stream", metadata, CancellationToken.None);
        }
        catch (Exception exception)
        {
            var failedOperation = operation != "UNKNOWN" ? operation
                : diagnostic?.Operation != "UNKNOWN" ? diagnostic?.Operation : diagnostic?.LastCompleted;
            firstFailure = ReplicaStreamDiagnosticClient.Diagnostic(failedOperation ?? "UNKNOWN", exception);
        }
        finally
        {
            foreach (var container in containers)
            {
                try { await container.DisposeAsync(); }
                catch (Exception exception) { cleanupFailures.Add(ReplicaStreamDiagnosticClient.Diagnostic("CLEANUP", exception)); }
            }
            foreach (var path in files)
            {
                try { File.Delete(path); }
                catch (Exception exception) { cleanupFailures.Add(ReplicaStreamDiagnosticClient.Diagnostic("CLEANUP", exception)); }
            }
            try { if (Directory.Exists(directory)) Directory.Delete(directory, recursive: false); }
            catch (Exception exception) { cleanupFailures.Add(ReplicaStreamDiagnosticClient.Diagnostic("CLEANUP", exception)); }
        }
        if (firstFailure is not null || cleanupFailures.Count != 0)
            throw new InvalidOperationException(string.Join(";", new[] { firstFailure }.Where(value => value is not null).Concat(cleanupFailures)));
    }
}

public class ReplicaStreamStub : DispatchProxy
{
    public Func<MethodInfo, object?[], object?> Handler { get; set; } = null!;
    public int Calls { get; private set; }
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        Calls++;
        return Handler(targetMethod!, args!);
    }
}

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
