using System.Globalization;
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

// LocalSetup enforces Development + localhost + sgol_local before invoking this fixture.
internal static class LiveBoardSimulationSeeder
{
    public static async Task SeedAsync(SgolDbContext db, IServiceProvider services)
    {
        var now = DateTimeOffset.UtcNow;
        var today = WeekContract.LocalToday(now);
        var monday = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
        var name = $"Prueba · Tablero Vivo · {monday:dd/MM/yyyy}";
        var people = await (from u in db.AppUsers.AsNoTracking()
            join p in db.People.AsNoTracking() on u.PersonId equals p.Id
            join c in db.IdentityCredentials.AsNoTracking() on u.Id equals c.UserId
            join r in db.RoleAssignmentVersions.AsNoTracking() on u.Id equals r.UserId
            where (c.UserName.EndsWith(".demo") || c.UserName == "direccion.local") &&
                u.Status == AccountStatus.Active && r.BranchId == BranchScope.LorettaId &&
                r.Status == RoleAssignmentStatus.Active && r.ValidFrom <= now && (r.ValidTo == null || r.ValidTo > now) &&
                db.EmploymentVersions.Any(e => e.PersonId == p.Id && e.BranchId == BranchScope.LorettaId &&
                    e.Status == EmploymentStatus.Active && e.ValidFrom <= now && (e.ValidTo == null || e.ValidTo > now))
            orderby c.UserName
            select new { User = u.Id, Person = p.Id, p.DisplayName, Role = r.RoleCode }).ToArrayAsync();
        var director = people.First(p => p.Role == CanonicalRole.Direction);
        foreach (var role in new[] { CanonicalRole.SalesFloor })
            if (!people.Any(p => p.Role == role)) throw new InvalidOperationException("Falta equipo local activo en el rol " + role);
        Guid planId;
        var samples = new (string Title, string Requirement, string Mode, string Role, string State)[] {
            ("Entrega sin evidencia", "NINGUNO", "DIAS_FIJOS", CanonicalRole.SalesFloor, "delivered"),
            ("Revisar exhibición", "NINGUNO", "DIAS_FIJOS", CanonicalRole.SalesFloor, "active"),
            ("Registrar incidencias · comentario", "COMENTARIO", "DIAS_FIJOS", CanonicalRole.SalesFloor, "warning"),
            ("Verificar aparador · fotografía", "FOTOGRAFIA", "DIAS_FIJOS", CanonicalRole.SalesFloor, "urgent"),
            ("Adjuntar reporte · PDF", "DOCUMENTO", "DIAS_FIJOS", CanonicalRole.SalesFloor, "scheduled"),
            ("Ordenar almacén · pendiente anterior", "NINGUNO", "DISTRIBUIDA", CanonicalRole.SalesFloor, "overdue"),
            ("Inventario semanal · registrar avance", "NINGUNO", "PROGRESO", CanonicalRole.SalesFloor, "progress")
        };
        var seeded = new List<(Guid Assignment, Guid User, string State)>();
        await using (var tx = await db.Database.BeginTransactionAsync())
        {
            await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(73501929)");
            var existing = await db.DirectionPreparedPlans.AsNoTracking().SingleOrDefaultAsync(p => p.Name == name);
            if (existing is not null)
            {
                Console.WriteLine($"El plan de prueba ya existe: {existing.Id}. No se modificaron sus entregas ni avances.");
                return;
            }
            var definitions = new List<OperationalTask>();
            for (var i = 0; i < samples.Length; i++)
            {
                var code = $"SIM-BOARD-{i:D2}";
                var task = await db.OperationalTasks.SingleOrDefaultAsync(t => t.TaskCode == code);
                if (task is null)
                {
                    var sample = samples[i];
                    task = new OperationalTask { Id = Guid.CreateVersion7(), TaskCode = code,
                        Name = "Prueba · " + sample.Title, Requirement = sample.Requirement, AssignedRole = sample.Role,
                        ScheduleMode = sample.Mode, WeekDays = sample.Mode == "DIAS_FIJOS" ? 127 : 0,
                        Repetitions = sample.Mode == "DISTRIBUIDA" ? 7 : 1, StartHour = sample.State == "scheduled" ? 19 : 8,
                        DeadlineHours = 3, LoadMinutes = 1, GoalUnits = 20, ProgressUnit = "productos",
                        // Simulation definitions cannot be accidentally selected for normal plan generation.
                        Active = false };
                    task.Validate(); db.OperationalTasks.Add(task);
                }
                definitions.Add(task);
            }
            var localMonday = monday.ToDateTime(TimeOnly.MinValue);
            var plan = new DirectionPreparedPlan(Guid.CreateVersion7(), BranchScope.LorettaId, director.User, now,
                new(name, "Pruebas visuales y de ejecución", "SEMANAL", ISOWeek.GetYear(localMonday), ISOWeek.GetWeekOfYear(localMonday),
                    definitions.Select(t => t.TaskCode).ToArray()));
            plan.MarkAssigned(); planId = plan.Id;
            db.DirectionPreparedPlans.Add(plan);
            db.DirectionPreparedPlanItems.AddRange(definitions.Select((t, i) => new DirectionPreparedPlanItem(plan.Id, t.TaskCode, t.Name, i)));
            for (var i = 0; i < definitions.Count; i++)
            {
                var task = definitions[i]; var sample = samples[i];
                var team = people.Where(p => p.Role == task.AssignedRole).ToArray();
                var owner = team[i % team.Length];
                foreach (var day in OperationalSchedule.Dates(plan.StartsOn, plan.EndsOn, task))
                {
                    var begins = OperationalSchedule.Instant(day, task.StartHour);
                    var ends = task.ScheduleMode == "PROGRESO" ? OperationalSchedule.Instant(monday.AddDays(7)) : begins.AddHours(task.DeadlineHours);
                    // Today's explicit fixtures demonstrate all colors immediately; future days use the normal calendar.
                    if (day == today && task.ScheduleMode != "PROGRESO")
                    {
                        ends = sample.State switch { "scheduled" => now.AddHours(4), "warning" => now.AddMinutes(45),
                            "urgent" => now.AddMinutes(20), "overdue" => now.AddMinutes(-15), _ => now.AddHours(2) };
                        begins = ends.AddHours(-3);
                        if (sample.State == "scheduled") begins = now.AddHours(1);
                    }
                    var assignment = new OperationalAssignment { Id = Guid.CreateVersion7(), PlanId = plan.Id,
                        TaskId = task.Id, TaskCode = task.TaskCode, Name = task.Name, AssignedRole = task.AssignedRole,
                        Requirement = task.Requirement, PersonId = owner.Person, PersonName = owner.DisplayName,
                        StartsAt = begins, DueAt = ends, LoadMinutes = task.LoadMinutes, ScheduleMode = task.ScheduleMode,
                        PeriodType = task.PeriodType, Repetitions = task.Repetitions,
                        ScheduleKey = $"SIM-BOARD:{monday:yyyyMMdd}:{i}:{day:yyyyMMdd}",
                        GoalUnits = task.ScheduleMode == "PROGRESO" ? task.GoalUnits ?? throw new InvalidOperationException("La simulación requiere una meta completa.") : 0, ProgressUnit = task.ProgressUnit };
                    db.OperationalAssignments.Add(assignment);
                    if (day == today || task.ScheduleMode == "PROGRESO") seeded.Add((assignment.Id, owner.User, sample.State));
                }
            }
            db.AuditEvents.Add(new AuditEvent { Id = Guid.CreateVersion7(), OccurredAt = now, ActorUserId = director.User,
                ActorType = "USER", Action = "LIVE_BOARD_SIMULATION_CREATED", ResourceType = "DIRECTION_PREPARED_PLAN",
                ResourceId = plan.Id, BranchId = BranchScope.LorettaId, CorrelationId = Guid.CreateVersion7(), Outcome = "SUCCESS",
                AfterData = JsonSerializer.SerializeToDocument(new { plan.Name, plan.StartsOn, plan.EndsOn, SimulatedSchedules = true }) });
            await db.SaveChangesAsync(); await tx.CommitAsync();
        }
        db.ChangeTracker.Clear();
        var tasksService = services.GetRequiredService<OperationalTaskService>();
        foreach (var sample in seeded.Where(s => s.State == "delivered"))
            await tasksService.CompleteAsync(sample.User, sample.Assignment, null, null, default,
                "Entrega de prueba para comprobar el progreso del Tablero Vivo.");
        foreach (var sample in seeded.Where(s => s.State == "progress"))
            await tasksService.ProgressAsync(sample.User, sample.Assignment, 1, 5, "Avance inicial de prueba: 5 de 20 productos.", default);
        Console.WriteLine($"Plan semanal creado: {name} · {monday:dd/MM/yyyy}–{monday.AddDays(6):dd/MM/yyyy}");
        Console.WriteLine($"Tablero: /tablero?plan={planId:D}");
        foreach (var sample in seeded)
        {
            var task = await db.OperationalAssignments.AsNoTracking().SingleAsync(a => a.Id == sample.Assignment);
            Console.WriteLine($"{sample.State}: {task.Name} → {task.PersonName} · {task.AssignedRole}");
        }
    }

