using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Sgol.Operations;
using Xunit;

namespace Sgol.OperationsIntegrationTests;

[Trait("Category", "TechOpsProvisioning")]
public sealed class SyntheticEnvironmentProvisioningTests
{
    [Fact]
    public void BackupStorageDiagnosticTokensAreSanitized()
    {
        var sanitize = typeof(PostgreSqlPortableBackup).GetMethod(
            "SanitizeDiagnosticToken",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(sanitize);
        Assert.Equal("AmazonS3Exception", Sanitize("AmazonS3Exception"));
        Assert.Equal("InternalError", Sanitize("InternalError"));
        Assert.Equal("UNKNOWN", Sanitize(null));
        Assert.Equal("UNKNOWN", Sanitize(string.Empty));
        Assert.Equal("UNKNOWN", Sanitize("Access Denied"));
        Assert.Equal("UNKNOWN", Sanitize("Internal\nError"));
        Assert.Equal("UNKNOWN", Sanitize("AccessKey=synthetic"));
        Assert.Equal("UNKNOWN", Sanitize("http://storage.invalid/private"));
        Assert.Equal("UNKNOWN", Sanitize(new string('a', 65)));

        string Sanitize(string? value) => Assert.IsType<string>(sanitize.Invoke(null, [value]));
    }

    [Fact]
    public async Task BackupIdentitySupportsConditionalPutGetAndHead()
    {
        var phase = Required("SGOL_PROVISION_PHASE");
        if (phase == "create")
        {
            return;
        }

        Assert.Equal("verify", phase);
        var endpoint = Required("SGOL_PROVISION_DESTINATION_ENDPOINT");
        var bucket = Required("SGOL_PROVISION_MANIFEST_BUCKET");
        var content = "SGOL synthetic conditional backup probe"u8.ToArray();
        var hash = Convert.ToHexStringLower(SHA256.HashData(content));
        var key = $"provisioning/backup-conditional/{Guid.NewGuid():N}.bin";
        var operation = ProvisioningS3Operation.PUT_OBJECT;

        // This reproduces BACKUP credentials, policy, request shape, retries and timeout through the
        // loopback provisioning port; it does not exercise the private Worker-to-SeaweedFS network path.
        using var backup = BackupClient(endpoint);
        try
        {
            await using var input = new MemoryStream(content, writable: false);
            var put = new PutObjectRequest
            {
                BucketName = bucket,
                Key = key,
                InputStream = input,
                AutoCloseStream = false,
                ContentType = "application/octet-stream",
                IfNoneMatch = "*"
            };
            put.Metadata["sha256"] = hash;
            put.Headers.ContentLength = content.LongLength;
            await backup.PutObjectAsync(put);

            operation = ProvisioningS3Operation.GET_OBJECT;
            using (var get = await backup.GetObjectAsync(bucket, key))
            {
                await using var copy = new MemoryStream();
                await get.ResponseStream.CopyToAsync(copy);
                var stored = copy.ToArray();
                Assert.Equal(content, stored);
                Assert.Equal(hash, Convert.ToHexStringLower(SHA256.HashData(stored)));
            }

            operation = ProvisioningS3Operation.HEAD_OBJECT;
            var head = await backup.GetObjectMetadataAsync(bucket, key);
            Assert.Equal(content.LongLength, head.ContentLength);
            Assert.Equal(hash, head.Metadata["x-amz-meta-sha256"]);
        }
        catch (AmazonS3Exception exception)
        {
            var numericStatus = (int)exception.StatusCode;
            var httpStatus = numericStatus is >= 100 and <= 599
                ? numericStatus.ToString(System.Globalization.CultureInfo.InvariantCulture)
                : "null";
            var diagnostic = string.Create(
                System.Globalization.CultureInfo.InvariantCulture,
                $"TECH_OPS_BACKUP_PROBE_FAILED:OPERATION={operation}:TYPE={SanitizeProbeToken(exception.GetType().Name)}:HTTP={httpStatus}:S3CODE={SanitizeProbeToken(exception.ErrorCode)}");
            throw new InvalidOperationException(diagnostic);
        }
    }

    [Fact]
    public async Task ProvisionOrVerifySyntheticBucketsAndSeed()
    {
        var phase = Required("SGOL_PROVISION_PHASE");
        var sourceEndpoint = Required("SGOL_PROVISION_SOURCE_ENDPOINT");
        var destinationEndpoint = Required("SGOL_PROVISION_DESTINATION_ENDPOINT");
        var sourceBuckets = new[]
        {
            Required("SGOL_PROVISION_SOURCE_QUARANTINE_BUCKET"),
            Required("SGOL_PROVISION_SOURCE_CLEAN_BUCKET")
        };
        var destinationBuckets = new[]
        {
            Required("SGOL_PROVISION_DESTINATION_QUARANTINE_BUCKET"),
            Required("SGOL_PROVISION_DESTINATION_CLEAN_BUCKET"),
            Required("SGOL_PROVISION_MANIFEST_BUCKET")
        };

        if (phase == "create")
        {
            using var sourceAdmin = Client(sourceEndpoint, "SOURCE_ADMIN");
            using var destinationAdmin = Client(destinationEndpoint, "DESTINATION_ADMIN");
            foreach (var bucket in sourceBuckets)
            {
                await sourceAdmin.PutBucketAsync(bucket);
            }
            foreach (var bucket in destinationBuckets)
            {
                await destinationAdmin.PutBucketAsync(bucket);
            }

            SyntheticEvidenceFixture.AssertContract();
            var content = SyntheticEvidenceFixture.Content;
            var hash = SyntheticEvidenceFixture.Sha256;
            using var evidence = Client(sourceEndpoint, "EVIDENCE");
            var request = new PutObjectRequest
            {
                BucketName = sourceBuckets[1],
                Key = SyntheticEvidenceFixture.ObjectKey,
                InputStream = new MemoryStream(content),
                AutoCloseStream = true,
                ContentType = SyntheticEvidenceFixture.ContentType
            };
            request.Metadata["sgol-sha256"] = hash;
            request.Metadata["sgol-size-bytes"] = content.LongLength.ToString(
                System.Globalization.CultureInfo.InvariantCulture);
            request.Metadata["sgol-media-type"] = SyntheticEvidenceFixture.MetadataMediaType;
            await evidence.PutObjectAsync(request);
            return;
        }

        Assert.Equal("verify", phase);
        using var evidenceReader = Client(sourceEndpoint, "EVIDENCE");
        using var replicaSource = Client(sourceEndpoint, "REPLICA_SOURCE");
        using var backup = Client(destinationEndpoint, "BACKUP");
        using var replicaDestination = Client(destinationEndpoint, "REPLICA_DESTINATION");
        await AssertSyntheticSeedAsync(evidenceReader, sourceBuckets[1]);
        await AssertSyntheticSeedAsync(replicaSource, sourceBuckets[1]);
        await backup.ListObjectsV2Async(new ListObjectsV2Request { BucketName = destinationBuckets[2] });
        await replicaDestination.ListObjectsV2Async(new ListObjectsV2Request { BucketName = destinationBuckets[0] });

        var denied = await Assert.ThrowsAsync<AmazonS3Exception>(() => replicaSource.PutObjectAsync(
            new PutObjectRequest
            {
                BucketName = sourceBuckets[1],
                Key = "synthetic/must-not-write.txt",
                ContentBody = "denied"
            }));
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
    }

    private static async Task AssertSyntheticSeedAsync(AmazonS3Client client, string bucket)
    {
        using var response = await client.GetObjectAsync(bucket, SyntheticEvidenceFixture.ObjectKey);
        await using var copy = new MemoryStream();
        await response.ResponseStream.CopyToAsync(copy);
        var hash = Convert.ToHexStringLower(SHA256.HashData(copy.ToArray()));
        Assert.Equal(hash, response.Metadata["x-amz-meta-sgol-sha256"]);
        Assert.Equal(copy.Length.ToString(System.Globalization.CultureInfo.InvariantCulture),
            response.Metadata["x-amz-meta-sgol-size-bytes"]);
        Assert.Equal(SyntheticEvidenceFixture.MetadataMediaType,
            response.Metadata["x-amz-meta-sgol-media-type"]);
    }

    private static AmazonS3Client Client(string endpoint, string identity)
    {
        var accessKey = Required($"SGOL_PROVISION_{identity}_ACCESS_KEY");
        var secretKey = Required($"SGOL_PROVISION_{identity}_SECRET_KEY");
        return new AmazonS3Client(new BasicAWSCredentials(accessKey, secretKey), new AmazonS3Config
        {
            ServiceURL = endpoint,
            AuthenticationRegion = "us-east-1",
            ForcePathStyle = true,
            UseHttp = true,
            MaxErrorRetry = 0
        });
    }

    private static AmazonS3Client BackupClient(string endpoint)
    {
        var parsed = new Uri(endpoint, UriKind.Absolute);
        Assert.Equal(Uri.UriSchemeHttp, parsed.Scheme);
        Assert.True(parsed.IsLoopback);
        return new AmazonS3Client(
            new BasicAWSCredentials(
                Required("SGOL_PROVISION_BACKUP_ACCESS_KEY"),
                Required("SGOL_PROVISION_BACKUP_SECRET_KEY")),
            new AmazonS3Config
            {
                ServiceURL = endpoint,
                AuthenticationRegion = "us-east-1",
                ForcePathStyle = true,
                UseHttp = true,
                RetryMode = RequestRetryMode.Standard,
                MaxErrorRetry = 2,
                Timeout = TimeSpan.FromSeconds(60)
            });
    }

    private static string SanitizeProbeToken(string? value)
    {
        const int maximumLength = 64;
        if (string.IsNullOrEmpty(value) || value.Length > maximumLength ||
            !value.All(character => char.IsAsciiLetterOrDigit(character) || character is '.' or '_' or '-'))
        {
            return "UNKNOWN";
        }

        return value;
    }

    private enum ProvisioningS3Operation
    {
        PUT_OBJECT,
        GET_OBJECT,
        HEAD_OBJECT
    }

    private static string Required(string name) =>
        Environment.GetEnvironmentVariable(name) is { Length: > 0 } value
            ? value
            : throw new InvalidOperationException($"Missing synthetic provisioning setting: {name}");
}
