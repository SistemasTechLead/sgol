using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Sgol.Planning.Contracts;
using Sgol.Web.Infrastructure.Persistence;
using Sgol.Web.Infrastructure.Persistence.Planning;

internal static class MonthlyPostponementChecks
{
    private static void Check(bool value, string label) { if (!value) throw new Exception(label); Console.WriteLine("PASS monthly: " + label); }
    public static async Task Run(SgolDbContext db, QaFixtures.Actors fixture, CorrectionSchedulingChecks.Clock clock)
    {
        var service = new OperationalCarryoverService(db, clock, CorrectionSchedulingChecks.Service(db, clock));
        var rows = new List<OperationalAssignment>();
        foreach (var period in new[] { "MENSUAL", "MENSUAL", "MENSUAL", "SEMANAL" }) {
            var row = await CorrectionSchedulingChecks.Source(db, fixture, clock);
            row.Name = "Monthly range QA " + rows.Count;
            row.PeriodType = period; row.CompletedAt = null; row.CompletedBy = null; row.CompletionKind = null; row.ReviewStatus = null;
            db.Update(row); await db.SaveChangesAsync(); db.ChangeTracker.Clear(); rows.Add(row);
        }
        var ids = rows.Select(r => r.Id).ToArray();
        const string explanation = "Monthly window integration";
        var p = await service.PreviewPostponementAsync(fixture.Worker, ids, "ERROR_DE_ASIGNACION", explanation, default);
        Check(p.Conflicts.Length == 0, "mixed producer: " + string.Join("; ", p.Conflicts));
        Check(p.Through == new DateOnly(2026,12,3), "producer searches a month");
        Check(p.Items.Where(i => i.AssignmentId == rows[3].Id).All(i => WeekContract.LocalToday(i.Proposed) <= new DateOnly(2026,11,10)), "weekly proposal stays within seven days");
        Check(p.Days.Single(d => d.Day == new DateOnly(2026,11,30)).ReferenceKnown, "month-end has verified forecast reference");
        var id = await service.SubmitPostponementAsync(fixture.Worker, ids, "ERROR_DE_ASIGNACION", explanation, p.Fingerprint, Guid.NewGuid(), default);
        CarryoverDecision Command(int n, string action, DateOnly day) => new(id, rows[n].Id, rows[n].Revision, action,
            action == "REASSIGN" ? fixture.OtherPerson : null, day, "Monthly range decision", Guid.NewGuid());
        var outside = Command(0, "RESCHEDULE", new(2026,12,4));
        var invalid = await service.PreviewDecisionAsync(fixture.Director, outside, default);
        Check(invalid.Conflicts.Any(x => x.Contains("03/12/2026")), "day beyond month rejected by server");
        var before = JsonSerializer.Serialize(await db.OperationalAssignments.AsNoTracking().SingleAsync(a => a.Id == rows[0].Id));
        try { await service.ResolveItemAsync(fixture.Director, outside, invalid.Fingerprint, default); throw new Exception("invalid decision saved"); }
        catch (DirectionPlanException e) { Check(e.Code == "ARRASTRE_CONFLICTO", "invalid confirmation rejected"); }
        Check(before == JsonSerializer.Serialize(await db.OperationalAssignments.AsNoTracking().SingleAsync(a => a.Id == rows[0].Id)), "failed confirmation leaves assignment intact");
        var weekly = await service.PreviewDecisionAsync(fixture.Director, Command(3, "RESCHEDULE", new(2026,11,30)), default);
        Check(weekly.Conflicts.Any(x => x.Contains("10/11/2026")), "mixed request does not extend weekly decision range");
        foreach (var n in new[] { 0, 1 }) {
            var command = Command(n, n == 0 ? "RESCHEDULE" : "REASSIGN", new(2026,11,30));
            var preview = await service.PreviewDecisionAsync(fixture.Director, command, default);
            Check(preview.Conflicts.Length == 0, command.Action + " month-end preview: " + string.Join("; ", preview.Conflicts));
            await service.ResolveItemAsync(fixture.Director, command, preview.Fingerprint, default);
            await service.ResolveItemAsync(fixture.Director, command, preview.Fingerprint, default); db.ChangeTracker.Clear();
            var saved = await db.OperationalAssignments.AsNoTracking().SingleAsync(a => a.Id == rows[n].Id);
            Check(WeekContract.LocalToday(saved.StartsAt) == new DateOnly(2026,11,30) && saved.Comment == rows[n].Comment && saved.ProgressUnits == rows[n].ProgressUnits, "month-end persisted with progress and comment intact");
        }
        var request = await db.Set<OperationalCarryoverRequest>().AsNoTracking().SingleAsync(r => r.Id == id);
        Check(request.Results().Length == 2 && request.PendingItems().Length == 2, "retries add no duplicate history; mixed request stays pending");
        var historicalRow = await CorrectionSchedulingChecks.Source(db, fixture, clock);
        historicalRow.PeriodType = "MENSUAL"; historicalRow.CompletedAt = null; historicalRow.CompletedBy = null;
        historicalRow.CompletionKind = null; historicalRow.ReviewStatus = null;
        db.Update(historicalRow); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var historicalPreview = await service.PreviewPostponementAsync(fixture.Worker, [historicalRow.Id], "ERROR_DE_ASIGNACION", explanation, default);
        Check(historicalPreview.Conflicts.Length == 0, "historical fixture has valid proposal");
        // Reproduce an immutable version-2 request persisted before monthly windows existed.
        var legacy = new OperationalCarryoverRequest { Id = Guid.CreateVersion7(), Kind = "POSPONER", AlgorithmVersion = 2,
            BranchId = Sgol.Organization.Contracts.BranchScope.LorettaId, PersonId = fixture.Person, RequestedBy = fixture.Worker,
            IntentKey = Guid.NewGuid(), Cause = "ERROR_DE_ASIGNACION", CauseDescription = "Fixture historical request", Explanation = explanation,
            From = new(2026,11,4), Through = new(2026,11,10), Minutes = historicalRow.RemainingMinutes,
            IncidentStart = clock.UtcNow, IncidentEnd = clock.UtcNow, SubmittedAt = clock.UtcNow,
            SnapshotJson = JsonSerializer.Serialize(historicalPreview.Items), Fingerprint = historicalPreview.Fingerprint, ScheduleFingerprint = historicalPreview.ContextFingerprint };
        db.Add(legacy); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        legacy = await db.Set<OperationalCarryoverRequest>().AsNoTracking().SingleAsync(r => r.Id == legacy.Id);
        var oldDecision = new CarryoverDecision(legacy.Id, historicalRow.Id, historicalRow.Revision, "RESCHEDULE", null, new(2026,12,3), "Resolver petición antigua", Guid.NewGuid());
        var oldPreview = await service.PreviewDecisionAsync(fixture.Director, oldDecision, default);
        Check(oldPreview.Conflicts.Length == 0, "historical monthly request allows exact month boundary: " + string.Join("; ", oldPreview.Conflicts));
        await service.ResolveItemAsync(fixture.Director, oldDecision, oldPreview.Fingerprint, default); db.ChangeTracker.Clear();
        var savedLegacy = await db.Set<OperationalCarryoverRequest>().AsNoTracking().SingleAsync(r => r.Id == legacy.Id);
        Check(savedLegacy.Through == legacy.Through && savedLegacy.SnapshotJson == legacy.SnapshotJson && savedLegacy.Results().Length == 1, "historical original window and snapshot survive new resolution");
        // Leave a monthly and weekly task pending for real form/browser checks.
    }
}
