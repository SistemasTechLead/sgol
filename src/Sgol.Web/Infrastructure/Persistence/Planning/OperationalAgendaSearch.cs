using System.Text.Json;
using Sgol.Planning.Contracts;

namespace Sgol.Web.Infrastructure.Persistence.Planning;

// Both limits are deterministic and enter the preparation fingerprint.
public static class OperationalAgendaSearch
{
    public const int Version = 7;
    public const int Budget = 100000;
    public const int CandidateBudget = 2000000;

    public static string Diagnostic(DirectionPlanException error) => (error.Data["Reason"] as string) switch
    {
        "OCCURRENCE_SEARCH_SIZE_LIMIT" => $"La vista previa expande {error.Data["Occurrences"]} ocurrencias y supera el límite de búsqueda de {error.Data["MaximumSearchOccurrences"] ?? 1000}. Reduce el periodo o las tareas seleccionadas. Ampliar el presupuesto no cambia este límite; no se guardó trabajo parcial.",
        "COMPONENT_SIZE_LIMIT" => $"Un grupo de tareas relacionadas contiene {error.Data["Occurrences"]} ocurrencias y supera el límite de 1000 por grupo. Reduce el alcance del plan; no se guardó trabajo parcial.",
        "NODE_BUDGET" => $"Se agotó el presupuesto de {error.Data["Budget"]} nodos de búsqueda. No se demostró falta de capacidad; revisa las restricciones o reintenta la vista previa.",
        "CANDIDATE_BUDGET" => $"Se agotó el presupuesto de {error.Data["Budget"]} candidatos. No se demostró falta de capacidad; reduce el periodo o revisa las restricciones antes de reintentar.",
        _ => TemporalEligibilityContext.Message(error.Code)
    };

