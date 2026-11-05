using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Sgol.Planning.Contracts;

namespace Sgol.Web.Infrastructure.Persistence.Planning;

public sealed partial class ProcessPlanningService
{
    public async Task RescheduleAutomaticAsync(Guid actor, Guid planId, Guid startId, long revision, DateOnly day, CancellationToken token)
    {
        await direction.AuthorizeAsync(actor, token);
        await using var tx = await db.Database.BeginTransactionAsync(token);
        await OperationalTaskService.LockAsync(db, token);
        var start = await db.Set<PreparedProcessStart>().SingleOrDefaultAsync(s => s.Id == startId && s.PlanId == planId && s.Automatic, token)
            ?? throw Error("PROCESS_NOT_FOUND", 404);
        if (start.Status != "PENDIENTE" || start.Revision != revision || start.IncidenceHoldId != null) throw Error("PROCESS_CONFLICT", 409);
        var plan = await db.DirectionPreparedPlans.SingleAsync(p => p.Id == start.PlanId, token);
        if (day < WeekContract.LocalToday(clock.UtcNow) || day > plan.EndsOn) throw RescheduleError("Elige una fecha desde hoy hasta el final del calendario. Si el calendario terminó, prepara una ocasión nueva.");
        var from = OperationalSchedule.Instant(day); var through = from.AddDays(1);
        if (await db.Set<PreparedProcessStart>().AnyAsync(s => s.ProcessId == start.ProcessId && s.Id != start.Id && s.StartsAt >= from && s.StartsAt < through, token) ||
            await db.Set<OperationalSequenceRun>().AnyAsync(r => r.StartKey.StartsWith($"C:{start.ProcessId:N}:") && r.PlannedStartsAt >= from && r.PlannedStartsAt < through, token))
            throw RescheduleError("Este proceso ya tiene una ejecución en esa fecha. Elige otro día para no duplicarla.");
        var before = start.StartsAt;
        start.OriginalStartsAt ??= before;
        start.StartsAt = from.Add(before - OperationalSchedule.Instant(WeekContract.LocalToday(before)));
        start.LastAssignmentAttemptAt = null;
        start.PendingReason = "Fecha reprogramada. Pendiente de comprobar responsables y capacidad."; start.Revision++;
        Audit(actor, start.PlanId, "PROCESS_AUTOMATIC_DATE_RESCHEDULED", new { StartId = start.Id, From = before, Through = start.StartsAt, start.OriginalStartsAt });
        await db.SaveChangesAsync(token); await tx.CommitAsync(token);
    }
    private static DirectionPlanException RescheduleError(string message) => new("PROCESS_CONFLICT", 422) { Data = { ["CountConflict"] = message } };
    public async Task<(Guid Id, string Name)[]> AutomaticPlansAsync(Guid actor, Guid processId, CancellationToken token)
    {
        await TaskCatalogAuthorization.RequireReadAsync(db, actor, clock.UtcNow, token);
        var plans = await db.DirectionPreparedPlans.AsNoTracking()
            .Where(p => db.Set<PreparedProcessStart>().Any(s => s.PlanId == p.Id && s.ProcessId == processId && s.Automatic))
            .OrderByDescending(p => p.CreatedAt).Select(p => new { p.Id, p.Name }).ToArrayAsync(token);
        return plans.Select(p => (p.Id, p.Name)).ToArray();
    }
    public async Task RetryAutomaticAsync(Guid actor, Guid planId, CancellationToken token)
    {
        await direction.AuthorizeAsync(actor, token);
        await AssignAutomaticAsync(token, planId, actor);
    }

