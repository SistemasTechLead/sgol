using System.Text.Json;
using Sgol.Planning.Contracts;

namespace Sgol.Web.Infrastructure.Persistence.Planning;

public sealed record CorrectionSchedule(string Mode = "ASAP", DateOnly? From = null, DateOnly? Through = null);

public static class CorrectionScheduling
{
    public static SchedulingPlacement Find(CapacityLedger ledger, Guid person, OperationalAssignment task,
        DateTimeOffset now, CorrectionSchedule request)
    {
        var today = WeekContract.LocalToday(now);
        var from = request.Mode == "CUSTOM" ? request.From : today;
        var through = request.Mode == "CUSTOM" ? request.Through ?? request.From : today.AddDays(7);
        if (request.Mode is not ("ASAP" or "FREE" or "CUSTOM") || from == null || through == null ||
            from < today || through < from || through > today.AddDays(7))
            throw new DirectionPlanException("CORRECTION_RANGE_INVALID", 422);
        var duration = OperationalAgenda.Duration(task.LoadMinutes, task.ClosureMarginPercent ??
            throw new DirectionPlanException("SCHEDULING_POLICY_INVALID", 422));
        var worker = ledger.Workers.SingleOrDefault(w => w.Id == person);
        var reason = "CORRECTION_NO_SLOT";
        SchedulingPlacement Fail(string code) => new(false, now, now, code);
        if (worker == null || !worker.EligibleFor(task.AssignedRole, task.SpecializationId)) return Fail("REASSIGNMENT_NO_ELIGIBLE_RECIPIENT");
        for (var day = from.Value; day <= through.Value; day = day.AddDays(1))
        {
            if (ledger.Unavailable.Contains((person, day))) { reason = "SCHEDULE_ABSENT"; continue; }
            if (task.TaskId == ledger.Temporal.ClosingTaskId && ledger.Temporal.ClosingPerson(day) != person) { reason = "CLOSING_COVERAGE_REQUIRED"; continue; }
            var intervals = ledger.Temporal.Day(person, day, false).Intervals;
            if (intervals == null) { reason = "SCHEDULE_MISSING"; continue; }
            // Distributed progress contributes load, but its weekly deadline is not
            // a continuous execution reservation. Corrections do reserve their full interval.
            var blocks = ledger.Tasks.Where(t => t.Id != task.Id && t.PersonId == person &&
                (t.ReassignedFromId != null || t.ScheduleMode != "PROGRESO" && t.GoalUnits == 0) &&
                t.ReviewStatus is not ("REASIGNADA" or "NO_SATISFECHA") &&
                t.StartsAt < OperationalSchedule.Instant(day.AddDays(1)) && t.DueAt > OperationalSchedule.Instant(day))
                .OrderBy(t => t.StartsAt).ThenBy(t => t.DueAt).ToArray();
            if (request.Mode != "ASAP" && ledger.Legacy.GetValueOrDefault(person) > 0) { reason = "SCHEDULING_LEGACY_RESERVATION"; continue; }
            foreach (var interval in intervals.OrderBy(i => i.Entry))
            {
                var start = new[] { now, TemporalEligibilityContext.At(day, interval.Entry) }.Max();
                var exit = TemporalEligibilityContext.At(day, interval.Exit);
                if (worker.EmploymentEnd is {} employment && employment < exit) exit = employment;
                if (worker.RoleEnd is {} role && role < exit) exit = role;
                // Keep mandatory cash-circuit dependencies; the original critical anchor is historical.
                foreach (var peer in ledger.Tasks.Where(p => p.Id != task.Id && p.ReviewStatus is not ("REASIGNADA" or "NO_SATISFECHA") && WeekContract.LocalToday(p.StartsAt) == day))
                    foreach (var relation in OperationalAgenda.CashCircuit)
                    {
                        if (task.TaskCode == relation.After && peer.TaskCode == relation.Before && peer.DueAt > start) start = peer.DueAt;
                        if (task.TaskCode == relation.Before && peer.TaskCode == relation.After && peer.StartsAt < exit) exit = peer.StartsAt;
                    }
                if (request.Mode != "ASAP")
                    foreach (var block in blocks)
                    {
                        if (block.DueAt <= start) continue;
                        if (start.AddMinutes(duration) <= block.StartsAt) break;
                        start = block.DueAt;
                    }
                var due = start.AddMinutes(duration);
                if (due <= exit) return new(true, start, due, request.Mode);
            }
        }
        return Fail(reason);
    }

    public static string? Conflict(CapacityLedger ledger, OperationalAssignment task)
    {
        if (task.ReassignedFromId == null || task.CompletedAt != null || task.RetiredAt != null) return null;
        var day = WeekContract.LocalToday(task.StartsAt);
        if (ledger.Unavailable.Contains((task.PersonId, day))) return "SCHEDULE_ABSENT";
        var worker = ledger.Workers.SingleOrDefault(w => w.Id == task.PersonId);
        if (worker == null || !worker.EligibleFor(task.AssignedRole, task.SpecializationId)) return "REASSIGNMENT_NO_ELIGIBLE_RECIPIENT";
        if (worker.EmploymentEnd < task.DueAt || worker.RoleEnd < task.DueAt) return "SCHEDULE_EMPLOYMENT_END";
        if (task.TaskId == ledger.Temporal.ClosingTaskId && ledger.Temporal.ClosingPerson(day) != task.PersonId) return "CLOSING_COVERAGE_REQUIRED";
        return (ledger.Temporal.Day(task.PersonId, day, false).Intervals ?? []).Any(i =>
            TemporalEligibilityContext.At(day, i.Entry) <= task.StartsAt && task.DueAt <= TemporalEligibilityContext.At(day, i.Exit))
            ? null : "CORRECTION_SCHEDULE_CHANGED";
    }
}
