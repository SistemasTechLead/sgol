using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Sgol.Identity.Contracts;
using Sgol.Organization.Contracts;
using Sgol.Planning.Contracts;
using Sgol.Web.Infrastructure.Persistence;
using Sgol.Web.Infrastructure.Persistence.Auditing;
using Sgol.Web.Infrastructure.Persistence.Planning;
using Sgol.Web.Infrastructure.Persistence.Reporting;

// Invoked only by LocalSetup after its Development/localhost/sgol_local guard.
// Stable keys make repeat runs additive; existing operational records are never edited.
internal static class ReportSimulationSeeder
{
    public static async Task SeedAsync(SgolDbContext db)
    {
        var now = DateTimeOffset.UtcNow;
        var today = WeekContract.LocalToday(now);
        var monday = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
        var people = await (from u in db.AppUsers
            join p in db.People on u.PersonId equals p.Id
            join c in db.IdentityCredentials on u.Id equals c.UserId
            join r in db.RoleAssignmentVersions on u.Id equals r.UserId
            where (c.UserName.EndsWith(".demo") || c.UserName == "direccion.local") &&
                u.Status == AccountStatus.Active && r.Status == RoleAssignmentStatus.Active &&
                r.ValidTo == null && r.BranchId == BranchScope.LorettaId &&
                db.EmploymentVersions.Any(e => e.PersonId == p.Id && e.Status == EmploymentStatus.Active &&
                    e.ValidTo == null && e.BranchId == BranchScope.LorettaId)
            orderby c.UserName
            select new { UserId = u.Id, PersonId = p.Id, Name = p.DisplayName, Role = r.RoleCode }).ToArrayAsync();
        var director = people.Single(p => p.Role == CanonicalRole.Direction);
        var roles = new[] { CanonicalRole.SalesFloor, CanonicalRole.Subcoordination, CanonicalRole.Administration };
        if (roles.Any(role => !people.Any(p => p.Role == role)))
            throw new InvalidOperationException("La simulación requiere el equipo local activo en los tres roles.");
        await using var transaction = await db.Database.BeginTransactionAsync();
        // Prevent concurrent seed runs from racing on stable keys.
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(73501928)");
        var tasks = new List<OperationalTask>();
        var labels = new[] { "Surtir y ordenar exhibiciones", "Verificar inventario", "Actualizar controles operativos", "Revisar incidencias y comprobantes" };
        for (var roleIndex = 0; roleIndex < roles.Length; roleIndex++)
        for (var type = 0; type < labels.Length; type++)
        {
            var code = $"SIM-REP-{roleIndex}{type}";
            var task = await db.OperationalTasks.SingleOrDefaultAsync(t => t.TaskCode == code);
            if (task == null)
            {
                task = new OperationalTask { Id = Guid.CreateVersion7(), TaskCode = code,
                    Name = "Simulación · " + labels[type], AssignedRole = roles[roleIndex],
                    ScheduleMode = type == 1 ? "PROGRESO" : type == 2 ? "DIAS_FIJOS" : "DISTRIBUIDA",
                    WeekDays = type == 2 ? 62 : 0, StartHour = 9, Repetitions = type == 2 ? 5 : 1,
                    GoalUnits = 100 + 50 * roleIndex, ProgressUnit = roleIndex == 0 ? "productos" : "registros",
                    Requirement = type == 3 ? "NINGUNO" : "COMENTARIO", LoadMinutes = 30 + 30 * type,
                    // Historical fixtures must not become recurring production task definitions.
                    Active = false };
                task.Validate(); db.OperationalTasks.Add(task);
            }
            tasks.Add(task);
        }
        var createdPlans = 0; var createdAssignments = 0;
        foreach (var offset in new[] { -35, -14, -7, 0, 7 })
        {
            var start = monday.AddDays(offset);
            for (var roleIndex = 0; roleIndex < roles.Length; roleIndex++)
            {
                var area = new[] { "Piso de ventas", "Coordinación operativa", "Administración" }[roleIndex];
                var name = $"Simulación · {area} · {start:yyyy-MM-dd}";
                if (await db.DirectionPreparedPlans.AnyAsync(p => p.Name == name)) continue;
                var roleTasks = tasks.Where(t => t.AssignedRole == roles[roleIndex]).ToArray();
                var plan = new DirectionPreparedPlan(Guid.CreateVersion7(), BranchScope.LorettaId, director.UserId,
                    OperationalSchedule.Instant(start).AddDays(-1), new DirectionPlanInput(name, area, "SEMANAL",
                    ISOWeek.GetYear(start.ToDateTime(TimeOnly.MinValue)), ISOWeek.GetWeekOfYear(start.ToDateTime(TimeOnly.MinValue)),
                    roleTasks.Select(t => t.TaskCode).ToArray()));
                plan.MarkAssigned(); db.DirectionPreparedPlans.Add(plan); createdPlans++;
                db.DirectionPreparedPlanItems.AddRange(roleTasks.Select((t, i) => new DirectionPreparedPlanItem(plan.Id, t.TaskCode, t.Name, i)));
                var team = people.Where(p => p.Role == roles[roleIndex]).ToArray();
                for (var i = 0; i < 12; i++)
                {
                    var person = team[(i + roleIndex) % team.Length];
                    var task = roleTasks[(i + i / 4) % roleTasks.Length];
                    var state = offset > 0 ? 6 : i % 7;
                    // Current-period fixtures include live tasks and reviewable deliveries.
                    var begins = offset == 0 ? OperationalSchedule.Instant(today, 0) : OperationalSchedule.Instant(start.AddDays(i % 5), 9);
                    if (state == 6) begins = OperationalSchedule.Instant(start.AddDays(6), 18);
                    if (begins > now && state != 6 && offset <= 0) begins = now.AddHours(-4);
                    var due = state == 4 ? now.AddHours(-8 - i * 7) : begins.AddHours(36);
                    if (state == 4 && begins >= due) begins = due.AddHours(-24);
                    if (offset == 0 && state == 5) due = now.AddHours(12 + i);
                    var delivered = state <= 3 ? begins.AddMinutes(45 + i * 5) : (DateTimeOffset?)null;
                    if (delivered > now) delivered = now.AddMinutes(-90);
                    if (delivered <= begins) begins = delivered.Value.AddMinutes(-30);
                    var goal = task.ScheduleMode == "PROGRESO" ? task.GoalUnits ?? throw new InvalidOperationException("La simulación requiere una meta completa.") : 0;
                    var units = goal == 0 || state == 6 ? 0 : state is 0 or 3 ? goal : state is 1 or 2 ? goal / 2 : goal * (15 + i * 6) / 100;
                    var assignment = new OperationalAssignment { Id = Guid.CreateVersion7(), PlanId = plan.Id,
                        TaskId = task.Id, TaskCode = task.TaskCode, Name = task.Name, AssignedRole = task.AssignedRole,
                        PersonId = person.PersonId, PersonName = person.Name, LoadMinutes = task.LoadMinutes,
                        StartsAt = begins, DueAt = due, CompletedAt = delivered, Requirement = task.Requirement,
                        ScheduleMode = task.ScheduleMode, PeriodType = task.PeriodType, Repetitions = task.Repetitions,
                        ScheduleKey = $"SIM-REPORTS:{start:yyyyMMdd}:{roleIndex}:{i}",
                        GoalUnits = goal, ProgressUnits = units, ProgressUnit = task.ProgressUnit,
                        Comment = delivered != null ? "Entrega ficticia para visualizar los reportes operativos." : null,
                        ReviewStatus = state switch { 0 => "CONCLUIDA", 1 => "NO_SATISFECHA", 2 => "REASIGNADA", _ => null },
                        ReviewedAt = state <= 2 ? delivered!.Value.AddMinutes(15) : null,
                        ReviewedBy = state <= 2 ? director.UserId : null,
                        Conclusion = state == 0 ? "Actividad completada conforme al objetivo (simulación)." : null,
                        ReviewComment = state switch { 0 => "Resultado satisfactorio verificado en esta simulación.",
                            1 => "Faltan registros y se detectaron diferencias; requiere corrección (simulación).",
                            2 => "Se reasigna al siguiente responsable para completar la actividad (simulación).", _ => null } };
                    db.OperationalAssignments.Add(assignment); createdAssignments++;
                    if (units > 0)
                        db.OperationalProgressEntries.Add(new OperationalProgressEntry { Id = Guid.CreateVersion7(), AssignmentId = assignment.Id,
                            ActorId = person.UserId, OccurredAt = delivered ?? (begins.AddMinutes(10) < now ? begins.AddMinutes(10) : now),
                            Units = units, Note = "Avance ficticio para la simulación de reportes." });
                    if (state == 2)
                    {
                        var next = team[(i + roleIndex + 1) % team.Length];
                        db.OperationalAssignments.Add(new OperationalAssignment { Id = Guid.CreateVersion7(), PlanId = plan.Id,
                            TaskId = task.Id, TaskCode = task.TaskCode, Name = task.Name, AssignedRole = task.AssignedRole,
                            PersonId = next.PersonId, PersonName = next.Name, StartsAt = assignment.ReviewedAt!.Value,
                            DueAt = assignment.ReviewedAt.Value.AddHours(72), LoadMinutes = task.LoadMinutes,
                            Requirement = task.Requirement, ScheduleMode = task.ScheduleMode, PeriodType = task.PeriodType,
                            Repetitions = task.Repetitions, ReassignedFromId = assignment.Id, GoalUnits = goal,
                            ProgressUnit = task.ProgressUnit, ScheduleKey = assignment.ScheduleKey + ":retry" });
                        createdAssignments++;
                    }
                }
            }
        }
        // A goal-focused plan also spreads deliveries over the week, making trends visible.
        foreach (var role in roles)
        {
            var name = $"Simulación · Metas · {ReportCatalog.RoleName(role)} · {monday:yyyy-MM-dd}";
            if (await db.DirectionPreparedPlans.AnyAsync(p => p.Name == name)) continue;
            var task = tasks.Single(t => t.AssignedRole == role && t.ScheduleMode == "PROGRESO");
            var plan = new DirectionPreparedPlan(Guid.CreateVersion7(), BranchScope.LorettaId, director.UserId,
                OperationalSchedule.Instant(monday).AddDays(-1), new DirectionPlanInput(name, "Control de metas", "SEMANAL",
                ISOWeek.GetYear(monday.ToDateTime(TimeOnly.MinValue)), ISOWeek.GetWeekOfYear(monday.ToDateTime(TimeOnly.MinValue)), [task.TaskCode]));
            plan.MarkAssigned(); db.DirectionPreparedPlans.Add(plan); createdPlans++;
            db.DirectionPreparedPlanItems.Add(new DirectionPreparedPlanItem(plan.Id, task.TaskCode, task.Name, 0));
            var team = people.Where(p => p.Role == role).ToArray();
            for (var i = 0; i < 6; i++)
            {
                var person = team[i % team.Length];
                var begins = i == 5 ? OperationalSchedule.Instant(monday.AddDays(6), 9) : OperationalSchedule.Instant(monday.AddDays(Math.Min(i, (int)(today.DayNumber - monday.DayNumber))), 0);
                var delivered = i is 0 or 1 or 3 ? (begins.AddMinutes(30) < now ? begins.AddMinutes(30) : now.AddMinutes(-1)) : (DateTimeOffset?)null;
                if (delivered <= begins) begins = delivered.Value.AddMinutes(-30);
                var complete = i is 0 or 3;
                var due = i == 2 ? now.AddHours(-6) : i == 4 ? now.AddHours(24) : begins.AddHours(48);
                if (due <= begins) begins = due.AddHours(-24);
                var goal = task.GoalUnits ?? throw new InvalidOperationException("La simulación requiere una meta completa.");
                var units = i == 5 ? 0 : i is 0 or 1 or 3 ? goal : goal * (i == 2 ? 35 : 65) / 100;
                var assignment = new OperationalAssignment { Id = Guid.CreateVersion7(), PlanId = plan.Id, TaskId = task.Id,
                    TaskCode = task.TaskCode, Name = task.Name, AssignedRole = role, PersonId = person.PersonId, PersonName = person.Name,
                    StartsAt = begins, DueAt = due,
                    LoadMinutes = task.LoadMinutes, Requirement = task.Requirement, ScheduleMode = task.ScheduleMode,
                    GoalUnits = goal, ProgressUnits = units, ProgressUnit = task.ProgressUnit,
                    CompletedAt = delivered, ReviewStatus = complete ? "CONCLUIDA" : null,
                    ReviewedAt = complete ? delivered!.Value : null, ReviewedBy = complete ? director.UserId : null,
                    Comment = delivered != null ? "Meta completada con datos ficticios." : null,
                    Conclusion = complete ? "Objetivo alcanzado y verificado (simulación)." : null,
                    ReviewComment = complete ? "Se validó el cumplimiento del objetivo (simulación)." : null,
                    ScheduleKey = $"SIM-REPORTS:GOALS:{monday:yyyyMMdd}:{role}:{i}" };
                db.OperationalAssignments.Add(assignment); createdAssignments++;
                if (units > 0) db.OperationalProgressEntries.Add(new OperationalProgressEntry { Id = Guid.CreateVersion7(),
                    AssignmentId = assignment.Id, ActorId = person.UserId, OccurredAt = delivered ?? begins,
                    Units = units, Note = "Avance ficticio de la meta para visualizar gráficos." });
            }
        }
        var capacityFrom = monday.AddDays(-35); var capacityThrough = monday.AddDays(13);
        foreach (var person in people.Where(p => p.Role != CanonicalRole.Direction))
        {
            if (await db.OperationalCapacities.AnyAsync(c => c.PersonId == person.PersonId &&
                c.From <= capacityThrough && c.Through >= capacityFrom)) continue;
            db.OperationalCapacities.Add(new OperationalCapacity { Id = Guid.CreateVersion7(), PersonId = person.PersonId,
                From = capacityFrom, Through = capacityThrough, Percent = person.Name.StartsWith("Ana") ? 60 : 100,
                DailyMinutes = person.Role == CanonicalRole.SalesFloor ? 90 : 120,
                Reason = "Simulación de disponibilidad para visualizar la carga de trabajo." });
        }
        var templates = 0;
        foreach (var person in people)
        foreach (var definition in ReportCatalog.Definitions)
        {
            var name = "Simulación · " + definition.Name;
            if (await db.ReportTemplates.AnyAsync(t => t.UserId == person.UserId && t.Name == name)) continue;
            db.ReportTemplates.Add(new ReportTemplate { Id = Guid.CreateVersion7(), UserId = person.UserId,
                Name = name, UpdatedAt = now, Configuration = JsonSerializer.Serialize(new ReportFilter {
                    Kind = definition.Id, Range = "month", Group = definition.Id == "plans" ? "plan" : "person",
                    Columns = "task,person,plan,status,due,progress,minutes,comment" }, new JsonSerializerOptions(JsonSerializerDefaults.Web)) });
            templates++;
        }
        if (createdPlans > 0 || templates > 0)
            db.AuditEvents.Add(new AuditEvent { Id = Guid.CreateVersion7(), OccurredAt = now, ActorType = "SYSTEM",
                Action = "LOCAL_REPORT_SIMULATION_SEEDED", ResourceType = "REPORT_SIMULATION", Outcome = "SUCCESS",
                CorrelationId = Guid.CreateVersion7(), BranchId = BranchScope.LorettaId,
                Reason = "Datos ficticios solicitados para visualizar reportes en el entorno local.",
                AfterData = JsonSerializer.SerializeToDocument(new { createdPlans, createdAssignments, templates }) });
        foreach (var assignment in db.OperationalAssignments.Local.Where(a => db.Entry(a).State == EntityState.Added))
        {
            if (assignment.StartsAt >= assignment.DueAt || assignment.CompletedAt < assignment.StartsAt ||
                assignment.CompletedAt > now || assignment.ReviewedAt < assignment.CompletedAt || assignment.ReviewedAt > now ||
                assignment.ProgressUnits < 0 || assignment.ProgressUnits > assignment.GoalUnits ||
                db.OperationalProgressEntries.Local.Where(p => p.AssignmentId == assignment.Id).Sum(p => p.Units) != assignment.ProgressUnits)
                throw new InvalidOperationException("La simulación contiene una fecha o un avance incoherente.");
        }
        await db.SaveChangesAsync(); await transaction.CommitAsync();
        Console.WriteLine($"Simulación de reportes: {createdPlans} planes, {createdAssignments} asignaciones y {templates} plantillas agregadas. Los datos existentes se conservaron.");
    }
}
