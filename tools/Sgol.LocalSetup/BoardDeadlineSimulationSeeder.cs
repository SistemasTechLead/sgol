using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Sgol.BuildingBlocks.Time;
using Sgol.Identity.Contracts;
using Sgol.Organization.Contracts;
using Sgol.Planning.Contracts;
using Sgol.Web.Infrastructure.Persistence;
using Sgol.Web.Infrastructure.Persistence.Auditing;
using Sgol.Web.Infrastructure.Persistence.Planning;

// Invoked only after LocalSetup checks Development + localhost + sgol_local.
internal static class BoardDeadlineSimulationSeeder
{
    public static async Task SeedAsync(SgolDbContext db)
    {
        var now = DateTimeOffset.UtcNow;
        var today = WeekContract.LocalToday(now);
        var monday = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
        var name = $"Prueba · Tablero por vencimiento · {today:dd/MM/yyyy}";
        var users = await (from u in db.AppUsers.AsNoTracking()
            join p in db.People.AsNoTracking() on u.PersonId equals p.Id
            join c in db.IdentityCredentials.AsNoTracking() on u.Id equals c.UserId
            join r in db.RoleAssignmentVersions.AsNoTracking() on u.Id equals r.UserId
            where (c.UserName == "ana.demo" || c.UserName == "direccion.local") &&
                u.Status == AccountStatus.Active && r.BranchId == BranchScope.LorettaId &&
                r.Status == RoleAssignmentStatus.Active && r.ValidFrom <= now && (r.ValidTo == null || r.ValidTo > now) &&
                db.EmploymentVersions.Any(e => e.PersonId == p.Id && e.BranchId == BranchScope.LorettaId &&
                    e.Status == EmploymentStatus.Active && e.ValidFrom <= now && (e.ValidTo == null || e.ValidTo > now))
            select new { u.Id, u.PersonId, p.DisplayName, r.RoleCode, c.UserName }).ToArrayAsync();
        var director = users.Single(u => u.UserName == "direccion.local" && u.RoleCode == CanonicalRole.Direction);
        var owner = users.Single(u => u.UserName == "ana.demo" && u.RoleCode == CanonicalRole.SalesFloor);
        var samples = new[] {
            (Title: "Preparar inventario · ayer a hoy", Start: OperationalSchedule.Instant(today.AddDays(-1), 18), Due: OperationalSchedule.Instant(today, 18)),
            (Title: "Revisar exhibición · hoy", Start: OperationalSchedule.Instant(today, 8), Due: OperationalSchedule.Instant(today, 12)),
            (Title: "Ordenar mostrador · hoy", Start: OperationalSchedule.Instant(today, 14), Due: OperationalSchedule.Instant(today, 20)),
            (Title: "Preparar reporte · hoy a mañana", Start: OperationalSchedule.Instant(today, 18), Due: OperationalSchedule.Instant(today.AddDays(1), 18))
        };
        await using var tx = await db.Database.BeginTransactionAsync();
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(73501930)");
        var plan = await db.DirectionPreparedPlans.SingleOrDefaultAsync(p => p.Name == name && p.BranchId == BranchScope.LorettaId);
        if (plan is null)
        {
            var definitions = new List<OperationalTask>();
            for (var i = 0; i < samples.Length; i++)
            {
                var code = $"SD-{today:yyyyMMdd}-{i:D2}";
                var sample = samples[i];
                var task = new OperationalTask { Id = Guid.CreateVersion7(), TaskCode = code, Name = "Prueba · " + sample.Title,
                    Requirement = "NINGUNO", AssignedRole = CanonicalRole.SalesFloor, Active = false,
                    LoadMinutes = 1, StartHour = TimeZoneInfo.ConvertTime(sample.Start, OperationalSchedule.Zone).Hour,
                    DeadlineHours = (int)(sample.Due - sample.Start).TotalHours };
                task.Validate(); definitions.Add(task); db.OperationalTasks.Add(task);
            }
            var date = monday.ToDateTime(TimeOnly.MinValue);
            plan = new DirectionPreparedPlan(Guid.CreateVersion7(), BranchScope.LorettaId, director.Id, now,
                new(name, "Ejemplo de visibilidad por día de vencimiento", "SEMANAL", ISOWeek.GetYear(date), ISOWeek.GetWeekOfYear(date),
                    definitions.Select(t => t.TaskCode).ToArray()));
            plan.MarkAssigned(); db.DirectionPreparedPlans.Add(plan);
            for (var i = 0; i < definitions.Count; i++)
            {
                var task = definitions[i]; var sample = samples[i];
                db.DirectionPreparedPlanItems.Add(new(plan.Id, task.TaskCode, task.Name, i));
                db.OperationalAssignments.Add(new() { Id = Guid.CreateVersion7(), PlanId = plan.Id, TaskId = task.Id,
                    TaskCode = task.TaskCode, Name = task.Name, AssignedRole = task.AssignedRole, Requirement = task.Requirement,
                    PersonId = owner.PersonId, PersonName = owner.DisplayName, StartsAt = sample.Start, DueAt = sample.Due,
                    LoadMinutes = task.LoadMinutes, PeriodType = task.PeriodType, ScheduleMode = task.ScheduleMode,
                    ScheduleKey = $"SIM-DUE:{today:yyyyMMdd}:{i}" });
            }
            db.AuditEvents.Add(new AuditEvent { Id = Guid.CreateVersion7(), OccurredAt = now, ActorUserId = director.Id,
                ActorType = "USER", Action = "BOARD_DEADLINE_SIMULATION_CREATED", ResourceType = "DIRECTION_PREPARED_PLAN",
                ResourceId = plan.Id, BranchId = BranchScope.LorettaId, CorrelationId = Guid.CreateVersion7(), Outcome = "SUCCESS",
                AfterData = JsonSerializer.SerializeToDocument(new { plan.Name, plan.StartsOn, plan.EndsOn, SimulatedSchedules = true }) });
            await db.SaveChangesAsync();
        }
        // Verify the real EF query and authorized reader before committing new data.
        var assignments = await db.OperationalAssignments.AsNoTracking().Where(a => a.PlanId == plan.Id).ToArrayAsync();
        foreach (var offset in new[] { -1, 0, 1 })
        {
            var day = today.AddDays(offset);
            var board = await new LiveBoardReader(db, new ExampleClock(OperationalSchedule.Instant(day, 12))).ReadAsync(director.Id, plan.Id);
            var expected = assignments.Where(a => LiveBoardReader.ForDay(day).Compile()(a)).Select(a => a.Id).Order().ToArray();
            var actual = board.Tasks.Select(t => t.Id).Order().ToArray();
            if (board.PlanId != plan.Id || !actual.SequenceEqual(expected) || actual.Length != (offset == 0 ? 3 : offset == 1 ? 1 : 0))
                throw new InvalidOperationException("Falló la verificación del tablero por vencimiento: " + day);
            Console.WriteLine($"Verificado {day:dd/MM/yyyy}: {actual.Length} tareas en tablero.");
        }
        await tx.CommitAsync();
        Console.WriteLine($"Plan semanal: {name} · {plan.StartsOn:dd/MM/yyyy}–{plan.EndsOn:dd/MM/yyyy}");
        Console.WriteLine($"Tablero: https://localhost:7240/tablero?plan={plan.Id:D}");
        foreach (var a in assignments.OrderBy(a => a.DueAt))
            Console.WriteLine($"{a.Name} → {a.PersonName} · {TimeZoneInfo.ConvertTime(a.StartsAt, OperationalSchedule.Zone):dd/MM/yyyy HH:mm}–{TimeZoneInfo.ConvertTime(a.DueAt, OperationalSchedule.Zone):dd/MM/yyyy HH:mm}");
    }

    private sealed class ExampleClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow => now;
    }
}
