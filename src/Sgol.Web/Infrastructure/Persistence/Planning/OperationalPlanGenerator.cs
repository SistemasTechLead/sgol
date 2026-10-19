using Microsoft.EntityFrameworkCore;
using Sgol.Planning.Contracts;

namespace Sgol.Web.Infrastructure.Persistence.Planning;

internal static class OperationalPlanGenerator
{
    private sealed record Occurrence(OperationalTask Task, DateOnly Day, int Slot, MonthlyOccurrence? Monthly = null);
    public static async Task<OperationalAssignment[]> GenerateAsync(SgolDbContext db, DirectionPreparedPlan plan,
        IReadOnlyList<string> codes, DateTimeOffset now, CancellationToken token, bool allowReserved = false, CapacityLedger? preparedLedger = null)
    {
        var tasks = await db.OperationalTasks.AsNoTracking().Where(t => codes.Contains(t.TaskCode) && t.Active && !t.Deleted).ToArrayAsync(token);
        if (tasks.Length != codes.Count || tasks.Any(t => t.PeriodType == "OCASIONAL" || t.ProcessId != OperationalSequence.GeneralId)) throw new DirectionPlanException("DIRECTION_PLAN_TASK_INVALID", 422);
        if (tasks.Length == 0) throw new DirectionPlanException("PLAN_TASKS_REQUIRED", 422);
        foreach (var task in tasks) task.Validate();
        var pending = new List<MonthlyPlanPending>();
        var occurrences = tasks.SelectMany(t => t.PreferredMonthDays.Length > 0
            ? MonthlyTaskCalendar.Occurrences(t, plan.StartsOn, plan.EndsOn).Select(m =>
                new Occurrence(t, m.ValidDate ?? new DateOnly(m.Year, m.Month, DateTime.DaysInMonth(m.Year, m.Month)), 0, m))
            : OperationalSchedule.Dates(plan.StartsOn, plan.EndsOn, t)
                .GroupBy(d => d).SelectMany(g => g.Select((d, slot) => new Occurrence(t, d, slot)))).Take(10001).ToArray();
        if (occurrences.Length > 10000) throw new DirectionPlanException("PLAN_TOO_MANY_OCCURRENCES", 422);
        // Reserve constrained dates first so distributed repetitions see their real base load.
        bool Distributed(OperationalTask t) => t.ScheduleMode == "DISTRIBUIDA" && t.PeriodType == "SEMANAL";
        occurrences = occurrences.OrderBy(o => Distributed(o.Task)).ThenByDescending(o => o.Task.SpecializationId != null)
            .ThenByDescending(o => o.Task.LoadMinutes).ThenBy(o => o.Day).ThenBy(o => o.Task.TaskCode).ThenBy(o => o.Slot).ToArray();
        var ledger = preparedLedger ?? await OperationalCapacityService.LedgerAsync(db, now, token);
        var initialLedger = ledger;
        var specialties = await db.Set<Sgol.Web.Infrastructure.Persistence.Organization.Specialization>().AsNoTracking().ToDictionaryAsync(s => s.Id, token);
        var taskIds = tasks.Select(t => t.Id).ToArray();
        var existing = await db.OperationalAssignments.AsNoTracking().Where(a => taskIds.Contains(a.TaskId) && a.ReassignedFromId == null)
            .Select(a => new { a.TaskId, a.StartsAt, a.ScheduleKey, a.PersonId }).ToArrayAsync(token);
        var assignments = new List<OperationalAssignment>();
        var rotationPeers = await db.OperationalAssignments.AsNoTracking().Where(a => taskIds.Contains(a.TaskId) && a.ReviewStatus != "REASIGNADA")
            .Select(a => new { a.TaskId, a.StartsAt, a.PersonId }).ToArrayAsync(token);
        foreach (var occurrence in occurrences)
        {
            var task = occurrence.Task;
            var monthly = occurrence.Monthly;
            void Pending(string reason) => pending.Add(new(monthly!.Key(task.Id), task.TaskCode, task.Name,
                monthly.Year, monthly.Month, monthly.PreferredDay, reason));
            if (monthly != null && existing.Any(a => a.ScheduleKey == monthly.Key(task.Id))) continue;
            if (monthly != null && monthly.ValidDate == null) { Pending("MONTH_DAY_NOT_IN_MONTH"); continue; }
            if (monthly != null && occurrence.Day < ledger.Today) { Pending("MONTH_DATE_PASSED"); continue; }
            // Operations may include expired hours on the creation day, never earlier dates.
            if (plan.IsWeeklyOperationsPlan && occurrence.Day < WeekContract.LocalToday(now)) continue;
            var weekStart = WeeklyBoardSnapshot.Monday(occurrence.Day);
            var weekEnd = weekStart.AddDays(6);
            bool InWeek(DateTimeOffset start) => WeekContract.LocalToday(start) >= weekStart && WeekContract.LocalToday(start) <= weekEnd;
            int ReservedOn(DateOnly date) => existing.Count(a => a.TaskId == task.Id && WeekContract.LocalToday(a.StartsAt) == date) +
                assignments.Count(a => a.TaskId == task.Id && WeekContract.LocalToday(a.StartsAt) == date);
            if (Distributed(task))
            {
                var target = occurrences.Count(o => o.Task.Id == task.Id && o.Day >= weekStart && o.Day <= weekEnd &&
                    (!plan.IsWeeklyOperationsPlan || o.Day >= WeekContract.LocalToday(now))) + existing.Count(a => a.TaskId == task.Id && InWeek(a.StartsAt) &&
                        (WeekContract.LocalToday(a.StartsAt) < plan.StartsOn || WeekContract.LocalToday(a.StartsAt) > plan.EndsOn));
                if (existing.Count(a => a.TaskId == task.Id && InWeek(a.StartsAt)) + assignments.Count(a => a.TaskId == task.Id && InWeek(a.StartsAt)) >= target) continue;
            }
            else if (monthly == null && (existing.Any(a => a.ScheduleKey == $"{task.Id:N}:{occurrence.Day:yyyyMMdd}:{occurrence.Slot}") ||
                existing.Count(a => a.ScheduleKey == null && a.TaskId == task.Id && WeekContract.LocalToday(a.StartsAt) == occurrence.Day) > occurrence.Slot)) continue;
            var starts = task.StartOn(occurrence.Day);
            var due = task.ScheduleMode == "PROGRESO" ? OperationalSchedule.Instant(occurrence.Day.AddDays(7)) : starts.AddHours(task.DeadlineHours);
            var assignment = new OperationalAssignment { Id = Guid.CreateVersion7(), PlanId = plan.Id, TaskId = task.Id, TaskRevision = task.Revision,
                TaskCode = task.TaskCode, Name = task.Name, Description = task.Description, Requirement = task.Requirement, AssignedRole = task.AssignedRole,
                MinimumPhotos = task.MinimumPhotos, MaximumPhotos = task.MaximumPhotos, MaximumPhotoBytes = task.MaximumPhotoBytes,
                SpecializationId = task.SpecializationId, SpecializationName = task.SpecializationId is { } specialty ? specialties[specialty].Name : null,
                LoadMinutes = task.LoadMinutes, StartsAt = starts, DueAt = due, PeriodType = task.PeriodType, Repetitions = task.Repetitions,
                ScheduleMode = task.ScheduleMode, DifferentPeople = task.DifferentPeople,
                PreferredMonthDays = task.PreferredMonthDays,
                GoalUnits = task.ScheduleMode == "PROGRESO" ? task.GoalUnits ?? 0 : 0, ProgressUnit = task.ProgressUnit };
            var matching = ledger.Workers.Where(w => task.SpecializationId != null || w.Role == task.AssignedRole).ToArray();
            if (matching.Length == 0) { if (monthly != null) { Pending("PLAN_NO_WORKER_FOR_ROLE"); continue; } throw new DirectionPlanException("PLAN_NO_WORKER_FOR_ROLE", 422); }
            matching = matching.Where(w => w.Qualified(task.SpecializationId)).ToArray();
            if (matching.Length == 0) { if (monthly != null) { Pending("PLAN_NO_SPECIALIST"); continue; } throw new DirectionPlanException("PLAN_NO_SPECIALIST", 422); }
            int Repetitions(Guid person) => !task.DifferentPeople ? 0 : rotationPeers.Count(a => a.PersonId == person && a.TaskId == task.Id &&
                WeekContract.LocalToday(a.StartsAt) >= weekStart && WeekContract.LocalToday(a.StartsAt) <= weekEnd) +
                assignments.Count(a => a.PersonId == person && a.TaskId == task.Id && WeekContract.LocalToday(a.StartsAt) >= weekStart && WeekContract.LocalToday(a.StartsAt) <= weekEnd);
            var from = DateOnly.FromDayNumber(Math.Max(weekStart.DayNumber, ledger.Today.DayNumber));
            var through = DateOnly.FromDayNumber(Math.Max(weekEnd.DayNumber, ledger.Today.DayNumber));
            var dates = monthly != null ? MonthlyTaskCalendar.Candidates(occurrence.Day, plan.StartsOn, plan.EndsOn, ledger.Today) : Distributed(task) ? CapacityLedger.Days(
                DateOnly.FromDayNumber(Math.Max(Math.Max(weekStart.DayNumber, plan.StartsOn.DayNumber),
                    plan.IsWeeklyOperationsPlan ? WeekContract.LocalToday(now).DayNumber : plan.StartsOn.DayNumber)),
                DateOnly.FromDayNumber(Math.Min(weekEnd.DayNumber, plan.EndsOn.DayNumber))) : [occurrence.Day];
            // Keep the configured frequency spread across distinct days (or balanced slots for >7/week).
            var options = new List<(DateOnly Day, CapacityWorker Worker, decimal Daily, decimal Total, decimal Period)>();
            foreach (var date in dates)
            {
                // Sunday is explicit: seven weekly repetitions or a fixed weekday selection.
                if (Distributed(task) && task.Repetitions != 7 && date.DayOfWeek == DayOfWeek.Sunday) continue;
                var distributionDays = task.Repetitions == 7 ? 7 : 6;
                if (Distributed(task) && ReservedOn(date) >= (task.Repetitions + distributionDays - 1) / distributionDays) continue;
                assignment.StartsAt = task.StartOn(date);
                assignment.DueAt = task.ScheduleMode == "PROGRESO" ? OperationalSchedule.Instant(date.AddDays(7)) : assignment.StartsAt.AddHours(task.DeadlineHours);
                var received = !plan.IsWeeklyOperationsPlan && assignment.StartsAt <= now ? now : (DateTimeOffset?)null;
                var total = matching.Sum(w => ledger.DayLoad(w.Id, date));
                foreach (var candidate in matching.Where(w => ledger.CanAssign(w.Id, assignment, received)))
                    options.Add((date, candidate, ledger.Occupancy(candidate.Id, date < ledger.Today ? ledger.Today : date,
                        date < ledger.Today ? ledger.Today : date), total, task.SpecializationId == null ?
                        ledger.Occupancy(candidate.Id, from, through) : ledger.SpecializedPeriodLoad(candidate.Id, assignment)));
            }
            var selected = options.OrderBy(o => monthly != null ? Math.Abs(o.Day.DayNumber - occurrence.Day.DayNumber) : 0)
                .ThenBy(o => Distributed(task) ? ReservedOn(o.Day) : 0)
                .ThenBy(o => Distributed(task) ? o.Total : 0)
                .ThenBy(o => o.Daily)
                .ThenBy(o => monthly != null ? o.Day : DateOnly.MinValue)
                .ThenBy(o => Repetitions(o.Worker.Id))
                .ThenBy(o => o.Period)
                .ThenBy(o => o.Day).ThenBy(o => o.Worker.Code, StringComparer.Ordinal).ThenBy(o => o.Worker.Id).FirstOrDefault();
            var worker = selected.Worker;
            if (worker == null)
            {
                if (monthly != null) { Pending("PLAN_NO_AVAILABLE_WORKER"); continue; }
                var rejected = new List<PlanningCandidateConflict>();
                foreach (var date in dates)
                {
                    if (Distributed(task) && task.Repetitions != 7 && date.DayOfWeek == DayOfWeek.Sunday) continue;
                    assignment.StartsAt = task.StartOn(date);
                    assignment.DueAt = task.ScheduleMode == "PROGRESO" ? OperationalSchedule.Instant(date.AddDays(7)) : assignment.StartsAt.AddHours(task.DeadlineHours);
                    foreach (var candidate in matching)
                    {
                        var decision = ledger.CheckTime(candidate.Id, assignment, !plan.IsWeeklyOperationsPlan && assignment.StartsAt <= now ? now : null);
                        var reason = !decision.Eligible ? decision.Reason :
                            Distributed(task) && ReservedOn(date) >= (task.Repetitions + (task.Repetitions == 7 ? 7 : 6) - 1) / (task.Repetitions == 7 ? 7 : 6)
                                ? "PLAN_DAILY_REPETITIONS_RESERVED" : "PLAN_CAPACITY_EXCEEDED";
                        var intervals = ledger.Temporal.Day(candidate.Id, date).Intervals;
                        var hours = intervals == null ? "Sin configurar" : intervals.Length == 0 ? "Descanso" : string.Join(" · ", intervals.Select(i => $"{i.Entry / 60:00}:{i.Entry % 60:00}–{i.Exit / 60:00}:{i.Exit % 60:00}"));
                        var day = date < ledger.Today ? ledger.Today : date;
                        rejected.Add(new(date, assignment.StartsAt, candidate.Name, reason, hours, ledger.DayLoad(candidate.Id, day), ledger.DayCapacity(candidate.Id, day)));
                    }
                }
                var error = new DirectionPlanException("PLAN_NO_AVAILABLE_WORKER", 422);
                error.Data[nameof(PlanningAssignmentConflict)] = new PlanningAssignmentConflict(task.TaskCode, task.Name, task.LoadMinutes, rejected.ToArray());
                throw error;
            }
            assignment.StartsAt = task.StartOn(selected.Day);
            assignment.DueAt = task.ScheduleMode == "PROGRESO" ? OperationalSchedule.Instant(selected.Day.AddDays(7)) : assignment.StartsAt.AddHours(task.DeadlineHours);
            var slot = Distributed(task) ? Enumerable.Range(0, 101).First(s =>
                !existing.Any(a => a.ScheduleKey == $"{task.Id:N}:{selected.Day:yyyyMMdd}:{s}") &&
                !assignments.Any(a => a.ScheduleKey == $"{task.Id:N}:{selected.Day:yyyyMMdd}:{s}") &&
                s >= existing.Count(a => a.TaskId == task.Id && a.ScheduleKey == null && WeekContract.LocalToday(a.StartsAt) == selected.Day)) : occurrence.Slot;
            assignment.ScheduleKey = monthly?.Key(task.Id) ?? $"{task.Id:N}:{selected.Day:yyyyMMdd}:{slot}";
            var reception = !plan.IsWeeklyOperationsPlan && assignment.StartsAt <= now ? now : (DateTimeOffset?)null;
            assignment.PersonId = worker.Id; assignment.PersonName = worker.Name; assignment.AssignedRole = worker.Role;
            assignment.PreviousLoadMinutes = ledger.Tasks.Where(a => a.PersonId == worker.Id).Sum(a => (long)a.RemainingMinutes) + ledger.Legacy.GetValueOrDefault(worker.Id);
            ledger.CaptureTime(assignment, reception); assignments.Add(assignment);
            ledger = new(ledger.Workers, ledger.Tasks.Append(assignment).ToArray(), ledger.Capacities, ledger.Unavailable, ledger.Legacy, ledger.Today)
                { Temporal = ledger.Temporal, PlannedChanges = ledger.PlannedChanges };
        }
        if (!allowReserved && occurrences.Length > 0 && assignments.Count == 0 && pending.Count == 0) throw new DirectionPlanException("PLAN_OCCURRENCES_RESERVED", 422);
        OperationalPlanBalancer.Balance(ledger, assignments, plan.IsWeeklyOperationsPlan, now, token);
        // Capture the final recipient and previous load in the original generation order.
        ledger = initialLedger;
        foreach (var assignment in assignments)
        {
            assignment.PreviousLoadMinutes = ledger.Tasks.Where(a => a.PersonId == assignment.PersonId).Sum(a => (long)a.RemainingMinutes) + ledger.Legacy.GetValueOrDefault(assignment.PersonId);
            ledger.CaptureTime(assignment, !plan.IsWeeklyOperationsPlan && assignment.StartsAt <= now ? now : null);
            ledger = new(ledger.Workers, ledger.Tasks.Append(assignment).ToArray(), ledger.Capacities, ledger.Unavailable, ledger.Legacy, ledger.Today)
                { Temporal = ledger.Temporal, PlannedChanges = ledger.PlannedChanges, Override = ledger.Override };
        }
        if (tasks.Any(t => t.PreferredMonthDays.Length > 0))
        {
            var retained = System.Text.Json.JsonSerializer.Deserialize<MonthlyPlanPending[]>(plan.MonthlyPendingJson)!
                .Where(p => !codes.Contains(p.TaskCode));
            plan.CaptureMonthlyPending(retained.Concat(pending).ToArray());
        }
        return assignments.ToArray();
    }
}
