using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Sgol.BuildingBlocks.Time;
using Sgol.BuildingBlocks.Identifiers;
using Sgol.JobInfrastructure;
using Sgol.Web.Infrastructure.Persistence;
using SkiaSharp;
using System.Security.Cryptography;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;
using Sgol.Evidence.Contracts;
using Sgol.Evidence.Technical;
using Sgol.Web.Infrastructure.Evidence;

internal static class StorageChecks
{
    public static async Task RunAsync(IAmazonS3 maintenance, string runtimePassword, IServiceProvider provider, Action<bool, string> check)
    {
        await maintenance.PutCORSConfigurationAsync(new PutCORSConfigurationRequest { BucketName = "sgol-quarantine",
            Configuration = new CORSConfiguration { Rules = [new CORSRule { Id = "sgol-evidence-upload", AllowedOrigins = ["https://localhost:7287"],
                AllowedMethods = ["PUT"], AllowedHeaders = ["Content-Type", "Content-Length", "If-None-Match", "x-amz-meta-sgol-sha256", "x-amz-meta-sgol-media-type", "x-amz-meta-sgol-size-bytes"], MaxAgeSeconds = 600 }] } });
        var options = new EvidenceStorageOptions { Endpoint = "http://127.0.0.1:8339", Region = "us-east-1", QuarantineBucket = "sgol-quarantine",
            CleanBucket = "sgol-clean", AccessKey = "security-runtime", SecretKey = runtimePassword, AllowedUploadOrigins = ["https://localhost:7287"], AllowInsecureTransport = true };
        using var runtime = new AmazonS3Client(options.AccessKey, options.SecretKey, new AmazonS3Config { ServiceURL = options.Endpoint,
            ForcePathStyle = true, AuthenticationRegion = options.Region, MaxErrorRetry = 0 });
        try { await runtime.PutBucketAsync("security-forbidden-bucket"); check(false, "Runtime S3 must not create buckets"); }
        catch (AmazonS3Exception e) { check(e.StatusCode == HttpStatusCode.Forbidden, "O03 runtime S3 cannot provision buckets"); }
        var storage = new S3PrivateObjectStorage(runtime, Options.Create(options));
        var scanner = new ClamAvScanner(Options.Create(new EvidenceScannerOptions { Host = "127.0.0.1", Port = 3319 }));
        await scanner.CheckAvailabilityAsync(default);
        // Valid tiny PNG: the production technical validator, ClamAV and both S3 buckets are real.
        using var bitmap = new SKBitmap(32, 32); bitmap.Erase(SKColors.LightBlue);
        using var image = SKImage.FromBitmap(bitmap); using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
        var png = encoded.ToArray();
        var pipeline = new EvidenceInspectionPipeline(new FileTechnicalValidator(), new CryptographicEvidenceObjectKeyFactory(), storage, scanner);
        await using var input = new MemoryStream(png);
        var inspected = await pipeline.InspectAsync(input, "image/png", "security.png", default);
        check(inspected.Result == EvidenceScanResult.Limpio && inspected.Metadata != null, "O03 real technical validation/quarantine/ClamAV/promotion: " + inspected.Result + "/" + inspected.ErrorCode);
        var metadata = inspected.Metadata!;
        var expiration = DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds()).AddSeconds(2);
        var download = await storage.CreateCleanDownloadAuthorizationAsync(metadata, Guid.NewGuid(), expiration, default);
        using var http = new HttpClient();
        var good = await http.GetAsync(download.Url);
        check(good.IsSuccessStatusCode && Convert.ToHexString(SHA256.HashData(await good.Content.ReadAsByteArrayAsync())).Equals(metadata.Sha256, StringComparison.OrdinalIgnoreCase), "O03 signed GET preserves evidence SHA256");
        await Task.Delay(3100);
        check((int)(await http.GetAsync(download.Url)).StatusCode == 403, "O03 expired signed GET rejected");
        var uploadMeta = metadata with { Key = new CryptographicEvidenceObjectKeyFactory().Create() };
        var upload = await storage.CreateQuarantineUploadAuthorizationAsync(uploadMeta, DateTimeOffset.UtcNow.AddMinutes(1), default);
        using var request = new HttpRequestMessage(HttpMethod.Put, upload.Url) { Content = new ByteArrayContent(png) };
        request.Content.Headers.ContentType = new("image/png");
        // These headers are part of the signed contract produced by the real adapter.
        request.Headers.TryAddWithoutValidation("If-None-Match", "*");
        request.Headers.TryAddWithoutValidation("x-amz-meta-sgol-sha256", uploadMeta.Sha256);
        request.Headers.TryAddWithoutValidation("x-amz-meta-sgol-media-type", uploadMeta.MediaType.ToString());
        request.Headers.TryAddWithoutValidation("x-amz-meta-sgol-size-bytes", uploadMeta.SizeBytes.ToString());
        check((await http.SendAsync(request)).IsSuccessStatusCode, "O03 signed PUT allowed through POST-denying gateway");
        await storage.DeleteAsync(EvidenceStorageArea.Quarantine, uploadMeta.Key, default);
        await storage.DeleteAsync(EvidenceStorageArea.Clean, metadata.Key, default);
        await using var jobScope = provider.CreateAsyncScope();
        var jobDb = jobScope.ServiceProvider.GetRequiredService<SgolDbContext>();
        var ids = jobScope.ServiceProvider.GetRequiredService<IUuidGenerator>();
        var job = new CleanExpiredEvidenceUploadsJob(storage, ids);
        var runner = new ScheduledJobRunner(jobDb, new ScheduledJobRegistry([job]), jobScope.ServiceProvider.GetRequiredService<IClock>(), ids,
            new JobDatabaseOptions(jobDb.Database.GetConnectionString()!), NullLogger<ScheduledJobRunner>.Instance);
        var scheduled = DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 60 * 60);
        check(await runner.RunAsync(job.Name, scheduled, Guid.NewGuid()) == ScheduledJobResult.Completed, "D03 real scheduled cleanup job and locks operate with runtime role");
        check(await runner.RunAsync(job.Name, scheduled, Guid.NewGuid()) == ScheduledJobResult.AlreadyCompleted, "D03 scheduled job replay is idempotent");
        await using var eicar = new MemoryStream(System.Text.Encoding.ASCII.GetBytes("X5O!P%@AP[4\\PZX54(P^)7CC)7}$EICAR-STANDARD-ANTIVIRUS-TEST-FILE!$H+H*"));
        check((await scanner.ScanAsync(eicar, eicar.Length, default)).Result == EvidenceScanResult.Infectado, "O03 isolated ClamAV rejects EICAR standard test");
    }
}
