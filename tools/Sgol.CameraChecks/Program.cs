using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;
using SkiaSharp;
using Sgol.Evidence.Contracts;
using Sgol.Evidence.Technical;
using Sgol.Web.Infrastructure.Evidence;

static void Check(bool condition, string message) { if (!condition) throw new Exception(message); Console.WriteLine("PASS: " + message); }
byte[] Jpeg(int width = 64, int height = 32)
{
    using var bitmap = new SKBitmap(width, height); using var canvas = new SKCanvas(bitmap); canvas.Clear(SKColors.Blue);
    using var data = bitmap.Encode(SKEncodedImageFormat.Jpeg, 90); return data.ToArray();
}
byte[] WithComment(byte[] jpeg, string text)
{
    var comment = Encoding.ASCII.GetBytes(text);
    return [jpeg[0], jpeg[1], 255, 254, (byte)((comment.Length + 2) >> 8), (byte)(comment.Length + 2), ..comment, ..jpeg[2..]];
}
var validator = new FileTechnicalValidator(); var keys = new CryptographicEvidenceObjectKeyFactory();
var original = WithComment(Jpeg(3200, 1600), "PRIVATE_METADATA_MARKER_123456");
await using (var input = new MemoryStream(original))
{
    var valid = await validator.ValidateAsync(input, "image/jpeg", "capture.jpg", default);
    Check(valid.IsValid && valid.File is not null, "Camera JPEG passes technical validation");
    await using var file = valid.File!;
    var normalized = await PhotographNormalizer.NormalizeAsync(file.Content, default);
    using var image = SKBitmap.Decode(normalized);
    Check(image.Width == 2560 && image.Height == 1280, "Normalization limits dimensions and preserves aspect ratio");
    Check(!Encoding.ASCII.GetString(normalized).Contains("PRIVATE_METADATA_MARKER"), "Normalization strips embedded metadata");
}
await using (var malformed = new MemoryStream([255,216,255,217]))
{
    try { await PhotographNormalizer.NormalizeAsync(malformed, default); throw new Exception("Malformed JPEG accepted"); }
    catch (InvalidDataException) { Console.WriteLine("PASS: Malformed JPEG cannot be normalized"); }
}
await using (var falseMime = new MemoryStream(Encoding.UTF8.GetBytes("<script>alert(1)</script>")))
{
    var result = await validator.ValidateAsync(falseMime, "image/jpeg", "capture.jpg", default);
    Check(!result.IsValid, "HTML disguised as JPEG is rejected");
}
var authorization = new EvidenceCameraAuthorization(new EphemeralDataProtectionProvider());
var grant = new EvidenceCameraGrant(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow.AddMinutes(30));
var proof = authorization.Issue(grant);
Check(authorization.Read(proof, grant.Actor, grant.Obligation, grant.Requirement, DateTimeOffset.UtcNow) == grant, "Capture proof is bound to actor, task and requirement");
Check(authorization.Read(proof, Guid.NewGuid(), grant.Obligation, grant.Requirement, DateTimeOffset.UtcNow) is null &&
    authorization.Read(proof, grant.Actor, grant.Obligation, grant.Requirement, grant.ExpiresAt) is null &&
    authorization.Read(proof[..^5] + "wrong", grant.Actor, grant.Obligation, grant.Requirement, DateTimeOffset.UtcNow) is null, "Forged, foreign and expired capture proofs are rejected");

