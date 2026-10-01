using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Sgol.BuildingBlocks.Identifiers;
using Sgol.BuildingBlocks.Time;
using Sgol.JobInfrastructure;
using Sgol.Web.Infrastructure.Evidence;
using Sgol.Web.Infrastructure.Persistence;

namespace Sgol.FrontendBrowserTests;

internal sealed partial class BrowserFixture
{
    public async Task<(int Items, int Versions, int Linked, int Intents, int Completed, int Contributions, int Scanned)> EvidenceCountsAsync(Guid obligation)
    {
        await using var db = new SgolDbContext(new DbContextOptionsBuilder<SgolDbContext>().UseNpgsql(connectionString).Options);
        var items = await db.EvidenceItems.Where(i => i.ObligationId == obligation).Select(i => i.Id).ToListAsync();
        return (items.Count, await db.EvidenceVersions.CountAsync(v => items.Contains(v.EvidenceItemId)),
            await db.FileObjects.CountAsync(f => f.ObligationId == obligation && f.LinkedEvidenceItemId != null),
            await db.AuditEvents.CountAsync(a => a.Action == "EVIDENCE_UPLOAD_INTENT_CREATED"),
            await db.AuditEvents.CountAsync(a => a.Action == "EVIDENCE_UPLOAD_COMPLETED"),
            await db.AuditEvents.CountAsync(a => a.Action == "EVIDENCE_CONTRIBUTED"),
            await db.AuditEvents.CountAsync(a => a.Action == "EVIDENCE_FILE_INSPECTED"));
    }

    public async Task InspectEvidenceAsync(Guid obligation, EvidenceInspectionPipeline pipeline)
    {
        await using var db = new SgolDbContext(new DbContextOptionsBuilder<SgolDbContext>().UseNpgsql(connectionString).Options);
        await using var transaction = await db.Database.BeginTransactionAsync();
        var file = await db.FileObjects.SingleAsync(f => f.ObligationId == obligation && f.ScanStatus == Sgol.Evidence.Contracts.EvidenceFileStatuses.Pending);
        var outbox = await db.OutboxEvents.SingleAsync(o => o.AggregateId == file.Id && o.EventType == EvidenceInspectionOutboxHandler.ContractEventType);
        using var payload = JsonDocument.Parse(outbox.Payload);
        // The same production handler and transaction are invoked deterministically, without a timer race.
        var clock = new SystemClock();
        var handler = new EvidenceInspectionOutboxHandler(pipeline, clock, new Uuid7Generator(clock));
        await handler.HandleAsync(new(outbox.Id, outbox.EventType, outbox.AggregateId, Guid.NewGuid(),
            payload.RootElement.GetProperty("data"), db), CancellationToken.None);
        outbox.ProcessedAt = DateTimeOffset.UtcNow;
        outbox.AttemptCount = 1;
        await db.SaveChangesAsync(); await transaction.CommitAsync();
    }

    public async Task RejectContributionAuditAsync(bool reject)
    {
        await using var db = new SgolDbContext(new DbContextOptionsBuilder<SgolDbContext>().UseNpgsql(connectionString).Options);
        if (reject) await db.Database.ExecuteSqlRawAsync("ALTER TABLE audit_event ADD CONSTRAINT front017_reject_audit CHECK (action <> 'EVIDENCE_CONTRIBUTED') NOT VALID");
        else await db.Database.ExecuteSqlRawAsync("ALTER TABLE audit_event DROP CONSTRAINT front017_reject_audit");
    }
}
