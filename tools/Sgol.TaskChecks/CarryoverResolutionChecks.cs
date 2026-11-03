using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Sgol.Planning.Contracts;
using Sgol.Web.Infrastructure.Persistence;
using Sgol.Web.Infrastructure.Persistence.Planning;

internal static class CarryoverResolutionChecks
{
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); Console.WriteLine("PASS resolution: " + message); }
    public static async Task Run(SgolDbContext db, bool ui)
    {
        Check(!db.Database.HasPendingModelChanges(), "model matches migration snapshot");
        var historical = JsonSerializer.Serialize(await db.Set<OperationalCarryoverRequest>().AsNoTracking().OrderBy(r => r.Id).ToArrayAsync());
        await db.Database.MigrateAsync();
        Check(historical == JsonSerializer.Serialize(await db.Set<OperationalCarryoverRequest>().AsNoTracking().OrderBy(r => r.Id).ToArrayAsync()), "repeated migration preserves old requests");
        var fixture = await QaFixtures.CreateAsync(db);
        var clock = new CorrectionSchedulingChecks.Clock { UtcNow = TemporalEligibilityContext.At(new(2026,11,3), 810) };
        var service = new OperationalCarryoverService(db, clock, CorrectionSchedulingChecks.Service(db, clock));
        var existing = await db.Set<OperationalCarryoverRequest>().AsNoTracking().Where(r => r.State == "PENDIENTE" && r.Kind == "POSPONER").ToArrayAsync();
        foreach (var request in existing.Where(r => r.PendingItems().Any(i => i.Task.StartsWith("TAR-0379")))) {
            var item = request.PendingItems().First(i => i.Task.StartsWith("TAR-0379"));
            var assignment = await db.OperationalAssignments.AsNoTracking().SingleAsync(a => a.Id == item.AssignmentId);
            var diagnostic = await service.PreviewDecisionAsync(fixture.Director, new(request.Id, item.AssignmentId, assignment.Revision, "APPROVE", null, null, "", Guid.Empty), default);
            Check(diagnostic.Blocks.Any(b => b.Title == "Cruce de horario" && b.Detail.Contains("09:30–10:15") && b.Detail.Contains("Dar seguimiento a pedidos")), "historical request identifies the actual overlapping task and time");
            Check(diagnostic.Blocks.All(b => !b.Detail.Contains(item.Task) && !b.Detail.Contains("10/11/2026")), "card diagnosis does not repeat current task or proposed date");
        }
        var rows = new List<OperationalAssignment>();
        for (var n = 0; n < 5; n++) {
            var row = await CorrectionSchedulingChecks.Source(db, fixture, clock);
            row.Name = $"Resolución QA {n + 1}"; row.CompletedAt = null; row.CompletedBy = null; row.CompletionKind = null; row.ReviewStatus = null;
            db.Update(row); await db.SaveChangesAsync(); db.ChangeTracker.Clear(); rows.Add(row);
        }
        var preview = await service.PreviewPostponementAsync(fixture.Worker, rows.Select(r => r.Id).ToArray(), "ERROR_DE_ASIGNACION", "Aún no se me capacita. Prueba aislada de resolución por tarea.", default);
        Check(preview.Conflicts.Length == 0, "real producer generates multi-task proposal: " + string.Join("; ", preview.Conflicts));
        var requestId = await service.SubmitPostponementAsync(fixture.Worker, rows.Select(r => r.Id).ToArray(), "ERROR_DE_ASIGNACION", preview.ExplanationFallback(), preview.Fingerprint, Guid.NewGuid(), default);
        // Explanation is part of the producer fingerprint.
        async Task<OperationalCarryoverRequest> Request() => await db.Set<OperationalCarryoverRequest>().AsNoTracking().SingleAsync(r => r.Id == requestId);
        CarryoverDecision Command(int index, string action, Guid? recipient = null, DateOnly? day = null) => new(requestId, rows[index].Id, rows[index].Revision, action, recipient, day, "Decisión QA conservada", Guid.NewGuid());
        async Task<CarryoverDecisionPreview> Preview(CarryoverDecision command) {
            var p = await service.PreviewDecisionAsync(fixture.Director, command, default);
            Check(p.Conflicts.Length == 0, command.Action + " preview: " + string.Join("; ", p.Conflicts)); return p;
        }
        async Task Resolve(CarryoverDecision command) { var p = await Preview(command); await service.ResolveItemAsync(fixture.Director, command, p.Fingerprint, default); db.ChangeTracker.Clear(); }
        var approve = Command(0, "APPROVE"); var approval = await Preview(approve);
        await service.ResolveItemAsync(fixture.Director, approve, approval.Fingerprint, default);
        await service.ResolveItemAsync(fixture.Director, approve, approval.Fingerprint, default); db.ChangeTracker.Clear();
        Check((await Request()).PendingItems().Length == 4 && (await Request()).Results().Length == 1, "approval and identical retry resolve one item only");
        var rejected = Command(1, "REJECT"); await Resolve(rejected);
        Check((await db.OperationalAssignments.AsNoTracking().SingleAsync(a => a.Id == rows[1].Id)).DueAt == rows[1].DueAt, "rejection preserves obligation and dates");
        var day = preview.From;
        var outside = await service.PreviewDecisionAsync(fixture.Director, Command(2, "RESCHEDULE", day: preview.Through.AddDays(1)), default);
        Check(outside.Conflicts.Any(), "outside authorized window is blocked");
        var invalidRecipient = await service.PreviewDecisionAsync(fixture.Director, Command(2, "REASSIGN", fixture.Person, day), default);
        Check(invalidRecipient.Conflicts.Any(), "same or ineligible recipient is blocked");
        var capacityLimit = new OperationalCapacity { Id = Guid.CreateVersion7(), PersonId = fixture.OtherPerson, From = day, Through = day, Percent = 100, DailyMinutes = 1, Reason = "Isolated capacity test" };
        db.Add(capacityLimit); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var noCapacity = await service.PreviewDecisionAsync(fixture.Director, Command(2, "REASSIGN", fixture.OtherPerson, day), default);
        Check(noCapacity.Conflicts.Any(), "insufficient recipient capacity cannot be overridden");
        Check(noCapacity.Blocks.Count(b => b.Title == "Capacidad insuficiente") == 1, "capacity conflict has one structured numeric explanation");
        db.Remove(capacityLimit); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var lastSlot = new OperationalCapacity { Id = Guid.CreateVersion7(), PersonId = fixture.OtherPerson, From = day, Through = day, Percent = 100, DailyMinutes = 10, Reason = "Isolated last slot" };
        db.Add(lastSlot); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var firstChoice = Command(2, "REASSIGN", fixture.OtherPerson, day);
        var competingChoice = Command(3, "REASSIGN", fixture.OtherPerson, day);
        var firstSlot = await Preview(firstChoice); var competingSlot = await Preview(competingChoice);
        await service.ResolveItemAsync(fixture.Director, firstChoice, firstSlot.Fingerprint, default); db.ChangeTracker.Clear();
        try { await service.ResolveItemAsync(fixture.Director, competingChoice, competingSlot.Fingerprint, default); throw new Exception("last slot double booked"); }
        catch (DirectionPlanException e) { Check(e.Code == "ARRASTRE_CONFLICTO", "two previews competing for last capacity slot cannot both apply"); }
        db.Remove(lastSlot); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        Check(await db.Set<OperationalTransfer>().AnyAsync(t => t.AssignmentId == rows[2].Id && t.FromPersonId == fixture.Person && t.ToPersonId == fixture.OtherPerson), "reassignment records receipt and original author snapshot");
        Check(!(await Request()).Recognizes(rows[2].Id), "reassignment does not grant carryover recognition");
        await Resolve(Command(3, "RESCHEDULE", day: day));
        var last = Command(4, "RESCHEDULE", day: day); var stale = await Preview(last);
        var current = await db.OperationalAssignments.SingleAsync(a => a.Id == rows[4].Id); current.Comment = "Concurrent edit"; current.Revision++; await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        try { await service.ResolveItemAsync(fixture.Director, last, stale.Fingerprint, default); throw new Exception("stale preview accepted"); } catch (DirectionPlanException e) { Check(e.Code == "ARRASTRE_CONFLICTO", "stale preview leaves item pending"); }
        var fresh = last with { Revision = current.Revision, Action = "REJECT", Day = null };
        await Resolve(fresh);
        Check((await Request()).State == "RESUELTA" && (await Request()).Results().Length == 5, "mixed request closes only after all items resolved");
        var timing = await new OperationalCarryoverReader(db).ForAsync(rows[0].Id);
        Check(timing.ApprovedCount == 1 && !timing.Pending, "reader counts per-item approval");
        timing = await new OperationalCarryoverReader(db).ForAsync(rows[2].Id);
        Check(timing.ApprovedCount == 0 && !timing.Pending, "reader excludes reassignment from approvals");
        await service.RestorePostponementAsync(fixture.Director, requestId, [rows[3].Id], "Restauración QA explícita", default);
        Check((await db.OperationalAssignments.AsNoTracking().SingleAsync(a => a.Id == rows[3].Id)).DueAt == rows[3].DueAt, "individual reprogramming can be restored without changing mixed request");
        var denied = Command(0, "REJECT");
        try { await service.PreviewDecisionAsync(fixture.Worker, denied, default); throw new Exception("self approval accepted"); } catch (DirectionPlanException e) { Check(e.Status == 403, "self resolution denied"); }
        // Another genuine producer request remains for HTTP/browser validation.
        for (var n = 0; n < 2; n++) {
            var row = await CorrectionSchedulingChecks.Source(db, fixture, clock);
            row.Name = $"Resolución QA formulario {n + 1}"; row.CompletedAt = null; row.CompletedBy = null; row.CompletionKind = null; row.ReviewStatus = null;
            db.Update(row); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        }
        var eligible = await service.OverdueAsync(fixture.Worker, default);
        var selected = eligible.Where(x => x.Blocked == null).Select(x => x.Assignment.Id).ToArray();
        var next = await service.PreviewPostponementAsync(fixture.Worker, selected, "ERROR_DE_ASIGNACION", "Aún no se me capacita. Revisión desde formulario real.", default);
        Check(next.Conflicts.Length == 0, "second producer preview");
        var nextId = await service.SubmitPostponementAsync(fixture.Worker, selected, "ERROR_DE_ASIGNACION", "Aún no se me capacita. Revisión desde formulario real.", next.Fingerprint, Guid.NewGuid(), default);
        var delivered = await db.OperationalAssignments.SingleAsync(a => a.Id == selected[0]);
        delivered.CompletedAt = clock.UtcNow; delivered.CompletedBy = fixture.Worker; delivered.CompletionKind = "ENTREGA"; delivered.ReviewStatus = "PENDIENTE_REVISION"; delivered.Revision++;
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var close = new CarryoverDecision(nextId, delivered.Id, delivered.Revision, "APPROVE", null, null, "Entregada durante la espera", Guid.NewGuid());
        var closedPreview = await service.PreviewDecisionAsync(fixture.Director, close, default);
        Check(closedPreview.Conflicts.Length == 0 && closedPreview.Proposed == null, "delivery during wait closes without moving assignment");
        await service.ResolveItemAsync(fixture.Director, close, closedPreview.Fingerprint, default); db.ChangeTracker.Clear();
        Check((await db.OperationalAssignments.AsNoTracking().SingleAsync(a => a.Id == delivered.Id)).DueAt == delivered.DueAt, "delivered dates preserved");
        if (ui) await CarryoverResolutionHttpChecks.Run(db, fixture, clock);
    }
    private static string ExplanationFallback(this PostponementPreview _) => "Aún no se me capacita. Prueba aislada de resolución por tarea.";
}
