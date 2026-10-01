using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Sgol.Assignment.Contracts;
using Sgol.BuildingBlocks.Versioning;
using Sgol.Configuration.Contracts;
using Sgol.Generation.Contracts;
using Sgol.IntegrationTests;
using Sgol.Notifications.Contracts;
using Sgol.Organization.Contracts;
using Sgol.Planning.Contracts;
using Sgol.Web.Infrastructure.Persistence;

namespace Sgol.FrontendBrowserTests;

internal sealed record BrowserWorkTask(Guid Id, Guid NoticeId, Guid UserId, Guid PeriodId, string State);

internal sealed partial class BrowserFixture
{
    public async Task<IReadOnlyList<BrowserWorkTask>> SeedMyWorkAsync(bool enrolledUsers = false, string taskCode = "TAR-0008")
    {
        await using var db = new SgolDbContext(new DbContextOptionsBuilder<SgolDbContext>().UseNpgsql(connectionString).Options);
        var now = DateTimeOffset.UtcNow;
        var effective = now.AddDays(-2);
        VersionRecord Published(Guid id) => new(id, VersionStatuses.Current, effective, null, "Configuración sintética FRONT-016", null, 2);
        var release = new ConfigurationRelease(Guid.CreateVersion7(), BranchScope.LorettaId);
        release.ApplyPublished(Published(release.Id), 1, Accounts[0].UserId, effective);
        db.ConfigurationReleases.Add(release);
        var definition = TaskDefinitionCatalog.Require(taskCode);
        using var payload = JsonDocument.Parse("{}");
        var version = new TaskDefinitionVersion(Guid.CreateVersion7(), definition.Id, 1, 1, payload, release.Id);
        version.ApplyPublished(Published(version.Id), true); db.TaskDefinitionVersions.Add(version);
        using var schedule = JsonDocument.Parse("null");
        var rule = new ActivationRuleVersion(Guid.CreateVersion7(), definition.Id, version.Id, release.Id, null, 1,
            ActivationModes.Manual, schedule.RootElement, ActivationOriginSchemas.ManualReference);
        rule.ApplyPublished(Published(rule.Id)); db.ActivationRuleVersions.Add(rule);
        var policy = new EvidencePolicyVersion(Guid.CreateVersion7(), definition.Id, version.Id, release.Id, null, 1);
        policy.ApplyPublished(Published(policy.Id)); db.EvidencePolicyVersions.Add(policy);
        db.EvidenceRequirementVersions.AddRange(EvidencePolicyCatalog.Require(taskCode)
            .Select(r => new EvidenceRequirementVersion(Guid.CreateVersion7(), policy.Id, definition.Id, r)));
        var local = TimeZoneInfo.ConvertTime(now, TimeZoneInfo.FindSystemTimeZoneById("America/Mexico_City"));
        WeekPeriod Period(DateTime date)
        {
            var year = ISOWeek.GetYear(date); var week = ISOWeek.GetWeekOfYear(date); var range = WeekContract.Calculate(year, week);
            return new(Guid.CreateVersion7(), BranchScope.LorettaId, year, week, range.StartsOn, range.EndsOn, WeekContract.Current);
        }
        var current = Period(local.DateTime); var future = Period(local.DateTime.AddDays(7));
        db.WeekPeriods.AddRange(current, future); await db.SaveChangesAsync();
        // The visual fixture needs active, MFA-enrolled synthetic evidence authors.
        if (enrolledUsers)
        {
            foreach (var account in Accounts)
            {
                var user = await db.AppUsers.SingleAsync(item => item.Id == account.UserId);
                user.MfaEnrolledAt ??= now.AddDays(-1);
            }
            await db.SaveChangesAsync();
        }
        var tasks = new List<BrowserWorkTask>();
        foreach (var account in Accounts)
        {
            foreach (var state in new[] { "DISPONIBLE", "VENCIDA", "CONCLUIDA", "FUTURA" })
            {
                var period = state == "FUTURA" ? future : current;
                var reference = "FRONT-016-" + account.Role + "-" + state;
                var request = new GenerationRequest(Guid.CreateVersion7(), Guid.CreateVersion7(),
                    Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(reference))).ToLowerInvariant(),
                    rule.Id, BranchScope.LorettaId, period.Id, ActivationOriginSchemas.ManualReference,
                    reference, Accounts[0].UserId, now.AddSeconds(-20));
                db.GenerationRequests.Add(request); await db.SaveChangesAsync();
                var obligation = new WorkObligation(Guid.CreateVersion7(), version.Id, BranchScope.LorettaId,
                    period.Id, request.Id, reference, policy.Id);
                db.WorkObligations.Add(obligation); await db.SaveChangesAsync(); request.LinkObligation(obligation.Id);
                var assignment = new AssignmentVersion(Guid.CreateVersion7(), obligation.Id, account.PersonId,
                    AssignmentVersionStatuses.Current, AssignmentTypes.Automatic, JsonDocument.Parse("{}"), now.AddSeconds(-10));
                var notice = new InternalNotice(Guid.CreateVersion7(), account.UserId, assignment.Id, assignment.AssignedAt);
                db.AssignmentVersions.Add(assignment); db.InternalNotices.Add(notice); await db.SaveChangesAsync();
                if (state == "VENCIDA") await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE work_obligation SET due_at = {now.AddMinutes(-5)} WHERE id = {obligation.Id}");
                if (state == "CONCLUIDA") await ObligationConclusionTestData.ConcludeAsync(db, obligation.Id, now.AddSeconds(-5), receiptDifference: taskCode == "TAR-0092");
                tasks.Add(new(obligation.Id, notice.Id, account.UserId, period.Id, state));
            }
        }
        return tasks;
    }

    public async Task<string> MyWorkRowsAsync(bool includeNoticeAndAudit = true)
    {
        await using var db = new SgolDbContext(new DbContextOptionsBuilder<SgolDbContext>().UseNpgsql(connectionString).Options);
        string[] names = ["work_obligation", "assignment_version", "week_period", "file_object", "evidence_item", "evidence_version",
            "evidence_review_snapshot", "execution_result", "work_plan", "plan_version", "plan_version_obligation",
            "idempotency_record", "outbox_event", "scheduled_job_run", "internal_notice", "audit_event"];
        var tables = db.Model.GetEntityTypes().Select(t => t.GetTableName()).OfType<string>()
            .Where(t => names.Contains(t, StringComparer.Ordinal) && (includeNoticeAndAudit || t is not ("internal_notice" or "audit_event")))
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal);
        var rows = new List<string>();
        foreach (var table in tables)
        {
            var sql = "SELECT COALESCE(jsonb_agg(to_jsonb(t) ORDER BY to_jsonb(t)::text)::text, '[]') AS \"Value\" FROM \"" + table + "\" t";
            rows.Add(table + ":" + await db.Database.SqlQueryRaw<string>(sql).SingleAsync());
        }
        return string.Join('\n', rows);
    }
}
