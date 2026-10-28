using Microsoft.EntityFrameworkCore;
using Sgol.Planning.Contracts;

namespace Sgol.Web.Infrastructure.Persistence.Planning;

internal static class OperationalPlanGenerator
{
    private sealed record Occurrence(OperationalTask Task, DateOnly Day, int Slot, MonthlyOccurrence? Monthly = null);
    public static async Task<OperationalAssignment[]> GenerateAsync(SgolDbContext db, DirectionPreparedPlan plan,
        IReadOnlyList<string> codes, DateTimeOffset now, CancellationToken token, bool allowReserved = false, CapacityLedger? preparedLedger = null)
    {
        using var timing = Sgol.Web.Infrastructure.Http.PerformanceWork.Measure("planning.generate");
        var tasks = await db.OperationalTasks.AsNoTracking().Where(t => codes.Contains(t.TaskCode) && t.Active && !t.Deleted).ToArrayAsync(token);
        var known = await db.OperationalTasks.AsNoTracking().CountAsync(t => codes.Contains(t.TaskCode), token);
        if (known != codes.Distinct().Count() || tasks.Any(t => t.PeriodType == "OCASIONAL" || t.ProcessId != OperationalSequence.GeneralId)) throw new DirectionPlanException("DIRECTION_PLAN_TASK_INVALID", 422);
        if (tasks.Length == 0) throw new DirectionPlanException("PLAN_TASKS_REQUIRED", 422);
        foreach (var task in tasks) task.Validate();
        var policy = plan.SchedulingPolicyId is { } policyId ? await db.Set<OperationalSchedulingPolicy>().AsNoTracking().SingleAsync(p => p.Id == policyId, token) : null;
        if (policy != null)
        {
            var missing = tasks.Where(t => !OperationalAgenda.IsDefined(t.OperationalCriticality)).ToArray();
            if (missing.Length > 0) throw new DirectionPlanException("SCHEDULING_CRITICALITY_REQUIRED", 422) { Data = { ["Tasks"] = string.Join(", ", missing.Select(t => t.TaskCode + " · " + t.Name)) } };
        }
        // The existing dated-notice payload also persists unassigned fixed occurrences.
        var pending = new List<MonthlyPlanPending>();
        bool Distributed(OperationalTask t) => t.ScheduleMode == "DISTRIBUIDA" && t.PeriodType == "SEMANAL" && t.OperationalCriticality != "CRITICA";
        if (plan.IsRollingMonthPlan && tasks.Any(t => t.PeriodType != "MENSUAL")) throw new DirectionPlanException("DIRECTION_PLAN_TASK_INVALID", 422);
        var occurrences = tasks.SelectMany(t => plan.IsRollingMonthPlan
            ? MonthlyTaskCalendar.RollingOccurrences(t, plan.StartsOn, plan.EndsOn).Select(m => new Occurrence(t, m.ValidDate ?? plan.StartsOn, 0, m))
            : MonthlyTaskCalendar.UsesCalendar(t)
            ? MonthlyTaskCalendar.Occurrences(t, plan.StartsOn, plan.EndsOn).Select(m =>
                new Occurrence(t, m.ValidDate ?? new DateOnly(m.Year, m.Month, DateTime.DaysInMonth(m.Year, m.Month)), 0, m))
            : (policy?.AgendaVersion >= 2 && Distributed(t)
                ? OperationalSchedule.Dates(WeeklyBoardSnapshot.Monday(plan.StartsOn), WeeklyBoardSnapshot.Monday(plan.EndsOn).AddDays(6), t)
                    .Select(d => DateOnly.FromDayNumber(Math.Clamp(d.DayNumber, plan.StartsOn.DayNumber, plan.EndsOn.DayNumber)))
                : OperationalSchedule.Dates(plan.StartsOn, plan.EndsOn, t))
                .GroupBy(d => d).SelectMany(g => g.Select((d, slot) => new Occurrence(t, d, slot)))).Take(10001).ToArray();
        if (occurrences.Length > 10000) throw new DirectionPlanException("PLAN_TOO_MANY_OCCURRENCES", 422);
        var occurrenceCount = occurrences.Length;
        // Reserve constrained dates first so distributed repetitions see their real base load.
        var ledger = preparedLedger ?? await OperationalCapacityService.LedgerAsync(db, now, token);
        var eligibleWorkers = tasks.ToDictionary(t => t.Id, t => ledger.Workers.Where(w => w.EligibleFor(t.AssignedRole, t.SpecializationId)).ToArray());
        int CandidateCount(OperationalTask task) => eligibleWorkers[task.Id].Length;
        occurrences = occurrences.OrderBy(o => policy == null ? 0 : OperationalAgenda.Priority(o.Task.OperationalCriticality))
            .ThenBy(o => policy?.AgendaVersion >= 2 && o.Monthly != null ? 1 : 0)
            .ThenBy(o => Distributed(o.Task) ? 1 : 0).ThenBy(o => policy?.AgendaVersion >= 2 ? CandidateCount(o.Task) : 0).ThenByDescending(o => o.Task.SpecializationId != null)
            .ThenByDescending(o => o.Task.LoadMinutes).ThenBy(o => o.Day).ThenBy(o => o.Task.TaskCode).ThenBy(o => o.Slot).ToArray();
        var initialLedger = ledger;
        // Weekly operations include today's elapsed work at its original calendar
        // anchor. Other plan types retain the future-only preparation boundary.
        var notBefore = plan.IsWeeklyOperationsPlan ? OperationalSchedule.Instant(ledger.Today) : plan.SchedulingNotBefore ?? now;
        var specialties = await db.Set<Sgol.Web.Infrastructure.Persistence.Organization.Specialization>().AsNoTracking().ToDictionaryAsync(s => s.Id, token);
        var taskIds = tasks.Select(t => t.Id).ToArray();
        var existing = await db.OperationalAssignments.AsNoTracking().Where(a => taskIds.Contains(a.TaskId) && a.ReassignedFromId == null)
            .Select(a => new { a.TaskId, a.StartsAt, a.ScheduleKey, a.PersonId }).ToArrayAsync(token);
        if (policy != null && !plan.IsWeeklyOperationsPlan)
        {
            // An elapsed critical anchor is a dated notice, never a repetition to move
            // onto tomorrow. Existing work remains a protected reservation.
            occurrences = occurrences.Where(o => {
                if (o.Monthly != null || o.Task.OperationalCriticality != "CRITICA" || o.Task.StartOn(o.Day) >= now) return true;
                var key = $"{o.Task.Id:N}:{o.Day:yyyyMMdd}:{o.Slot}";
                if (!existing.Any(a => a.TaskId == o.Task.Id && (a.ScheduleKey == key ||
                    a.ScheduleKey == null && WeekContract.LocalToday(a.StartsAt) == o.Day)))
                    pending.Add(new(key, o.Task.TaskCode, o.Task.Name, o.Day.Year, o.Day.Month, o.Day.Day, "CRITICAL_ACTIVATION_PASSED"));
                return false;
            }).ToArray();
        }
        var assignments = new List<OperationalAssignment>();
        var rotationPeers = await db.OperationalAssignments.AsNoTracking().Where(a => taskIds.Contains(a.TaskId) && a.ReviewStatus != "REASIGNADA")
            .Select(a => new { a.TaskId, a.StartsAt, a.PersonId }).ToArrayAsync(token);
        DateOnly Week(DateTimeOffset instant) => WeeklyBoardSnapshot.Monday(WeekContract.LocalToday(instant));
        var reservedKeys = existing.Where(a => a.ScheduleKey != null).Select(a => a.ScheduleKey!).ToHashSet(StringComparer.Ordinal);
        var byTaskDay = existing.GroupBy(a => (a.TaskId, WeekContract.LocalToday(a.StartsAt))).ToDictionary(g => g.Key, g => g.Count());
        var byTaskWeek = existing.GroupBy(a => (a.TaskId, Week(a.StartsAt))).ToDictionary(g => g.Key, g => g.Count());
        var outsideWeek = existing.Where(a => WeekContract.LocalToday(a.StartsAt) < plan.StartsOn || WeekContract.LocalToday(a.StartsAt) > plan.EndsOn).GroupBy(a => (a.TaskId, Week(a.StartsAt))).ToDictionary(g => g.Key, g => g.Count());
        var scheduledWeek = occurrences.Where(o => !plan.IsWeeklyOperationsPlan || o.Day >= WeekContract.LocalToday(now)).GroupBy(o => (o.Task.Id, WeeklyBoardSnapshot.Monday(o.Day))).ToDictionary(g => g.Key, g => g.Count());
        var rotations = rotationPeers.GroupBy(a => (a.TaskId, Week(a.StartsAt), a.PersonId)).ToDictionary(g => g.Key, g => g.Count());
        var searchBudget = OperationalAgendaSearch.Budget * plan.SchedulingSearchMultiplier;
        var candidateBudget = OperationalAgendaSearch.CandidateBudget * plan.SchedulingSearchMultiplier;
        var explored = 0;
        var candidatesExamined = 0;
        DirectionPlanException? conflict = null;
        var reflow = policy?.AgendaVersion >= 2;
        if (reflow)
        {
            if (occurrences.Length > 1000) throw new DirectionPlanException("SCHEDULING_SEARCH_INCONCLUSIVE", 422) { Data = { ["Reason"] = "OCCURRENCE_SEARCH_SIZE_LIMIT", ["Occurrences"] = occurrences.Length, ["MaximumSearchOccurrences"] = 1000, ["Tasks"] = tasks.Length } };
            foreach (var occurrence in occurrences.Where(o => o.Task.OperationalCriticality == "CRITICA" && !Distributed(o.Task) && o.Day >= ledger.Today && o.Monthly == null))
            {
                var task = occurrence.Task;
                if (reservedKeys.Contains($"{task.Id:N}:{occurrence.Day:yyyyMMdd}:{occurrence.Slot}") || existing.Count(a => a.ScheduleKey == null && a.TaskId == task.Id && WeekContract.LocalToday(a.StartsAt) == occurrence.Day) > occurrence.Slot) continue;
                var probe = new OperationalAssignment { TaskId = task.Id, TaskCode = task.TaskCode, LoadMinutes = task.LoadMinutes, ScheduleMode = task.ScheduleMode, SpecializationId = task.SpecializationId };
                OperationalAgenda.Capture(probe, policy!, task.OperationalCriticality, task.StartOn(occurrence.Day));
                var candidates = eligibleWorkers[task.Id].ToArray();
                if (candidates.Any(w => OperationalAgenda.Find(ledger, w.Id, probe, notBefore).Eligible && ledger.CanAssign(w.Id, probe))) continue;
                var rejected = candidates.Select(w => {
                    var placement = OperationalAgenda.Find(ledger, w.Id, probe, notBefore);
                    var intervals = ledger.Temporal.Day(w.Id, occurrence.Day, ledger.Temporal.UsesCoverageSchedule(probe)).Intervals;
                    var hours = intervals == null ? "Sin configurar" : intervals.Length == 0 ? "Descanso" : string.Join(" · ", intervals.Select(i => $"{i.Entry / 60:00}:{i.Entry % 60:00}–{i.Exit / 60:00}:{i.Exit % 60:00}"));
                    return new PlanningCandidateConflict(occurrence.Day, probe.StartsAt, w.Name, placement.Eligible ? "PLAN_CAPACITY_EXCEEDED" : placement.Reason, hours, ledger.DayLoad(w.Id, occurrence.Day), ledger.DayCapacity(w.Id, occurrence.Day)) { NominalDueAt = probe.DueAt };
                }).ToArray();
                throw new DirectionPlanException("PLAN_NO_AVAILABLE_WORKER", 422) { Data = { [nameof(PlanningAssignmentConflict)] = new PlanningAssignmentConflict(task.TaskCode, task.Name, task.LoadMinutes, rejected) } };
            }
        }
        bool ForwardPossible(int index)
        {
            foreach (var remaining in occurrences.Skip(index).Where(o => o.Task.LoadMinutes >= 60 && o.Task.ScheduleMode != "PROGRESO" && o.Monthly == null))
            {
                var definition = remaining.Task;
                if (!Distributed(definition) && (reservedKeys.Contains($"{definition.Id:N}:{remaining.Day:yyyyMMdd}:{remaining.Slot}") || byTaskDay.GetValueOrDefault((definition.Id,remaining.Day)) > remaining.Slot)) continue;
                var week = WeeklyBoardSnapshot.Monday(remaining.Day);
                if (Distributed(definition) && byTaskWeek.GetValueOrDefault((definition.Id,week)) >= scheduledWeek.GetValueOrDefault((definition.Id,week))) continue;
                var dates = Distributed(definition) ? CapacityLedger.Days(DateOnly.FromDayNumber(Math.Max(Math.Max(week.DayNumber,plan.StartsOn.DayNumber),ledger.Today.DayNumber)),DateOnly.FromDayNumber(Math.Min(week.AddDays(6).DayNumber,plan.EndsOn.DayNumber))) : [remaining.Day];
                var possible = false;
                foreach (var day in dates)
                {
                    if (Distributed(definition) && (definition.Repetitions != 7 && day.DayOfWeek == DayOfWeek.Sunday || byTaskDay.GetValueOrDefault((definition.Id,day)) >= (definition.Repetitions + (definition.Repetitions == 7 ? 7 : 6) - 1)/(definition.Repetitions == 7 ? 7 : 6))) continue;
                    var probe = new OperationalAssignment { Id=Guid.Empty,TaskId=definition.Id,TaskCode=definition.TaskCode,LoadMinutes=definition.LoadMinutes,ScheduleMode=definition.ScheduleMode,SpecializationId=definition.SpecializationId };
                    OperationalAgenda.Capture(probe,policy!,definition.OperationalCriticality,definition.StartOn(day));
                    foreach (var candidate in eligibleWorkers[definition.Id])
                    {
                        var slot = OperationalAgenda.Find(ledger,candidate.Id,probe,notBefore);
                        if (!slot.Eligible) continue;
                        OperationalAgenda.Apply(probe,slot);
                        if (ledger.CanAssign(candidate.Id,probe)) { possible=true; break; }
                    }
                    if (possible) break;
                }
                if (!possible) return false;
            }
            return true;
        }
        async Task<bool> Search(int index)
        {
            // Bound the synchronous call stack for large weekly catalogues.
            // Search remains sequential; yielding does not share the ledger concurrently.
            if (index % 32 == 0) await Task.Yield();
            token.ThrowIfCancellationRequested();
            if (index == occurrences.Length) return true;
            if (++explored > searchBudget && reflow) throw new DirectionPlanException("SCHEDULING_SEARCH_INCONCLUSIVE", 422) { Data = { ["Reason"] = "NODE_BUDGET", ["Explored"] = explored, ["Budget"] = searchBudget, ["Task"] = occurrences[index].Task.TaskCode, ["OccurrenceIndex"] = index } };
            var occurrence = occurrences[index];
            var task = occurrence.Task;
            var monthly = occurrence.Monthly;
            void Pending(string reason) => pending.Add(new(monthly!.Key(task.Id), task.TaskCode, task.Name,
                monthly.Year, monthly.Month, monthly.PreferredDay, reason));
            if (monthly != null && reservedKeys.Contains(monthly.Key(task.Id))) return await Search(index + 1);
            if (monthly?.RollingStart is { } rollingStart && existing.Count(a => a.TaskId == task.Id &&
                WeekContract.LocalToday(a.StartsAt) >= rollingStart && WeekContract.LocalToday(a.StartsAt) <= monthly.RollingEnd &&
                (a.ScheduleKey == null || !a.ScheduleKey.StartsWith($"{task.Id:N}:R:{rollingStart:yyyyMMdd}:", StringComparison.Ordinal))) > monthly.DistributedSlot)
                return await Search(index + 1);
            if (monthly?.DistributedSlot is { } monthlySlot && monthly.RollingStart == null && existing.Count(a => a.TaskId == task.Id &&
                (a.ScheduleKey == null || !a.ScheduleKey.StartsWith($"{task.Id:N}:M:", StringComparison.Ordinal)) &&
                WeekContract.LocalToday(a.StartsAt).Year == monthly.Year && WeekContract.LocalToday(a.StartsAt).Month == monthly.Month) > monthlySlot)
                return await Search(index + 1);
            if (monthly != null && monthly.ValidDate == null) { Pending("MONTH_DAY_NOT_IN_MONTH"); return await Search(index + 1); }
            if (monthly != null && monthly.DistributedSlot == null && occurrence.Day < ledger.Today) { Pending("MONTH_DATE_PASSED"); return await Search(index + 1); }
            // Operations may include expired hours on the creation day, never earlier dates.
            if (plan.IsWeeklyOperationsPlan && monthly?.DistributedSlot == null && occurrence.Day < WeekContract.LocalToday(now)) return await Search(index + 1);
            var weekStart = WeeklyBoardSnapshot.Monday(occurrence.Day);
            var weekEnd = weekStart.AddDays(6);
            int ReservedOn(DateOnly date) => byTaskDay.GetValueOrDefault((task.Id, date));
            if (Distributed(task))
            {
                var target = scheduledWeek.GetValueOrDefault((task.Id, weekStart)) + (reflow ? 0 : outsideWeek.GetValueOrDefault((task.Id, weekStart)));
                if (byTaskWeek.GetValueOrDefault((task.Id, weekStart)) >= target) return await Search(index + 1);
            }
            else if (monthly == null && (reservedKeys.Contains($"{task.Id:N}:{occurrence.Day:yyyyMMdd}:{occurrence.Slot}") ||
                existing.Count(a => a.ScheduleKey == null && a.TaskId == task.Id && WeekContract.LocalToday(a.StartsAt) == occurrence.Day) > occurrence.Slot)) return await Search(index + 1);
            var starts = task.StartOn(occurrence.Day);
            var due = task.ScheduleMode == "PROGRESO" ? OperationalSchedule.Instant(occurrence.Day.AddDays(7)) : starts.AddHours(task.DeadlineHours);
            var assignment = new OperationalAssignment { Id = Guid.CreateVersion7(), PlanId = plan.Id, TaskId = task.Id, TaskRevision = task.Revision, DefinitionSnapshotJson = AssignmentDefinition.Capture(task),
                TaskCode = task.TaskCode, Name = task.Name, Description = task.Description, Requirement = task.Requirement, ChecklistJson = task.ChecklistJson, AssignedRole = task.AssignedRole,
                MinimumPhotos = task.MinimumPhotos, MaximumPhotos = task.MaximumPhotos, MaximumPhotoBytes = task.MaximumPhotoBytes,
                SpecializationId = task.SpecializationId, SpecializationName = task.SpecializationId is { } specialty ? specialties[specialty].Name : null,
                LoadMinutes = task.LoadMinutes, StartsAt = starts, DueAt = due, PeriodType = task.PeriodType, Repetitions = task.Repetitions,
                ScheduleMode = task.ScheduleMode, DifferentPeople = task.DifferentPeople,
                PreferredMonthDays = task.PreferredMonthDays,
                GoalUnits = task.ScheduleMode == "PROGRESO" ? task.GoalUnits ?? 0 : 0, ProgressUnit = task.ProgressUnit };
            void At(DateOnly date)
            {
                if (policy != null) OperationalAgenda.Capture(assignment, policy, task.OperationalCriticality, task.StartOn(date));
                else { assignment.StartsAt = task.StartOn(date); assignment.DueAt = task.ScheduleMode == "PROGRESO" ? OperationalSchedule.Instant(date.AddDays(7)) : assignment.StartsAt.AddHours(task.DeadlineHours); }
            }
            At(occurrence.Day);
            var matching = ledger.Workers.Where(w => task.SpecializationId != null || w.Role == task.AssignedRole).ToArray();
            if (matching.Length == 0) { if (monthly != null) { Pending("PLAN_NO_WORKER_FOR_ROLE"); return await Search(index + 1); } throw new DirectionPlanException("PLAN_NO_WORKER_FOR_ROLE", 422); }
            matching = matching.Where(w => w.Qualified(task.SpecializationId)).ToArray();
            if (matching.Length == 0) { if (monthly != null) { Pending("PLAN_NO_SPECIALIST"); return await Search(index + 1); } throw new DirectionPlanException("PLAN_NO_SPECIALIST", 422); }
            int Repetitions(Guid person) => task.DifferentPeople ? rotations.GetValueOrDefault((task.Id, weekStart, person)) : 0;
            var from = DateOnly.FromDayNumber(Math.Max(weekStart.DayNumber, ledger.Today.DayNumber));
            var through = DateOnly.FromDayNumber(Math.Max(weekEnd.DayNumber, ledger.Today.DayNumber));
            var dates = monthly != null ? MonthlyTaskCalendar.Candidates(monthly, plan.StartsOn, plan.EndsOn, ledger.Today) : Distributed(task) ? CapacityLedger.Days(
                DateOnly.FromDayNumber(Math.Max(Math.Max(weekStart.DayNumber, plan.StartsOn.DayNumber),
                    plan.IsWeeklyOperationsPlan ? WeekContract.LocalToday(now).DayNumber : plan.StartsOn.DayNumber)),
                DateOnly.FromDayNumber(Math.Min(weekEnd.DayNumber, plan.EndsOn.DayNumber))) : [occurrence.Day];
            // Keep the configured frequency spread across distinct days (or balanced slots for >7/week).
            var options = new List<(DateOnly Day, CapacityWorker Worker, decimal Daily, decimal Total, decimal Period, SchedulingPlacement Placement)>();
            foreach (var date in dates)
            {
                // Sunday is explicit: seven weekly repetitions or a fixed weekday selection.
                if (Distributed(task) && task.Repetitions != 7 && date.DayOfWeek == DayOfWeek.Sunday) continue;
                var distributionDays = task.Repetitions == 7 ? 7 : 6;
                if (Distributed(task) && ReservedOn(date) >= (task.Repetitions + distributionDays - 1) / distributionDays) continue;
                At(date);
                var received = !plan.IsWeeklyOperationsPlan && assignment.StartsAt <= now ? now : (DateTimeOffset?)null;
                var total = matching.Sum(w => ledger.DayLoad(w.Id, date));
                foreach (var candidate in matching)
                {
                    At(date);
                    var placement = policy == null ? new SchedulingPlacement(true, assignment.StartsAt, assignment.DueAt, "LEGACY_CONTRACT") : OperationalAgenda.Find(ledger, candidate.Id, assignment, notBefore);
                    if (!placement.Eligible) continue;
                    OperationalAgenda.Apply(assignment, placement);
                    if (!ledger.CanAssign(candidate.Id, assignment, received)) continue;
                    options.Add((date, candidate, ledger.Occupancy(candidate.Id, date < ledger.Today ? ledger.Today : date,
                        date < ledger.Today ? ledger.Today : date), total, task.SpecializationId == null ?
                        ledger.Occupancy(candidate.Id, from, through) : ledger.SpecializedPeriodLoad(candidate.Id, assignment), placement));
                }
            }
            var ordered = options.OrderBy(o => monthly != null && monthly.RollingStart == null ? Math.Abs(o.Day.DayNumber - occurrence.Day.DayNumber) : 0)
                .ThenBy(o => Distributed(task) ? ReservedOn(o.Day) : 0)
                .ThenBy(o => Distributed(task) ? o.Total : 0)
                .ThenBy(o => policy != null && task.OperationalCriticality == "IMPORTANTE" ? o.Placement.StartsAt : DateTimeOffset.MinValue)
                .ThenBy(o => o.Daily)
                .ThenBy(o => policy == null ? DateTimeOffset.MinValue : o.Placement.StartsAt)
                .ThenBy(o => monthly != null ? o.Day : DateOnly.MinValue)
                .ThenBy(o => Repetitions(o.Worker.Id))
                .ThenBy(o => o.Period)
                .ThenBy(o => o.Day).ThenBy(o => o.Worker.Code, StringComparer.Ordinal).ThenBy(o => o.Worker.Id).ToArray();
            var selected = ordered.FirstOrDefault();
            var worker = selected.Worker;
            if (worker == null)
            {
                if (monthly != null) { Pending(monthly.RollingStart != null ? "ROLLING_MONTH_NO_SLOT" : monthly.DistributedSlot == null ? "PLAN_NO_AVAILABLE_WORKER" : "MONTH_DISTRIBUTED_NO_SLOT"); return await Search(index + 1); }
                var rejected = new List<PlanningCandidateConflict>();
                foreach (var date in dates)
                {
                    if (Distributed(task) && task.Repetitions != 7 && date.DayOfWeek == DayOfWeek.Sunday) continue;
                    At(date);
                    foreach (var candidate in matching)
                    {
                        var decision = ledger.CheckTime(candidate.Id, assignment, !plan.IsWeeklyOperationsPlan && assignment.StartsAt <= now ? now : null);
                        var placement = policy == null ? new SchedulingPlacement(true, assignment.StartsAt, assignment.DueAt, "LEGACY_CONTRACT") : OperationalAgenda.Find(ledger, candidate.Id, assignment, notBefore);
                        var reason = !placement.Eligible ? placement.Reason : !decision.Eligible ? decision.Reason :
                            Distributed(task) && ReservedOn(date) >= (task.Repetitions + (task.Repetitions == 7 ? 7 : 6) - 1) / (task.Repetitions == 7 ? 7 : 6)
                                ? "PLAN_DAILY_REPETITIONS_RESERVED" : "PLAN_CAPACITY_EXCEEDED";
                        var intervals = ledger.Temporal.Day(candidate.Id, date, ledger.Temporal.UsesCoverageSchedule(assignment)).Intervals;
                        var hours = intervals == null ? "Sin configurar" : intervals.Length == 0 ? "Descanso" : string.Join(" · ", intervals.Select(i => $"{i.Entry / 60:00}:{i.Entry % 60:00}–{i.Exit / 60:00}:{i.Exit % 60:00}"));
                        var day = date < ledger.Today ? ledger.Today : date;
                        rejected.Add(new(date, assignment.StartsAt, candidate.Name, reason, hours, ledger.DayLoad(candidate.Id, day), ledger.DayCapacity(candidate.Id, day)) { NominalDueAt = assignment.DueAt });
                    }
                }
                var error = new DirectionPlanException("PLAN_NO_AVAILABLE_WORKER", 422);
                error.Data[nameof(PlanningAssignmentConflict)] = new PlanningAssignmentConflict(task.TaskCode, task.Name, task.LoadMinutes, rejected.ToArray());
                conflict = error;
                return false;
            }
            var originalLedger = ledger;
            var originalRole = assignment.AssignedRole;
            var choices = ordered.AsEnumerable();
            if (reflow)
                // Try already computed placements before enumerating every possible minute.
                // Keep the complete domain as a lazy fallback when backtracking needs it.
                choices = ordered.Concat(Expanded()).DistinctBy(o => (o.Day, o.Worker.Id, o.Placement.StartsAt, o.Placement.DueAt));
            IEnumerable<(DateOnly Day, CapacityWorker Worker, decimal Daily, decimal Total, decimal Period, SchedulingPlacement Placement)> Expanded()
            {
                var expanded = new List<(DateOnly Day, CapacityWorker Worker, decimal Daily, decimal Total, decimal Period, SchedulingPlacement Placement)>();
                foreach (var date in dates)
                {
                    if (Distributed(task) && (task.Repetitions != 7 && date.DayOfWeek == DayOfWeek.Sunday || ReservedOn(date) >= (task.Repetitions + (task.Repetitions == 7 ? 7 : 6) - 1) / (task.Repetitions == 7 ? 7 : 6))) continue;
                    foreach (var candidate in matching)
                    {
                        At(date);
                        foreach (var placement in OperationalAgenda.Alternatives(originalLedger, candidate.Id, assignment, notBefore))
                        {
                            token.ThrowIfCancellationRequested();
                            if (++candidatesExamined > candidateBudget) throw new DirectionPlanException("SCHEDULING_SEARCH_INCONCLUSIVE", 422) { Data = { ["Reason"] = "CANDIDATE_BUDGET", ["Candidates"] = candidatesExamined, ["Budget"] = candidateBudget, ["Task"] = task.TaskCode, ["OccurrenceIndex"] = index } };
                            OperationalAgenda.Apply(assignment, placement);
                            if (originalLedger.CanAssign(candidate.Id, assignment, !plan.IsWeeklyOperationsPlan && assignment.StartsAt <= now ? now : null))
                                expanded.Add((date,candidate,originalLedger.Occupancy(candidate.Id,date,date),0,0,placement));
                        }
                    }
                }
                foreach (var option in expanded.OrderBy(o => monthly != null && monthly.RollingStart == null ? Math.Abs(o.Day.DayNumber - occurrence.Day.DayNumber) : 0)
                    .ThenBy(o => task.OperationalCriticality == "IMPORTANTE" ? (o.Placement.StartsAt - task.StartOn(o.Day)).TotalMinutes : Math.Abs((o.Placement.StartsAt - task.StartOn(o.Day)).TotalMinutes))
                    .ThenBy(o => Repetitions(o.Worker.Id)).ThenBy(o => o.Daily).ThenBy(o => o.Day).ThenBy(o => o.Worker.Code, StringComparer.Ordinal).ThenBy(o => o.Worker.Id)) yield return option;
            }
            foreach (var choice in choices)
            {
            selected = choice; worker = selected.Worker;
            At(selected.Day);
            OperationalAgenda.Apply(assignment, selected.Placement);
            var slot = Distributed(task) ? Enumerable.Range(0, 101).First(s =>
                !reservedKeys.Contains($"{task.Id:N}:{selected.Day:yyyyMMdd}:{s}") &&
                s >= existing.Count(a => a.TaskId == task.Id && a.ScheduleKey == null && WeekContract.LocalToday(a.StartsAt) == selected.Day)) : occurrence.Slot;
            assignment.ScheduleKey = monthly?.Key(task.Id) ?? $"{task.Id:N}:{selected.Day:yyyyMMdd}:{slot}";
            var reception = !plan.IsWeeklyOperationsPlan && assignment.StartsAt <= now ? now : (DateTimeOffset?)null;
            assignment.PersonId = worker.Id; assignment.PersonName = worker.Name; assignment.AssignedRole = worker.Role;
            assignment.PreviousLoadMinutes = ledger.Tasks.Where(a => a.PersonId == worker.Id).Sum(a => (long)a.RemainingMinutes) + ledger.Legacy.GetValueOrDefault(worker.Id);
            ledger.CaptureTime(assignment, reception); assignments.Add(assignment);
            reservedKeys.Add(assignment.ScheduleKey);
            var dateKey = (task.Id, selected.Day); byTaskDay[dateKey] = byTaskDay.GetValueOrDefault(dateKey) + 1;
            var weekKey = (task.Id, Week(assignment.StartsAt)); byTaskWeek[weekKey] = byTaskWeek.GetValueOrDefault(weekKey) + 1;
            var rotationKey = (task.Id, Week(assignment.StartsAt), worker.Id); rotations[rotationKey] = rotations.GetValueOrDefault(rotationKey) + 1;
            ledger = ledger.WithAdded(assignment);
            var pendingCount = pending.Count;
            if ((!reflow || ForwardPossible(index + 1)) && await Search(index + 1)) return true;
            if (pending.Count > pendingCount) pending.RemoveRange(pendingCount, pending.Count - pendingCount);
            ledger = originalLedger;
            assignments.RemoveAt(assignments.Count - 1);
            reservedKeys.Remove(assignment.ScheduleKey);
            byTaskDay[dateKey]--; byTaskWeek[weekKey]--; rotations[rotationKey]--;
            assignment.AssignedRole = originalRole;
            if (!reflow) break;
            }
            if (monthly?.DistributedSlot != null) { Pending(monthly.RollingStart != null ? "ROLLING_MONTH_NO_SLOT" : "MONTH_DISTRIBUTED_NO_SLOT"); return await Search(index + 1); }
            return false;
        }
        if (!await Search(0)) throw conflict ?? new DirectionPlanException("PLAN_NO_AVAILABLE_WORKER", 422);

        if (!allowReserved && occurrenceCount > 0 && assignments.Count == 0 && pending.Count == 0) throw new DirectionPlanException("PLAN_OCCURRENCES_RESERVED", 422);
        if (!reflow) OperationalPlanBalancer.Balance(ledger, assignments, plan.IsWeeklyOperationsPlan, now, token);
        // Validate the complete result, including reservations and capacity, after every search
        // or balancing path. Saving a partial or internally inconsistent agenda is forbidden.
        foreach (var assignment in assignments.Where(a => a.SchedulingPolicyId != null))
        {
            var others = new CapacityLedger(initialLedger.Workers, initialLedger.Tasks.Concat(assignments.Where(a => a.Id != assignment.Id)).ToArray(), initialLedger.Capacities,
                initialLedger.Unavailable, initialLedger.Legacy, initialLedger.Today) { Temporal = initialLedger.Temporal, PlannedChanges = initialLedger.PlannedChanges, Override = initialLedger.Override };
            if (!others.CanAssign(assignment.PersonId, assignment, !plan.IsWeeklyOperationsPlan && assignment.StartsAt <= now ? now : null))
                throw new DirectionPlanException("SCHEDULING_AGENDA_INVALID", 422);
        }
        // Capture the final recipient and previous load in the original generation order.
        ledger = initialLedger;
        foreach (var assignment in assignments)
        {
            assignment.PreviousLoadMinutes = ledger.Tasks.Where(a => a.PersonId == assignment.PersonId).Sum(a => (long)a.RemainingMinutes) + ledger.Legacy.GetValueOrDefault(assignment.PersonId);
            ledger.CaptureTime(assignment, !plan.IsWeeklyOperationsPlan && assignment.StartsAt <= now ? now : null);
            ledger = ledger.WithAdded(assignment);
        }
        if (pending.Count > 0 || tasks.Any(MonthlyTaskCalendar.UsesCalendar))
        {
            var retained = System.Text.Json.JsonSerializer.Deserialize<MonthlyPlanPending[]>(plan.MonthlyPendingJson)!
                .Where(p => !codes.Contains(p.TaskCode));
            plan.CaptureMonthlyPending(retained.Concat(pending).ToArray());
        }
        return assignments.ToArray();
    }
}
