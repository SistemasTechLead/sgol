using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Sgol.BuildingBlocks.Identifiers;
using Sgol.BuildingBlocks.Time;
using Sgol.Evidence.Contracts;
using Sgol.JobInfrastructure;
using Sgol.Web.Infrastructure.Persistence.Auditing;

namespace Sgol.Web.Infrastructure.Evidence;

public sealed class EvidenceInspectionOutboxHandler(
    EvidenceInspectionPipeline pipeline,
    IClock clock,
    IUuidGenerator uuidGenerator) : IOutboxHandler
{
    public const string ContractEventType = "EVIDENCE.FILE_INSPECTION_REQUESTED.V1";
    public string EventType => ContractEventType;

    public bool IsPayloadValid(JsonElement data) =>
        data.ValueKind == JsonValueKind.Object && data.EnumerateObject().Count() == 1 &&
        data.TryGetProperty("fileObjectId", out var id) && id.ValueKind == JsonValueKind.String &&
        Guid.TryParseExact(id.GetString(), "D", out var parsed) && parsed != Guid.Empty;

    public async Task HandleAsync(OutboxDeliveryContext context, CancellationToken cancellationToken)
    {
        var fileId = Guid.ParseExact(context.Data.GetProperty("fileObjectId").GetString()!, "D");
        var file = await context.DbContext.FileObjects.SingleOrDefaultAsync(x => x.Id == fileId, cancellationToken)
            ?? throw new JobExecutionException("EVIDENCE_FILE_NOT_FOUND");
        if (file.ScanStatus != EvidenceFileStatuses.Pending)
        {
            return;
        }

        EvidenceInspectionReceipt receipt;
        try
        {
            receipt = await pipeline.InspectStoredAsync(new EvidenceObjectMetadata(
                EvidenceObjectKey.Parse(file.ObjectKey), file.SizeBytes, file.Sha256,
                file.DeclaredMediaType switch
                {
                    "image/jpeg" => EvidenceMediaType.Jpeg,
                    "image/png" => EvidenceMediaType.Png,
                    "application/pdf" => EvidenceMediaType.Pdf,
                    _ => throw new JobExecutionException("EVIDENCE_MEDIA_INVALID")
                }), cancellationToken);
        }
        catch (EvidenceObjectNotFoundException)
        {
            receipt = new EvidenceInspectionReceipt(EvidenceScanResult.Invalido, null, "OBJECT_MISSING");
        }
        catch (EvidenceObjectIntegrityException)
        {
            receipt = new EvidenceInspectionReceipt(EvidenceScanResult.Invalido, null, "INTEGRITY");
        }
        catch (EvidenceStorageUnavailableException) when (context.Attempt < 5)
        {
            throw new JobExecutionException("EVIDENCE_STORAGE_UNAVAILABLE");
        }
        catch (EvidenceStorageUnavailableException)
        {
            receipt = new EvidenceInspectionReceipt(EvidenceScanResult.ErrorEscaneo, null, "UNAVAILABLE");
        }

        if (receipt.Result == EvidenceScanResult.ErrorEscaneo && context.Attempt < 5)
        {
            throw new JobExecutionException(receipt.ErrorCode is "TIMEOUT" ? "EVIDENCE_SCAN_TIMEOUT" : "EVIDENCE_SCAN_UNAVAILABLE");
        }

        var now = clock.UtcNow;
        switch (receipt.Result)
        {
            case EvidenceScanResult.Limpio:
                file.MarkClean(file.DeclaredMediaType, SafeEngine(receipt.EngineVersion), now);
                break;
            case EvidenceScanResult.Infectado:
                file.MarkTerminal(EvidenceFileStatuses.Infected, file.DeclaredMediaType,
                    SafeEngine(receipt.EngineVersion), "MALWARE_DETECTED", now);
                break;
            case EvidenceScanResult.Invalido:
                file.MarkTerminal(EvidenceFileStatuses.Invalid, receipt.Metadata?.MediaType.ToMediaType(),
                    SafeEngine(receipt.EngineVersion), SafeCode(receipt.ErrorCode, "TECHNICAL_INVALID"), now);
                break;
            default:
                file.MarkTerminal(EvidenceFileStatuses.ScanError, null, SafeEngine(receipt.EngineVersion),
                    SafeCode(receipt.ErrorCode, "SCAN_ERROR"), now);
                break;
        }

        context.DbContext.AuditEvents.Add(new AuditEvent
        {
            Id = uuidGenerator.NewUuid(),
            OccurredAt = now,
            ActorType = "SYSTEM",
            Action = "EVIDENCE_FILE_INSPECTED",
            ResourceType = "FILE_OBJECT",
            ResourceId = file.Id,
            BranchId = file.BranchId,
            CorrelationId = context.CorrelationId,
            BeforeData = JsonSerializer.SerializeToDocument(new { status = EvidenceFileStatuses.Pending }),
            AfterData = JsonSerializer.SerializeToDocument(new { status = file.ScanStatus }),
            Outcome = "SUCCESS"
        });
        EvidenceTelemetry.Scans.Add(1, new("result", file.ScanStatus), new("attempt", context.Attempt));
        if (file.ScanStatus is EvidenceFileStatuses.Infected or EvidenceFileStatuses.Invalid or EvidenceFileStatuses.ScanError)
        {
            EvidenceTelemetry.ScanFailures.Add(1, new("result", file.ScanStatus), new("attempt", context.Attempt));
        }
    }

    private static string? SafeEngine(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim()[..Math.Min(value.Trim().Length, 64)];
    private static string SafeCode(string? value, string fallback) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= 64 && value.All(c => c is >= 'A' and <= 'Z' or >= '0' and <= '9' or '_') ? value : fallback;
}
