using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Sgol.BuildingBlocks.Time;
using Sgol.BuildingBlocks.Identifiers;
using Sgol.Planning.Contracts;
using Sgol.Web.Infrastructure.Persistence;
using Sgol.Web.Infrastructure.Persistence.Auditing;
using Sgol.Web.Infrastructure.Persistence.Configuration;
using Sgol.Web.Infrastructure.Persistence.Planning;

internal static class TransferChecks
{
    private sealed class TestClock : IClock { public DateTimeOffset UtcNow { get; set; } = TemporalEligibilityContext.At(new(2026, 10, 24), 10 * 60); }
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    public static async Task Run(SgolDbContext db)
    {
        var fixture=await QaFixtures.CreateAsync(db);
        var clock = new TestClock(); var day = WeekContract.LocalToday(clock.UtcNow);
        var direction = new EfDirectionPreparedPlanService(db, new AuditTransaction(db), clock, new Uuid7Generator(clock), new DirectionPlanningCatalogReader(db));
        var service = new OperationalSupervisionService(db, clock, direction);
        var actor = fixture.Director;
        var workers = (await OperationalCapacityService.WorkersAsync(db, clock.UtcNow, default)).Where(w => w.Id==fixture.Person || w.Id==fixture.OtherPerson).GroupBy(w => w.Id).Where(g => g.Count() == 1).Select(g => g.Single()).Take(2).ToArray();
        Check(workers.Length == 2, "Need two independently owned synthetic workers");
        var from = workers[0]; var to = workers[1];
        var fromUser = await db.AppUsers.Where(u => u.PersonId == from.Id).Select(u => u.Id).SingleAsync();
        var toUser = await db.AppUsers.Where(u => u.PersonId == to.Id).Select(u => u.Id).SingleAsync();
        var state = await db.Set<TemporalAssignmentState>().SingleAsync(s => s.Id == 1); state.Enabled = true;
        // Explicit all-day test availability is written exclusively to the guarded test copy.
        foreach (var w in workers) {
            var exception = await db.Set<PersonalScheduleException>().SingleOrDefaultAsync(s => s.PersonId == w.Id && s.Day == day);
            if (exception == null) { exception = new() { Id = Guid.CreateVersion7(), PersonId = w.Id, Day = day, Revision = 1, ChangedBy = actor, ChangedAt = clock.UtcNow, IntentKey = Guid.NewGuid(), Reason = "Isolated transfer test" }; db.Add(exception); }
            exception.IntervalsJson = JsonSerializer.Serialize(new[] { new WorkInterval(9 * 60, 13 * 60), new WorkInterval(14 * 60, 18 * 60) });
            foreach (var a in await db.AvailabilityDayVersions.Where(a => a.PersonId == w.Id && a.LocalDate == day).ToArrayAsync()) db.Entry(a).Property(x => x.IsAvailable).CurrentValue = true;
        }
        var definition = await db.OperationalTasks.SingleAsync(t=>t.Id==fixture.Definition);
        var plan = await db.DirectionPreparedPlans.SingleAsync(p=>p.Id==fixture.Plan);
        var source = new OperationalAssignment { Id = Guid.CreateVersion7(), PlanId = plan.Id, TaskId = definition.Id,
            TaskCode = definition.TaskCode, Name = "Prueba aislada: transferencia 60 de 100", Requirement = "NINGUNO", AssignedRole = from.Role,
            PersonId = from.Id, PersonName = from.Name, LoadMinutes = 100, GoalUnits = 100, ProgressUnits = 60,
            StartsAt = clock.UtcNow.AddHours(-2), DueAt = clock.UtcNow.AddHours(-1), ProgressUnit = "unidades" };
        var block = new OperationalAssignment { Id = Guid.CreateVersion7(), PlanId = plan.Id, TaskId = definition.Id,
            TaskCode = definition.TaskCode, Name = "Reserva que no debe moverse", Requirement = "NINGUNO", AssignedRole = to.Role,
            PersonId = to.Id, PersonName = to.Name, LoadMinutes = 240, StartsAt = clock.UtcNow, DueAt = clock.UtcNow.AddHours(2) };
        db.AddRange(source, block);
        db.Add(new OperationalAttachment { AssignmentId = source.Id, FileName = "evidencia-previa.pdf", ContentType = "application/pdf", Content = [1, 2, 3, 4], CreatedAt = clock.UtcNow.AddHours(-1) });
        db.Add(new OperationalProgressEntry { Id = Guid.CreateVersion7(), AssignmentId = source.Id, ActorId = fromUser, Units = 60, Note = "Trabajo previo", OccurredAt = clock.UtcNow.AddHours(-1) });
        await OperationalCapacityService.StoreSettingAsync(db, new(to.Id, day, day, 0, 240, "Prueba de sobrecarga"), default);
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var request = new TransferRequest(source.Id, source.Revision, to.Id, 30, clock.UtcNow.AddHours(1), "Ausencia operativa del responsable");
        var transfersBefore = await db.Set<OperationalTransfer>().CountAsync();
        var before = await db.OperationalAssignments.AsNoTracking().OrderBy(a => a.Id).ToArrayAsync();
        var preview = await service.PreviewTransferAsync(actor, request, default);
        Check(preview.LoadAfter > preview.Available && preview.Overlaps.Contains(block.Name), "Preview warns of overload and overlaps");
        Check(await db.Set<OperationalTransfer>().CountAsync()==transfersBefore, "Preview must not persist a transfer");
        // Concurrent advancement invalidates the preview, without side effects.
        var concurrent = await db.OperationalAssignments.SingleAsync(a => a.Id == source.Id); concurrent.Revision++;
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        await Reject(() => service.TransferAsync(actor, request, preview.Fingerprint, default), "TRANSFER_STALE");
        request = request with { Revision = concurrent.Revision };
        preview = await service.PreviewTransferAsync(actor, request, default);
        await service.TransferAsync(actor, request, preview.Fingerprint, default); db.ChangeTracker.Clear();
        var moved = await db.OperationalAssignments.AsNoTracking().SingleAsync(a => a.Id == source.Id);
        Check(moved.PersonId == to.Id && moved.ProgressUnits == 60 && moved.GoalUnits == 100 && moved.RemainingMinutes == 30,
            "Transfer preserves cumulative goal and exact supervisor estimate without double discount");
        Check(moved.CompletedAt == null && moved.ReviewStatus == null && moved.LoadMinutes == 100, "Transfer does not fabricate delivery/review or rewrite original load");
        var receipt = await db.Set<OperationalTransfer>().SingleAsync(t => t.AssignmentId == moved.Id);
        Check(receipt.PreviousDueAt == source.DueAt && receipt.PreviousStartsAt == source.StartsAt && receipt.ProgressUnits == 60 && receipt.ActorId == actor, "Receipt preserves original dates, progress and decision author");
        Check(await db.OperationalProgressEntries.AnyAsync(e => e.AssignmentId == source.Id && e.ActorId == fromUser && e.Units == 60), "Original units remain attributed to original author");
        Check((await db.OperationalAttachments.SingleAsync(a => a.AssignmentId == source.Id)).Content.SequenceEqual(new byte[] { 1, 2, 3, 4 }), "Existing evidence bytes preserved");
        var after = await db.OperationalAssignments.AsNoTracking().OrderBy(a => a.Id).ToArrayAsync();
        Check(JsonSerializer.Serialize(before.Where(a => a.Id != source.Id)) == JsonSerializer.Serialize(after.Where(a => a.Id != source.Id)), "All unrelated assignments remain identical");
        Check(await db.AuditEvents.AnyAsync(e => e.Action == "OPERATIONAL_TASK_TRANSFERRED" && e.ResourceId == source.Id), "Transfer audited");
        await Reject(() => service.TransferAsync(actor, request, preview.Fingerprint, default), "TRANSFER_STALE");
        db.ChangeTracker.Clear();
        // Remaining effort declines from the handoff baseline: 60->80 units uses half of 30, not 20% of 30.
        moved.ProgressUnits = 80; Check(moved.RemainingMinutes == 15, "Remaining effort follows post-transfer units");
        moved.ProgressUnits = 100; Check(moved.RemainingMinutes == 0, "Finished goal has no remaining load");
        var next = new TransferRequest(source.Id, (await db.OperationalAssignments.AsNoTracking().SingleAsync(a => a.Id == source.Id)).Revision,
            from.Id, 20, clock.UtcNow.AddHours(1), "Segunda transferencia");
        await Reject(() => service.PreviewTransferAsync(actor, next with { RecipientId = to.Id }, default), "TRANSFER_RECIPIENT");
        await Reject(() => service.PreviewTransferAsync(actor, next with { DueAt = clock.UtcNow.AddHours(3.5) }, default), "TRANSFER_DEADLINE");
        var afterBreak = await service.PreviewTransferAsync(actor, next with { DueAt = clock.UtcNow.AddHours(5) }, default);
        Check(afterBreak.ShiftMinutesBeforeDue == 240, "A later shift interval is allowed without counting the rest break as working time");
        clock.UtcNow = TemporalEligibilityContext.At(day, 13 * 60 + 30);
        await Reject(() => service.PreviewTransferAsync(actor, next with { DueAt = clock.UtcNow.AddMinutes(20) }, default), "SCHEDULE_NOT_PRESENT_AT_ACTIVATION");
        clock.UtcNow = TemporalEligibilityContext.At(day, 10 * 60);
        var pending = await db.OperationalAssignments.SingleAsync(a => a.Id == source.Id); pending.CompletedAt = clock.UtcNow; pending.ReviewStatus = "PENDIENTE_REVISION"; pending.Revision++;
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        await Reject(() => service.PreviewTransferAsync(actor, next with { Revision = pending.Revision }, default), "TRANSFER_STATE");
        await Reject(() => service.PreviewTransferAsync(toUser, next, default), "ACCESO_DENEGADO");
        Check(await db.Set<OperationalTransfer>().CountAsync() == transfersBefore+1, "Rejected requests never create handoffs");
        var tasks = new OperationalTaskService(db, direction, clock, new Sgol.Evidence.Technical.FileTechnicalValidator(), new CheckScanner());
        Check((await tasks.ContributionsAsync(actor, source.Id, default)).Single().PersonId == from.Id, "SQL contribution reader attributes preserved units");
        Check((await tasks.MyTransfersAsync(fromUser, default)).Any(t => t.AssignmentId == source.Id), "Previous owner retains transfer history");
        Check((await tasks.LifecycleHistoryAsync(actor, source.Id, default)).Any(e => e.Kind == "TRANSFERENCIA"), "Supervisor can read transfer lifecycle");
        await Process(db, service, actor, from, to, plan.Id, definition, clock);
        Console.WriteLine("PASS transfer: overdue 60/100 task, explicit recipient, immediate handoff, immutable original dates/progress authors, exact 30-minute pending estimate, overload and overlap preview, unchanged unrelated commitments, audit, stale and duplicate protection, same-person rejection, shift and break limits, pending-review exclusion, worker authorization.");
    }
    private static async Task Process(SgolDbContext db, OperationalSupervisionService service, Guid actor,
        CapacityWorker from, CapacityWorker to, Guid plan, OperationalTask definition, TestClock clock)
    {
        db.ChangeTracker.Clear();
        var root = new OperationalSequence { Id = Guid.CreateVersion7(), Name = "Prueba aislada de transferencia de etapa" };
        var version = new OperationalSequenceVersion { Id = Guid.CreateVersion7(), SequenceId = root.Id, Number = 1,
            Name = root.Name, StartKind = "OCASIONAL", CreatedBy = actor, CreatedAt = clock.UtcNow };
        var nextDefinition=new OperationalTask {Id=Guid.CreateVersion7(),TaskCode="QA-"+Guid.NewGuid().ToString("N")[..12],Name="QA downstream",Requirement="NINGUNO",OperationalCriticality="FLEXIBLE",LoadMinutes=20};
        db.AddRange(root, version,nextDefinition);
        db.AddRange(new OperationalSequenceStep { VersionId = version.Id, Position = 1, TaskId = definition.Id, ReleaseCondition = "INICIO", DeadlineHours = 1 },
            new OperationalSequenceStep { VersionId = version.Id, Position = 2, TaskId = nextDefinition.Id, DeadlineHours = 1 });
        await db.SaveChangesAsync();
        var run = new OperationalSequenceRun { Id = Guid.CreateVersion7(), VersionId = version.Id, PlanId = plan,
            Name = root.Name, StartKey = Guid.NewGuid().ToString(), CreatedBy = actor, CreatedAt = clock.UtcNow, PlannedStartsAt = clock.UtcNow };
        var stage = new OperationalSequenceStage { Id = Guid.CreateVersion7(), RunId = run.Id, VersionId = version.Id, Position = 1,
            TaskId = definition.Id, TaskRevision = 1, TaskCode = definition.TaskCode, Name = "Etapa seleccionada", Role = from.Role,
            Requirement = "NINGUNO", LoadMinutes = 20, PlannedStartsAt = clock.UtcNow, PlannedDueAt = clock.UtcNow.AddHours(1), ReservedPersonId = from.Id, ReservedPersonName = from.Name };
        var downstream = new OperationalSequenceStage { Id = Guid.CreateVersion7(), RunId = run.Id, VersionId = version.Id, Position = 2,
            TaskId = nextDefinition.Id, TaskRevision = 1, TaskCode = nextDefinition.TaskCode, Name = "Etapa que sigue esperando", Role = from.Role,
            Requirement = "NINGUNO", LoadMinutes = 20, PlannedStartsAt = clock.UtcNow.AddHours(1), PlannedDueAt = clock.UtcNow.AddHours(2), ReservedPersonId = from.Id, ReservedPersonName = from.Name };
        db.AddRange(run, stage, downstream); await db.SaveChangesAsync();
        var assignment = new OperationalAssignment { Id = Guid.CreateVersion7(), PlanId = plan, TaskId = definition.Id, TaskCode = definition.TaskCode,
            Name = stage.Name, Requirement = "NINGUNO", AssignedRole = from.Role, LoadMinutes = 20, PersonId = from.Id, PersonName = from.Name,
            StartsAt = clock.UtcNow, DueAt = clock.UtcNow.AddHours(1) };
        var attempt = new OperationalSequenceAttempt { Id = Guid.CreateVersion7(), RunId = run.Id, StageId = stage.Id,
            PlanId = plan, Number = 1, Round = 1, Status = "ACTIVO", AssignmentId = assignment.Id, CreatedAt = clock.UtcNow };
        db.AddRange(assignment, attempt); await db.SaveChangesAsync();
        var enabled = new OperationalSequenceEvent { Id = Guid.CreateVersion7(), RunId = run.Id, StageId = stage.Id, AttemptId = attempt.Id,
            Kind = "INICIO", EventKey = Guid.NewGuid().ToString(), ActorId = actor, OccurredAt = clock.UtcNow };
        db.Add(enabled); await db.SaveChangesAsync();
        stage.CurrentAttemptId = attempt.Id; stage.Status = "DISPONIBLE"; stage.EnabledAt = stage.EligibleAt = stage.DependencySatisfiedAt = clock.UtcNow;
        stage.DependencyEventId = enabled.Id; stage.EffectiveDueAt = assignment.DueAt;
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var before = JsonSerializer.Serialize(await db.Set<OperationalSequenceStage>().AsNoTracking().SingleAsync(s => s.Id == downstream.Id));
        var request = new TransferRequest(assignment.Id, assignment.Revision, to.Id, 20, clock.UtcNow.AddMinutes(45), "Transferir solo etapa activa");
        var preview = await service.PreviewTransferAsync(actor, request, default);
        await service.TransferAsync(actor, request, preview.Fingerprint, default); db.ChangeTracker.Clear();
        var after = await db.Set<OperationalSequenceStage>().AsNoTracking().SingleAsync(s => s.Id == stage.Id);
        Check(after.ReservedPersonId == to.Id && after.CurrentAttemptId == attempt.Id && after.DependencyEventId == enabled.Id && after.Status == "DISPONIBLE", "Active stage handoff preserves attempt and dependency");
        Check(before == JsonSerializer.Serialize(await db.Set<OperationalSequenceStage>().AsNoTracking().SingleAsync(s => s.Id == downstream.Id)), "No downstream activation, reassignment or rescheduling");
        var current = await db.OperationalAssignments.AsNoTracking().SingleAsync(a => a.Id == assignment.Id);
        var changedStage = await db.Set<OperationalSequenceStage>().SingleAsync(s => s.Id == stage.Id); changedStage.Status = "CANCELADA";
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        await Reject(() => service.PreviewTransferAsync(actor, request with { Revision = current.Revision, RecipientId = from.Id }, default), "TRANSFER_PROCESS_STATE");
        Console.WriteLine("PASS process transfer: active stage only; same attempt, evidence link and dependency; downstream stage byte-for-byte unchanged; rejected stale process state.");
    }
    private static async Task Reject(Func<Task> action, string code)
    {
        try { await action(); throw new Exception("Expected " + code); }
        catch (DirectionPlanException e) { Check(e.Code == code, $"Expected {code}, got {e.Code}"); }
    }
}
