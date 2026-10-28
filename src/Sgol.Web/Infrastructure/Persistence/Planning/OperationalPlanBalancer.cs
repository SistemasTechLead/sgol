using Sgol.Planning.Contracts;

namespace Sgol.Web.Infrastructure.Persistence.Planning;

// Only changes recipients of newly generated work, never its dates or saved assignments.
internal static class OperationalPlanBalancer
{
    public const int PolicyVersion = 3;

    public static void Balance(CapacityLedger ledger, IReadOnlyList<OperationalAssignment> generated,
        bool operations, DateTimeOffset now, CancellationToken token)
    {
        var workers = ledger.Workers.OrderBy(w => w.Code, StringComparer.Ordinal).ThenBy(w => w.Id).ToArray();
        // Progress goals retain their existing attribution across days. Their load still counts.
        foreach (var group in generated.Where(a => a.GoalUnits == 0)
            .GroupBy(a => ledger.Day(a)).OrderBy(g => g.Key))
        {
            var day = group.Key;
            var tasks = group.OrderBy(a => a.TaskCode, StringComparer.Ordinal).ThenBy(a => a.StartsAt)
                .ThenBy(a => a.ScheduleKey, StringComparer.Ordinal).ToArray();
            DateTimeOffset? Reception(OperationalAssignment task) => !operations && task.StartsAt <= now ? now : null;
            decimal Cost(Guid person, decimal load)
            {
                var capacity = ledger.DayCapacity(person, day);
                return capacity > 0 ? load * load / capacity : 0;
            }
            decimal Improvement(Guid from, Guid to, decimal minutes)
            {
                var source = ledger.DayLoad(from, day); var target = ledger.DayLoad(to, day);
                return Cost(from, source) + Cost(to, target) - Cost(from, source - minutes) - Cost(to, target + minutes);
            }
            bool Eligible(CapacityLedger state, CapacityWorker worker, OperationalAssignment task) =>
                worker.EligibleFor(task.AssignedRole, task.SpecializationId) && state.CanAssign(worker.Id, task, Reception(task));
            void Assign(OperationalAssignment task, CapacityWorker worker)
            {
                task.PersonId = worker.Id; task.PersonName = worker.Name; task.AssignedRole = worker.Role;
            }
            // Start with the work having the fewest eligible people. Otherwise morning work can
            // consume assignments that would have balanced a later shift, trapping simple exchanges.
            var groupIds = tasks.Select(t => t.Id).ToHashSet();
            var baseTasks = ledger.Tasks.Where(t => !groupIds.Contains(t.Id)).ToArray();
            CapacityLedger WithTasks(OperationalAssignment[] items) => new(ledger.Workers, items, ledger.Capacities,
                ledger.Unavailable, ledger.Legacy, ledger.Today)
                { Temporal = ledger.Temporal, PlannedChanges = ledger.PlannedChanges, Override = ledger.Override };
            var proposed = WithTasks(baseTasks);
            var choices = tasks.Select(t => (Task: t, Workers: workers.Where(w => Eligible(proposed, w, t)).ToArray()))
                .OrderBy(t => t.Workers.Length).ThenByDescending(t => t.Task.RemainingMinutes)
                .ThenBy(t => t.Task.TaskCode, StringComparer.Ordinal).ThenBy(t => t.Task.StartsAt)
                .ThenBy(t => t.Task.ScheduleKey, StringComparer.Ordinal).ToArray();
            var placements = new List<(OperationalAssignment Task, CapacityWorker Worker)>();
            foreach (var choice in choices)
            {
                token.ThrowIfCancellationRequested();
                var task = choice.Task;
                var worker = choice.Workers.Where(w => Eligible(proposed, w, task))
                    .OrderBy(w => Cost(w.Id, proposed.DayLoad(w.Id, day) + task.RemainingMinutes) - Cost(w.Id, proposed.DayLoad(w.Id, day)))
                    .ThenBy(w => !task.DifferentPeople ? 0 : proposed.Tasks.Count(t => t.PersonId == w.Id && t.TaskId == task.TaskId &&
                        WeeklyBoardSnapshot.Monday(WeekContract.LocalToday(t.StartsAt)) == WeeklyBoardSnapshot.Monday(WeekContract.LocalToday(task.StartsAt))))
                    .ThenBy(w => w.Id != task.PersonId).ThenBy(w => w.Code, StringComparer.Ordinal).ThenBy(w => w.Id).FirstOrDefault();
                if (worker == null) break; // Keep the already valid generation if this ordering cannot fit.
                placements.Add((task, worker));
                proposed = proposed.WithAdded(new OperationalAssignment {
                    Id=task.Id, TaskId=task.TaskId, PersonId=worker.Id, AssignedRole=worker.Role,
                    StartsAt=task.StartsAt, DueAt=task.DueAt, LoadMinutes=task.LoadMinutes,
                    SchedulingPolicyId=task.SchedulingPolicyId, ClosureMarginPercent=task.ClosureMarginPercent, OperationalCriticality=task.OperationalCriticality, NominalStartsAt=task.NominalStartsAt, ScheduleMode=task.ScheduleMode,
                    ProgressUnits=task.ProgressUnits, GoalUnits=task.GoalUnits, PeriodType=task.PeriodType,
                    SpecializationId=task.SpecializationId });
            }
            if (placements.Count == tasks.Length &&
                workers.Sum(w => Cost(w.Id, proposed.DayLoad(w.Id, day))) < workers.Sum(w => Cost(w.Id, ledger.DayLoad(w.Id, day))))
            {
                foreach (var placement in placements) Assign(placement.Task, placement.Worker);
                ledger.InvalidateLoads();
            }
            while (true)
            {
                token.ThrowIfCancellationRequested();
                decimal best = 0;
                OperationalAssignment? move = null; CapacityWorker? recipient = null;
                foreach (var task in tasks)
                    foreach (var worker in workers)
                    {
                        if (worker.Id == task.PersonId) continue;
                        var gain = Improvement(task.PersonId, worker.Id, task.RemainingMinutes);
                        if (gain <= best || !Eligible(ledger, worker, task)) continue;
                        best = gain; move = task; recipient = worker;
                    }
                if (move != null)
                {
                    Assign(move, recipient!); ledger.InvalidateLoads(); continue;
                }
                // A large task may not fit a direct move even though exchanging two tasks improves both loads.
                OperationalAssignment? left = null; OperationalAssignment? right = null;
                for (var i = 0; i < tasks.Length; i++)
                    for (var j = i + 1; j < tasks.Length; j++)
                    {
                        var a = tasks[i]; var b = tasks[j];
                        if (a.PersonId == b.PersonId || a.RemainingMinutes == b.RemainingMinutes) continue;
                        var gain = Improvement(a.PersonId, b.PersonId, a.RemainingMinutes - b.RemainingMinutes);
                        if (gain <= best) continue;
                        var withoutPair = new CapacityLedger(ledger.Workers, ledger.Tasks.Where(t => t.Id != a.Id && t.Id != b.Id).ToArray(),
                            ledger.Capacities, ledger.Unavailable, ledger.Legacy, ledger.Today)
                            { Temporal = ledger.Temporal, PlannedChanges = ledger.PlannedChanges, Override = ledger.Override };
                        if (!Eligible(withoutPair, workers.Single(w => w.Id == b.PersonId), a) ||
                            !Eligible(withoutPair, workers.Single(w => w.Id == a.PersonId), b)) continue;
                        best = gain; left = a; right = b;
                    }
                if (left == null) break;
                var leftWorker = workers.Single(w => w.Id == left.PersonId);
                Assign(left, workers.Single(w => w.Id == right!.PersonId)); Assign(right!, leftWorker);
                ledger.InvalidateLoads();
            }
        }
    }
}
