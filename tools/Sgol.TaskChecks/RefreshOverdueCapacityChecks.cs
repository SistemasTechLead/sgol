using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Sgol.BuildingBlocks.Time;
using Sgol.BuildingBlocks.Identifiers;
using Sgol.Web.Infrastructure.Persistence;
using Sgol.Web.Infrastructure.Persistence.Auditing;
using Sgol.Web.Infrastructure.Persistence.Configuration;
using Sgol.Web.Infrastructure.Persistence.Planning;
using Sgol.Planning.Contracts;

internal static class RefreshOverdueCapacityChecks
{
    sealed class Clock : IClock { public DateTimeOffset UtcNow => new(2026, 11, 4, 16, 0, 0, TimeSpan.Zero); }
    static void Check(bool value, string message) { if (!value) throw new Exception(message); Console.WriteLine("PASS refresh overdue: " + message); }
    public static async Task Run(SgolDbContext db)
    {
        var fixture = await QaFixtures.CreateAsync(db);
        var clock = new Clock(); var day = WeekContract.LocalToday(clock.UtcNow);
        var direction = new EfDirectionPreparedPlanService(db, new AuditTransaction(db), clock, new Uuid7Generator(clock), new DirectionPlanningCatalogReader(db));
        var service = new AssignmentRefreshService(db, direction, clock);
        var policy = await OperationalSchedulingPolicyService.CurrentAsync(db, default);
        var target = await db.OperationalTasks.SingleAsync(t => t.Id == fixture.Definition);
        target.ScheduleMode = "DIAS_FIJOS"; target.WeekDays = 127; target.StartHour = 12; target.OperationalCriticality = "CRITICA";
        OperationalTask Definition(string name, int load) { var t = new OperationalTask { Id = Guid.CreateVersion7(), TaskCode = "QA-" + Guid.NewGuid().ToString("N")[..10], Name = name, Requirement = "NINGUNO", LoadMinutes = load, OperationalCriticality = "FLEXIBLE" }; db.Add(t); return t; }
        OperationalAssignment Row(OperationalTask t, DateOnly date, int minute)
        {
            var row = new OperationalAssignment { Id = Guid.CreateVersion7(), PlanId = fixture.Plan, TaskId = t.Id, TaskCode = t.TaskCode, PersonId = fixture.Person, PersonName = "Refresh worker", AssignedRole = t.AssignedRole };
            AssignmentDefinition.Adopt(row, t, null, false); OperationalAgenda.Capture(row, policy, t.OperationalCriticality, TemporalEligibilityContext.At(date, minute)); db.Add(row); return row;
        }
        var old = Row(Definition("Historical backlog", 600), day.AddDays(-2), 540);
        old.ProgressUnits = 1; // must not be cleared to free capacity
        var boundary = Row(Definition("Expired at calculation boundary", 10), day, 585); // closes at 10:00
        var active = Row(Definition("Unexpired morning", 100), day, 540);
        var future = Row(Definition("Future afternoon", 100), day, 900);
        var changed = Row(target, day, 720);
        db.Add(new OperationalAttachment { AssignmentId = old.Id, FileName = "historical.pdf", ContentType = "application/pdf", Content = [1], CreatedAt = clock.UtcNow.AddDays(-1) });
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var protectedIds = new[] { old.Id, boundary.Id, active.Id, future.Id };
        var before = JsonSerializer.Serialize(await db.OperationalAssignments.AsNoTracking().Where(a => protectedIds.Contains(a.Id)).OrderBy(a => a.Id).ToArrayAsync());
        target = await db.OperationalTasks.SingleAsync(t => t.Id == target.Id); target.LoadMinutes = 40; target.Revision++; await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var normal = await OperationalCapacityService.LedgerAsync(db, clock.UtcNow, default);
        Check(normal.DayLoad(fixture.Person, day) > normal.DayCapacity(fixture.Person, day), "operational ledger still reports historical backlog");
        var request = new AssignmentRefreshRequest(target.Id, Guid.NewGuid(), clock.UtcNow.AddMinutes(3), PersonId: fixture.Person);
        var preview = await service.PreviewAsync(fixture.Director, request, default);
        Check(preview.CanApply, "unchanged overdue demand does not block valid refresh: " + string.Join(";", preview.Conflicts));
        Check(preview.Notices.Any(n => n.StartsWith("2 pendientes vencidos")), "past days and exact expiration boundary excluded");
        await service.ApplyAsync(fixture.Director, request, preview.Fingerprint, "Regresión vencidas", default); db.ChangeTracker.Clear();
        Check(before == JsonSerializer.Serialize(await db.OperationalAssignments.AsNoTracking().Where(a => protectedIds.Contains(a.Id)).OrderBy(a => a.Id).ToArrayAsync()), "historical progress and unchanged current/future agenda preserved");
        Check(await db.OperationalAttachments.AnyAsync(a => a.AssignmentId == old.Id), "historical evidence preserved");
        target = await db.OperationalTasks.SingleAsync(t => t.Id == target.Id); target.LoadMinutes = 60; target.Revision++; await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var beforeFailure = JsonSerializer.Serialize(await db.OperationalAssignments.AsNoTracking().OrderBy(a => a.Id).ToArrayAsync());
        var blocked = await service.PreviewAsync(fixture.Director, request with { Key = Guid.NewGuid() }, default);
        Check(!blocked.CanApply && blocked.Issues.Any(i => i.Code == "capacity" && i.TaskCode != null), "unexpired and future demand still enforce real capacity and identify the blocked assignment");
        Check(beforeFailure == JsonSerializer.Serialize(await db.OperationalAssignments.AsNoTracking().OrderBy(a => a.Id).ToArrayAsync()), "failed search has no persisted effects");
        Check(blocked.Differences.Where(d => d.Fields.SequenceEqual(new[] { "Reacomodo por capacidad" })).All(d => d.Before!.StartsAt == d.After!.StartsAt), "failed candidates do not leak neighbour movements");
        try { await service.ApplyAsync(fixture.Director, request, preview.Fingerprint, "Stale", default); throw new Exception("Stale accepted"); }
        catch (DirectionPlanException e) when (e.Code == "REFRESH_STALE") { Check(true, "concurrent catalog edit invalidates old proposal"); }
        // An expired target with activity must be rescheduled, so it must consume demand again.
        var executable = await db.OperationalAssignments.SingleAsync(a => a.Id == changed.Id);
        executable.Revision++;
        executable.StartsAt = OperationalSchedule.Instant(day.AddDays(-1), 12); executable.DueAt = executable.StartsAt.AddMinutes(45); executable.NominalStartsAt = executable.StartsAt;
        db.Add(new OperationalAttachment { AssignmentId = executable.Id, FileName = "active.pdf", ContentType = "application/pdf", Content = [2], CreatedAt = clock.UtcNow });
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        blocked = await service.PreviewAsync(fixture.Director, request with { Key = Guid.NewGuid() }, default);
        Check(!blocked.CanApply && blocked.Issues.Any(i => i.Code == "capacity"), "rescheduled expired assignment is not exempted from new capacity");
    }
}