var storage = new MemoryStorage(); var scanner = new CheckScanner();
var metadata = new EvidenceObjectMetadata(keys.Create(), original.Length, Convert.ToHexStringLower(SHA256.HashData(original)), EvidenceMediaType.Jpeg);
await storage.PutQuarantineAsync(metadata, new MemoryStream(original), default);
var pipeline = new EvidenceInspectionPipeline(validator, keys, storage, scanner);
var receipt = await pipeline.InspectStoredAsync(metadata, default, true);
Check(receipt.Result == EvidenceScanResult.Limpio && receipt.Metadata != metadata && storage.Clean.ContainsKey(receipt.Metadata!.Key.Value), "Only the normalized verified object is promoted to private clean storage");
Check(storage.Quarantine.ContainsKey(metadata.Key.Value) && !storage.Clean.ContainsKey(metadata.Key.Value), "Original capture remains quarantined and never becomes downloadable");
var retry = await pipeline.InspectStoredAsync(metadata, default, true);
Check(retry.Metadata == receipt.Metadata && storage.Clean.Count == 1, "Inspection retry recovers the same immutable normalized object");
scanner.Result = EvidenceScanResult.Infectado;
var blocked = await pipeline.InspectStoredAsync(metadata, default, true);
Check(blocked.Result == EvidenceScanResult.Infectado, "Infected input cannot bypass malware analysis through normalization");
scanner.Result = EvidenceScanResult.ErrorEscaneo;
Check((await pipeline.InspectStoredAsync(metadata, default, true)).Result == EvidenceScanResult.ErrorEscaneo, "Scanner errors fail closed");

if (args.Contains("--clamav") || args.Contains("--performance-clamav"))
{
    var clam = new ClamAvScanner(Options.Create(new EvidenceScannerOptions { Host = args.Contains("--performance-clamav") ? "127.0.0.1" : "localhost", Port = args.Contains("--performance-clamav") ? 3347 : 3310 }));
    await clam.CheckAvailabilityAsync(default);
    await using var clean = new MemoryStream(Jpeg());
    Check((await clam.ScanAsync(clean, clean.Length, default)).Result == EvidenceScanResult.Limpio, "Real ClamAV accepts a clean photograph");
    // Standard harmless antivirus test string, assembled in memory; never executable and never written as a test file.
    var test = "X5O!P%@AP[4" + "\\PZX54(P^)7CC)7}$" + "EICAR-STANDARD-ANTIVIRUS-TEST-FILE!$H+H*";
    var flaggedBytes = WithComment(Jpeg(), test);
    await using var flagged = new MemoryStream(flaggedBytes);
    var valid = await validator.ValidateAsync(flagged, "image/jpeg", "capture.jpg", default);
    Check(valid.IsValid, "Antivirus test is embedded in a structurally valid JPEG");
    await using var validated = valid.File!;
    var rewritten = await PhotographNormalizer.NormalizeAsync(validated.Content, default);
    Check(!Encoding.ASCII.GetString(rewritten).Contains("EICAR-STANDARD"), "Photo rewriting removes nonpixel antivirus test content regardless of scanner signatures");
    await using var antivirusTest = new MemoryStream(Encoding.ASCII.GetBytes(test));
    Check((await clam.ScanAsync(antivirusTest, antivirusTest.Length, default)).Result == EvidenceScanResult.Infectado, "Real ClamAV detects the standard antivirus test");
}
Console.WriteLine("Camera processing and security checks passed.");

var requirementId = Guid.NewGuid(); var itemId = Guid.NewGuid(); var versionId = Guid.NewGuid();
var primary = new EvidenceReviewFileInput(Guid.NewGuid(), requirementId, itemId, EvidenceFileStatuses.Clean, EvidenceBucketClasses.Clean);
var secondary = new EvidenceReviewFileInput(Guid.NewGuid(), requirementId, itemId, EvidenceFileStatuses.Clean, EvidenceBucketClasses.Clean);
EvidenceReviewEvaluationInput ReviewInput(EvidenceReviewFileInput second) => new(Guid.NewGuid(), Guid.NewGuid(), "TAR-0018",
    [new("FOTOGRAFIA_FINAL", "FOTOGRAFIA", "SIEMPRE", 1)],
    [new(requirementId, "FOTOGRAFIA_FINAL", "FOTOGRAFIA", "SIEMPRE", 1, true, itemId,
        [new(versionId, EvidenceVersionStatuses.Current, primary.FileId, null, primary, [primary, second], 2)])]);
