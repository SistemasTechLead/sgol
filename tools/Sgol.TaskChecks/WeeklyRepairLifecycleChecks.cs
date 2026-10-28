using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Sgol.BuildingBlocks.Time;
using Sgol.BuildingBlocks.Identifiers;
using Sgol.Planning.Contracts;
using Sgol.Web.Infrastructure.Persistence;
using Sgol.Web.Infrastructure.Persistence.Auditing;
using Sgol.Web.Infrastructure.Persistence.Configuration;
using Sgol.Web.Infrastructure.Persistence.Planning;

internal static class WeeklyRepairLifecycleChecks
{
    private sealed class TestClock : IClock { public DateTimeOffset UtcNow { get; set; } = new(2026,10,22,1,0,0,TimeSpan.Zero); }
    private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
    public static async Task Run(SgolDbContext db)
    {
        var clock = new TestClock();
        var direction = new EfDirectionPreparedPlanService(db, new AuditTransaction(db), clock, new Uuid7Generator(clock), new DirectionPlanningCatalogReader(db));
        var service = new OperationalWeeklyRepairService(db, direction, clock);
        var actor = await db.RoleAssignmentVersions.Where(r => r.RoleCode == "DIRECCION" && r.ValidTo == null).Select(r => r.UserId).FirstAsync();
        var from = new DateOnly(2026,10,22); var through = new DateOnly(2026,10,25);
        var start = OperationalSchedule.Instant(from); var end = OperationalSchedule.Instant(through.AddDays(1));
        var planId = await db.OperationalAssignments.Where(a => a.StartsAt >= start && a.StartsAt < end).Select(a => a.PlanId).Distinct().SingleAsync();
        var blocked = await service.PreviewAsync(actor, planId, from, through, default);
        Require(!blocked.CanApply && blocked.Conflicts.Any(c => c.Contains("TAR-0179")), "Actual earlier commitment must block, with identity");
        // Dedicated-copy fixture only: simulate the legitimate resolution that production still needs.
        var count = await db.OperationalAssignments.Where(a => a.TaskCode == "TAR-0179" && a.StartsAt < start && a.DueAt > start && a.CompletedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.CompletedAt, clock.UtcNow).SetProperty(a => a.CompletionKind, "SUPERVISION").SetProperty(a => a.CompletedBy, actor));
        Require(count == 1, "Expected one earlier commitment in fixture");
        var before = await db.OperationalAssignments.AsNoTracking().OrderBy(a => a.Id).ToArrayAsync();
        var preview = await service.PreviewAsync(actor, planId, from, through, default);
        Require(preview.CanApply, "Resolved fixture must have a complete agenda: " + string.Join("; ", preview.Conflicts));
        Require(preview.Differences.Count(d => d.Before != null && d.After != null) == 120 && preview.Differences.Count(d => d.Before == null) == 1 && preview.Differences.Count(d => d.After == null) == 1, "Exact identities: 120 retained, one addition, one retirement");
        var repeat = await service.PreviewAsync(actor, planId, from, through, default);
        Require(repeat.Fingerprint == preview.Fingerprint, "Stable preview, including deterministic addition ID");
        var definition = await db.OperationalTasks.AsNoTracking().SingleAsync(t => t.TaskCode == "TAR-0204");
        await db.OperationalTasks.Where(t => t.Id == definition.Id).ExecuteUpdateAsync(s => s.SetProperty(t => t.Revision, t => t.Revision + 1));
        try { await service.ApplyAsync(actor, planId, from, through, preview.Fingerprint, default); throw new Exception("Stale catalogue accepted"); }
        catch (DirectionPlanException e) { Require(e.Code == "CAPACITY_STALE", "Stale preview rejected"); }
        await db.OperationalTasks.Where(t => t.Id == definition.Id).ExecuteUpdateAsync(s => s.SetProperty(t => t.Revision, definition.Revision));
        var previousNow = clock.UtcNow;
        clock.UtcNow = start.AddHours(10);
        try { await service.ApplyAsync(actor, planId, from, through, preview.Fingerprint, default); throw new Exception("Activated preview accepted"); }
        catch (DirectionPlanException e) { Require(e.Code == "CAPACITY_STALE", "Activation invalidates scope"); }
        clock.UtcNow = previousNow;
        await service.ApplyAsync(actor, planId, from, through, preview.Fingerprint, default);
        db.ChangeTracker.Clear();
        var after = await db.OperationalAssignments.AsNoTracking().OrderBy(a => a.Id).ToArrayAsync();
        var affected = preview.Differences.Select(d => (d.Before ?? d.After)!.Id).ToHashSet();
        Require(JsonSerializer.Serialize(before.Where(a => !affected.Contains(a.Id))) == JsonSerializer.Serialize(after.Where(a => !affected.Contains(a.Id))), "All fields outside confirmed set preserved");
        foreach (var diff in preview.Differences.Where(d => d.Before != null && d.After != null))
        {
            var row = after.Single(a => a.Id == diff.Before!.Id);
            Require(row.PersonId == diff.Before!.PersonId && row.CompletedAt == diff.Before.CompletedAt && row.ProgressUnits == diff.Before.ProgressUnits && row.ReviewStatus == diff.Before.ReviewStatus, "Person and lifecycle preserved");
            Require(row.StartsAt == diff.After!.StartsAt && row.DueAt == diff.After.DueAt, "Preview equals applied rows");
        }
        var audits = await db.AuditEvents.CountAsync();
        await service.ApplyAsync(actor, planId, from, through, preview.Fingerprint, default);
        Require(audits == await db.AuditEvents.CountAsync() && after.Length == await db.OperationalAssignments.CountAsync(), "Idempotent confirmation");
        WeeklyReflowChecks.Validate(await OperationalCapacityService.LedgerAsync(db, clock.UtcNow, default), after.Where(a => a.StartsAt >= start && a.StartsAt < end && a.PlanId == planId).ToArray());
        Console.WriteLine("PASS weekly repair: actual blocker, exact scope, stable fingerprint, stale catalogue, activation race, preserved IDs/lifecycle/outside rows, preview=apply and replay.");
    }
}
