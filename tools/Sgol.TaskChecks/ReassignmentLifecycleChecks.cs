using Sgol.Identity.Contracts;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Sgol.BuildingBlocks.Time;
using Sgol.BuildingBlocks.Identifiers;
using Sgol.Planning.Contracts;
using Sgol.Web.Infrastructure.Persistence;
using Sgol.Web.Infrastructure.Persistence.Auditing;
using Sgol.Web.Infrastructure.Persistence.Configuration;
using Sgol.Web.Infrastructure.Persistence.Planning;

internal static class ReassignmentLifecycleChecks
{
    private sealed class TestClock : IClock { public DateTimeOffset UtcNow { get; set; } = new(2026, 10, 22, 19, 0, 0, TimeSpan.Zero); }
    private static void Require(bool value, string message) { if (!value) throw new Exception(message); }

    // Synthetic lifecycle fragments belong only to this guarded test database.
    public static async Task Run(SgolDbContext db)
    {
        var fixture=await QaFixtures.CreateAsync(db);
        var clock = new TestClock();
        var direction = new EfDirectionPreparedPlanService(db, new AuditTransaction(db), clock, new Uuid7Generator(clock), new DirectionPlanningCatalogReader(db));
        var service = new OperationalSupervisionService(db, clock, direction);
        var actor = fixture.Director;
        await ViriSimulation(db, service, actor, clock);
        clock.UtcNow = new(2026, 10, 22, 19, 0, 0, TimeSpan.Zero);
        var seeded=await SourceAsync(db,fixture,clock,10);
        var id=seeded.Id;
        var source=await db.OperationalAssignments.SingleAsync(a=>a.Id==id);
        source.Requirement = "COMENTARIO"; source.CompletedAt = clock.UtcNow.AddMinutes(-30);
        source.CompletedBy = actor; source.CompletionKind = "ENTREGA"; source.Comment = "Entrega anterior de prueba";
        source.ReviewStatus = "PENDIENTE_REVISION";
        var definition = await db.OperationalTasks.SingleAsync(t => t.Id == source.TaskId);
        definition.Requirement = "FOTOGRAFIA"; definition.Description = "Nueva evidencia de la agenda";
        definition.MinimumPhotos = 1; definition.MaximumPhotos = 2; definition.Revision++;
        var block = new OperationalAssignment {
            Id = Guid.CreateVersion7(), PlanId = source.PlanId, TaskId = source.TaskId, TaskCode = source.TaskCode,
            Name = "Reserva de prueba", Requirement = "NINGUNO", PersonId = source.PersonId, PersonName = source.PersonName,
            AssignedRole = source.AssignedRole, StartsAt = clock.UtcNow, DueAt = clock.UtcNow.AddMinutes(20), LoadMinutes = 15,
            SchedulingPolicyId = source.SchedulingPolicyId, ClosureMarginPercent = source.ClosureMarginPercent,
            NominalStartsAt = clock.UtcNow, OperationalCriticality = "IMPORTANTE"
        };
        db.OperationalAssignments.Add(block); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var before = await db.OperationalAssignments.AsNoTracking().OrderBy(a => a.Id).ToArrayAsync();
        source = before.Single(a => a.Id == id);
        var correctionId = await service.ReviewAsync(actor, id, source.Revision, "MISMA_PERSONA", "Subir foto", default);
        db.ChangeTracker.Clear();
        var old = await db.OperationalAssignments.AsNoTracking().SingleAsync(a => a.Id == id);
        var correction = await db.OperationalAssignments.AsNoTracking().SingleAsync(a => a.ReassignedFromId == id);
        Require(correctionId == correction.Id, "Review returns the new correction for navigation");
        Require(correction.PersonId == source.PersonId && correction.StartsAt == clock.UtcNow && correction.DueAt > correction.StartsAt, "Correction starts immediately despite an existing reservation");
        Require(correction.DueAt - correction.StartsAt == TimeSpan.FromMinutes(15), "Preserve ten minute workload and fifty percent margin");
        Require(correction.Requirement == "FOTOGRAFIA" && correction.MinimumPhotos == 1 && correction.MaximumPhotos == 2 && correction.Description == definition.Description && correction.TaskRevision == definition.Revision, "Correction uses current evidence contract");
        Require(correction.CompletedAt == null && correction.Comment == null && correction.ReviewedAt == null && correction.ReassignmentComment == "Subir foto", "New correction has independent lifecycle");
        Require(old.Requirement == "COMENTARIO" && old.Comment == source.Comment && old.CompletedAt == source.CompletedAt && old.StartsAt == source.StartsAt && old.DueAt == source.DueAt && old.ReviewStatus == "REASIGNADA", "Original delivery and execution window preserved");
        var after = await db.OperationalAssignments.AsNoTracking().OrderBy(a => a.Id).ToArrayAsync();
        Require(JsonSerializer.Serialize(before.Where(a => a.Id != id)) == JsonSerializer.Serialize(after.Where(a => a.Id != id && a.Id != correction.Id)), "Unrelated assignments byte for byte unchanged");
        Require(await db.AuditEvents.AnyAsync(e => e.Action == "OPERATIONAL_CORRECTION_ASSIGNED" && e.ResourceId == correction.Id), "Correction audited");
        var count = after.Length; var audits = await db.AuditEvents.CountAsync();
        try { await service.ReviewAsync(actor, id, source.Revision, "MISMA_PERSONA", "Repetir", default); throw new Exception("Stale review accepted"); }
        catch (DirectionPlanException e) { Require(e.Code == "TASK_CONFLICT", "Stale request rejects duplicate corrections"); }
        db.ChangeTracker.Clear();
        clock.UtcNow = OperationalSchedule.Instant(new DateOnly(2026, 10, 22), 23).AddMinutes(55);
        try { await service.ReviewAsync(actor, correction.Id, correction.Revision, "MISMA_PERSONA", "Sin turno", default); throw new Exception("Out of shift correction accepted"); }
        catch (DirectionPlanException e) { Require(e.Code is "SCHEDULE_NOT_PRESENT_AT_ACTIVATION" or "SCHEDULE_NOT_PRESENT_AT_RECEPTION", "Specific shift conflict instead of generic capacity message: " + e.Code); }
        db.ChangeTracker.Clear();
        Require(count == await db.OperationalAssignments.CountAsync() && audits == await db.AuditEvents.CountAsync(), "Failed/stale requests leave no assignments or audit writes");
        var unchanged = await db.OperationalAssignments.AsNoTracking().SingleAsync(a => a.Id == correction.Id);
        Require(JsonSerializer.Serialize(unchanged) == JsonSerializer.Serialize(correction), "Failure preserves lifecycle and dates");
        clock.UtcNow = correction.StartsAt.AddMinutes(1);
        await service.ReviewAsync(actor, correction.Id, correction.Revision, "ROL", "Revisar evidencia", default);
        db.ChangeTracker.Clear();
        var next = await db.OperationalAssignments.AsNoTracking().SingleAsync(a => a.ReassignedFromId == correction.Id);
        var ledger = await OperationalCapacityService.LedgerAsync(db, clock.UtcNow, default);
        Require(ledger.Workers.Any(w => w.Id == next.PersonId && w.EligibleFor(next.AssignedRole, next.SpecializationId)), "Role reassignment keeps qualification");
        Require(next.StartsAt >= clock.UtcNow && next.Requirement == "FOTOGRAFIA", "Role path also creates fresh correction with current requirement");
        // An older assignment without agenda policy must also get a real slot,
        // rather than carrying its historical deadline into the correction.
        clock.UtcNow = next.StartsAt.AddMinutes(1);
        var legacy = await db.OperationalAssignments.SingleAsync(a => a.Id == next.Id);
        legacy.SchedulingPolicyId = null; legacy.ClosureMarginPercent = null; legacy.NominalStartsAt = null;
        legacy.StartsAt = clock.UtcNow.AddHours(-1); legacy.DueAt = clock.UtcNow.AddMinutes(-30);
        legacy.CompletedAt = clock.UtcNow.AddMinutes(-20); legacy.CompletedBy = actor;
        legacy.CompletionKind = "ENTREGA"; legacy.ReviewStatus = "PENDIENTE_REVISION"; legacy.Revision++;
        await db.SaveChangesAsync(); var legacyRevision = legacy.Revision; db.ChangeTracker.Clear();
        await service.ReviewAsync(actor, next.Id, legacyRevision, "MISMA_PERSONA", "Corregir entrega anterior", default);
        db.ChangeTracker.Clear();
        var current = await OperationalSchedulingPolicyService.CurrentAsync(db, default);
        var legacyCorrection = await db.OperationalAssignments.AsNoTracking().SingleAsync(a => a.ReassignedFromId == next.Id);
        var legacyOriginal = await db.OperationalAssignments.AsNoTracking().SingleAsync(a => a.Id == next.Id);
        Require(legacyCorrection.SchedulingPolicyId == current.Id && legacyCorrection.StartsAt >= clock.UtcNow && legacyCorrection.DueAt > legacyCorrection.StartsAt, "Legacy correction adopts a current valid agenda");
        Require(legacyOriginal.SchedulingPolicyId == null && legacyOriginal.DueAt < clock.UtcNow, "Legacy delivery keeps its original policy and dates");
        Console.WriteLine("PASS reassignment: expired delivery, same recipient, occupied slot, current evidence, original history, unrelated commitments, audits, stale request, shift failure rollback and role path.");
        Console.WriteLine("PASS legacy reassignment: fresh scheduling policy, workload margin, future slot and preserved original contract.");
    }