var reviewInput = ReviewInput(secondary);
var review = EvidenceReviewEvaluator.Evaluate(reviewInput);
Check(review.Result == EvidenceReviewResults.Complete && review.CanonicalInput.Contains(secondary.FileId.ToString()), "Review checks and fingerprints every member of the photograph collection");
try { EvidenceReviewEvaluator.Evaluate(ReviewInput(secondary with { ScanStatus = EvidenceFileStatuses.Infected })); throw new Exception("Infected secondary accepted"); }
catch (EvidenceReviewUnavailableException) { Console.WriteLine("PASS: A clean primary cannot conceal an infected secondary photograph"); }
try { EvidenceReviewEvaluator.Evaluate(ReviewInput(secondary with { LinkedEvidenceItemId = Guid.NewGuid() })); throw new Exception("Foreign secondary accepted"); }
catch (EvidenceReviewUnavailableException) { Console.WriteLine("PASS: Review rejects a photograph linked to another evidence item"); }
var missing = reviewInput.Requirements[0] with { Versions = [reviewInput.Requirements[0].Versions[0] with { Files = [primary] }] };
try { EvidenceReviewEvaluator.Evaluate(reviewInput with { Requirements = [missing] }); throw new Exception("Missing collection member accepted"); }
catch (EvidenceReviewUnavailableException) { Console.WriteLine("PASS: A missing collection member cannot silently turn multiple photos into a single-photo review"); }

sealed class CheckScanner : IFileMalwareScanner
{
    public EvidenceScanResult Result { get; set; } = EvidenceScanResult.Limpio;
    public Task CheckAvailabilityAsync(CancellationToken token) => Task.CompletedTask;
    public Task<EvidenceScanOutcome> ScanAsync(Stream stream, long length, CancellationToken token) => Task.FromResult(new EvidenceScanOutcome(Result));
}
sealed class MemoryStorage : IPrivateObjectStorage
{
    public Dictionary<string, (EvidenceObjectMetadata Metadata, byte[] Content)> Quarantine { get; } = [];
    public Dictionary<string, (EvidenceObjectMetadata Metadata, byte[] Content)> Clean { get; } = [];
    public async Task PutQuarantineAsync(EvidenceObjectMetadata metadata, Stream content, CancellationToken token)
    {
        if (Quarantine.ContainsKey(metadata.Key.Value)) throw new EvidenceObjectAlreadyExistsException();
        using var buffer = new MemoryStream(); await content.CopyToAsync(buffer, token); Quarantine[metadata.Key.Value] = (metadata, buffer.ToArray());
    }
    public Task<Stream> OpenReadAsync(EvidenceStorageArea area, EvidenceObjectKey key, CancellationToken token) =>
        Task.FromResult<Stream>(new MemoryStream((area == EvidenceStorageArea.Clean ? Clean : Quarantine).TryGetValue(key.Value, out var value) ? value.Content : throw new EvidenceObjectNotFoundException()));
    public Task<EvidenceObjectMetadata> GetMetadataAsync(EvidenceStorageArea area, EvidenceObjectKey key, CancellationToken token) =>
        Task.FromResult((area == EvidenceStorageArea.Clean ? Clean : Quarantine)[key.Value].Metadata);
    public Task PromoteToCleanAsync(EvidenceObjectMetadata metadata, CancellationToken token) { Clean[metadata.Key.Value] = Quarantine[metadata.Key.Value]; Quarantine.Remove(metadata.Key.Value); return Task.CompletedTask; }
    public Task DeleteAsync(EvidenceStorageArea area, EvidenceObjectKey key, CancellationToken token) { (area == EvidenceStorageArea.Clean ? Clean : Quarantine).Remove(key.Value); return Task.CompletedTask; }
    public Task CheckAvailabilityAsync(CancellationToken token) => Task.CompletedTask;
    public Task<EvidenceDownloadAuthorization> CreateCleanDownloadAuthorizationAsync(EvidenceObjectMetadata metadata, Guid fileId, DateTimeOffset expires, CancellationToken token) => throw new NotSupportedException();
    public Task<EvidenceUploadAuthorization> CreateQuarantineUploadAuthorizationAsync(EvidenceObjectMetadata metadata, DateTimeOffset expires, CancellationToken token) => throw new NotSupportedException();
}
