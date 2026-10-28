using Sgol.Planning.Contracts;
using System.Text.Json;

namespace Sgol.Web.Infrastructure.Persistence.Planning;

public sealed partial class OperationalCapacityService
{
    private sealed record CapacityPlacementChoice(CapacityWorker Worker, SchedulingPlacement Placement, CapacityMove[] Reflow);

    // Only future, unprotected movable assignments are proposed here. Every change is
    // returned in the existing confirmation, never written during this simulation.
    private static CapacityPlacementChoice? WithTargetReflow(CapacityLedger ledger, CapacityWorker worker, OperationalAssignment incoming,
        DateTimeOffset now, DateTimeOffset notBefore, HashSet<Guid> protectedIds)
    {
        if (incoming.SchedulingPolicyId == null || incoming.GoalUnits > 0 || !ledger.CanFit(worker.Id, incoming)) return null;
        var day = WeekContract.LocalToday(incoming.StartsAt);
        var priority = OperationalAgenda.Priority(incoming.OperationalCriticality);
        var peers = ledger.Tasks.Where(t => t.TransferRemainingMinutes == null && t.PersonId == worker.Id && t.Id != incoming.Id && t.SchedulingPolicyId != null && t.CompletedAt == null &&
            t.GoalUnits == 0 && t.ProgressUnits == 0 && t.ScheduleMode != "PROGRESO" && t.OccasionId == null && !protectedIds.Contains(t.Id) && t.StartsAt > now &&
            WeekContract.LocalToday(t.StartsAt) == day && t.OperationalCriticality != "CRITICA" &&
            (ledger.Temporal.UsesBaseSchedule(incoming) && ledger.Temporal.UsesBaseSchedule(t) || OperationalAgenda.Priority(t.OperationalCriticality) > priority || OperationalAgenda.Priority(t.OperationalCriticality) == priority && t.StartsAt >= incoming.StartsAt))
            .OrderBy(t => OperationalAgenda.Priority(t.OperationalCriticality)).ThenBy(t => t.StartsAt).ThenBy(t => t.TaskCode, StringComparer.Ordinal).ThenBy(t => t.Id).ToArray();
        if (peers.Length == 0) return null;
        var ids = peers.Select(t => t.Id).ToHashSet();
        CapacityLedger State(OperationalAssignment[] tasks) => new(ledger.Workers, tasks, ledger.Capacities, ledger.Unavailable, ledger.Legacy, ledger.Today)
            { Temporal = ledger.Temporal, PlannedChanges = ledger.PlannedChanges, Override = ledger.Override };
        var state = State(ledger.Tasks.Where(t => !ids.Contains(t.Id)).ToArray());
        var placement = OperationalAgenda.Find(state, worker.Id, incoming, notBefore);
        if (!placement.Eligible) return null;
        OperationalAssignment Copy(OperationalAssignment task) => JsonSerializer.Deserialize<OperationalAssignment>(JsonSerializer.Serialize(task))!;
        if (ledger.Temporal.UsesBaseSchedule(incoming))
        {
            var projectedIncoming = Copy(incoming); projectedIncoming.PersonId = worker.Id; projectedIncoming.AssignedRole = worker.Role;
            var fixedIds = peers.Select(t => t.Id).Append(incoming.Id).ToHashSet();
            var fixedState = State(ledger.Tasks.Where(t => !fixedIds.Contains(t.Id)).ToArray());
            try
            {
                var agenda = OperationalAgendaSearch.SolveFixed(fixedState, peers.Append(projectedIncoming), notBefore, default);
                var placedIncoming = agenda.Single(t => t.Id == incoming.Id);
                var reflow = agenda.Where(t => t.Id != incoming.Id).Select(t => {
                    var previous = peers.Single(p => p.Id == t.Id);
                    return new CapacityMove(t.Id, t.Name, t.PersonName, worker.Id, t.PersonName, t.RemainingMinutes, t.StartsAt, t.DueAt) {
                        PreviousStartsAt = previous.StartsAt, PreviousDueAt = previous.DueAt, OperationalCriticality = t.OperationalCriticality };
                }).Where(m => m.StartsAt != m.PreviousStartsAt || m.DueAt != m.PreviousDueAt).ToArray();
                return new(worker, new(true, placedIncoming.StartsAt, placedIncoming.DueAt, "ELIGIBLE"), reflow);
            }
            catch (DirectionPlanException e) when (e.Code is "SCHEDULING_INFEASIBLE" or "SCHEDULING_LEGACY_RESERVATION") { return null; }
        }
        var projected = Copy(incoming); projected.PersonId = worker.Id; OperationalAgenda.Apply(projected, placement);
        if (!state.CanAssign(worker.Id, projected, projected.StartsAt <= now ? now : null)) return null;
        state = State(state.Tasks.Append(projected).ToArray());
        var moves = new List<CapacityMove>();
        foreach (var peer in peers)
        {
            var next = Copy(peer);
            var shifted = OperationalAgenda.Find(state, worker.Id, next, new[] { peer.StartsAt, notBefore }.Max());
            if (!shifted.Eligible) return null;
            OperationalAgenda.Apply(next, shifted);
            if (!state.CanAssign(worker.Id, next, next.StartsAt <= now ? now : null)) return null;
            if (next.StartsAt != peer.StartsAt || next.DueAt != peer.DueAt)
                moves.Add(new(peer.Id, peer.Name, peer.PersonName, worker.Id, peer.PersonName, peer.RemainingMinutes, next.StartsAt, next.DueAt) {
                    PreviousStartsAt = peer.StartsAt, PreviousDueAt = peer.DueAt, OperationalCriticality = peer.OperationalCriticality });
            state = State(state.Tasks.Append(next).ToArray());
        }
        return new(worker, placement, moves.ToArray());
    }
}
