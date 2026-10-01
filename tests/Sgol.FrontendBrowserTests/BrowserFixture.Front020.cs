using Microsoft.EntityFrameworkCore;
using Sgol.Organization.Contracts;
using Sgol.Web.Infrastructure.Persistence;
using Sgol.Web.Infrastructure.Persistence.Continuity;
using Sgol.Web.Infrastructure.Persistence.Auditing;

namespace Sgol.FrontendBrowserTests;

internal sealed partial class BrowserFixture
{
    public async Task Front020AuditAsync()
    {
        await using var db = new SgolDbContext(new DbContextOptionsBuilder<SgolDbContext>().UseNpgsql(connectionString).Options);
        db.AuditEvents.Add(new AuditEvent { Id = Guid.CreateVersion7(), OccurredAt = DateTimeOffset.UtcNow.AddSeconds(-1), ActorType = "SYSTEM", Action = "SYNTHETIC_UNKNOWN_ACTION", ResourceType = "SYNTHETIC_RESOURCE", BranchId = BranchScope.LorettaId, CorrelationId = Guid.CreateVersion7(), Outcome = "SUCCESS", AfterData = System.Text.Json.JsonDocument.Parse("{\"status\":\"SYNTHETIC\",\"password\":\"SYNTHETIC-MUST-NOT-APPEAR\",\"nested\":{\"value\":\"SYNTHETIC-MUST-NOT-APPEAR\"}}") });
        await db.SaveChangesAsync();
    }
    public async Task<Guid> Front020ReportAsync(string status, bool truncated = false)
    {
        await using var db = new SgolDbContext(new DbContextOptionsBuilder<SgolDbContext>().UseNpgsql(connectionString).Options);
        var id = Guid.CreateVersion7(); var now = DateTimeOffset.UtcNow.AddSeconds(-1);
        var eventType = status switch { "REQUESTED" => "RECOVERY_RECONCILIATION_REQUESTED", "REFERENCE_CAPTURING" => "RECOVERY_REFERENCE_CAPTURE_STARTED", "REFERENCE_READY" => "RECOVERY_REFERENCE_READY", "RESTORE_STARTED" => "RECOVERY_RESTORE_STARTED", "RECONCILING" => "RECOVERY_RECONCILIATION_STARTED", "FAILED" => "RECOVERY_RECONCILIATION_FAILED", _ => "RECOVERY_RECONCILIATION_COMPLETED" };
        db.RecoveryReconciliations.Add(new() { Id = id, BranchId = BranchScope.LorettaId, RequestedBy = Accounts[0].UserId, Reason = "Simulacro sintético", RequestedAt = now });
        db.RecoveryReconciliationEvents.Add(new() { Id = Guid.CreateVersion7(), ReconciliationId = id, Sequence = 1, EventType = "RECOVERY_RECONCILIATION_REQUESTED", Status = "REQUESTED", ActorUserId = Accounts[0].UserId, OccurredAt = now, CorrelationId = Guid.CreateVersion7() });
        db.RecoveryReconciliationEvents.Add(new() { Id = Guid.CreateVersion7(), ReconciliationId = id, Sequence = 2, EventType = eventType, Status = status, TechnicalActor = "SGOL_WORKER", OccurredAt = now, CorrelationId = Guid.CreateVersion7(), ReferenceRootSha256 = new('a', 64), ActualRootSha256 = new(status == "DIFFERENT" ? 'b' : 'a', 64), DifferenceCount = status == "DIFFERENT" ? 1 : 0, DifferencesTruncated = truncated, ObservedRpoSeconds = 10, ObservedRtoSeconds = 20 });
        if (status == "DIFFERENT") db.RecoveryReconciliationDifferences.Add(new() { Id = Guid.CreateVersion7(), ReconciliationId = id, Ordinal = 1, Group = "evidence", ResourceType = "EVIDENCE_VERSION", StableKey = "EVIDENCIA-SINTETICA", Kind = "IDENTITY_MISSING", ExpectedSha256 = new('a', 64) });
        await db.SaveChangesAsync(); return id;
    }
    public async Task<string> Front020RowsAsync()
    {
        await using var db = new SgolDbContext(new DbContextOptionsBuilder<SgolDbContext>().UseNpgsql(connectionString).Options);
        var rows = new List<string> { await Front019RowsAsync() };
        foreach (var table in new[] { "recovery_reconciliation", "recovery_reconciliation_event", "recovery_reconciliation_difference" })
        {
            // Closed table names owned by this synthetic fixture; no request input enters SQL.
            var sql = "SELECT COALESCE(jsonb_agg(to_jsonb(t) ORDER BY to_jsonb(t)::text), '[]')::text AS \"Value\" FROM \"" + table + "\" t";
            rows.Add(await db.Database.SqlQueryRaw<string>(sql).SingleAsync());
        }
        return string.Join('\n', rows);
    }
    public async Task<int> Front020ViewsAsync()
    {
        await using var db = new SgolDbContext(new DbContextOptionsBuilder<SgolDbContext>().UseNpgsql(connectionString).Options);
        return await db.AuditEvents.CountAsync(e => e.Action == "RECOVERY_RECONCILIATION_VIEWED");
    }
}
