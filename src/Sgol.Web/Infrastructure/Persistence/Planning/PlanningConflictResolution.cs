using Sgol.Planning.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Sgol.Web.Infrastructure.Persistence.Planning;

public sealed record PlanningCapacityResolution(string Person, DateOnly Day, decimal PreviousCapacity,
    decimal RequiredCapacity, PlanCapacityAdjustment Adjustment, bool FullAvailableDay = false);
public sealed record PlanningMoveResolution(PlanOccurrenceMove Move, string Task, int Minutes, string Person,
    decimal Forecast, decimal Capacity, DateTimeOffset StartsAt, DateTimeOffset DueAt);

// Proposals are read-only. The complete agenda must be recalculated after selecting one.
public static class PlanningConflictResolution
{
    public static async Task<PlanningMoveResolution[]> ProposeMovesAsync(SgolDbContext db, PlanningAssignmentConflict conflict,
        CapacityLedger ledger, PlanningCapacityPreview preview, DirectionPlanInput input, CancellationToken token)
    {
        var policy = await OperationalSchedulingPolicyService.CurrentAsync(db, token);
        if (policy.AgendaVersion < 2) return [];
        var tasks = await db.OperationalTasks.AsNoTracking().Where(t => input.TaskCodes.Contains(t.TaskCode) && t.Active && !t.Deleted).ToArrayAsync(token);
        var taskIds = tasks.Select(t => t.Id).ToArray();
        var existing = await db.OperationalAssignments.AsNoTracking().Where(a => taskIds.Contains(a.TaskId) && a.ReassignedFromId == null)
            .Select(a => new { a.TaskId, a.ScheduleKey, a.StartsAt }).ToArrayAsync(token);
        var adjusted = new CapacityLedger(ledger.Workers, ledger.Tasks, ledger.Capacities, ledger.Unavailable, ledger.Legacy, ledger.Today)
            { Temporal = ledger.Temporal, ExcludedDemandIds = ledger.ExcludedDemandIds, PlannedChanges = PlanningCapacity.Changes(input, preview, ledger) };
        bool Reserved(OperationalTask t, DateOnly day) => existing.Any(a => a.TaskId == t.Id &&
            (a.ScheduleKey == $"{t.Id:N}:{day:yyyyMMdd}:0" || WeekContract.LocalToday(a.StartsAt) == day));
        DateOnly Destination(OperationalTask t, DateOnly source) => input.OccurrenceMoves?.SingleOrDefault(m => m.TaskCode == t.TaskCode && m.FromDay == source)?.ToDay ?? source;
        var fixedDemand = new Dictionary<(Guid, DateOnly), decimal>();
        foreach (var task in tasks.Where(t => t.ScheduleMode == "DIAS_FIJOS"))
        {
            var eligible = ledger.Workers.Where(w => w.EligibleFor(task.AssignedRole, task.SpecializationId)).ToArray();
            if (eligible.Length != 1) continue;
            foreach (var day in OperationalSchedule.Dates(preview.From, preview.Through, task).Where(d => !Reserved(task, d)))
            {
                var key = (eligible[0].Id, Destination(task, day));
                fixedDemand[key] = fixedDemand.GetValueOrDefault(key) + task.LoadMinutes;
            }
        }
        var affected = conflict.Demand.Where(d => d.Origin == "NUEVA" && d.CanMove).ToArray();
        if (affected.Length == 0 && conflict.Demand.Length == 0)
            affected = conflict.Candidates.Select(c => new PlanningDemandItem(conflict.TaskCode, conflict.Task, conflict.LoadMinutes, c.Day, "NUEVA", true)
                { SourceDay = input.OccurrenceMoves?.SingleOrDefault(m => m.TaskCode == conflict.TaskCode && m.ToDay == c.Day)?.FromDay ?? c.Day }).Distinct().ToArray();
        var options = new List<PlanningMoveResolution>();
        foreach (var item in affected)
        {
            var task = tasks.SingleOrDefault(t => t.TaskCode == item.TaskCode);
            if (task == null || task.OperationalCriticality != "FLEXIBLE" || task.ScheduleMode != "DIAS_FIJOS" ||
                !OperationalSchedule.Dates(item.SourceDay, item.SourceDay, task).Any() || Reserved(task, item.SourceDay)) continue;
            foreach (var day in CapacityLedger.Days(preview.From, preview.Through).Where(d => d > item.Day && WeeklyBoardSnapshot.Monday(d) == WeeklyBoardSnapshot.Monday(item.SourceDay)))
            {
                if (OperationalSchedule.Dates(preview.From, preview.Through, task).Any(d => d != item.SourceDay && Destination(task, d) == day) || Reserved(task, day)) continue;
                foreach (var worker in ledger.Workers.Where(w => w.EligibleFor(task.AssignedRole, task.SpecializationId)))
                {
                    var forecast = adjusted.DayLoad(worker.Id, day) + fixedDemand.GetValueOrDefault((worker.Id, day));
                    var capacity = adjusted.DayCapacity(worker.Id, day);
                    var sourceLoad = adjusted.DayLoad(worker.Id, item.Day) + fixedDemand.GetValueOrDefault((worker.Id, item.Day));
                    if (forecast + task.LoadMinutes > capacity || forecast + task.LoadMinutes >= sourceLoad) continue;
                    var probe = new OperationalAssignment { TaskId = task.Id, TaskCode = task.TaskCode, LoadMinutes = task.LoadMinutes,
                        ScheduleMode = task.ScheduleMode, SpecializationId = task.SpecializationId };
                    OperationalAgenda.Capture(probe, policy, task.OperationalCriticality, task.StartOn(day));
                    var placement = OperationalAgenda.Find(adjusted, worker.Id, probe, OperationalSchedule.Instant(ledger.Today));
                    if (!placement.Eligible) continue;
                    options.Add(new(new(task.TaskCode, item.SourceDay, day), task.Name, task.LoadMinutes, worker.Name,
                        forecast, capacity, placement.StartsAt, placement.DueAt));
                }
            }
        }
        return options.OrderByDescending(o => o.Minutes).ThenBy(o => (o.Forecast + o.Minutes) / o.Capacity).ThenBy(o => o.Move.ToDay)
            .DistinctBy(o => o.Move).ToArray();
    }
    public static PlanningCapacityResolution[] Propose(PlanningAssignmentConflict conflict, CapacityLedger ledger,
        PlanningCapacityPreview preview, DirectionPlanInput input)
    {
        var changes = PlanningCapacity.Changes(input, preview, ledger);
        int Maximum(Guid person, DateOnly day)
        {
            var maximumLedger = new CapacityLedger(ledger.Workers, ledger.Tasks, ledger.Capacities, ledger.Unavailable, ledger.Legacy, ledger.Today)
            {
                Temporal = ledger.Temporal,
                ExcludedDemandIds = ledger.ExcludedDemandIds,
                Override = new CapacityChange(person, day, day, 100, 480, "Comprobar límite de propuesta")
            };
            return (int)Math.Floor(maximumLedger.DayCapacity(person, day));
        }
        var minimum = conflict.Candidates.Where(c => c.PersonId != Guid.Empty && c.Reason == "PLAN_CAPACITY_EXCEEDED")
            .GroupBy(c => (c.PersonId, c.Day)).Select(g => g.OrderByDescending(c => c.PendingMinutes).First())
            .Select(c => {
                var configured = ledger.Setting(c.PersonId, c.Day);
                var adjusted = changes.SingleOrDefault(a => a.PersonId == c.PersonId && a.From <= c.Day && a.Through >= c.Day);
                var percent = adjusted?.Percent ?? configured?.Percent ?? 100;
                var required = Math.Ceiling(c.PendingMinutes + conflict.LoadMinutes);
                var minutes = percent == 0 ? 481 : (int)Math.Ceiling(required * 100 / percent);
                // If the reduced percentage cannot cover the demand within the allowed base,
                // explicitly propose restoring 100%, never silently bypass its meaning.
                if (minutes > 480) { percent = 100; minutes = (int)required; }
                var usable = Maximum(c.PersonId, c.Day);
                if (required <= c.CapacityMinutes || required > usable || minutes is < 1 or > 480 ||
                    c.Day < preview.From || c.Day > preview.Through) return null;
                return new PlanningCapacityResolution(c.Person, c.Day, c.CapacityMinutes, required,
                    new(c.PersonId, percent, c.Day, minutes));
            }).OfType<PlanningCapacityResolution>().OrderBy(o => o.Day).ThenBy(o => o.Person).ToArray();
        return minimum.SelectMany(option => {
            var available = Maximum(option.Adjustment.PersonId, option.Day);
            return available > option.RequiredCapacity
                ? new[] { option, option with { RequiredCapacity = available, Adjustment = new(option.Adjustment.PersonId, 100, option.Day, available), FullAvailableDay = true } }
                : new[] { option };
        }).ToArray();
    }
}
