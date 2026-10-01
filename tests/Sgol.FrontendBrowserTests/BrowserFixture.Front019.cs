using Microsoft.EntityFrameworkCore;
using Sgol.IntegrationTests;
using Sgol.Web.Infrastructure.Persistence;

namespace Sgol.FrontendBrowserTests;

internal sealed partial class BrowserFixture
{
    public async Task<string> Front019RowsAsync()
    {
        await using var db = new SgolDbContext(new DbContextOptionsBuilder<SgolDbContext>().UseNpgsql(connectionString).Options);
        var rows = new List<string>();
        foreach (var table in new[] { "work_obligation", "assignment_version", "execution_result", "evidence_version", "evidence_review_snapshot", "validation_requirement", "validation_decision_version", "audit_event", "idempotency_record", "internal_notice", "outbox_event" })
        {
            var sql = "SELECT COALESCE(jsonb_agg(to_jsonb(t) ORDER BY to_jsonb(t)::text)::text, '[]') AS \"Value\" FROM \"" + table + "\" t";
            rows.Add(await db.Database.SqlQueryRaw<string>(sql).SingleAsync());
        }
        return string.Join('\n', rows);
    }
    public async Task Front019RejectAuditAsync(bool reject)
    {
        await using var db = new SgolDbContext(new DbContextOptionsBuilder<SgolDbContext>().UseNpgsql(connectionString).Options);
        await db.Database.ExecuteSqlRawAsync(reject ? "ALTER TABLE audit_event ADD CONSTRAINT front019_reject_audit CHECK (action <> 'VALIDATION_DECISION_ISSUED') NOT VALID" : "ALTER TABLE audit_event DROP CONSTRAINT front019_reject_audit");
    }
    public async Task Front019ConcludeAsync(Guid id)
    {
        await using var db = new SgolDbContext(new DbContextOptionsBuilder<SgolDbContext>().UseNpgsql(connectionString).Options);
        await ObligationConclusionTestData.ConcludeAsync(db, id, DateTimeOffset.UtcNow.AddSeconds(-1));
    }
}
