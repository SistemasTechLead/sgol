using System.Net;
using System.Security.Cryptography;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Xunit;

namespace Sgol.OperationsIntegrationTests;

[Trait("Category", "TechOpsProvisioning")]
public sealed class SyntheticEnvironmentProvisioningTests
{
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

            var content = "TECH-OPS-001 synthetic evidence object"u8.ToArray();
            var hash = Convert.ToHexStringLower(SHA256.HashData(content));
            using var evidence = Client(sourceEndpoint, "EVIDENCE");
            var request = new PutObjectRequest
            {
                BucketName = sourceBuckets[1],
                Key = "synthetic/tech-ops-001.txt",
                InputStream = new MemoryStream(content),
                AutoCloseStream = true,
                ContentType = "text/plain"
            };
            request.Metadata["sgol-sha256"] = hash;
            request.Metadata["sgol-size-bytes"] = content.LongLength.ToString(
                System.Globalization.CultureInfo.InvariantCulture);
            request.Metadata["sgol-media-type"] = "Text";
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
        using var response = await client.GetObjectAsync(bucket, "synthetic/tech-ops-001.txt");
        await using var copy = new MemoryStream();
        await response.ResponseStream.CopyToAsync(copy);
        var hash = Convert.ToHexStringLower(SHA256.HashData(copy.ToArray()));
        Assert.Equal(hash, response.Metadata["x-amz-meta-sgol-sha256"]);
        Assert.Equal(copy.Length.ToString(System.Globalization.CultureInfo.InvariantCulture),
            response.Metadata["x-amz-meta-sgol-size-bytes"]);
        Assert.Equal("Text", response.Metadata["x-amz-meta-sgol-media-type"]);
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

    private static string Required(string name) =>
        Environment.GetEnvironmentVariable(name) is { Length: > 0 } value
            ? value
            : throw new InvalidOperationException($"Missing synthetic provisioning setting: {name}");
}
