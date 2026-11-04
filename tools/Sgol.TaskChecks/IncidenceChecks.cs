using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Sgol.BuildingBlocks.Time;
using Sgol.Identity.Contracts;
using Sgol.Organization.Contracts;
using Sgol.Planning.Contracts;
using Sgol.Web.Infrastructure.Persistence;
using Sgol.Web.Infrastructure.Persistence.Planning;

internal static partial class IncidenceChecks
{
    internal sealed class Clock : IClock { public DateTimeOffset UtcNow { get; set; } = OperationalSchedule.Instant(new(2026, 11, 4), 10); }
    static void Check(bool pass, string name) { if (!pass) throw new Exception("FAIL incidence: " + name); Console.WriteLine("PASS incidence: " + name); }
    static async Task Reject(Func<Task> action, string name)
    {
        try { await action(); } catch (DirectionPlanException) { Check(true, name); return; }
        throw new Exception("Expected rejection: " + name);
    }
    internal static async Task<OperationalAssignment> Row(SgolDbContext db, QaFixtures.Actors f, Clock clock, DateTimeOffset starts, int load = 10, int goal = 0, DateTimeOffset? due = null)
    {
        var t = await db.OperationalTasks.AsNoTracking().SingleAsync(t => t.Id == f.Definition);
        var a = new OperationalAssignment { Id = Guid.CreateVersion7(), PlanId = f.Plan, TaskId = f.Definition, TaskCode = t.TaskCode,
            Name = "Incidencia · tarea de prueba", PersonId = f.Person, PersonName = "Persona ausente", AssignedRole = CanonicalRole.SalesFloor,
            Requirement = "NINGUNO", LoadMinutes = load, OperationalCriticality = "FLEXIBLE", DefinitionSnapshotJson = JsonSerializer.Serialize(t) };
        OperationalAgenda.Capture(a, await OperationalSchedulingPolicyService.CurrentAsync(db, default), "FLEXIBLE", starts);
        if (goal > 0) { a.GoalUnits = goal; a.ScheduleMode = "PROGRESO"; a.DueAt = due!.Value; }
        db.Add(a); await db.SaveChangesAsync(); db.ChangeTracker.Clear(); return a;
    }
    public static async Task Run(SgolDbContext source, bool ui)
    {
        var clock = new Clock();
        await using var db = new SgolDbContext(new DbContextOptionsBuilder<SgolDbContext>().UseNpgsql(source.Database.GetConnectionString()).Options, bonusClock: clock);
        var f = await QaFixtures.CreateAsync(db);
        var overdue = await Row(db, f, clock, clock.UtcNow.AddHours(-1));
        var pending = await Row(db, f, clock, clock.UtcNow.AddHours(1));
        var historical = await Row(db, f, clock, clock.UtcNow.AddDays(-1));
        var supervision = CorrectionSchedulingChecks.Service(db, clock);
        var service = new OperationalIncidenceService(db, clock, supervision);
        var request = new IncidenceRequest(f.Person, new(2026, 11, 4), Guid.NewGuid(), "Ausencia sintética para pruebas", clock.UtcNow.AddMinutes(2), []);
        var before = JsonSerializer.Serialize(await db.OperationalAssignments.AsNoTracking().OrderBy(a => a.Id).ToArrayAsync());
        var preview = await service.PreviewAsync(f.Director, request, default);
        Check(preview.CanApply, "preview complete: " + string.Join(" | ", preview.Cases.Select(c => c.Cause)));
        Check(preview.Changes.Single(c => c.Before.Id == overdue.Id).Action == "RETIRE", "expired today retires");
        Check(preview.Changes.Single(c => c.Before.Id == pending.Id).Action == "TRANSFER", "unexpired transfers");
        Check(preview.Changes.All(c => c.Before.Id != historical.Id), "previous-day obligation untouched");
        Check(before == JsonSerializer.Serialize(await db.OperationalAssignments.AsNoTracking().OrderBy(a => a.Id).ToArrayAsync()), "preview writes nothing");
        await Reject(async () => await service.ApplyAsync(f.Worker, request, preview.Fingerprint, default), "only Direction applies");
        var saved = await service.ApplyAsync(f.Director, request, preview.Fingerprint, default); db.ChangeTracker.Clear();
        Check(await db.AvailabilityDayVersions.AnyAsync(a => a.PersonId == f.Person && a.LocalDate == request.Day && !a.IsAvailable && a.Status == AvailabilityVersionStatus.Current), "availability persisted atomically");
        Check(!await db.OperationalAssignments.AnyAsync(a => a.Id == overdue.Id) && await db.OperationalAssignments.IgnoreQueryFilters().AnyAsync(a => a.Id == overdue.Id), "retirement leaves history");
        Check(await db.OperationalAssignments.AnyAsync(a => a.Id == pending.Id && a.PersonId != f.Person), "transfer changes owner");
        Check(await db.Set<OperationalTransfer>().AnyAsync(t => t.AssignmentId == pending.Id), "transfer receipt exists");
        Check(await db.Set<TaskBonusExclusion>().AnyAsync(e => e.ObligationId == overdue.Id), "economic exclusion exists");
        Check(await db.AuditEvents.AnyAsync(a => a.ResourceId == saved.Id && a.Action == "ABSENCE_APPLIED"), "joint audit exists");
        Check((await service.ApplyAsync(f.Director, request, preview.Fingerprint, default)).Id == saved.Id, "lost response replay returns same receipt");
        await Reject(async () => await service.ApplyAsync(f.Director, request with { Reason = "Otra intención" }, preview.Fingerprint, default), "same key different intent rejected");
        var bonus = new TaskBonusService(db, clock, supervision);
        var result = (await bonus.ReadAsync(f.Worker, TaskBonusCalculator.Monday(request.Day), true, default)).Single();
        Check(result.Details.Where(d => d.ObligationId == overdue.Id).All(d => d.Points == 0 && d.Deducted == 0), "retired obligation has no base or deduction");
        Check(result.Details.All(d => d.ObligationId != pending.Id), "transferred base not duplicated on absent person");
        var guarded = false;
        try { await Row(db, f, clock, clock.UtcNow.AddHours(3)); } catch (DbUpdateException) { guarded = true; db.ChangeTracker.Clear(); }
        Check(guarded, "new assignment on confirmed absence rejected at persistence boundary");
        var future = request with { Day = request.Day.AddDays(1), Key = Guid.NewGuid(), NotBefore = OperationalSchedule.Instant(request.Day.AddDays(1)) };
        preview = await service.PreviewAsync(f.Director, future, default);
        Check(preview.CanApply && preview.Changes.Length == 0, "future absence without obligations");
        await service.ApplyAsync(f.Director, future, preview.Fingerprint, default);
        await BoundaryChecks(db, clock);
        await RiskChecks(db, clock);
        await ProcessChecks(db, clock);
        EconomicChecks(clock);
        await ConcurrentChecks(db, clock);
        Check(!db.Database.HasPendingModelChanges(), "migration matches model");
        await DowngradeGuard(db);
        if (ui) await HttpChecks(db, clock);
    }
    static async Task BoundaryChecks(SgolDbContext db, Clock clock)
    {
        var f = await QaFixtures.CreateAsync(db); var a = await Row(db, f, clock, clock.UtcNow.AddHours(1));
        var service = new OperationalIncidenceService(db, clock, CorrectionSchedulingChecks.Service(db, clock));
        var request = new IncidenceRequest(f.Person, new(2026, 11, 4), Guid.NewGuid(), "Prueba del límite de vencimiento", clock.UtcNow.AddMinutes(2), []);
        var preview = await service.PreviewAsync(f.Director, request, default);
        clock.UtcNow = a.DueAt;
        await Reject(async () => await service.ApplyAsync(f.Director, request, preview.Fingerprint, default), "deadline crossed between preview and apply rejects entire proposal");
        Check(!await db.Set<OperationalIncidence>().AnyAsync(i => i.PersonId == f.Person) && !await db.AvailabilityDayVersions.AnyAsync(i => i.PersonId == f.Person), "stale proposal leaves no partial absence");
        preview = await service.PreviewAsync(f.Director, request with { NotBefore = clock.UtcNow.AddMinutes(2) }, default);
        Check(preview.Changes.Single(c => c.Before.Id == a.Id).Action == "RETIRE", "exact due instant is expired");
    }
}
