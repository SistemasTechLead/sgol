using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Sgol.BuildingBlocks.Identifiers;
using Sgol.Evidence.Contracts;
using Sgol.JobInfrastructure;
using Sgol.Web.Infrastructure.Persistence.Auditing;

namespace Sgol.Web.Infrastructure.Evidence;

public sealed class CleanExpiredEvidenceUploadsJob(
    IPrivateObjectStorage storage,
    IUuidGenerator uuidGenerator) : IScheduledJob
{
    public const string JobName = "CLEAN_EXPIRED_EVIDENCE_UPLOADS";
    public string Name => JobName;
    public bool PreventOverlappingSlots => true;

    public async Task ExecuteAsync(ScheduledJobContext context, CancellationToken cancellationToken)
    {
        var run = await context.DbContext.ScheduledJobRuns.SingleAsync(item => item.Id == context.RunId, cancellationToken);
        var normalized = await context.DbContext.FileObjects.Where(f => f.ReceivedObjectKey != null && f.ScannedAt < context.ScheduledFor.AddDays(-1)).Take(100).ToListAsync(cancellationToken);
        foreach (var file in normalized)
        {
            await storage.DeleteAsync(EvidenceStorageArea.Quarantine, EvidenceObjectKey.Parse(file.ReceivedObjectKey!), cancellationToken);
            file.ClearReceivedObject();
        }
        await context.DbContext.SaveChangesAsync(cancellationToken);
        while (true)
        {
            var rows = await context.DbContext.FileObjects
                // The runner keeps this transaction open through S3 deletion and SaveChanges.
                // Linkers take the same row lock; a stale selection cannot delete committed evidence.
                .FromSqlInterpolated($$"""
                    SELECT * FROM file_object
                    WHERE linked_evidence_item_id IS NULL AND
                        ((scan_status = {{EvidenceFileStatuses.Pending}} AND upload_completed_at IS NULL AND upload_expires_at <= {{context.ScheduledFor}}) OR
                         (scan_status = {{EvidenceFileStatuses.Clean}} AND link_expires_at <= {{context.ScheduledFor}}))
                    ORDER BY CASE WHEN scan_status = {{EvidenceFileStatuses.Clean}} THEN link_expires_at ELSE upload_expires_at END, id
                    LIMIT 100 FOR UPDATE SKIP LOCKED
                    """)
                .ToListAsync(cancellationToken);
            if (rows.Count == 0) break;

            foreach (var file in rows)
            {
                var area = file.ScanStatus == EvidenceFileStatuses.Clean ? EvidenceStorageArea.Clean : EvidenceStorageArea.Quarantine;
                await storage.DeleteAsync(area, EvidenceObjectKey.Parse(file.ObjectKey), cancellationToken);
                var code = file.ScanStatus == EvidenceFileStatuses.Clean ? "UNLINKED_EXPIRED" : "UPLOAD_EXPIRED";
                var before = file.ScanStatus;
                file.ExpireUnlinked(code, context.ScheduledFor);
                context.DbContext.AuditEvents.Add(new AuditEvent
                {
                    Id = uuidGenerator.NewUuid(),
                    OccurredAt = context.ScheduledFor,
                    ActorType = "SYSTEM",
                    Action = "EVIDENCE_ORPHAN_CLEANED",
                    ResourceType = "FILE_OBJECT",
                    ResourceId = file.Id,
                    BranchId = file.BranchId,
                    CorrelationId = context.CorrelationId,
                    BeforeData = JsonSerializer.SerializeToDocument(new { status = before }),
                    AfterData = JsonSerializer.SerializeToDocument(new { status = file.ScanStatus, failureCode = code }),
                    Outcome = "SUCCESS"
                });
            }

            var last = rows[^1];
            run.Checkpoint = JsonSerializer.Serialize(new { schemaVersion = 1, kind = JobName, fileObjectId = last.Id });
            await context.DbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
