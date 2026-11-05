using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Sgol.BuildingBlocks.Time;
using Sgol.BuildingBlocks.Identifiers;
using Sgol.Evidence.Contracts;
using Sgol.Evidence.Technical;
using Sgol.Planning.Contracts;
using Sgol.Web.Infrastructure.Persistence;
using Sgol.Web.Infrastructure.Persistence.Auditing;
using Sgol.Web.Infrastructure.Persistence.Configuration;
using Sgol.Web.Infrastructure.Persistence.Planning;

internal static class AutomaticProcessCalendarChecks
{
    internal static async Task Run(SgolDbContext db)
    {
        var f = await QaFixtures.CreateAsync(db);
        var clock = new Clock(); var validator = new FileTechnicalValidator(); var scanner = new Scanner();
        var direction = new EfDirectionPreparedPlanService(db, new AuditTransaction(db), clock, new Uuid7Generator(clock), new DirectionPlanningCatalogReader(db), validator, scanner);
        var catalog = new OperationalProcessCatalogService(db, direction, clock, new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> { ["Processes:OperationsEnabled"] = "true" }).Build());
        var tasks = new OperationalTaskService(db, direction, clock, validator, scanner);
        var planning = new ProcessPlanningService(db, direction, clock, validator, scanner);
        var engine = new OperationalProcessService(db, direction, clock, validator, scanner);

