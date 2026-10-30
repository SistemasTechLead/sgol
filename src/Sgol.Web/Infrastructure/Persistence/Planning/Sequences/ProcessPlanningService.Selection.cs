using System.Security.Cryptography;
using System.Text;
using Sgol.Planning.Contracts;

namespace Sgol.Web.Infrastructure.Persistence.Planning;

public sealed partial class ProcessPlanningService
{
    private static Dictionary<int, Guid> ResolvePeople(ProcessPlanInput input, Guid version,
        OperationalSequenceStep[] steps, OperationalTask[] tasks, DateTimeOffset[] starts,
        CapacityLedger ledger, OperationalSchedulingPolicy? policy, DateTimeOffset now, CancellationToken token)
    {
        var people = new Dictionary<int, Guid>(input.People);
        for (var index = 0; index < steps.Length; index++)
        {
            token.ThrowIfCancellationRequested();
            var step = steps[index]; var task = tasks[index];
            var automatic = people[step.Position] == Guid.Empty;
            var candidates = ledger.Workers.Where(w => w.EligibleFor(task.AssignedRole, task.SpecializationId) &&
                (automatic || w.Id == people[step.Position])).ToArray();
            if (!automatic && candidates.Length == 0) throw Error("OCCASION_RECIPIENT_INVALID");
            var choices = new List<(Guid Person, decimal Occupancy, string Draw)>();
            var failures = new List<DirectionPlanException>();
            foreach (var candidate in candidates)
            {
                token.ThrowIfCancellationRequested();
                people[step.Position] = candidate.Id;
                var simulated = ledger;
                var own = new List<OperationalAssignment>();
                try
                {
                    // Simulate the selected prefix, never charge the entire process to a candidate.
                    // This also checks every calendar occurrence for the same stage recipient.
                    foreach (var start in starts)
                    {
                        var reservations = Reservations(input.Key, version, start, people, steps.Take(index + 1).ToArray(), policy, simulated, now).ToArray();
                        foreach (var reservation in reservations)
                        {
                            RequireReservation(simulated, reservation, steps.Single(s => s.TaskId == reservation.TaskId));
                            simulated = simulated.WithAdded(reservation);
                            if (reservation.TaskId == task.Id) own.Add(reservation);
                        }
                    }
                    var days = own.SelectMany(r => simulated.Footprint(r)).Select(f => f.Day).Distinct().ToArray();
                    var occupancy = days.Average(day => simulated.DayCapacity(candidate.Id, day) > 0
                        ? simulated.DayLoad(candidate.Id, day) / simulated.DayCapacity(candidate.Id, day) : decimal.MaxValue);
                    var draw = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{input.Key:N}:{step.Position}:{candidate.Id:N}")));
                    choices.Add((candidate.Id, occupancy, draw));
                }
                catch (DirectionPlanException e) when (automatic &&
                    (e.Code.StartsWith("SCHEDUL", StringComparison.Ordinal) || e.Code == "PLAN_NO_AVAILABLE_WORKER" || e.Code == "CLOSING_COVERAGE_REQUIRED"))
                {
                    // A free daily balance is insufficient when no valid working interval exists.
                    failures.Add(e);
                }
            }
            if (choices.Count == 0)
            {
                var error = Error("PLAN_NO_AVAILABLE_WORKER");
                error.Data["StagePosition"] = step.Position; error.Data["StageName"] = task.Name;
                if (failures.Count > 0) error.Data["CandidateFailures"] = failures.ToArray();
                throw error;
            }
            people[step.Position] = choices.OrderBy(c => c.Occupancy).ThenBy(c => c.Draw, StringComparer.Ordinal).First().Person;
        }
        return people;
    }
}