    // Explicit repair: fixed calendar and recipients. The caller determines the authorized,
    // unprotected set. Inputs are cloned, so failure or an exhausted budget has no side effects.
    public static OperationalAssignment[] SolveFixed(CapacityLedger initial, IEnumerable<OperationalAssignment> input,
        DateTimeOffset notBefore, CancellationToken token, int budget = Budget)
    {
        using var timing = Sgol.Web.Infrastructure.Http.PerformanceWork.Measure("planning.fixed-search");
        token.ThrowIfCancellationRequested();
        var rows = input.Select(t => JsonSerializer.Deserialize<OperationalAssignment>(JsonSerializer.Serialize(t))!).ToArray();
        var jobs = rows.OrderBy(t => OperationalAgenda.Priority(t.OperationalCriticality))
            .ThenBy(t => t.OperationalCriticality == "IMPORTANTE" ? t.NominalStartsAt : DateTimeOffset.MinValue)
            .ThenByDescending(t => t.LoadMinutes).ThenBy(t => t.TaskCode, StringComparer.Ordinal)
            .ThenBy(t => t.NominalStartsAt).ThenBy(t => t.ScheduleKey, StringComparer.Ordinal).ToArray();
        var ids = rows.Select(t => t.Id).ToHashSet();
        var baseline = new CapacityLedger(initial.Workers, initial.Tasks.Where(t => !ids.Contains(t.Id)).ToArray(), initial.Capacities,
            initial.Unavailable, initial.Legacy, initial.Today) { Temporal = initial.Temporal, PlannedChanges = initial.PlannedChanges, Override = initial.Override };
        foreach (var row in rows.Where(t => t.SchedulingPolicyId != null && t.GoalUnits == 0 && t.ScheduleMode != "PROGRESO"))
        {
            var day = WeekContract.LocalToday(row.NominalStartsAt ?? row.StartsAt);
            if (baseline.SchedulingPeers(row.PersonId, day).Any(t => t.SchedulingPolicyId == null && t.CompletedAt == null &&
                t.StartsAt < OperationalSchedule.Instant(day.AddDays(1)) && t.DueAt > OperationalSchedule.Instant(day)) ||
                day == baseline.Today && baseline.Legacy.GetValueOrDefault(row.PersonId) > 0)
                throw new DirectionPlanException("SCHEDULING_LEGACY_RESERVATION", 422);
        }
        var components = new List<List<OperationalAssignment>>();
        var unassigned = jobs.ToList();
        var footprints = jobs.ToDictionary(t => t.Id, t => baseline.Footprint(t).Select(f => f.Day).ToHashSet());
        bool Connected(OperationalAssignment a, OperationalAssignment b) =>
            a.PersonId == b.PersonId && footprints[a.Id].Overlaps(footprints[b.Id]) ||
            WeekContract.LocalToday(a.StartsAt) == WeekContract.LocalToday(b.StartsAt) && OperationalAgenda.CashCircuit.Any(r =>
                a.TaskCode == r.Before && b.TaskCode == r.After || a.TaskCode == r.After && b.TaskCode == r.Before);
        while (unassigned.Count > 0)
        {
            token.ThrowIfCancellationRequested();
            var component = new List<OperationalAssignment> { unassigned[0] }; unassigned.RemoveAt(0);
            for (var i = 0; i < component.Count; i++)
                foreach (var row in unassigned.Where(t => Connected(component[i], t)).ToArray()) { component.Add(row); unassigned.Remove(row); }
            components.Add(component);
        }
        var explored = 0;
        var candidates = 0;
        IEnumerable<SchedulingPlacement> Choices(CapacityLedger state, OperationalAssignment task)
        {
            if (!state.Workers.Any(w => w.Id == task.PersonId && w.EligibleFor(task.AssignedRole, task.SpecializationId))) yield break;
            foreach (var slot in OperationalAgenda.Alternatives(state, task.PersonId, task, notBefore))
            {
                token.ThrowIfCancellationRequested();
                if (++candidates > CandidateBudget) throw new DirectionPlanException("SCHEDULING_SEARCH_INCONCLUSIVE", 422) { Data = { ["Reason"] = "CANDIDATE_BUDGET", ["Candidates"] = candidates, ["Budget"] = CandidateBudget } };
                OperationalAgenda.Apply(task, slot);
                if (state.CanAssign(task.PersonId, task)) yield return slot;
            }
        }
        bool Search(CapacityLedger state, int index)
        {
            token.ThrowIfCancellationRequested();
            if (index == jobs.Length) return true;
            if (++explored > budget) throw new DirectionPlanException("SCHEDULING_SEARCH_INCONCLUSIVE", 422) { Data = { ["Reason"] = "NODE_BUDGET", ["Explored"] = explored, ["Budget"] = budget } };
            var task = jobs[index];
            var slots = Choices(state, task).OrderBy(p => task.OperationalCriticality == "IMPORTANTE" ?
                (p.StartsAt - task.NominalStartsAt!.Value).TotalMinutes : Math.Abs((p.StartsAt - task.NominalStartsAt!.Value).TotalMinutes)).ToArray();
            foreach (var slot in slots)
            {
                OperationalAgenda.Apply(task, slot);
                var next = state.WithAdded(task);
                // Propagate every remaining domain before descending. A flexible that already
                // fits may still have to move to preserve a long obligation's only window.
                if (jobs.Skip(index + 1).Any(t => !Choices(next, t).Any())) continue;
                if (Search(next, index + 1)) return true;
            }
            return false;
        }
        foreach (var component in components)
        {
            if (component.Count > 1000) throw new DirectionPlanException("SCHEDULING_SEARCH_INCONCLUSIVE", 422) { Data = { ["Reason"] = "COMPONENT_SIZE_LIMIT", ["Occurrences"] = component.Count } };
            jobs = component.OrderBy(t => OperationalAgenda.Priority(t.OperationalCriticality))
                .ThenBy(t => t.OperationalCriticality == "IMPORTANTE" ? t.NominalStartsAt : DateTimeOffset.MinValue)
                .ThenByDescending(t => t.LoadMinutes).ThenBy(t => t.TaskCode, StringComparer.Ordinal).ThenBy(t => t.NominalStartsAt).ThenBy(t => t.ScheduleKey, StringComparer.Ordinal).ToArray();
            if (!Search(baseline, 0)) throw new DirectionPlanException("SCHEDULING_INFEASIBLE", 422);
            foreach (var row in jobs) baseline = baseline.WithAdded(row);
        }
        return rows;
    }
}
