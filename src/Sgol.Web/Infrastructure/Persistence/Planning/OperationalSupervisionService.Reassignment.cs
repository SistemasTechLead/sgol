using System.Text.Json;
using Sgol.Planning.Contracts;

namespace Sgol.Web.Infrastructure.Persistence.Planning;

public sealed partial class OperationalSupervisionService
{
    // Evidence corrections are received now, independently of operational reservations.
    private static SchedulingPlacement CorrectionPlacement(CapacityLedger ledger, Guid person,
        OperationalAssignment replacement, DateTimeOffset nominal, DateTimeOffset now)
    {
        var candidate = JsonSerializer.Deserialize<OperationalAssignment>(JsonSerializer.Serialize(replacement))!;
        candidate.StartsAt = nominal;
        candidate.DueAt = nominal.AddMinutes(OperationalAgenda.Duration(candidate.LoadMinutes,
            candidate.ClosureMarginPercent ?? throw new DirectionPlanException("SCHEDULING_POLICY_INVALID", 422)));
        var placement = new SchedulingPlacement(true, candidate.StartsAt, candidate.DueAt, "CORRECTION_OVERLAP_ALLOWED");
        var temporal = ledger.CheckTime(person, candidate, now);
        if (!temporal.Eligible) return placement with { Eligible = false, Reason = temporal.Reason };
        // CheckTime validates reception, absence and employment, without checking
        // other work or capacity. Never let a correction authorize a longer shift.
        if (ledger.Temporal.Enabled)
        {
            var day = WeekContract.LocalToday(nominal);
            var baseExit = (ledger.Temporal.Day(person, day, false).Intervals ?? [])
                .Where(i => TemporalEligibilityContext.At(day, i.Entry) <= nominal && nominal < TemporalEligibilityContext.At(day, i.Exit))
                .Select(i => (DateTimeOffset?)TemporalEligibilityContext.At(day, i.Exit)).FirstOrDefault();
            if (baseExit == null) return placement with { Eligible = false, Reason = "SCHEDULE_NOT_PRESENT_AT_RECEPTION" };
            var exit = temporal.Windows?.Where(w => w.Entry <= nominal && nominal < w.Exit)
                .Select(w => (DateTimeOffset?)w.Exit).FirstOrDefault();
            if (exit == null) return placement with { Eligible = false, Reason = "SCHEDULE_NOT_PRESENT_AT_RECEPTION" };
            if (baseExit < exit) exit = baseExit;
            if (exit < placement.DueAt) placement = placement with { DueAt = exit.Value };
        }
        return placement;
    }
}