    private static async Task<OperationalAssignment> SourceAsync(SgolDbContext db,QaFixtures.Actors fixture,TestClock clock,int load)
    {
        var definition=await db.OperationalTasks.SingleAsync(t=>t.Id==fixture.Definition);definition.LoadMinutes=load;
        var row=new OperationalAssignment {Id=Guid.CreateVersion7(),PlanId=fixture.Plan,TaskId=definition.Id,TaskCode=definition.TaskCode,TaskRevision=definition.Revision,Name="Synthetic historical delivery",PersonId=fixture.Person,PersonName="QA worker",AssignedRole=CanonicalRole.SalesFloor,Requirement="COMENTARIO",Comment="Historical evidence",LoadMinutes=load,CompletedAt=clock.UtcNow.AddMinutes(-30),CompletedBy=fixture.Worker,CompletionKind="ENTREGA",ReviewStatus="PENDIENTE_REVISION"};
        OperationalAgenda.Capture(row,await OperationalSchedulingPolicyService.CurrentAsync(db,default),"FLEXIBLE",clock.UtcNow.AddHours(-2));
        db.Add(row);await db.SaveChangesAsync();db.ChangeTracker.Clear();return row;
    }

    private static async Task ViriSimulation(SgolDbContext db, OperationalSupervisionService service, Guid actor, TestClock clock)
    {
        var viriFixture=await QaFixtures.CreateAsync(db,1140);
        clock.UtcNow=TemporalEligibilityContext.At(new DateOnly(2026,10,22),17*60+41);
        var source=await SourceAsync(db,viriFixture,clock,40);var id=source.Id;
        var before = await db.OperationalAssignments.AsNoTracking().OrderBy(a => a.Id).ToArrayAsync();
        var ledger = await OperationalCapacityService.LedgerAsync(db, clock.UtcNow, default);
        var projected = JsonSerializer.Deserialize<OperationalAssignment>(JsonSerializer.Serialize(source))!;
        projected.Id = Guid.NewGuid();
        OperationalAgenda.Capture(projected, await OperationalSchedulingPolicyService.CurrentAsync(db, default),
            (await db.OperationalTasks.SingleAsync(t => t.Id == source.TaskId)).OperationalCriticality, clock.UtcNow);
        var previous = OperationalAgenda.Find(ledger, source.PersonId, projected, clock.UtcNow);
        Console.WriteLine($"VIRI baseline at 17:41: eligible={previous.Eligible}, reason={previous.Reason}");
        await service.ReviewAsync(actor, source.Id, source.Revision, "MISMA_PERSONA", "Aclarar evidencia de Manet", default);
        db.ChangeTracker.Clear();
        var correction = await db.OperationalAssignments.AsNoTracking().SingleAsync(a => a.ReassignedFromId == id);
        Require(correction.PersonId == source.PersonId && correction.StartsAt == clock.UtcNow && correction.DueAt == clock.UtcNow.AddMinutes(60), "Viri 40+50% starts at 17:41 and ends at 18:41");
        var after = await db.OperationalAssignments.AsNoTracking().OrderBy(a => a.Id).ToArrayAsync();
        Require(JsonSerializer.Serialize(before.Where(a => a.Id != id)) == JsonSerializer.Serialize(after.Where(a => a.Id != id && a.Id != correction.Id)), "Viri: unrelated assignments unchanged");
        Console.WriteLine("PASS historical scenario: synthetic recipient and task, 17:41-18:41, 40+50%, current agenda and unchanged unrelated assignments.");

        // Fill the entire remaining shift and set zero capacity, in the test copy only.
        var day = WeekContract.LocalToday(clock.UtcNow);
        var saturated = new OperationalAssignment { Id = Guid.CreateVersion7(), PlanId = source.PlanId, TaskId = source.TaskId,
            TaskCode = source.TaskCode, Name = "Simulación: turno lleno", Requirement = "NINGUNO", PersonId = source.PersonId,
            PersonName = source.PersonName, AssignedRole = source.AssignedRole, LoadMinutes = 200,
            StartsAt = clock.UtcNow, DueAt = TemporalEligibilityContext.At(day, 19 * 60),
            SchedulingPolicyId = correction.SchedulingPolicyId, ClosureMarginPercent = correction.ClosureMarginPercent,
            NominalStartsAt = clock.UtcNow, OperationalCriticality = "CRITICA" };
        db.OperationalAssignments.Add(saturated);
        await OperationalCapacityService.StoreSettingAsync(db, new(source.PersonId, day, day, 0, 1, "Simulación: capacidad cero"), default);
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        clock.UtcNow = TemporalEligibilityContext.At(day, 18 * 60 + 55);
        await service.ReviewAsync(actor, correction.Id, correction.Revision, "MISMA_PERSONA", "Aclaración con turno lleno", default);
        db.ChangeTracker.Clear();
        var capped = await db.OperationalAssignments.AsNoTracking().SingleAsync(a => a.ReassignedFromId == correction.Id);
        Require(capped.StartsAt == clock.UtcNow && capped.DueAt == TemporalEligibilityContext.At(day, 19 * 60), "Full agenda and zero capacity still receive correction; deadline capped at 19:00");
        var capacityService = new OperationalCapacityService(db, clock, service);
        var adjustment = await capacityService.PreviewAsync(actor, new(source.PersonId, day, day, 0, 1,
            "Simulación: no mover correcciones", true,
            capped.OperationalCriticality != "CRITICA" && capped.OccasionId == null ? [capped.Id] : []), default);
        Require(!adjustment.Moves.Any(m => m.TaskId == capped.Id), "Capacity adjustment cannot move an evidence correction, even if explicitly selected among activated tasks");
        Console.WriteLine("PASS capacity adjustment preserves the correction's activation and deadline.");
        var snapshot = JsonSerializer.Serialize(capped); var audits = await db.AuditEvents.CountAsync();
        clock.UtcNow = capped.DueAt;
        try { await service.ReviewAsync(actor, capped.Id, capped.Revision, "MISMA_PERSONA", "En salida", default); throw new Exception("Exit accepted"); }
        catch (DirectionPlanException e) { Require(e.Code is "SCHEDULE_NOT_PRESENT_AT_ACTIVATION" or "SCHEDULE_NOT_PRESENT_AT_RECEPTION", "Exact exit rejects receipt"); }
        db.ChangeTracker.Clear();
        Require(audits == await db.AuditEvents.CountAsync() && snapshot == JsonSerializer.Serialize(await db.OperationalAssignments.AsNoTracking().SingleAsync(a => a.Id == capped.Id)), "Exact exit rejection rolls back completely");
        Console.WriteLine("PASS Viri: full shift, zero capacity, immediate 18:55 activation, deadline capped to 19:00, exact exit rejection and rollback.");

        foreach (var minute in new[] { 13 * 60 + 25, 14 * 60 })
        {
            var fixture = JsonSerializer.Deserialize<OperationalAssignment>(JsonSerializer.Serialize(source))!;
            fixture.Id = Guid.CreateVersion7(); fixture.ReassignedFromId = null; fixture.ScheduleKey = null;
            db.OperationalAssignments.Add(fixture); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
            clock.UtcNow = TemporalEligibilityContext.At(day, minute);
            if (minute == 14 * 60)
            {
                try { await service.ReviewAsync(actor, fixture.Id, fixture.Revision, "MISMA_PERSONA", "En descanso", default); throw new Exception("Break accepted"); }
                catch (DirectionPlanException e) { Require(e.Code is "SCHEDULE_NOT_PRESENT_AT_ACTIVATION" or "SCHEDULE_NOT_PRESENT_AT_RECEPTION", "Break rejects receipt"); }
                db.ChangeTracker.Clear();
                Require(!await db.OperationalAssignments.AnyAsync(a => a.ReassignedFromId == fixture.Id), "Break creates no partial correction");
            }
            else
            {
                await service.ReviewAsync(actor, fixture.Id, fixture.Revision, "MISMA_PERSONA", "Antes del descanso", default);
                db.ChangeTracker.Clear();
                var lunch = await db.OperationalAssignments.AsNoTracking().SingleAsync(a => a.ReassignedFromId == fixture.Id);
                Require(lunch.StartsAt == clock.UtcNow && lunch.DueAt == TemporalEligibilityContext.At(day, 13 * 60 + 30), "Correction stops at break, never crosses into rest");
            }
        }
        Console.WriteLine("PASS Viri: 13:25-13:30 correction capped to break; 14:00 receipt rejected without partial correction.");
    }
}
