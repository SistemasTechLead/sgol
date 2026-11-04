using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Sgol.BuildingBlocks.Identifiers;
using Sgol.Evidence.Technical;
using Sgol.Identity.Contracts;
using Sgol.Planning.Contracts;
using Sgol.Web.Infrastructure.Persistence;
using Sgol.Web.Infrastructure.Persistence.Auditing;
using Sgol.Web.Infrastructure.Persistence.Configuration;
using Sgol.Web.Infrastructure.Persistence.Planning;

internal static partial class IncidenceChecks
{
    static async Task ProcessChecks(SgolDbContext db, Clock clock)
    {
        foreach (var activate in new[] { false, true })
        {
            clock.UtcNow = OperationalSchedule.Instant(new(2026, 11, 4), 10);
            var f = await QaFixtures.CreateAsync(db);
            var direction = new EfDirectionPreparedPlanService(db, new AuditTransaction(db), clock, new Uuid7Generator(clock), new DirectionPlanningCatalogReader(db));
            var validator = new FileTechnicalValidator(); var scanner = new CheckScanner();
            var catalog = new OperationalProcessCatalogService(db, direction, clock, new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> { ["Processes:OperationsEnabled"] = "true" }).Build());
            var tasks = new OperationalTaskService(db, direction, clock, validator, scanner);
            var planning = new ProcessPlanningService(db, direction, clock, validator, scanner);
            var engine = new OperationalProcessService(db, direction, clock, validator, scanner);
            var id = Guid.CreateVersion7();
            await catalog.CreateAsync(f.Director, id, "Proceso de ausencia aislado", "", default); db.ChangeTracker.Clear();
            for (var n = 0; n < 2; n++)
            {
                await tasks.SaveAsync(f.Director, new() { Revision = 0, ProcessId = id, Name = $"Incidencia etapa {n}", Requirement = "NINGUNO", AssignedRole = CanonicalRole.SalesFloor,
                    LoadMinutes = 10, OperationalCriticality = "FLEXIBLE", PeriodType = "OCASIONAL" }, default); db.ChangeTracker.Clear();
            }
            var editor = await catalog.ReadAsync(f.Director, id, default);
            await catalog.SaveAsync(f.Director, new ProcessDraftInput { Id = id, Revision = editor.Process.Revision, Name = editor.Process.Name, StartHour = 11,
                Steps = editor.Tasks.OrderBy(t => t.Name).Select((t,n) => new OperationalSequenceStep { TaskId = t.Id, Position = n + 1,
                    Action = n == 0 ? "ENTREGAR" : "AUTORIZAR", ReleaseCondition = n == 0 ? "INICIO" : "ENTREGA", DeadlineHours = 1 }).ToList() }, default);
            db.ChangeTracker.Clear(); editor = await catalog.ReadAsync(f.Director, id, default);
            await catalog.PublishAsync(f.Director, id, editor.Process.Revision, default); db.ChangeTracker.Clear(); editor = await catalog.ReadAsync(f.Director, id, default);
            await catalog.SetEnabledAsync(f.Director, id, editor.Process.Revision, true, default); db.ChangeTracker.Clear();
            var input = new ProcessPlanInput { ProcessId = id, Name = "Ausencia en reserva", Mode = "OCASION", OccasionTime = new(11,0), From = new(2026,11,4), Through = new(2026,11,4), People = new() { [1] = f.Person, [2] = f.OtherPerson } };
            input.Fingerprint = (await planning.PreviewAsync(f.Director, input, default)).Fingerprint;
            var plan = await planning.PrepareAsync(f.Director, input, default); db.ChangeTracker.Clear();
            Guid? run = null;
            if (activate)
            {
                run = (await planning.ActivateAsync(f.Director, plan, default)).Single(); db.ChangeTracker.Clear();
                await engine.TickAsync(default); db.ChangeTracker.Clear();
                var scheduledBefore = JsonSerializer.Serialize(await db.Set<OperationalSequenceStage>().AsNoTracking().Where(s=>s.RunId==run).OrderBy(s=>s.Id).ToArrayAsync());
                await engine.TickAsync(default); db.ChangeTracker.Clear();
                Check(scheduledBefore==JsonSerializer.Serialize(await db.Set<OperationalSequenceStage>().AsNoTracking().Where(s=>s.RunId==run).OrderBy(s=>s.Id).ToArrayAsync()),
                    "unchanged scheduled process timer does not manufacture a new revision");
                clock.UtcNow = OperationalSchedule.Instant(input.From, 11); await engine.TickAsync(default); db.ChangeTracker.Clear();
            }
            var svc = new OperationalIncidenceService(db, clock, CorrectionSchedulingChecks.Service(db, clock));
            var request = new IncidenceRequest(f.Person, input.From, Guid.NewGuid(), "Retiro explícito para reprogramación", clock.UtcNow.AddMinutes(2), []);
            var preview = await svc.PreviewAsync(f.Director, request, default);
            var affected = preview.Changes.FirstOrDefault(c => c.Before.PersonId == f.Person)?.Before.Id ?? preview.Cases.First().EntityId;
            request = request with { Decisions = [new(affected,"RETIRE")] };
            preview = await svc.PreviewAsync(f.Director, request, default);
            Check(preview.CanApply, "process retirement preview: " + string.Join(";",preview.Cases.Select(c=>c.Cause)));
            await svc.ApplyAsync(f.Director, request, preview.Fingerprint, default); db.ChangeTracker.Clear();
            if (activate)
            {
                Check(await db.Set<OperationalSequenceRun>().AnyAsync(r=>r.Id==run && r.Status=="EN_CURSO"), "absence does not cancel process");
                Check(await db.Set<OperationalSequenceStage>().Where(s=>s.RunId==run).AllAsync(s=>s.Status=="PENDIENTE_REPROGRAMACION"), "affected stage and dependent reservations held");
                var attempts = JsonSerializer.Serialize(await db.Set<OperationalSequenceAttempt>().AsNoTracking().Where(a=>a.RunId==run).OrderBy(a=>a.Id).ToArrayAsync());
                clock.UtcNow=clock.UtcNow.AddMinutes(30); await engine.TickAsync(default); db.ChangeTracker.Clear();
                Check(attempts==JsonSerializer.Serialize(await db.Set<OperationalSequenceAttempt>().AsNoTracking().Where(a=>a.RunId==run).OrderBy(a=>a.Id).ToArrayAsync()), "timer does not recreate retired work or satisfy dependency");
            }
            else
            {
                Check(await db.Set<PreparedProcessStart>().Where(s=>s.PlanId==plan).AllAsync(s=>s.IncidenceHoldId!=null), "prepared plan retains explicit incidence hold");
                await Reject(async()=>await planning.ActivateAsync(f.Director,plan,default), "held prepared plan cannot activate"); db.ChangeTracker.Clear();
                Check(!(await ProcessPlanningService.PreparedReservationsAsync(db,default)).Any(a=>a.PersonId==f.Person), "held prepared reservations released");
            }
        }
        clock.UtcNow = OperationalSchedule.Instant(new(2026,11,4),10);
    }
}
