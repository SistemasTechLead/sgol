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

    public async Task ExecuteAsync(ScheduledJobContext context, CancellationToken cancellationToken)
    {
        var run = await context.DbContext.ScheduledJobRuns.SingleAsync(item => item.Id == context.RunId, cancellationToken);
        while (true)
        {
            var rows = await context.DbContext.FileObjects
                .Where(file => file.LinkedEvidenceItemId == null &&
                    ((file.ScanStatus == EvidenceFileStatuses.Pending && file.UploadCompletedAt == null && file.UploadExpiresAt <= context.ScheduledFor) ||
                     (file.ScanStatus == EvidenceFileStatuses.Clean && file.LinkExpiresAt <= context.ScheduledFor)))
                .OrderBy(file => file.ScanStatus == EvidenceFileStatuses.Clean ? file.LinkExpiresAt : file.UploadExpiresAt)
                .ThenBy(file => file.Id)
                .Take(100)
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
