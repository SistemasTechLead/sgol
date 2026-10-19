using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sgol.BuildingBlocks.Time;
using Sgol.Identity.Contracts;
using Sgol.Organization.Contracts;
using Sgol.Planning.Contracts;
using Sgol.Web.Infrastructure.Persistence;
using Sgol.Web.Infrastructure.Persistence.Auditing;
using Sgol.Web.Infrastructure.Persistence.Planning;

// LocalSetup restricts this fixture to Development / localhost / sgol_local.
internal static class BoardAllPlansSeeder
{
    public static async Task SeedAsync(SgolDbContext db, IServiceProvider services)
    {
        var now = services.GetRequiredService<IClock>().UtcNow;
        var day = WeekContract.LocalToday(now);
        var users = await (from u in db.AppUsers.AsNoTracking()
            join p in db.People.AsNoTracking() on u.PersonId equals p.Id
            join c in db.IdentityCredentials.AsNoTracking() on u.Id equals c.UserId
            join r in db.RoleAssignmentVersions.AsNoTracking() on u.Id equals r.UserId
            where (c.UserName == "ana.demo" || c.UserName == "direccion.local") && u.Status == AccountStatus.Active &&
                r.BranchId == BranchScope.LorettaId && r.Status == RoleAssignmentStatus.Active &&
                r.ValidFrom <= now && (r.ValidTo == null || r.ValidTo > now) &&
                db.EmploymentVersions.Any(e => e.PersonId == p.Id && e.BranchId == BranchScope.LorettaId &&
                    e.Status == EmploymentStatus.Active && e.ValidFrom <= now && (e.ValidTo == null || e.ValidTo > now))
            select new { u.Id, u.PersonId, p.DisplayName, r.RoleCode, c.UserName }).ToArrayAsync();
        var director = users.Single(u => u.UserName == "direccion.local" && u.RoleCode == CanonicalRole.Direction);
        var owner = users.Single(u => u.UserName == "ana.demo" && u.RoleCode == CanonicalRole.SalesFloor);
        Guid Key(string label) => new(SHA256.HashData(Encoding.UTF8.GetBytes($"board-all:{day:yyyyMMdd}:{label}"))[..16]);
        var ids = new List<Guid>();
        await using (var tx = await db.Database.BeginTransactionAsync())
        {
            await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(734651001)");
            foreach (var kind in new[] { "OPERACIONES", "SEMANAL", "MENSUAL", "TRIMESTRAL", "SEMESTRAL", "ANUAL", "OCASIONAL", "SIN_HOY" })
            {
                var id = Key(kind); ids.Add(id);
                if (await db.DirectionPreparedPlans.AnyAsync(p => p.Id == id)) continue;
                var type = kind == "OPERACIONES" ? "SEMANAL" : kind == "SIN_HOY" ? "MENSUAL" : kind;
                var name = $"Prueba · {kind.ToLowerInvariant()} · {day:dd/MM/yyyy}";
                var offsets = kind == "SIN_HOY" ? new[] { 1, 2 } : new[] { -1, 0, 1, 2 };
                var definitions = offsets.Select((offset, i) => new OperationalTask {
                    Id = Key($"task:{kind}:{i}"), TaskCode = "BA-" + Key($"task:{kind}:{i}").ToString("N")[..12],
                    Name = $"Prueba · {kind.ToLowerInvariant()} · cierre {day.AddDays(offset):dd/MM}",
                    AssignedRole = CanonicalRole.SalesFloor, Requirement = "NINGUNO", LoadMinutes = 1,
                    PeriodType = type == "SEMESTRAL" ? "MENSUAL" : type, DeadlineHours = offset == 1 ? 36 : 12,
                    IsWeeklyOperationsTask = kind == "OPERACIONES"
                }).ToArray();
                foreach (var t in definitions) { t.Validate(); db.Add(t); }
                var date = day.ToDateTime(TimeOnly.MinValue);
                int? number = type switch { "SEMANAL" => ISOWeek.GetWeekOfYear(date), "MENSUAL" => day.Month,
                    "TRIMESTRAL" => (day.Month - 1) / 3 + 1, "SEMESTRAL" => (day.Month - 1) / 6 + 1, _ => null };
                var plan = type == "OCASIONAL" ? DirectionPreparedPlan.ForOccasion(id, BranchScope.LorettaId, director.Id, now,
                    name, day.AddDays(-1), day.AddDays(2)) : new DirectionPreparedPlan(id, BranchScope.LorettaId, director.Id, now,
                    new(name, "Pruebas del tablero", type, type == "SEMANAL" ? ISOWeek.GetYear(date) : day.Year, number,
                        definitions.Select(t => t.TaskCode).ToArray(), IsWeeklyOperationsPlan: kind == "OPERACIONES"));
                plan.MarkAssigned(); db.Add(plan);
                for (var i = 0; i < definitions.Length; i++)
                {
                    var t = definitions[i]; var offset = offsets[i];
                    db.Add(new DirectionPreparedPlanItem(id, t.TaskCode, t.Name, i));
                    db.Add(new OperationalAssignment { Id = Key($"assignment:{kind}:{i}"), PlanId = id, TaskId = t.Id,
                        TaskRevision = t.Revision, TaskCode = t.TaskCode, Name = t.Name, Requirement = t.Requirement,
                        AssignedRole = t.AssignedRole, LoadMinutes = 1, PeriodType = t.PeriodType, PersonId = owner.PersonId,
                        PersonName = owner.DisplayName, StartsAt = OperationalSchedule.Instant(offset == 1 ? day : day.AddDays(offset), 8),
                        DueAt = OperationalSchedule.Instant(day.AddDays(offset), 20), ScheduleKey = $"BOARD-ALL:{day:yyyyMMdd}:{kind}:{i}" });
                }
                db.Add(new AuditEvent { Id = Guid.CreateVersion7(), OccurredAt = now, ActorUserId = director.Id, ActorType = "USER",
                    Action = "BOARD_ALL_PLANS_FIXTURE_CREATED", ResourceType = "DIRECTION_PREPARED_PLAN", ResourceId = id,
                    BranchId = BranchScope.LorettaId, CorrelationId = Guid.CreateVersion7(), Outcome = "SUCCESS",
                    AfterData = JsonSerializer.SerializeToDocument(new { plan.Name, Fixture = true, day, kind }) });
            }
            await db.SaveChangesAsync(); await tx.CommitAsync();
        }

        // Use the real catalog publishing and process activation services. Waiting stages
        // remain reservations; they must never become fake board assignments.
        var processId = Key("process"); var processPlanId = Key("process-plan"); ids.Add(processPlanId);
        var catalog = services.GetRequiredService<OperationalProcessCatalogService>();
        var taskService = services.GetRequiredService<OperationalTaskService>();
        var processName = $"Prueba · Proceso tablero · {day:dd/MM/yyyy}";
        if (!await db.Set<OperationalSequence>().AnyAsync(p => p.Id == processId))
            await catalog.CreateAsync(director.Id, processId, processName, "Prueba del origen y cierres diarios", default);
        foreach (var (title, role) in new[] { ("Preparar documento", CanonicalRole.SalesFloor), ("Autorizar documento", CanonicalRole.Direction) })
            if (!await db.OperationalTasks.AnyAsync(t => t.ProcessId == processId && t.Name == title))
                await taskService.SaveAsync(director.Id, new() { ProcessId = processId, Name = title, Requirement = "DOCUMENTO",
                    AssignedRole = role, PeriodType = "OCASIONAL", LoadMinutes = 1, DeadlineHours = 1, Revision = 0 }, default);
        var editor = await catalog.ReadAsync(director.Id, processId, default);
        if (editor.Process.PublishedVersionId == null)
        {
            await catalog.SaveAsync(director.Id, new() { Id = processId, Revision = editor.Process.Revision, Name = processName,
                Description = "Prueba del origen y cierres diarios", StartKind = "OCASIONAL", StartHour = 8,
                Steps = [new() { Position = 1, TaskId = editor.Tasks.Single(t => t.Name == "Preparar documento").Id,
                    Action = "ENTREGAR", ReleaseCondition = "INICIO", DeadlineKind = "FIJO", DeadlineHours = 12 },
                    new() { Position = 2, TaskId = editor.Tasks.Single(t => t.Name == "Autorizar documento").Id,
                    Action = "AUTORIZAR", ReleaseCondition = "ENTREGA", DeadlineHours = 1 }] }, default);
            editor = await catalog.ReadAsync(director.Id, processId, default);
            await catalog.PublishAsync(director.Id, processId, editor.Process.Revision, default);
        }
        editor = await catalog.ReadAsync(director.Id, processId, default);
        if (!editor.Process.Enabled) await catalog.SetEnabledAsync(director.Id, processId, editor.Process.Revision, true, default);
        await using (var tx = await db.Database.BeginTransactionAsync())
        {
            await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(734651001)");
            if (!await db.DirectionPreparedPlans.AnyAsync(p => p.Id == processPlanId))
            {
                db.Add(DirectionPreparedPlan.ForProcess(processPlanId, BranchScope.LorettaId, director.Id, now, processName, day, day.AddDays(3)));
                var versionId = editor.Process.PublishedVersionId!.Value;
                foreach (var (t, i) in editor.Tasks.Select((t, i) => (t, i))) db.Add(new DirectionPreparedPlanItem(processPlanId, t.TaskCode, t.Name, i));
                foreach (var offset in new[] { 0, 1, 2 }) db.Add(new PreparedProcessStart {
                    Id = Key($"process-start:{offset}"), PlanId = processPlanId, ProcessId = processId, VersionId = versionId,
                    StartsAt = OperationalSchedule.Instant(day.AddDays(offset), 8), StartKey = $"BOARD-ALL:{day:yyyyMMdd}:process:{offset}",
                    Participants = JsonSerializer.Serialize(new Dictionary<int, Guid> { [1] = owner.PersonId, [2] = director.PersonId }) });
                db.Add(new AuditEvent { Id = Guid.CreateVersion7(), OccurredAt = now, ActorUserId = director.Id, ActorType = "USER",
                    Action = "BOARD_ALL_PROCESS_FIXTURE_PREPARED", ResourceType = "DIRECTION_PREPARED_PLAN", ResourceId = processPlanId,
                    BranchId = BranchScope.LorettaId, CorrelationId = Guid.CreateVersion7(), Outcome = "SUCCESS",
                    AfterData = JsonSerializer.SerializeToDocument(new { Fixture = true, day }) });
                await db.SaveChangesAsync();
            }
            await services.GetRequiredService<ProcessPlanningService>().ActivateAsync(director.Id, processPlanId, default);
            await tx.CommitAsync();
        }
        var reader = new LiveBoardReader(db, services.GetRequiredService<IClock>());
        var snapshot = await reader.ReadAsync(director.Id, null);
        var expected = await (from a in db.OperationalAssignments.AsNoTracking().Where(LiveBoardReader.ForDay(day))
            join p in db.DirectionPreparedPlans.AsNoTracking() on a.PlanId equals p.Id
            where p.Status == "ASIGNADO" && p.BranchId == BranchScope.LorettaId select a.Id).ToArrayAsync();
        if (snapshot.PlanId != null || !snapshot.Tasks.Select(t => t.Id).Order().SequenceEqual(expected.Order())) throw new Exception("Vista conjunta incompleta");
        foreach (var id in ids)
        {
            var selected = await reader.ReadAsync(director.Id, id);
            var count = id == Key("SIN_HOY") ? 0 : 1;
            if (selected.PlanId != id || selected.Tasks.Count != count || selected.DayTasks.Count != snapshot.DayTasks.Count ||
                selected.Delivered != snapshot.Delivered || selected.Tasks.Any(t => t.PlanId != id || string.IsNullOrEmpty(t.Origin)))
                throw new Exception($"Falló filtro/origen/progreso de plan {id}");
            foreach (var task in selected.Tasks)
            {
                var single = await reader.ReadAsync(director.Id, null, selectedTask: task.Id);
                if (single.Tasks.Count != 1 || single.Tasks[0].Id != task.Id || single.DayTasks.Count != snapshot.DayTasks.Count)
                    throw new Exception("Falló filtro de tarea");
            }
            Console.WriteLine($"VERIFICADO {selected.Plans.Single(p => p.Id == id).Name}: {count} cierres hoy · {id}");
        }
        var invalid = await reader.ReadAsync(director.Id, Guid.NewGuid(), selectedTask: Guid.NewGuid());
        if (invalid.PlanId != null || invalid.TaskId != null || invalid.Tasks.Count != snapshot.Tasks.Count) throw new Exception("Fallback inválido");
        Console.WriteLine($"VERIFICADO tablero completo: {snapshot.Tasks.Count} cierres hoy; progreso global {snapshot.Delivered}/{snapshot.DayTasks.Count}.");
        Console.WriteLine("VERIFICADO fechas ajenas excluidas, filtro individual, origen y plan vacío. Reejecución idempotente.");
        await File.WriteAllTextAsync(Path.GetFullPath(".local/board-all-snapshot.json"), JsonSerializer.Serialize(snapshot));
    }
}