    public static async Task PrepareAnaAsync(SgolDbContext db)
    {
        var now = DateTimeOffset.UtcNow;
        var today = WeekContract.LocalToday(now);
        var monday = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
        var name = $"Prueba · Tablero Vivo · {monday:dd/MM/yyyy}";
        var ana = await (from c in db.IdentityCredentials
            join u in db.AppUsers on c.UserId equals u.Id
            join p in db.People on u.PersonId equals p.Id
            where c.UserName == "ana.demo" && u.Status == AccountStatus.Active &&
                db.RoleAssignmentVersions.Any(r => r.UserId == u.Id && r.BranchId == BranchScope.LorettaId &&
                    r.RoleCode == CanonicalRole.SalesFloor && r.Status == RoleAssignmentStatus.Active &&
                    r.ValidFrom <= now && (r.ValidTo == null || r.ValidTo > now)) &&
                db.EmploymentVersions.Any(e => e.PersonId == p.Id && e.BranchId == BranchScope.LorettaId &&
                    e.Status == EmploymentStatus.Active && e.ValidFrom <= now && (e.ValidTo == null || e.ValidTo > now))
            select new { u.PersonId, p.DisplayName }).SingleAsync();
        var director = await db.RoleAssignmentVersions.Where(r => r.RoleCode == CanonicalRole.Direction &&
            r.Status == RoleAssignmentStatus.Active && r.BranchId == BranchScope.LorettaId &&
            r.ValidFrom <= now && (r.ValidTo == null || r.ValidTo > now)).Select(r => r.UserId).FirstAsync();
        await using var tx = await db.Database.BeginTransactionAsync();
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(734651001)");
        var plan = await db.DirectionPreparedPlans.SingleAsync(p => p.Name == name && p.BranchId == BranchScope.LorettaId);
        var assignments = await db.OperationalAssignments.Where(a => a.PlanId == plan.Id).ToArrayAsync();
        if (assignments.Any(a => !a.TaskCode.StartsWith("SIM-BOARD-")))
            throw new InvalidOperationException("Solo se puede ajustar la simulación del tablero.");
        var removed = assignments.Where(a => a.AssignedRole != CanonicalRole.SalesFloor).ToArray();
        var removedIds = removed.Select(a => a.Id).ToArray();
        // Do not silently remove uploaded evidence or ongoing camera sessions.
        if (await db.OperationalAttachments.AnyAsync(f => removedIds.Contains(f.AssignmentId)) || await db.OperationalCaptureSessions.AnyAsync(p => removedIds.Contains(p.AssignmentId)) ||
            await db.OperationalPhotographs.AnyAsync(p => removedIds.Contains(p.AssignmentId)) ||
            await db.OperationalProgressEntries.AnyAsync(p => removedIds.Contains(p.AssignmentId)))
            throw new InvalidOperationException("Hay evidencias o avances fuera de Piso de ventas en la simulación.");
        db.OperationalAssignments.RemoveRange(removed);
        var removedCodes = removed.Select(a => a.TaskCode).Distinct().ToArray();
        db.DirectionPreparedPlanItems.RemoveRange(await db.DirectionPreparedPlanItems.Where(i => i.PlanId == plan.Id && removedCodes.Contains(i.TaskCode)).ToArrayAsync());
        var dailyIds = await db.OperationalAssignments.AsNoTracking().Where(a => a.PlanId == plan.Id && a.AssignedRole == CanonicalRole.SalesFloor &&
            a.CompletedAt == null).Where(LiveBoardReader.ForDay(today)).Select(a => a.Id).ToArrayAsync();
        var pending = assignments.Where(a => dailyIds.Contains(a.Id)).ToArray();
        var before = pending.Select(a => new { a.Id, a.PersonId, a.PersonName, a.StartsAt, a.DueAt, a.Revision }).ToArray();
        foreach (var task in pending)
        {
            task.PersonId = ana.PersonId; task.PersonName = ana.DisplayName;
            if (task.ScheduleMode != "PROGRESO")
            {
                task.DueAt = task.TaskCode switch {
                    "SIM-BOARD-02" => now.AddMinutes(45), "SIM-BOARD-03" => now.AddMinutes(20),
                    "SIM-BOARD-04" => now.AddHours(4), "SIM-BOARD-05" => now.AddMinutes(-15), _ => now.AddHours(2) };
                task.StartsAt = task.DueAt.AddHours(-3);
            }
            task.Revision++;
        }
        db.AuditEvents.Add(new AuditEvent { Id = Guid.CreateVersion7(), OccurredAt = now, ActorUserId = director,
            ActorType = "USER", Action = "LIVE_BOARD_SIMULATION_PREPARED_FOR_ANA", ResourceType = "DIRECTION_PREPARED_PLAN",
            ResourceId = plan.Id, BranchId = BranchScope.LorettaId, CorrelationId = Guid.CreateVersion7(), Outcome = "SUCCESS",
            BeforeData = JsonSerializer.SerializeToDocument(before), AfterData = JsonSerializer.SerializeToDocument(new {
                RemovedAssignments = removedIds, Pending = pending.Select(a => new { a.Id, a.PersonId, a.StartsAt, a.DueAt }) }) });
        await db.SaveChangesAsync(); await tx.CommitAsync();
        db.ChangeTracker.Clear();
        var board = await new LiveBoardReader(db, new SystemClock()).ReadAsync(director, plan.Id);
        if (board.Tasks.Any(t => t.Role != CanonicalRole.SalesFloor) || board.Tasks.Where(t => t.CompletedAt == null).Any(t => t.PersonName != ana.DisplayName))
            throw new InvalidOperationException("La verificación de Piso de ventas y Ana falló.");
        Console.WriteLine($"Plan ajustado: {name} · solo Piso de ventas · {removed.Length} asignaciones de otros roles retiradas.");
        foreach (var task in board.OrderedTasks)
            Console.WriteLine($"{task.Color(board.Now)}: {task.Name} → {task.PersonName} · {board.Time(task.StartsAt)}–{board.Time(task.DueAt)}");
    }

}
