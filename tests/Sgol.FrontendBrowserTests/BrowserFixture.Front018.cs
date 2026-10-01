using Microsoft.EntityFrameworkCore;
using Sgol.Web.Infrastructure.Persistence;

namespace Sgol.FrontendBrowserTests;

internal sealed partial class BrowserFixture
{
    public async Task<(int Snapshots, int Reviews, int Replacements, int Conclusions)> Front018CountsAsync(Guid id)
    {
        await using var db = new SgolDbContext(new DbContextOptionsBuilder<SgolDbContext>().UseNpgsql(connectionString).Options);
        return (await db.EvidenceReviewSnapshots.CountAsync(s => s.ObligationId == id),
            await db.AuditEvents.CountAsync(a => a.Action == "EVIDENCE_REVIEW_SNAPSHOT_CREATED"),
            await db.AuditEvents.CountAsync(a => a.Action == "EVIDENCE_REPLACED"),
            await db.AuditEvents.CountAsync(a => a.Action == "OBLIGATION_CONCLUDED"));
    }
    public async Task Front018RejectAuditAsync(string action, bool reject)
    {
        if (action is not ("EVIDENCE_REPLACED" or "OBLIGATION_CONCLUDED")) throw new ArgumentException("Unsupported synthetic audit action.", nameof(action));
        await using var db = new SgolDbContext(new DbContextOptionsBuilder<SgolDbContext>().UseNpgsql(connectionString).Options);
        if (reject) await db.Database.ExecuteSqlRawAsync(action == "EVIDENCE_REPLACED"
            ? "ALTER TABLE audit_event ADD CONSTRAINT front018_reject_audit CHECK (action <> 'EVIDENCE_REPLACED') NOT VALID"
            : "ALTER TABLE audit_event ADD CONSTRAINT front018_reject_audit CHECK (action <> 'OBLIGATION_CONCLUDED') NOT VALID");
        else await db.Database.ExecuteSqlRawAsync("ALTER TABLE audit_event DROP CONSTRAINT front018_reject_audit");
    }
    public async Task<string> Front018BusinessRowsAsync()
    {
        await using var db = new SgolDbContext(new DbContextOptionsBuilder<SgolDbContext>().UseNpgsql(connectionString).Options);
        string[] names = ["work_obligation", "assignment_version", "evidence_item", "evidence_version", "execution_result", "idempotency_record", "outbox_event", "internal_notice"];
        var rows = new List<string>();
        foreach (var table in names)
        {
            var sql = "SELECT COALESCE(jsonb_agg(to_jsonb(t) ORDER BY to_jsonb(t)::text)::text, '[]') AS \"Value\" FROM \"" + table + "\" t";
            rows.Add(table + ":" + await db.Database.SqlQueryRaw<string>(sql).SingleAsync());
        }
        return string.Join('\n', rows);
    }
}
