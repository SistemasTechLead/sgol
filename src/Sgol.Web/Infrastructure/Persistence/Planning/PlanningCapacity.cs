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
    public DateTimeOffset? NominalStartsAt { get; init; }
    public DateTimeOffset? DueAt { get; init; }
    public string? OperationalCriticality { get; init; }
    public int LoadMinutes { get; init; }
}
public sealed record PlanDailyLoad(DateOnly Day, long PlanMinutes, decimal PendingMinutes, decimal CapacityMinutes);
public sealed record PlanningSpecialistGap(string Specialization, DirectionPlanTask[] Tasks);
public sealed record PlanningCandidateConflict(DateOnly Day, DateTimeOffset StartsAt, string Person, string Reason, string Hours, decimal PendingMinutes, decimal CapacityMinutes)
{
    public DateTimeOffset? NominalDueAt { get; init; }
}
public sealed record PlanningAssignmentConflict(string TaskCode, string Task, int LoadMinutes, PlanningCandidateConflict[] Candidates);
public sealed record PlanningCapacityPreview(DateOnly From, DateOnly Through, string Fingerprint, PlanningCapacityRow[] Workers)
{
    public string[] ExcludedTasks { get; init; } = [];
    public PlanningSpecialistGap[] SpecialistGaps { get; init; } = [];
    public MonthlyPlanPending[] MonthlyPending { get; set; } = [];
    public TemporalPlanRow[] TemporalRows { get; set; } = [];
    public string? AgendaFingerprint { get; set; }
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
        var range = DirectionPlanContract.Calculate(input, input.IsWeeklyOperationsPlan ? ledger.Today : null);
        var adjusted = new CapacityLedger(ledger.Workers, ledger.Tasks, ledger.Capacities, ledger.Unavailable, ledger.Legacy, ledger.Today)
            { Temporal = ledger.Temporal, PlannedChanges = Changes(input, preview, ledger) };
        var policy = await OperationalSchedulingPolicyService.CurrentAsync(db, token);
        var evaluatedAt = ledger.Temporal.Now == default ? DateTimeOffset.UtcNow : ledger.Temporal.Now;
        var plan = new DirectionPreparedPlan(Guid.NewGuid(), Sgol.Organization.Contracts.BranchScope.LorettaId, Guid.NewGuid(), evaluatedAt.ToUniversalTime(), input);
        plan.CaptureSchedulingPolicy(policy.Id);
        var generated = await OperationalPlanGenerator.GenerateAsync(db, plan, input.TaskCodes, evaluatedAt, token, allowReserved: true, preparedLedger: adjusted);
        preview.MonthlyPending = JsonSerializer.Deserialize<MonthlyPlanPending[]>(plan.MonthlyPendingJson)!;
        preview.TemporalRows = generated.Select(a => {
            var day = WeekContract.LocalToday(a.StartsAt);
            var spans = adjusted.Temporal.Day(a.PersonId, day, adjusted.Temporal.UsesCoverageSchedule(a)).Intervals;
            var hours = spans == null ? "Sin configurar" : string.Join(" · ", spans.Select(s => $"{s.Entry/60:00}:{s.Entry%60:00}–{s.Exit/60:00}:{s.Exit%60:00}"));
            return new TemporalPlanRow(day, a.TaskCode + " · " + a.Name, a.PersonName, "ELIGIBLE", true, hours) {
                StartsAt = a.StartsAt, NominalStartsAt = a.NominalStartsAt, DueAt = a.DueAt, OperationalCriticality = a.OperationalCriticality, LoadMinutes = a.LoadMinutes };
        }).OrderBy(r => r.Day).ThenBy(r => r.Person).ThenBy(r => r.StartsAt).ToArray();
        preview.AgendaFingerprint = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { preview.Fingerprint,
            Rows = generated.OrderBy(a => a.ScheduleKey, StringComparer.Ordinal).Select(a => new { a.TaskId, a.PersonId, a.NominalStartsAt, a.StartsAt, a.DueAt, a.ScheduleKey, a.LoadMinutes, a.OperationalCriticality, a.SchedulingPolicyId }), preview.MonthlyPending })));
        var loads = generated.GroupBy(a => WeekContract.LocalToday(a.StartsAt)).ToDictionary(g => g.Key, g => g.Sum(a => (long)a.LoadMinutes));
        var workers = preview.Workers.Select(w => w.PersonId).ToArray();
        return CapacityLedger.Days(range.StartsOn, range.EndsOn).Select(day => new PlanDailyLoad(day,
            loads.GetValueOrDefault(day), workers.Sum(id => adjusted.DayLoad(id, day)), workers.Sum(id => adjusted.DayCapacity(id, day)))).ToArray();
    }

    public static async Task<(PlanningCapacityPreview Preview, CapacityLedger Ledger)> ReadAsync(
        SgolDbContext db, DirectionPlanInput input, DateTimeOffset now, CancellationToken token)
    {
        input = DirectionPlanContract.Normalize(input);
        var range = DirectionPlanContract.Calculate(input, input.IsWeeklyOperationsPlan ? WeekContract.LocalToday(now) : null);
        var ledger = await OperationalCapacityService.LedgerAsync(db, now, token);
        var from = DateOnly.FromDayNumber(Math.Max(range.StartsOn.DayNumber, ledger.Today.DayNumber));
        var through = DateOnly.FromDayNumber(Math.Max(range.EndsOn.DayNumber, from.DayNumber));
        var tasks = await db.OperationalTasks.AsNoTracking().Where(t => input.TaskCodes.Contains(t.TaskCode) && t.Active && !t.Deleted)
            .OrderBy(t => t.TaskCode).ToArrayAsync(token);
        var selectedDefinitions = await db.OperationalTasks.AsNoTracking().Where(t => input.TaskCodes.Contains(t.TaskCode)).OrderBy(t => t.TaskCode).ToArrayAsync(token);
        if (selectedDefinitions.Length != input.TaskCodes.Count || tasks.Any(t => t.PeriodType == "OCASIONAL" || t.ProcessId != OperationalSequence.GeneralId)) throw new DirectionPlanException("DIRECTION_PLAN_TASK_INVALID", 422);
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
        var policy = await OperationalSchedulingPolicyService.CurrentAsync(db, token);
        var snapshot = JsonSerializer.SerializeToUtf8Bytes(new { policy, input.SchedulingSearchMultiplier, SearchVersion = OperationalAgendaSearch.Version, SearchBudget = OperationalAgendaSearch.Budget, CandidateBudget = OperationalAgendaSearch.CandidateBudget, input.SchedulingNotBefore, BalancePolicyVersion = OperationalPlanBalancer.PolicyVersion, input.PeriodType, input.Year, input.PeriodNumber, input.TaskCodes, input.IsWeeklyOperationsPlan,
            from, through, selectedDefinitions, tasks, peers, DefaultDailyMinutes = OperationalCapacity.DefaultDailyMinutes, ledger.Today, ledger.Workers, ledger.Tasks, ledger.Capacities, ledger.Temporal,
            Unavailable = ledger.Unavailable.OrderBy(a => a.Item1).ThenBy(a => a.Item2).Select(a => new { Person = a.Item1, Day = a.Item2 }),
            Legacy = ledger.Legacy.OrderBy(a => a.Key).Select(a => new { Person = a.Key, Minutes = a.Value }) });
        // Actual temporal rows are produced by the shared generator in DailyLoadAsync.
        TemporalPlanRow[] temporalRows = [];
        return (new PlanningCapacityPreview(from, through, Convert.ToHexString(SHA256.HashData(snapshot)), workers) { ExcludedTasks = selectedDefinitions.Where(t => !t.Active || t.Deleted).Select(t => t.TaskCode + " · " + t.Name).ToArray(), SpecialistGaps = specialistGaps, TemporalRows = temporalRows, TemporalPolicyEnabled = ledger.Temporal.Enabled }, ledger);
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
