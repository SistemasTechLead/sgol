using System.Net;
using System.Security.Cryptography;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;
using Sgol.Evidence.Contracts;
using Sgol.Evidence.Technical;
using Sgol.Testing;
using Sgol.Web.Infrastructure.Evidence;
using Xunit;

namespace Sgol.EvidenceIntegrationTests;

public sealed partial class EvidenceExternalInfrastructureTests
{
    [Fact]
    public async Task TechEvid003SignedOriginalDownloadPreservesHeadersBytesAndDeniesTamperingAndExpiry()
    {
        var storage = new S3PrivateObjectStorage(applicationClient!, Options.Create(storageOptions!));
        using var http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false });
        var absentKey = new CryptographicEvidenceObjectKeyFactory().Create();
        await Assert.ThrowsAsync<EvidenceObjectNotFoundException>(() =>
            storage.GetMetadataAsync(EvidenceStorageArea.Clean, absentKey, CancellationToken.None));
        // Inject corrupt metadata in a separate synthetic object before the read interval.
        var corruptBytes = EvidenceCorpus.Png();
        var corruptKey = new CryptographicEvidenceObjectKeyFactory().Create();
        var corrupt = new PutObjectRequest
        {
            BucketName = CleanBucket,
            Key = corruptKey.Value,
            ContentType = "image/png",
            InputStream = new MemoryStream(corruptBytes)
        };
        corrupt.Metadata["x-amz-meta-sgol-sha256"] = Convert.ToHexStringLower(SHA256.HashData(corruptBytes));
        corrupt.Metadata["x-amz-meta-sgol-media-type"] = "Png";
        corrupt.Metadata["x-amz-meta-sgol-size-bytes"] = (corruptBytes.Length + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
        await applicationClient!.PutObjectAsync(corrupt);
        await Assert.ThrowsAsync<EvidenceObjectIntegrityException>(() =>
            storage.GetMetadataAsync(EvidenceStorageArea.Clean, corruptKey, CancellationToken.None));
        var files = new[]
        {
            (EvidenceMediaType.Png, EvidenceCorpus.Png(), "png"),
            (EvidenceMediaType.Jpeg, EvidenceCorpus.Jpeg(), "jpg"),
            (EvidenceMediaType.Pdf, EvidenceCorpus.Pdf(), "pdf")
        };
        foreach (var (type, bytes, extension) in files)
        {
            var metadata = new EvidenceObjectMetadata(new CryptographicEvidenceObjectKeyFactory().Create(),
                bytes.Length, Convert.ToHexStringLower(SHA256.HashData(bytes)), type);
            // Provision a validated clean fixture before the read interval; never mutate from download.
            await storage.PutQuarantineAsync(metadata, new MemoryStream(bytes), CancellationToken.None);
            await storage.PromoteToCleanAsync(metadata, CancellationToken.None);
            var id = Guid.CreateVersion7();
            var expiry = DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds()).AddSeconds(300);
            var authorization = await storage.CreateCleanDownloadAuthorizationAsync(metadata, id, expiry, CancellationToken.None);
            using (var response = await http.GetAsync(authorization.Url))
            {
                Assert.True(response.StatusCode == HttpStatusCode.OK, "Signed GET must return 200; URI omitted.");
                Assert.Equal(type.ToMediaType(), response.Content.Headers.ContentType?.MediaType);
                Assert.Equal("attachment", response.Content.Headers.ContentDisposition?.DispositionType);
                Assert.Equal($"evidence-{id:D}.{extension}", response.Content.Headers.ContentDisposition?.FileName?.Trim('"'));
                Assert.True(response.Headers.CacheControl?.Private == true && response.Headers.CacheControl.NoStore);
                Assert.True((await response.Content.ReadAsByteArrayAsync()).SequenceEqual(bytes), "Original bytes must match; content omitted.");
            }
            var tampered = new Uri(authorization.Url.AbsoluteUri.Replace("response-cache-control=", "response-cache-control=altered", StringComparison.Ordinal));
            using (var response = await http.GetAsync(tampered)) Assert.False(response.IsSuccessStatusCode, "Signed header tampering must be denied.");
            var wrongObject = new Uri(authorization.Url.AbsoluteUri.Replace(metadata.Key.Value,
                new CryptographicEvidenceObjectKeyFactory().Create().Value, StringComparison.Ordinal));
            using (var response = await http.GetAsync(wrongObject)) Assert.False(response.IsSuccessStatusCode, "Different object must be denied.");
            var signatureIndex = authorization.Url.AbsoluteUri.IndexOf("X-Amz-Signature=", StringComparison.Ordinal);
            Assert.True(signatureIndex >= 0);
            var signatureOffset = signatureIndex + "X-Amz-Signature=".Length;
            var raw = authorization.Url.AbsoluteUri;
            var alteredSignature = new Uri(raw[..signatureOffset] + (raw[signatureOffset] == '0' ? "1" : "0") + raw[(signatureOffset + 1)..]);
            using (var response = await http.GetAsync(alteredSignature)) Assert.False(response.IsSuccessStatusCode, "Invalid signature must be denied.");
            using (var request = new HttpRequestMessage(HttpMethod.Put, authorization.Url) { Content = new ByteArrayContent(bytes) })
            using (var response = await http.SendAsync(request)) Assert.False(response.IsSuccessStatusCode, "GET token must not authorize PUT.");
            using (var response = await http.GetAsync(new Uri(authorization.Url.GetLeftPart(UriPartial.Path))))
                Assert.False(response.IsSuccessStatusCode, "Anonymous object read must be denied.");
            using (var response = await http.GetAsync(new Uri(storageOptions!.Endpoint + "/" + CleanBucket)))
                Assert.False(response.IsSuccessStatusCode, "Anonymous listing must be denied.");
            Assert.True(metadata == await storage.GetMetadataAsync(EvidenceStorageArea.Clean, metadata.Key, CancellationToken.None),
                "Object metadata must remain unchanged; key/hash omitted.");

            if (type == EvidenceMediaType.Pdf)
            {
                var shortExpiry = DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds()).AddSeconds(4);
                var shortAuthorization = await storage.CreateCleanDownloadAuthorizationAsync(metadata, id, shortExpiry, CancellationToken.None);
                using (var response = await http.GetAsync(shortAuthorization.Url)) Assert.True(response.IsSuccessStatusCode);
                // Wait for a real clock boundary, not a delay to coordinate a database race or a retry.
                var boundary = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                using var timer = TimeProvider.System.CreateTimer(_ => boundary.TrySetResult(), null,
                    shortExpiry.AddSeconds(1) - DateTimeOffset.UtcNow, Timeout.InfiniteTimeSpan);
                await boundary.Task;
                using var expired = await http.GetAsync(shortAuthorization.Url);
                Assert.False(expired.IsSuccessStatusCode, "Provider must deny an expired authorization.");
            }
        }
    }
}