        async Task<Guid> Create(string kind, bool weekly = false, string monthDays = "27") {
            var id = Guid.CreateVersion7();
            await catalog.CreateAsync(f.Director, id, "Calendario automático " + kind + (weekly ? " operaciones" : ""), "", default); db.ChangeTracker.Clear();
            await tasks.SaveAsync(f.Director, new OperationalTask { Revision = 0, Name = "Etapa automática " + id, ProcessId = id, Requirement = "NINGUNO", LoadMinutes = 10,
                PeriodType = "OCASIONAL", OperationalCriticality = "CRITICA" }, default); db.ChangeTracker.Clear();
            await tasks.SaveAsync(f.Director, new OperationalTask { Revision = 0, Name = "Revisión automática " + id, ProcessId = id, Requirement = "NINGUNO", LoadMinutes = 10,
                PeriodType = "OCASIONAL", OperationalCriticality = "FLEXIBLE" }, default); db.ChangeTracker.Clear();
            var editor = await catalog.ReadAsync(f.Director, id, default);
            await catalog.SaveAsync(f.Director, new() { Id = id, Revision = editor.Process.Revision, Name = editor.Process.Name, StartKind = kind, StartHour = 10,
                Schedule = new() { Mode = kind == "SEMANAL" ? "DIAS_FIJOS" : "DISTRIBUIDA", WeekDays = 2, MonthDays = kind == "MENSUAL" ? monthDays : "", Time = new(10, 0), WeeklyOperations = weekly },
                Steps = editor.Tasks.OrderBy(t => t.Name).Select((t,i) => new OperationalSequenceStep { TaskId = t.Id, Position = i+1, ReleaseCondition = i==0 ? "INICIO" : "ENTREGA", Action = i==0 ? "ENTREGAR" : "AUTORIZAR", DeadlineHours = 1 }).ToList()
            }, default); db.ChangeTracker.Clear();
            editor = await catalog.ReadAsync(f.Director, id, default);
            await catalog.PublishAsync(f.Director, id, editor.Process.Revision, default); db.ChangeTracker.Clear();
            Check(!await db.Set<PreparedProcessStart>().AnyAsync(s => s.ProcessId == id), "Publishing alone creates no calendar");
            editor = await catalog.ReadAsync(f.Director, id, default);
            await catalog.SetEnabledAsync(f.Director, id, editor.Process.Revision, true, default); db.ChangeTracker.Clear();
            return id;
        }
        var monthly = await Create("MENSUAL");
        var starts = await db.Set<PreparedProcessStart>().Where(s => s.ProcessId == monthly).OrderBy(s => s.StartsAt).ToArrayAsync();
        Check(starts.Length == 12 && starts.All(s => WeekContract.LocalToday(s.StartsAt).Day == 27), "Twelve occurrences on the 27th cross the year boundary");
        var planId = starts[0].PlanId;
        var plan = await db.DirectionPreparedPlans.SingleAsync(p => p.Id == planId);
        Check(plan.StartsOn == new DateOnly(2026,11,5) && plan.EndsOn == new DateOnly(2027,11,4), "Twelve-month interval excludes its anniversary");
        foreach (var person in new[] { f.Person, f.OtherPerson }) db.Add(new PersonalScheduleException {
            Id = Guid.CreateVersion7(), PersonId = person, Day = new(2026,12,27), Revision = 1, ChangedBy = f.Director, ChangedAt = clock.UtcNow,
            IntentKey = Guid.CreateVersion7(), RequestHash = new string('0',64), Reason = "Calendario aislado: descanso", IntervalsJson = "[]" });
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        await planning.RetryAutomaticAsync(f.Director, planId, default);
        var view = await planning.ReadAsync(f.Director, planId, default);
        Check(view.Runs.Length == 11 && view.Starts.Count(s => s.Status == "PENDIENTE") == 1, "One unavailable date does not block eleven viable dates");
        var pending = view.Starts.Single(s => s.Status == "PENDIENTE");
        Check(pending.PendingReason!.Contains("Etapa") && pending.Participants == "{}" && pending.AgendaJson == null, "Pending date identifies its stage without reserving workload");
        Check(view.Configurations.Single(c => c.StartId == pending.Id).Stages[0].PersonName == "Pendiente de asignación", "Pending plan remains readable");
        var before = JsonSerializer.Serialize(await db.Set<OperationalSequenceStage>().AsNoTracking().OrderBy(s => s.Id).ToArrayAsync());
        var root = await catalog.ReadAsync(f.Director, monthly, default);
        await catalog.SetEnabledAsync(f.Director, monthly, root.Process.Revision, false, default); db.ChangeTracker.Clear();
        foreach (var person in new[] { f.Person, f.OtherPerson }) db.Add(new PersonalScheduleException {
            Id = Guid.CreateVersion7(), PersonId = person, Day = new(2026,12,27), Revision = 2, ChangedBy = f.Director, ChangedAt = clock.UtcNow,
            IntentKey = Guid.CreateVersion7(), RequestHash = new string('1',64), Reason = "Horario restablecido para prueba", IntervalsJson = "[{\"Entry\":540,\"Exit\":810},{\"Entry\":900,\"Exit\":1380}]" });
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        await planning.RetryAutomaticAsync(f.Director, planId, default);
        Check((await planning.ReadAsync(f.Director,planId,default)).Runs.Length == 11, "Disabled process does not assign pending dates");
        root = await catalog.ReadAsync(f.Director,monthly,default);
        await catalog.SetEnabledAsync(f.Director,monthly,root.Process.Revision,true,default); db.ChangeTracker.Clear();
        async Task ConcurrentRetry() {
            await using var concurrent = new SgolDbContext(new DbContextOptionsBuilder<SgolDbContext>().UseNpgsql(db.Database.GetConnectionString()).Options);
            var concurrentDirection = new EfDirectionPreparedPlanService(concurrent,new AuditTransaction(concurrent),clock,new Uuid7Generator(clock),new DirectionPlanningCatalogReader(concurrent),validator,scanner);
            await new ProcessPlanningService(concurrent,concurrentDirection,clock,validator,scanner).RetryAutomaticAsync(f.Director,planId,default);
        }
        await Task.WhenAll(ConcurrentRetry(), ConcurrentRetry()); db.ChangeTracker.Clear();
        await planning.RetryAutomaticAsync(f.Director, planId, default);
        view = await planning.ReadAsync(f.Director,planId,default);
        Check(view.Starts.Length == 12 && view.Runs.Length == 12 && view.Starts.All(s => s.Status == "ACTIVADO"), "Reactivation and retries reuse dates and recover capacity conflicts");
        var previousRuns = view.Runs.Where(r => r.StartKey != pending.StartKey).Select(r => r.Id).ToArray();
        Check(before == JsonSerializer.Serialize(await db.Set<OperationalSequenceStage>().AsNoTracking().Where(s => previousRuns.Contains(s.RunId)).OrderBy(s => s.Id).ToArrayAsync()), "Existing run snapshots remain unchanged");
        try { await planning.RetryAutomaticAsync(f.Worker,planId,default); throw new Exception("Expected forbidden"); }
        catch (DirectionPlanException e) when (e.Status == 403) { Check(true,"Worker cannot retry assignment"); }
        var occasion = await Create("OCASIONAL"); var weekly = await Create("SEMANAL", true);
        Check(!await db.Set<PreparedProcessStart>().AnyAsync(s => s.ProcessId == occasion || s.ProcessId == weekly), "Occasion and weekly operations are excluded");
        foreach (var kind in new[] { "SEMANAL", "TRIMESTRAL", "SEMESTRAL", "ANUAL" }) {
            var id = await Create(kind); var editor = await catalog.ReadAsync(f.Director,id,default);
            var expected = OperationalSequenceRules.Dates(plan.StartsOn,plan.EndsOn,editor.Configuration!).Count();
            Check(await db.Set<PreparedProcessStart>().CountAsync(s => s.ProcessId == id) == expected && expected > 0, kind + " uses its published recurrence");
            // Keep these calendars pending for the real HTTP/browser fixture.
        }
        var month31 = await Create("MENSUAL", monthDays:"31");
        Check(await db.Set<PreparedProcessStart>().CountAsync(s => s.ProcessId == month31) == 7, "Months without the 31st are skipped, preserving the existing rule");
        var lateStart = await db.Set<PreparedProcessStart>().AsNoTracking().Where(s => s.ProcessId == month31).OrderBy(s => s.StartsAt).FirstAsync();
        clock.UtcNow = new(2027,1,1,15,0,0,TimeSpan.Zero);
        await planning.RetryAutomaticAsync(f.Director,lateStart.PlanId,default);
        var late = await db.Set<PreparedProcessStart>().AsNoTracking().SingleAsync(s => s.Id == lateStart.Id);
        Check(late.Status == "PENDIENTE" && late.PendingReason!.Contains("ya pasó"), "Expired dates stay pending without automatic backlog assignment");
        await planning.RescheduleAutomaticAsync(f.Director,late.PlanId,late.Id,late.Revision,new(2027,1,28),default); db.ChangeTracker.Clear();
        var moved = await db.Set<PreparedProcessStart>().AsNoTracking().SingleAsync(s => s.Id == late.Id);
        Check(moved.OriginalStartsAt == late.StartsAt && WeekContract.LocalToday(moved.StartsAt) == new DateOnly(2027,1,28) && moved.StartKey == late.StartKey, "Manual rescheduling preserves the original date and calendar identity");
        await planning.RetryAutomaticAsync(f.Director,late.PlanId,default);
        Check(await db.Set<OperationalSequenceRun>().CountAsync(r => r.StartKey == late.StartKey) == 1, "Rescheduled date creates exactly one execution");
        clock.UtcNow = new(2026,11,5,15,0,0,TimeSpan.Zero);
        // Drive the first assigned monthly occurrence into execution using the real timer.
        foreach (var other in await db.Set<OperationalSequence>().Where(r => r.Id != monthly && r.Enabled).ToArrayAsync()) other.Enabled = false;
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        clock.UtcNow = starts[0].StartsAt;
        await engine.TickAsync(default); db.ChangeTracker.Clear();
        var run = await db.Set<OperationalSequenceRun>().SingleAsync(r => r.StartKey == starts[0].StartKey);
        var stage = await db.Set<OperationalSequenceStage>().SingleAsync(s => s.RunId == run.Id && s.Position == 1);
        Check(stage.Status == "DISPONIBLE", "The scheduled first stage becomes executable at its date");
        Console.WriteLine($"UI_CALENDAR_PROCESS={month31}");
        Console.WriteLine($"AUTOMATIC_CALENDAR_PASS process={monthly} plan={planId}");
    }
    private static void Check(bool ok,string name) { if(!ok) throw new Exception(name); Console.WriteLine("PASS " + name); }
    private sealed class Clock : IClock { public DateTimeOffset UtcNow { get; set; } = new(2026,11,5,15,0,0,TimeSpan.Zero); }
    private sealed class Scanner : IFileMalwareScanner {
        public Task CheckAvailabilityAsync(CancellationToken t) => Task.CompletedTask;
        public Task<EvidenceScanOutcome> ScanAsync(Stream s,long n,CancellationToken t) => Task.FromResult(new EvidenceScanOutcome(EvidenceScanResult.Limpio));
    }
}
