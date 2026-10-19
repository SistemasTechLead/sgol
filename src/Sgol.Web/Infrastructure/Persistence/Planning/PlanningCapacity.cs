using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Sgol.Planning.Contracts;

namespace Sgol.Web.Infrastructure.Persistence.Planning;

public sealed record PlanningCapacityRow(Guid PersonId, string Name, string Role, string[] Tasks,
    long CurrentLoadMinutes, long PeriodLoadMinutes, decimal AvailableMinutes, int Percent, int DailyMinutes);
public sealed record TemporalPlanRow(DateOnly Day, string Task, string Person, string Reason, bool Eligible, string Hours)
{
    public DateTimeOffset StartsAt { get; init; }
}
public sealed record PlanDailyLoad(DateOnly Day, long PlanMinutes, decimal PendingMinutes, decimal CapacityMinutes);
public sealed record PlanningSpecialistGap(string Specialization, DirectionPlanTask[] Tasks);
public sealed record PlanningCandidateConflict(DateOnly Day, DateTimeOffset StartsAt, string Person, string Reason, string Hours, decimal PendingMinutes, decimal CapacityMinutes);
public sealed record PlanningAssignmentConflict(string TaskCode, string Task, int LoadMinutes, PlanningCandidateConflict[] Candidates);
public sealed record PlanningCapacityPreview(DateOnly From, DateOnly Through, string Fingerprint, PlanningCapacityRow[] Workers)
{
    public PlanningSpecialistGap[] SpecialistGaps { get; init; } = [];
    public MonthlyPlanPending[] MonthlyPending { get; set; } = [];
    public TemporalPlanRow[] TemporalRows { get; set; } = [];
    public bool TemporalPolicyEnabled { get; init; }
}

// Read the same ledger used by generation and supervision; preparation never writes capacity.
public static class PlanningCapacity
{
    public static bool Expired(OperationalTask task, DateOnly day, DateTimeOffset now, TemporalEligibilityContext temporal)
    {
        var due = temporal.Enabled && task.Id == temporal.ClosingTaskId
            ? TemporalEligibilityContext.At(day, day.DayOfWeek == DayOfWeek.Sunday ? 900 : 1260)
            : task.ScheduleMode == "PROGRESO" ? OperationalSchedule.Instant(day.AddDays(7)) : task.StartOn(day).AddHours(task.DeadlineHours);
        return due <= now;
    }
    public static async Task<PlanDailyLoad[]> DailyLoadAsync(SgolDbContext db, DirectionPlanInput input,
        PlanningCapacityPreview preview, CapacityLedger ledger, CancellationToken token)
    {
        var range = DirectionPlanContract.Calculate(input.PeriodType, input.Year, input.PeriodNumber, input.IsWeeklyOperationsPlan ? ledger.Today : null);
        var tasks = await db.OperationalTasks.AsNoTracking()
            .Where(t => input.TaskCodes.Contains(t.TaskCode) && t.Active && !t.Deleted).ToArrayAsync(token);
        var taskIds = tasks.Select(t => t.Id).ToArray();
        var existing = await db.OperationalAssignments.AsNoTracking()
            .Where(a => taskIds.Contains(a.TaskId) && a.ReassignedFromId == null)
            .Select(a => new { a.TaskId, a.StartsAt, a.ScheduleKey }).ToArrayAsync(token);
        var loads = tasks.SelectMany(t => OperationalSchedule.Dates(range.StartsOn, range.EndsOn, t)
            .GroupBy(d => d).SelectMany(g => g.Select((day, slot) => new { Task = t, Day = day, Slot = slot })))
            .Where(o => !existing.Any(a => a.ScheduleKey == $"{o.Task.Id:N}:{o.Day:yyyyMMdd}:{o.Slot}") &&
                existing.Count(a => a.ScheduleKey == null && a.TaskId == o.Task.Id && WeekContract.LocalToday(a.StartsAt) == o.Day) <= o.Slot)
            .GroupBy(o => o.Day).ToDictionary(g => g.Key, g => g.Sum(o => (long)o.Task.LoadMinutes));
        var adjusted = new CapacityLedger(ledger.Workers, ledger.Tasks, ledger.Capacities, ledger.Unavailable, ledger.Legacy, ledger.Today)
            { Temporal = ledger.Temporal, PlannedChanges = Changes(input, preview, ledger) };
        if (tasks.Any(t => t.PreferredMonthDays.Length > 0))
        {
            var plan = new DirectionPreparedPlan(Guid.NewGuid(), Sgol.Organization.Contracts.BranchScope.LorettaId, Guid.NewGuid(),
                OperationalSchedule.Instant(ledger.Today, 0), input);
            var generated = await OperationalPlanGenerator.GenerateAsync(db, plan, input.TaskCodes,
                DateTimeOffset.UtcNow, token, allowReserved: true, preparedLedger: adjusted);
            preview.MonthlyPending = System.Text.Json.JsonSerializer.Deserialize<MonthlyPlanPending[]>(plan.MonthlyPendingJson)!;
            loads = generated.GroupBy(a => WeekContract.LocalToday(a.StartsAt)).ToDictionary(g => g.Key, g => g.Sum(a => (long)a.LoadMinutes));
            var monthlyNames = tasks.Where(t => t.PreferredMonthDays.Length > 0).Select(t => t.Name).ToHashSet();
            preview.TemporalRows = preview.TemporalRows.Where(r => !monthlyNames.Contains(r.Task)).Concat(
                generated.Where(a => a.PreferredMonthDays.Length > 0).Select(a => {
                    var day = WeekContract.LocalToday(a.StartsAt);
                    var spans = adjusted.Temporal.Day(a.PersonId, day).Intervals;
                    var hours = spans == null ? "Sin configurar" : string.Join(" · ", spans.Select(s => $"{s.Entry/60:00}:{s.Entry%60:00}–{s.Exit/60:00}:{s.Exit%60:00}"));
                    return new TemporalPlanRow(day, a.Name, a.PersonName, "ELIGIBLE", true, hours) { StartsAt = a.StartsAt };
                })).ToArray();
        }
        var workers = preview.Workers.Select(w => w.PersonId).ToArray();
        return CapacityLedger.Days(range.StartsOn, range.EndsOn).Select(day => new PlanDailyLoad(day,
            loads.GetValueOrDefault(day), workers.Sum(id => adjusted.DayLoad(id, day)),
            workers.Sum(id => adjusted.DayCapacity(id, day)))).ToArray();
    }

