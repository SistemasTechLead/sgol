using Microsoft.EntityFrameworkCore;
using Sgol.Web.Infrastructure.Persistence;

namespace Sgol.IntegrationTests;

internal static class Front016NoEffectSnapshot
{
    public static async Task<string> ReadAsync(SgolDbContext context, bool includeNoticesAndAudit = true)
    {
        // Fixed allowlist; compare complete rows, not only counts or executionStatus.
        string[] names = ["work_obligation", "assignment_version", "week_period", "evidence_item", "evidence_version",
            "evidence_review_snapshot", "execution_result", "work_plan", "plan_version", "plan_version_obligation",
            "idempotency_record", "outbox_event", "scheduled_job_run", "internal_notice", "audit_event"];
        var tables = context.Model.GetEntityTypes().Select(t => t.GetTableName()).OfType<string>()
            .Where(t => names.Contains(t, StringComparer.Ordinal) && (includeNoticesAndAudit || t is not ("internal_notice" or "audit_event")))
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal);
        var values = new List<string>();
        foreach (var table in tables)
        {
            var sql = "SELECT COALESCE(jsonb_agg(to_jsonb(t) ORDER BY to_jsonb(t)::text)::text, '[]') AS \"Value\" FROM \"" + table + "\" t";
            values.Add(table + ":" + await context.Database.SqlQueryRaw<string>(sql).SingleAsync());
        }
        return string.Join('\n', values);
    }
}
