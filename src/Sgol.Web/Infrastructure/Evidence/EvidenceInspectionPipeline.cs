using Sgol.Evidence.Contracts;

namespace Sgol.Web.Infrastructure.Evidence;

public sealed record EvidenceInspectionReceipt(
    EvidenceScanResult Result,
    EvidenceObjectMetadata? Metadata,
    string? ErrorCode,
    string? EngineVersion = null);

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

    public async Task<EvidenceInspectionReceipt> InspectStoredAsync(
        EvidenceObjectMetadata expected,
        CancellationToken cancellationToken)
    {
        var area = EvidenceStorageArea.Quarantine;
        Stream stored;
        try
        {
            stored = await storage.OpenReadAsync(area, expected.Key, cancellationToken);
        }
        catch (EvidenceObjectNotFoundException)
        {
            area = EvidenceStorageArea.Clean;
            stored = await storage.OpenReadAsync(area, expected.Key, cancellationToken);
        }

        await using (stored)
        {
            var verification = await validator.ValidateAsync(
                stored,
                expected.MediaType.ToMediaType(),
                GetSyntheticFileName(expected.MediaType),
                cancellationToken);
            if (!verification.IsValid || verification.File is null)
            {
                return new EvidenceInspectionReceipt(EvidenceScanResult.Invalido, expected, verification.ErrorCode);
            }

            await using var verified = verification.File;
            if (verified.SizeBytes != expected.SizeBytes || verified.Sha256 != expected.Sha256 ||
                verified.MediaType != expected.MediaType)
            {
                return new EvidenceInspectionReceipt(EvidenceScanResult.Invalido, expected, "INTEGRITY");
            }

            if (area == EvidenceStorageArea.Quarantine &&
                await HasIncompatibleCleanCopyAsync(expected, cancellationToken))
            {
                await storage.DeleteAsync(EvidenceStorageArea.Clean, expected.Key, cancellationToken);
                return new EvidenceInspectionReceipt(EvidenceScanResult.Invalido, expected, "CLEAN_COPY_MISMATCH");
            }

            var outcome = await scanner.ScanAsync(verified.Content, verified.SizeBytes, cancellationToken);
            if (outcome.Result == EvidenceScanResult.Limpio && area == EvidenceStorageArea.Quarantine)
            {
                await storage.PromoteToCleanAsync(expected, cancellationToken);
            }

            return new EvidenceInspectionReceipt(outcome.Result, expected, outcome.ErrorCode, outcome.EngineVersion);
        }
    }

    private async Task<bool> HasIncompatibleCleanCopyAsync(
        EvidenceObjectMetadata expected,
        CancellationToken cancellationToken)
    {
        Stream clean;
        try
        {
            clean = await storage.OpenReadAsync(EvidenceStorageArea.Clean, expected.Key, cancellationToken);
        }
        catch (EvidenceObjectNotFoundException)
        {
            return false;
        }

        await using (clean)
        {
            var validation = await validator.ValidateAsync(
                clean,
                expected.MediaType.ToMediaType(),
                GetSyntheticFileName(expected.MediaType),
                cancellationToken);
            if (!validation.IsValid || validation.File is null)
            {
                return true;
            }

            await using var file = validation.File;
            return file.SizeBytes != expected.SizeBytes || file.Sha256 != expected.Sha256 ||
                file.MediaType != expected.MediaType;
        }
    }

    private static string GetSyntheticFileName(EvidenceMediaType mediaType) => mediaType switch
    {
        EvidenceMediaType.Jpeg => "object.jpg",
        EvidenceMediaType.Png => "object.png",
        EvidenceMediaType.Pdf => "object.pdf",
        _ => throw new ArgumentOutOfRangeException(nameof(mediaType))
    };
}
