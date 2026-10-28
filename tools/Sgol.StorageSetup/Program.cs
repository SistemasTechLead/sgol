using System.Security.Cryptography;
using System.Text.Json;
using Amazon.S3;
using Amazon.S3.Model;

var endpoint = new Uri(Environment.GetEnvironmentVariable("SGOL_STORAGE_ENDPOINT") ?? throw new Exception("Falta endpoint."));
var inventory = args.Contains("--inventory");
if (!endpoint.IsLoopback || endpoint.Scheme != "http" || endpoint.Port is not (8333 or 8339 or 8349) ||
    endpoint.Port == 8333 && !inventory && !args.Contains("--activate-local")) throw new Exception("Destino storage no autorizado.");
using var s3 = new AmazonS3Client(Environment.GetEnvironmentVariable("SGOL_STORAGE_ACCESS_KEY"),
    Environment.GetEnvironmentVariable("SGOL_STORAGE_SECRET_KEY"), new AmazonS3Config
    { ServiceURL = endpoint.ToString(), ForcePathStyle = true, AuthenticationRegion = "us-east-1", MaxErrorRetry = 0, Timeout = TimeSpan.FromSeconds(15) });
var buckets = ((await s3.ListBucketsAsync()).Buckets ?? []).Select(x => x.BucketName).ToHashSet();
if (inventory)
{
    // ListBuckets is filtered by IAM ownership; the configured SGOL buckets must
    // also be read directly when validating a new provisioning identity.
    buckets.UnionWith(["sgol-quarantine", "sgol-clean"]);
    var objects = new List<object>();
    foreach (var bucket in buckets.Order())
    {
        string? continuation = null;
        do
        {
            var page = await s3.ListObjectsV2Async(new ListObjectsV2Request { BucketName = bucket, ContinuationToken = continuation });
            foreach (var item in page.S3Objects ?? [])
            {
                using var obj = await s3.GetObjectAsync(bucket, item.Key);
                objects.Add(new { bucket, key = item.Key, size = item.Size, sha256 = Convert.ToHexString(await SHA256.HashDataAsync(obj.ResponseStream)) });
            }
            continuation = page.IsTruncated == true ? page.NextContinuationToken : null;
        } while (continuation != null);
    }
    await File.WriteAllTextAsync(Environment.GetEnvironmentVariable("SGOL_STORAGE_INVENTORY_PATH") ?? throw new Exception("Falta destino privado de inventario."),
        JsonSerializer.Serialize(new { buckets = buckets.Order().ToArray(), objects }, new JsonSerializerOptions { WriteIndented = true }));
    Console.WriteLine($"Inventario verificado: {buckets.Count} buckets, {objects.Count} objetos leídos y hasheados.");
    return;
}
foreach (var bucket in new[] { "sgol-quarantine", "sgol-clean" })
{
    try { await s3.ListObjectsV2Async(new ListObjectsV2Request { BucketName = bucket, MaxKeys = 1 }); }
    catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
    { await s3.PutBucketAsync(bucket); }
}
await s3.PutCORSConfigurationAsync(new PutCORSConfigurationRequest
{
    BucketName = "sgol-quarantine", Configuration = new CORSConfiguration { Rules = [new CORSRule
    {
        Id = "sgol-evidence-upload", AllowedOrigins = [Environment.GetEnvironmentVariable("SGOL_STORAGE_ORIGIN") ?? "https://localhost:7240"],
        AllowedMethods = ["PUT"], AllowedHeaders = ["Content-Type", "Content-Length", "If-None-Match", "x-amz-meta-sgol-sha256", "x-amz-meta-sgol-media-type", "x-amz-meta-sgol-size-bytes"], MaxAgeSeconds = 600
    }] }
});
Console.WriteLine("Buckets y CORS preparados con identidad exclusiva de aprovisionamiento.");
