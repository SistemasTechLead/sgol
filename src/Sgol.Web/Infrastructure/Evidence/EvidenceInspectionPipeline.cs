using Sgol.Evidence.Contracts;

namespace Sgol.Web.Infrastructure.Evidence;

public sealed record EvidenceInspectionReceipt(
    EvidenceScanResult Result,
    EvidenceObjectMetadata? Metadata,
    string? ErrorCode);

public sealed class EvidenceInspectionPipeline(
    IFileTechnicalValidator validator,
    IEvidenceObjectKeyFactory keyFactory,
    IPrivateObjectStorage storage,
    IFileMalwareScanner scanner)
{
    public async Task<EvidenceInspectionReceipt> InspectAsync(
        Stream content,
        string declaredMediaType,
        string originalFileName,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(
            content,
            declaredMediaType,
            originalFileName,
            cancellationToken);
        if (!validation.IsValid || validation.File is null)
        {
            return new EvidenceInspectionReceipt(EvidenceScanResult.Invalido, null, validation.ErrorCode);
        }

        await using var validated = validation.File;
        var metadata = new EvidenceObjectMetadata(
            keyFactory.Create(),
            validated.SizeBytes,
            validated.Sha256,
            validated.MediaType);
        await storage.PutQuarantineAsync(metadata, validated.Content, cancellationToken);

        await using var stored = await storage.OpenReadAsync(
            EvidenceStorageArea.Quarantine,
            metadata.Key,
            cancellationToken);
        var verification = await validator.ValidateAsync(
            stored,
            metadata.MediaType.ToMediaType(),
            GetSyntheticFileName(metadata.MediaType),
            cancellationToken);
        if (!verification.IsValid || verification.File is null ||
            verification.File.SizeBytes != metadata.SizeBytes ||
            verification.File.Sha256 != metadata.Sha256 ||
            verification.File.MediaType != metadata.MediaType)
        {
            if (verification.File is not null)
            {
                await verification.File.DisposeAsync();
            }

            return new EvidenceInspectionReceipt(EvidenceScanResult.Invalido, metadata, "INTEGRITY");
        }

        await using var verified = verification.File;
        var outcome = await scanner.ScanAsync(verified.Content, verified.SizeBytes, cancellationToken);
        if (outcome.Result == EvidenceScanResult.Limpio)
        {
            await storage.PromoteToCleanAsync(metadata, cancellationToken);
        }

        return new EvidenceInspectionReceipt(outcome.Result, metadata, outcome.ErrorCode);
    }

    private static string GetSyntheticFileName(EvidenceMediaType mediaType) => mediaType switch
    {
        EvidenceMediaType.Jpeg => "object.jpg",
        EvidenceMediaType.Png => "object.png",
        EvidenceMediaType.Pdf => "object.pdf",
        _ => throw new ArgumentOutOfRangeException(nameof(mediaType))
    };
}