    // Each date commits independently. A conflicting date never rolls back the
    // other dates, and a failed start leaves no partial run, attempts or workload.
    internal async Task AssignAutomaticAsync(CancellationToken token, Guid? planId = null, Guid? actor = null)
    {
        var retryBefore = clock.UtcNow.AddMinutes(-5);
        var ids = await db.Set<PreparedProcessStart>().AsNoTracking()
            .Where(s => s.Automatic && s.Status == "PENDIENTE" && s.IncidenceHoldId == null &&
                (planId == null || s.PlanId == planId) &&
                (actor != null || s.LastAssignmentAttemptAt == null || s.LastAssignmentAttemptAt < retryBefore) &&
                db.Set<OperationalSequence>().Any(p => p.Id == s.ProcessId && p.Enabled && p.DeletedAt == null))
            .OrderBy(s => s.StartsAt).Select(s => s.Id).ToArrayAsync(token);
        foreach (var id in ids)
        {
            await using var tx = await db.Database.BeginTransactionAsync(token);
            await OperationalTaskService.LockAsync(db, token);
            var start = await db.Set<PreparedProcessStart>().SingleOrDefaultAsync(s => s.Id == id, token);
            if (start == null || start.Status != "PENDIENTE" || start.IncidenceHoldId != null ||
                !await db.Set<OperationalSequence>().AnyAsync(p => p.Id == start.ProcessId && p.Enabled && p.DeletedAt == null, token)) {
                await tx.CommitAsync(token); continue;
            }
            if (WeekContract.LocalToday(start.StartsAt) < WeekContract.LocalToday(clock.UtcNow)) {
                start.PendingReason = "La fecha prevista ya pasó. Reprograma esta fecha manualmente; no se asignará trabajo atrasado automáticamente.";
                start.LastAssignmentAttemptAt = clock.UtcNow; start.Revision++;
                await db.SaveChangesAsync(token); await tx.CommitAsync(token); db.ChangeTracker.Clear(); continue;
            }
            await tx.CreateSavepointAsync("automatic_date", token);
            try
            {
                var plan = await db.DirectionPreparedPlans.SingleAsync(p => p.Id == start.PlanId, token);
                var assigner = actor ?? plan.CreatedBy;
                await direction.AuthorizeAsync(assigner, token);
                var version = await db.Set<OperationalSequenceVersion>().AsNoTracking().SingleAsync(v => v.Id == start.VersionId, token);
                var steps = await db.Set<OperationalSequenceStep>().AsNoTracking().Where(s => s.VersionId == start.VersionId).OrderBy(s => s.Position).ToArrayAsync(token);
                var tasks = steps.Select(s => JsonSerializer.Deserialize<OperationalTask>(s.TaskSnapshot!)!).ToArray();
                var ledger = await OperationalCapacityService.LedgerAsync(db, clock.UtcNow, token);
                var policy = plan.SchedulingPolicyId is {} policyId
                    ? await db.Set<OperationalSchedulingPolicy>().AsNoTracking().SingleAsync(p => p.Id == policyId, token) : null;
                var input = new ProcessPlanInput { Key = start.Id, People = steps.ToDictionary(s => s.Position, _ => Guid.Empty) };
                var people = ResolvePeople(input, version.Id, steps, tasks, [start.StartsAt], ledger, policy, clock.UtcNow, token);
                var agenda = Reservations(start.Id, version.Id, start.StartsAt, people, steps, policy, ledger, clock.UtcNow).ToArray();
                foreach (var reservation in agenda) {
                    RequireReservation(ledger, reservation, steps.Single(s => s.TaskId == reservation.TaskId));
                    ledger = ledger.WithAdded(reservation);
                }
                start.Participants = JsonSerializer.Serialize(people);
                start.AgendaJson = policy == null ? null : JsonSerializer.Serialize(agenda);
                start.Status = "ACTIVADO"; start.PendingReason = null; start.LastAssignmentAttemptAt = clock.UtcNow; start.Revision++;
                await db.SaveChangesAsync(token);
                var engine = new OperationalProcessService(db, direction, clock, validator, scanner);
                var runId = await engine.StartAsync(assigner, start.ProcessId, start.Id, start.StartsAt, people, token, start.PlanId, start.VersionId, start.StartKey);
                Audit(assigner, start.PlanId, "PROCESS_AUTOMATIC_DATE_ASSIGNED", new { StartId = start.Id, RunId = runId });
                await db.SaveChangesAsync(token);
            }
            catch (DirectionPlanException error)
            {
                await tx.RollbackToSavepointAsync("automatic_date", token);
                db.ChangeTracker.Clear();
                start = await db.Set<PreparedProcessStart>().SingleAsync(s => s.Id == id, token);
                var message = Sgol.Web.Pages.Planning.ProcessesModel.PlanError(error);
                start.PendingReason = message.Length > 2000 ? message[..2000] : message;
                start.LastAssignmentAttemptAt = clock.UtcNow; start.Revision++;
                await db.SaveChangesAsync(token);
            }
            await tx.CommitAsync(token);
            db.ChangeTracker.Clear();
        }
    }
}