    public static async Task<(PlanningCapacityPreview Preview, CapacityLedger Ledger)> ReadAsync(
        SgolDbContext db, DirectionPlanInput input, DateTimeOffset now, CancellationToken token)
    {
        input = DirectionPlanContract.Normalize(input);
        var range = DirectionPlanContract.Calculate(input.PeriodType, input.Year, input.PeriodNumber, input.IsWeeklyOperationsPlan ? WeekContract.LocalToday(now) : null);
        var ledger = await OperationalCapacityService.LedgerAsync(db, now, token);
        var from = DateOnly.FromDayNumber(Math.Max(range.StartsOn.DayNumber, ledger.Today.DayNumber));
        var through = DateOnly.FromDayNumber(Math.Max(range.EndsOn.DayNumber, from.DayNumber));
        var tasks = await db.OperationalTasks.AsNoTracking().Where(t => input.TaskCodes.Contains(t.TaskCode) && t.Active && !t.Deleted)
            .OrderBy(t => t.TaskCode).ToArrayAsync(token);
        if (tasks.Length != input.TaskCodes.Count || tasks.Any(t => t.PeriodType == "OCASIONAL" || t.ProcessId != OperationalSequence.GeneralId)) throw new DirectionPlanException("DIRECTION_PLAN_TASK_INVALID", 422);
        var unstaffed = tasks.Where(t => t.SpecializationId != null && !ledger.Workers.Any(w => w.Qualified(t.SpecializationId))).ToArray();
        var specializationIds = unstaffed.Select(t => t.SpecializationId!.Value).Distinct().ToArray();
        var specializationNames = await db.Set<Sgol.Web.Infrastructure.Persistence.Organization.Specialization>().AsNoTracking()
            .Where(s => specializationIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id, s => s.Name, token);
        var specialistGaps = unstaffed.GroupBy(t => t.SpecializationId!.Value)
            .Select(g => new PlanningSpecialistGap(specializationNames[g.Key], g.Select(t => new DirectionPlanTask(t.TaskCode, t.Name)).ToArray()))
            .OrderBy(g => g.Specialization).ToArray();
        var scheduled = tasks.Where(t => OperationalSchedule.Dates(range.StartsOn, range.EndsOn, t).Any()).ToArray();
        var workers = ledger.Workers.Where(w => scheduled.Any(t => w.EligibleFor(t.AssignedRole, t.SpecializationId)))
            .OrderBy(w => w.Role).ThenBy(w => w.Name).ThenBy(w => w.Id).Select(w => {
                var setting = ledger.Setting(w.Id, from);
                return new PlanningCapacityRow(w.Id, w.Name, w.Role, scheduled.Where(t => w.EligibleFor(t.AssignedRole, t.SpecializationId)).Select(t => t.Name).ToArray(),
                    ledger.Tasks.Where(t => t.PersonId == w.Id).Sum(t => (long)t.RemainingMinutes) + ledger.Legacy.GetValueOrDefault(w.Id),
                    ledger.Load(w.Id, from, through), ledger.Available(w.Id, from, through), setting?.Percent ?? 100, setting?.DailyMinutes ?? OperationalCapacity.DefaultDailyMinutes);
            }).ToArray();
        // Include completed rotation peers as well as all ledger inputs in the concurrency snapshot.
        var peers = await db.OperationalAssignments.AsNoTracking().Where(a => input.TaskCodes.Contains(a.TaskCode))
            .OrderBy(a => a.Id).Select(a => new { a.Id, a.PersonId, a.StartsAt, a.ScheduleKey, a.ReviewStatus, a.Revision }).ToArrayAsync(token);
        var snapshot = JsonSerializer.SerializeToUtf8Bytes(new { BalancePolicyVersion = OperationalPlanBalancer.PolicyVersion, input.PeriodType, input.Year, input.PeriodNumber, input.TaskCodes, input.IsWeeklyOperationsPlan,
            from, through, tasks, peers, DefaultDailyMinutes = OperationalCapacity.DefaultDailyMinutes, ledger.Today, ledger.Workers, ledger.Tasks, ledger.Capacities, ledger.Temporal,
            Unavailable = ledger.Unavailable.OrderBy(a => a.Item1).ThenBy(a => a.Item2).Select(a => new { Person = a.Item1, Day = a.Item2 }),
            Legacy = ledger.Legacy.OrderBy(a => a.Key).Select(a => new { Person = a.Key, Minutes = a.Value }) });
        var context = new TemporalEligibilityContext { Enabled = true, Schedules = ledger.Temporal.Schedules, Exceptions = ledger.Temporal.Exceptions,
            Rotations = ledger.Temporal.Rotations, Coverages = ledger.Temporal.Coverages, ClosingTaskId=ledger.Temporal.ClosingTaskId };
        var temporalLedger=new CapacityLedger(ledger.Workers,ledger.Tasks,ledger.Capacities,ledger.Unavailable,ledger.Legacy,ledger.Today) { Temporal=context };
        var temporalRows = tasks.SelectMany(t => OperationalSchedule.Dates(range.StartsOn, range.EndsOn, t).SelectMany(day =>
            ledger.Workers.Where(w => w.EligibleFor(t.AssignedRole, t.SpecializationId) || t.Id == context.ClosingTaskId && context.ClosingPerson(day) == w.Id).Select(w => {
                var decision = temporalLedger.CheckTime(w.Id, new OperationalAssignment { TaskId = t.Id, StartsAt = t.StartOn(day), DueAt = t.ScheduleMode == "PROGRESO" ? OperationalSchedule.Instant(day.AddDays(7)) : t.StartOn(day).AddHours(t.DeadlineHours), LoadMinutes = t.LoadMinutes },!input.IsWeeklyOperationsPlan && t.StartOn(day)<=now?now:null);
                var isExpired = !input.IsWeeklyOperationsPlan && Expired(t, day, now, ledger.Temporal);
                var spans = context.Day(w.Id, day).Intervals;
                var hours = spans == null ? "Sin configurar" : spans.Length == 0 ? "Descanso" : string.Join(" · ", spans.Select(s => $"{s.Entry/60:00}:{s.Entry%60:00}–{s.Exit/60:00}:{s.Exit%60:00}"));
                return new TemporalPlanRow(day, t.Name, w.Name, isExpired ? "SCHEDULE_DEADLINE_PASSED" : !w.EligibleFor(t.AssignedRole, t.SpecializationId) ? "SCHEDULE_ROLE_CONFLICT" : decision.Reason, !isExpired && decision.Eligible && w.EligibleFor(t.AssignedRole, t.SpecializationId), hours) { StartsAt = t.StartOn(day) };
            }))).Take(10000).ToArray();
        return (new PlanningCapacityPreview(from, through, Convert.ToHexString(SHA256.HashData(snapshot)), workers) { SpecialistGaps = specialistGaps, TemporalRows = temporalRows, TemporalPolicyEnabled = ledger.Temporal.Enabled }, ledger);
    }

    public static CapacityChange[] Changes(DirectionPlanInput input, PlanningCapacityPreview preview, CapacityLedger ledger) =>
        (input.CapacityAdjustments ?? []).SelectMany(a => {
            var worker = preview.Workers.SingleOrDefault(w => w.PersonId == a.PersonId)
                ?? throw new DirectionPlanException("PLAN_CAPACITY_INVALID", 422);
            if (a.Percent > worker.Percent) throw new DirectionPlanException("PLAN_CAPACITY_INVALID", 422);
            var changes = new List<CapacityChange>();
            foreach (var day in CapacityLedger.Days(preview.From, preview.Through))
            {
                var setting = ledger.Setting(a.PersonId, day);
                var percent = Math.Min(a.Percent, setting?.Percent ?? 100);
                var minutes = setting?.DailyMinutes ?? OperationalCapacity.DefaultDailyMinutes;
                if (changes.LastOrDefault() is { } previous && previous.Percent == percent && previous.DailyMinutes == minutes)
                    changes[^1] = previous with { Through = day };
                else changes.Add(new(a.PersonId, day, day, percent, minutes, $"Ajuste al crear el plan: {input.Name}"));
            }
            return changes;
        }).ToArray();
}
