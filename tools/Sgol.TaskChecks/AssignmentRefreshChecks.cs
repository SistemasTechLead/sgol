using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Sgol.BuildingBlocks.Time;
using Sgol.Planning.Contracts;
using Sgol.Organization.Contracts;
using Sgol.Web.Infrastructure.Persistence;
using Sgol.Web.Infrastructure.Persistence.Planning;

internal static class AssignmentRefreshChecks
{
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); Console.WriteLine("PASS refresh: " + message); }
    private static async Task Reject(Func<Task> action, string code)
    {
        try { await action(); throw new Exception("Expected " + code); }
        catch (DirectionPlanException e) when (e.Code == code) { Console.WriteLine("PASS refresh rejects " + code); }
    }
    public static async Task RunAsync(SgolDbContext db, OperationalTaskService tasks, IDirectionPreparedPlanService direction,
        IClock clock, Guid actor, Guid collaborator, Guid person, Guid otherPerson)
    {
        var service = new AssignmentRefreshService(db, direction, clock);
        var day = WeekContract.LocalToday(clock.UtcNow);
        var directorPerson = await db.AppUsers.Where(u => u.Id == actor).Select(u => u.PersonId).SingleAsync();
        foreach (var id in new[] { person, otherPerson, directorPerson })
            db.Add(new PersonalScheduleVersion { Id = Guid.CreateVersion7(), PersonId = id, From = day,
                Revision = 1, ChangedBy = actor, ChangedAt = clock.UtcNow, IntentKey = Guid.NewGuid(),
                DaysJson = JsonSerializer.Serialize(Enumerable.Range(0,7).ToDictionary(d => d, d => new[] { new WorkInterval(8*60,20*60) })) });
        var state = new TemporalAssignmentState { Enabled = true }; db.Add(state);
        await tasks.SaveAsync(actor, new OperationalTask { Name = "Actualizar meta", ScheduleMode = "PROGRESO", GoalUnits = 100,
            ProgressUnit = "piezas", Requirement = "NINGUNO", LoadMinutes = 100, Revision = 0, OperationalCriticality = "FLEXIBLE" }, default);
        var definition = await db.OperationalTasks.AsNoTracking().SingleAsync(t => t.Name == "Actualizar meta");
        var plan = DirectionPreparedPlan.ForOccasion(Guid.CreateVersion7(), BranchScope.LorettaId, actor, clock.UtcNow, "Refresh checks", day, day.AddDays(7));
        db.Add(plan);
        var pending = new OperationalAssignment { Id = Guid.CreateVersion7(), PlanId = plan.Id, TaskId = definition.Id, TaskRevision = definition.Revision,
            DefinitionSnapshotJson = AssignmentDefinition.Capture(definition), OperationalCriticality = definition.OperationalCriticality, TaskCode = definition.TaskCode, Name = definition.Name,
            AssignedRole = definition.AssignedRole, PersonId = person, PersonName = "A", Requirement = definition.Requirement,
            LoadMinutes = definition.LoadMinutes, ScheduleMode = definition.ScheduleMode, GoalUnits = 100, ProgressUnits = 60,
            ProgressUnit = "piezas", StartsAt = clock.UtcNow.AddHours(-1), DueAt = clock.UtcNow.AddDays(3) };
        var submitted = AssignmentDefinition.Copy(pending); submitted.Id = Guid.CreateVersion7(); submitted.CompletedAt = clock.UtcNow;
        submitted.ProgressUnits = 100; submitted.CompletedBy = collaborator; submitted.CompletionKind = "ENTREGA"; submitted.ReviewStatus = "PENDIENTE_REVISION";
        var closed = AssignmentDefinition.Copy(pending); closed.Id = Guid.CreateVersion7(); closed.ReviewStatus = "NO_SATISFECHA";
        db.AddRange(pending, submitted, closed);
        db.Add(new OperationalProgressEntry { Id = Guid.CreateVersion7(), AssignmentId = pending.Id, ActorId = collaborator, OccurredAt = clock.UtcNow, Units = 60, Note = "Autoría que debe conservarse" });
        db.Add(new OperationalAttachment { AssignmentId = pending.Id, FileName = "previo.pdf", ContentType = "application/pdf", Content = [1,2,3], CreatedAt = clock.UtcNow });
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        Check(!(await service.CountsAsync(actor, default)).ContainsKey(definition.Id), "no false difference for identical effective parameters");
        var originalProtected = JsonSerializer.Serialize(await db.OperationalAssignments.AsNoTracking().Where(a => a.Id == submitted.Id || a.Id == closed.Id).OrderBy(a => a.Id).ToArrayAsync());
        definition.Requirement = "COMENTARIO";
        await tasks.SaveAsync(actor, definition, default); db.ChangeTracker.Clear();
        Check((await service.CountsAsync(actor, default))[definition.Id] == 1, "only the current pending assignment enables the button");
        var request = new AssignmentRefreshRequest(definition.Id, Guid.NewGuid(), clock.UtcNow.AddMinutes(3));
        var preview = await service.PreviewAsync(actor, request, default);
        Check(preview.CanApply && preview.Differences.Length == 1 && preview.Excluded == 2, "metadata update excludes delivery and administrative closure");
        Check(preview.Fingerprint == (await service.PreviewAsync(actor, request, default)).Fingerprint, "deterministic repeatable preview");
        await Reject(() => service.ApplyAsync(actor, request, preview.Fingerprint, "", default), "REFRESH_REASON_REQUIRED");
        await Reject(() => service.ApplyAsync(collaborator, request, preview.Fingerprint, "No autorizado", default), "ACCESO_DENEGADO");
        await service.ApplyAsync(actor, request, preview.Fingerprint, "Nuevo requisito", default); db.ChangeTracker.Clear();
        var saved = await db.OperationalAssignments.AsNoTracking().SingleAsync(a => a.Id == pending.Id);
        Check(saved.Requirement == "COMENTARIO" && saved.StartsAt == pending.StartsAt && saved.DueAt == pending.DueAt, "metadata adoption keeps its valid dates");
        var auditCount = await db.AuditEvents.CountAsync();
        await service.ApplyAsync(actor, request, preview.Fingerprint, "Nuevo requisito", default);
        Check(await db.AuditEvents.CountAsync() == auditCount, "retry does not duplicate writes or audit events");
        Check(originalProtected == JsonSerializer.Serialize(await db.OperationalAssignments.AsNoTracking().Where(a => a.Id == submitted.Id || a.Id == closed.Id).OrderBy(a => a.Id).ToArrayAsync()), "all fields of deliveries and closures remain unchanged");
        Check(saved.ProgressUnits == 60 && await db.OperationalProgressEntries.AnyAsync(e => e.AssignmentId == pending.Id && e.Units == 60 && e.ActorId == collaborator) &&
            (await db.OperationalAttachments.SingleAsync(e => e.AssignmentId == pending.Id)).Content.SequenceEqual(new byte[] {1,2,3}), "advance, author and evidence bytes survive");
        Check((await tasks.LifecycleHistoryAsync(actor, pending.Id, default)).Any(e => e.Kind == "ACTUALIZACION"), "refresh appears in lifecycle history");
        definition = await db.OperationalTasks.AsNoTracking().SingleAsync(t => t.Id == definition.Id);
        definition.GoalUnits = 40; definition.LoadMinutes = 80;
        await tasks.SaveAsync(actor, definition, default); db.ChangeTracker.Clear();
        request = request with { Key = Guid.NewGuid() };
        preview = await service.PreviewAsync(actor, request, default);
        Check(preview.CanApply, "lower goal with existing advance can be scheduled: " + string.Join(";", preview.Conflicts));
        await service.ApplyAsync(actor, request, preview.Fingerprint, "Meta menor", default); db.ChangeTracker.Clear();
        saved = await db.OperationalAssignments.AsNoTracking().SingleAsync(a => a.Id == pending.Id);
        Check(saved.GoalUnits == 40 && saved.ProgressUnits == 60 && saved.RemainingMinutes == 0 && saved.CompletedAt == null, "lowering goal preserves excess advance without automatic delivery or negative workload");
        definition = await db.OperationalTasks.AsNoTracking().SingleAsync(t => t.Id == definition.Id); definition.ProgressUnit = "cajas";
        await tasks.SaveAsync(actor, definition, default); db.ChangeTracker.Clear();
        Check(!(await service.PreviewAsync(actor, request with { Key = Guid.NewGuid() }, default)).CanApply, "unit conversion with advance is an explicit conflict");
        definition = await db.OperationalTasks.AsNoTracking().SingleAsync(t => t.Id == definition.Id); definition.ProgressUnit = "piezas"; definition.AssignedRole = "DIRECCION";
        await tasks.SaveAsync(actor, definition, default); db.ChangeTracker.Clear();
        request = request with { Key = Guid.NewGuid() };
        preview = await service.PreviewAsync(actor, request, default);
        Check(preview.CanApply && preview.Differences.Single().After!.PersonId == directorPerson, "changed required role selects an eligible alternative");
        await service.ApplyAsync(actor, request, preview.Fingerprint, "Cambio de rol", default); db.ChangeTracker.Clear();
        Check(await db.Set<OperationalTransfer>().AnyAsync(t => t.AssignmentId == pending.Id && t.FromPersonId == person && t.ToPersonId == directorPerson), "responsible change records a transfer and preserves its assignment identity");
        definition = await db.OperationalTasks.AsNoTracking().SingleAsync(t => t.Id == definition.Id); definition.Description = "Cambió mientras revisaba";
        await tasks.SaveAsync(actor, definition, default); db.ChangeTracker.Clear();
        request = request with { Key = Guid.NewGuid() };
        preview = await service.PreviewAsync(actor, request, default);
        var concurrent = await db.OperationalAssignments.SingleAsync(a => a.Id == pending.Id); concurrent.Revision++;
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        await Reject(() => service.ApplyAsync(actor, request, preview.Fingerprint, "Vista vencida", default), "REFRESH_STALE");
        Check((await db.OperationalAssignments.AsNoTracking().SingleAsync(a => a.Id == pending.Id)).Description != definition.Description, "stale proposal writes nothing");
        definition = await db.OperationalTasks.AsNoTracking().SingleAsync(t => t.Id == definition.Id); definition.AssignedRole = "SUBCOORDINACION";
        await tasks.SaveAsync(actor, definition, default); db.ChangeTracker.Clear();
        Check(!(await service.PreviewAsync(actor, request with { Key = Guid.NewGuid() }, default)).CanApply, "no eligible recipient blocks the complete operation");
        await RecurrenceAsync(db, tasks, service, clock, actor, person);
        await ProcessAsync(db, tasks, service, clock, actor, person);
        Console.WriteLine("PASS assignment refresh lifecycle checks");
    }

    private static async Task RecurrenceAsync(SgolDbContext db, OperationalTaskService tasks, AssignmentRefreshService service, IClock clock, Guid actor, Guid person)
    {
        var day = WeekContract.LocalToday(clock.UtcNow);
        await tasks.SaveAsync(actor, new OperationalTask { Name = "Recurrencia actualizable", Revision = 0, Requirement = "NINGUNO", LoadMinutes = 20,
            OperationalCriticality = "FLEXIBLE", ScheduleMode = "DIAS_FIJOS", WeekDays = 1 << (int)day.DayOfWeek, StartHour = 14 }, default);
        var task = await db.OperationalTasks.AsNoTracking().SingleAsync(t => t.Name == "Recurrencia actualizable");
        var plan = DirectionPreparedPlan.ForOccasion(Guid.CreateVersion7(), BranchScope.LorettaId, actor, clock.UtcNow, "Recurrencia", day, day.AddDays(6)); db.Add(plan);
        var row = new OperationalAssignment { Id = Guid.CreateVersion7(), PlanId = plan.Id, TaskId = task.Id, TaskCode = task.TaskCode,
            PersonId = person, PersonName = "A", AssignedRole = task.AssignedRole, Requirement = task.Requirement };
        AssignmentDefinition.Adopt(row, task, null, false);
        var policy = await OperationalSchedulingPolicyService.CurrentAsync(db, default);
        OperationalAgenda.Capture(row, policy, task.OperationalCriticality, task.StartOn(day));
        db.Add(row); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        task.WeekDays |= 1 << (int)day.AddDays(3).DayOfWeek;
        task.DifferentPeople = true;
        await tasks.SaveAsync(actor, task, default); db.ChangeTracker.Clear();
        var request = new AssignmentRefreshRequest(task.Id, Guid.NewGuid(), clock.UtcNow.AddMinutes(3));
        var preview = await service.PreviewAsync(actor, request, default);
        Check(preview.CanApply && preview.Differences.Count(d => d.Before == null) == 1, "increased recurrence adds exactly one future occurrence: " + string.Join(";", preview.Conflicts));
        await service.ApplyAsync(actor, request, preview.Fingerprint, "Añadir jueves", default); db.ChangeTracker.Clear();
        Check(await db.OperationalAssignments.CountAsync(a => a.TaskId == task.Id) == 2 && !(await service.CountsAsync(actor, default)).ContainsKey(task.Id), "recurrence adoption clears differences without creating duplicates");
        Check((await db.OperationalAssignments.AsNoTracking().SingleAsync(a => a.Id == row.Id)).StartsAt == row.StartsAt, "adding repetitions preserves an existing valid activation");
        Check(await db.OperationalAssignments.Where(a => a.TaskId == task.Id).Select(a => a.PersonId).Distinct().CountAsync() == 2, "different-people preference distributes new repetitions among eligible workers");
        task = await db.OperationalTasks.AsNoTracking().SingleAsync(t => t.Id == task.Id); task.WeekDays = 1 << (int)day.DayOfWeek;
        await tasks.SaveAsync(actor, task, default); db.ChangeTracker.Clear();
        request = request with { Key = Guid.NewGuid() };
        preview = await service.PreviewAsync(actor, request, default);
        Check(preview.CanApply && preview.Differences.Count(d => d.After == null) == 1, "reduced recurrence proposes retirement of an unused occurrence");
        await service.ApplyAsync(actor, request, preview.Fingerprint, "Retirar jueves", default); db.ChangeTracker.Clear();
        Check(await db.OperationalAssignments.CountAsync(a => a.TaskId == task.Id) == 1 && await db.AuditEvents.AnyAsync(e => e.Action == "OPERATIONAL_ASSIGNMENT_RETIRED"), "retirement retains an immutable audit record");
        task = await db.OperationalTasks.AsNoTracking().SingleAsync(t => t.Id == task.Id); task.Description = "Después de avance concurrente";
        await tasks.SaveAsync(actor, task, default); db.ChangeTracker.Clear();
        var current = await db.OperationalAssignments.SingleAsync(a => a.Id == row.Id);
        current.TransferRemainingMinutes = 10; current.TransferProgressUnits = 0;
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        task = await db.OperationalTasks.AsNoTracking().SingleAsync(t => t.Id == task.Id); task.LoadMinutes = 30;
        await tasks.SaveAsync(actor, task, default); db.ChangeTracker.Clear();
        request = request with { Key = Guid.NewGuid() };
        Check(!(await service.PreviewAsync(actor, request, default)).CanApply, "manual remaining-work estimate requires explicit replacement");
        request = request with { ReplaceRemainingEstimate = true };
        preview = await service.PreviewAsync(actor, request, default);
        Check(preview.CanApply && preview.Differences.Single().After!.TransferRemainingMinutes == null, "explicit replacement calculates workload from the new definition");
        var carryover = new OperationalCarryoverRequest { Id = Guid.CreateVersion7(), BranchId = BranchScope.LorettaId, PersonId = person,
            RequestedBy = actor, IntentKey = Guid.NewGuid(), Cause = "FALTA_TIEMPO_OPERATIVO", CauseDescription = "Prueba", IncidentStart = clock.UtcNow.AddMinutes(-10),
            IncidentEnd = clock.UtcNow, Minutes = 10, SubmittedAt = clock.UtcNow,
            SnapshotJson = JsonSerializer.Serialize(new[] { new CarryoverItem(row.Id, person, plan.Id, current.Revision, row.Name, row.AssignedRole, row.StartsAt, row.DueAt, row.DueAt.AddMinutes(10), true, row.Id, null, "", "", "A") }) };
        db.Add(carryover); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        Check(!(await service.PreviewAsync(actor, request, default)).CanApply, "pending carryover prevents contradictory concurrent decisions");
        var requestRow = await db.Set<OperationalCarryoverRequest>().SingleAsync(r => r.Id == carryover.Id);
        requestRow.Revision++; requestRow.State = "APROBADA"; requestRow.DecidedBy = actor; requestRow.DecidedAt = clock.UtcNow; requestRow.DecisionComment = "Prueba autorizada";
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        Check(!(await service.PreviewAsync(actor, request, default)).CanApply, "approved carryover deadline requires explicit override");
        request = request with { ChangeAuthorizedDeadline = true };
        preview = await service.PreviewAsync(actor, request, default);
        Check(preview.CanApply, "authorized deadline replacement is reviewable: " + string.Join(";", preview.Conflicts));
        await service.ApplyAsync(actor, request, preview.Fingerprint, "Nuevo cierre autorizado", default); db.ChangeTracker.Clear();
        var timing = await new OperationalCarryoverReader(db).ForAsync(row.Id);
        Check(timing.RefreshCount > 0 && timing.ReferenceDue == row.DueAt && timing.Label(row.DueAt.AddMinutes(15), row.DueAt.AddMinutes(5)).Contains("plazo original"), "reporting retains original deadline and recognizes late delivery after rescheduling");
    }

    private static async Task ProcessAsync(SgolDbContext db, OperationalTaskService tasks, AssignmentRefreshService service, IClock clock, Guid actor, Guid person)
    {
        var sequence = new OperationalSequence { Id = Guid.CreateVersion7(), Name = "Proceso de actualización", Enabled = false }; db.Add(sequence); await db.SaveChangesAsync();
        var version = new OperationalSequenceVersion { Id = Guid.CreateVersion7(), SequenceId = sequence.Id, Number = 1, Name = sequence.Name, StartKind = "OCASIONAL",
            CreatedAt = clock.UtcNow, CreatedBy = actor }; db.Add(version); await db.SaveChangesAsync();
        await tasks.SaveAsync(actor, new OperationalTask { Name = "Etapa actualizable", Revision = 0, ProcessId = sequence.Id, Requirement = "COMENTARIO", OperationalCriticality = "FLEXIBLE" }, default);
        var task = await db.OperationalTasks.AsNoTracking().SingleAsync(t => t.Name == "Etapa actualizable");
        db.Add(new OperationalSequenceStep { VersionId = version.Id, Position = 1, TaskId = task.Id, ReleaseCondition = "INICIO", DeadlineHours = 24,
            TaskSnapshot = JsonSerializer.Serialize(task) });
        await db.SaveChangesAsync();
        version.PublishedAt = clock.UtcNow; version.PublishedBy = actor;
        sequence.PublishedVersionId = version.Id; sequence.Enabled = true;
        await db.SaveChangesAsync();
        var plan = DirectionPreparedPlan.ForOccasion(Guid.CreateVersion7(), BranchScope.LorettaId, actor, clock.UtcNow, "Proceso", WeekContract.LocalToday(clock.UtcNow), WeekContract.LocalToday(clock.UtcNow).AddDays(7)); db.Add(plan);
        var run = new OperationalSequenceRun { Id = Guid.CreateVersion7(), VersionId = version.Id, PlanId = plan.Id, StartKey = "refresh-check:" + Guid.NewGuid(), Name = "Proceso",
            CreatedAt = clock.UtcNow, CreatedBy = actor, PlannedStartsAt = clock.UtcNow.AddHours(2), UsesTaskEvidence = true }; db.Add(run);
        var stage = new OperationalSequenceStage { Id = Guid.CreateVersion7(), RunId = run.Id, VersionId = version.Id, Position = 1, TaskId = task.Id, TaskRevision = task.Revision,
            TaskCode = task.TaskCode, Name = task.Name, Role = task.AssignedRole, Requirement = task.Requirement, LoadMinutes = task.LoadMinutes, OperationalCriticality = task.OperationalCriticality,
            DefinitionSnapshotJson = AssignmentDefinition.Capture(task), ReservedPersonId = person, ReservedPersonName = "A", PlannedStartsAt = run.PlannedStartsAt, PlannedDueAt = run.PlannedStartsAt.AddHours(1) }; db.Add(stage);
        var attempt = new OperationalSequenceAttempt { Id = Guid.CreateVersion7(), RunId = run.Id, StageId = stage.Id, PlanId = plan.Id, Number = 1, Round = 1, CreatedAt = clock.UtcNow }; db.Add(attempt);
        await db.SaveChangesAsync(); stage.CurrentAttemptId = attempt.Id; await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var oldVersion = JsonSerializer.Serialize(await db.Set<OperationalSequenceVersion>().AsNoTracking().SingleAsync(v => v.Id == version.Id));
        task.Requirement = "NINGUNO"; task.Description = "Nueva instrucción";
        await tasks.SaveAsync(actor, task, default); db.ChangeTracker.Clear();
        Check((await service.CountsAsync(actor, default))[task.Id] == 1, "uninstantiated pending process stage enables refresh");
        var request = new AssignmentRefreshRequest(task.Id, Guid.NewGuid(), clock.UtcNow.AddMinutes(3));
        var preview = await service.PreviewAsync(actor, request, default);
        Check(preview.CanApply && preview.Differences.Single().ProcessStage, "process stage receives an explicit pending-parameter migration");
        await service.ApplyAsync(actor, request, preview.Fingerprint, "Actualizar etapa pendiente", default); db.ChangeTracker.Clear();
        Check((await db.Set<OperationalSequenceStage>().SingleAsync(s => s.Id == stage.Id)).Requirement == "NINGUNO" && !await db.OperationalAssignments.AnyAsync(a => a.TaskId == task.Id), "waiting stage updates without creating a fake executable assignment");
        Check(oldVersion == JsonSerializer.Serialize(await db.Set<OperationalSequenceVersion>().AsNoTracking().SingleAsync(v => v.Id == version.Id)), "published process version remains immutable");
        Check(await db.Set<OperationalSequenceEvent>().AnyAsync(e => e.StageId == stage.Id && e.EventKey.StartsWith("refresh:")), "process migration records an event with the current attempt");
        var activeStage = await db.Set<OperationalSequenceStage>().SingleAsync(s => s.Id == stage.Id);
        activeStage.PlannedStartsAt = clock.UtcNow; activeStage.PlannedDueAt = clock.UtcNow.AddHours(1);
        activeStage.EnabledAt = clock.UtcNow; activeStage.EligibleAt = clock.UtcNow;
        activeStage.EffectiveDueAt = activeStage.PlannedDueAt; activeStage.Status = "DISPONIBLE";
        var executable = OperationalProcessService.Reservation(activeStage, plan.Id); executable.Id = Guid.CreateVersion7(); db.Add(executable);
        var activeAttempt = await db.Set<OperationalSequenceAttempt>().SingleAsync(a => a.Id == attempt.Id);
        activeAttempt.AssignmentId = executable.Id; activeAttempt.Status = "ACTIVO";
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        task = await db.OperationalTasks.AsNoTracking().SingleAsync(t => t.Id == task.Id); task.Requirement = "DOCUMENTO";
        await tasks.SaveAsync(actor, task, default); db.ChangeTracker.Clear();
        request = request with { Key = Guid.NewGuid() };
        preview = await service.PreviewAsync(actor, request, default);
        Check(preview.CanApply && preview.Differences.Single().Before!.Id == executable.Id, "active process attempt preserves executable assignment identity");
        await service.ApplyAsync(actor, request, preview.Fingerprint, "Actualizar evidencia de intento activo", default); db.ChangeTracker.Clear();
        Check((await db.OperationalAssignments.SingleAsync(a => a.Id == executable.Id)).Requirement == "DOCUMENTO" &&
            (await db.Set<OperationalSequenceStage>().SingleAsync(s => s.Id == stage.Id)).Requirement == "DOCUMENTO", "active assignment and stage adopt the same evidence requirement atomically");
        task = await db.OperationalTasks.AsNoTracking().SingleAsync(t => t.Id == task.Id); task.LoadMinutes += 5;
        await tasks.SaveAsync(actor, task, default); db.ChangeTracker.Clear();
        request = request with { Key = Guid.NewGuid() };
        preview = await service.PreviewAsync(actor, request, default);
        Check(preview.CanApply, "active stage can adopt calculated agenda without breaking its attempt: " + string.Join(";", preview.Conflicts));
        await service.ApplyAsync(actor, request, preview.Fingerprint, "Actualizar carga de etapa", default); db.ChangeTracker.Clear();
        var finalStage = await db.Set<OperationalSequenceStage>().SingleAsync(s => s.Id == stage.Id);
        var finalAssignment = await db.OperationalAssignments.SingleAsync(a => a.Id == executable.Id);
        Check(finalStage.SchedulingPolicyId == finalAssignment.SchedulingPolicyId && finalStage.ClosureMarginPercent == finalAssignment.ClosureMarginPercent &&
            finalStage.EffectiveDueAt == finalAssignment.DueAt && finalStage.CurrentAttemptId == attempt.Id,
            "process scheduling policy, deadline and current attempt remain coherent");
    }
}
