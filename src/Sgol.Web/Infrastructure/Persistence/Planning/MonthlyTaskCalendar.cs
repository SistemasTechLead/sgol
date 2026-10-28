using System.Globalization;
using Sgol.Planning.Contracts;

namespace Sgol.Web.Infrastructure.Persistence.Planning;

internal sealed record MonthlyOccurrence(int Year, int Month, int PreferredDay, DateOnly? ValidDate, int? DistributedSlot = null,
    DateOnly? RollingStart = null, DateOnly? RollingEnd = null)
{
    public string Key(Guid task) => RollingStart is { } start ? $"{task:N}:R:{start:yyyyMMdd}:{DistributedSlot}" : DistributedSlot is { } slot
        ? $"{task:N}:M:{Year:0000}{Month:00}:D:{slot}"
        : $"{task:N}:M:{Year:0000}{Month:00}:{PreferredDay:00}";
}

internal static class MonthlyTaskCalendar
{
    public static IEnumerable<MonthlyOccurrence> RollingOccurrences(OperationalTask task, DateOnly start, DateOnly end)
    {
        var preferred = Parse(task.PreferredMonthDays);
        for (var slot = 0; slot < task.Repetitions; slot++)
        {
            DateOnly? nominal = start.AddDays(slot * (end.DayNumber - start.DayNumber + 1) / task.Repetitions);
            if (preferred.Length > 0)
                nominal = Enumerable.Range(start.DayNumber, end.DayNumber - start.DayNumber + 1).Select(DateOnly.FromDayNumber)
                    .Where(d => d.Day == preferred[slot]).Select(d => (DateOnly?)d).FirstOrDefault();
            yield return new(start.Year, start.Month, nominal?.Day ?? preferred[slot], nominal, slot, start, end);
        }
    }
    public static bool UsesCalendar(OperationalTask task) => task.PreferredMonthDays.Length > 0 ||
        task.PeriodType == "MENSUAL" && task.ScheduleMode == "DISTRIBUIDA";
    public static int[] Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return [];
        var parts = value.Split(',');
        if (parts.Any(p => !int.TryParse(p.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var n) || n is < 1 or > 31))
            throw new DirectionPlanException("TASK_MONTH_DAYS_INVALID", 422);
        return parts.Select(p => int.Parse(p.Trim(), CultureInfo.InvariantCulture)).Distinct().Order().ToArray();
    }

    // A weekly plan owns the occurrence whose preferred date is within that plan.
    // Neighbour dates may never cross its boundaries; another plan cannot claim it.
    public static IEnumerable<MonthlyOccurrence> Occurrences(OperationalTask task, DateOnly from, DateOnly through)
    {
        var month = new DateOnly(from.Year, from.Month, 1);
        while (month <= through)
        {
            var last = DateTime.DaysInMonth(month.Year, month.Month);
            if (task.PreferredMonthDays.Length == 0 && UsesCalendar(task))
            {
                // An undated monthly repetition remains available later in its own month.
                // A rest day at the nominal date never becomes a fixed assignment date.
                var slot = 0;
                foreach (var nominal in OperationalSchedule.Dates(month, month.AddDays(last - 1), "MENSUAL", task.Repetitions))
                {
                    if (nominal <= through) yield return new(month.Year, month.Month, nominal.Day, nominal, slot);
                    slot++;
                }
            }
            foreach (var n in Parse(task.PreferredMonthDays))
            {
                DateOnly? date = n <= last ? month.AddDays(n - 1) : null;
                if (date is { } day ? day >= from && day <= through : month.AddDays(last - 1) >= from && month.AddDays(last - 1) <= through)
                    yield return new(month.Year, month.Month, n, date);
            }
            if (month.Year == 9999 && month.Month == 12) break;
            month = month.AddMonths(1);
        }
    }

    public static DateOnly[] Candidates(MonthlyOccurrence occurrence, DateOnly from, DateOnly through, DateOnly today)
    {
        if (occurrence.DistributedSlot == null) return Candidates(occurrence.ValidDate!.Value, from, through, today);
        var nominal = occurrence.ValidDate!.Value;
        var first = new[] { nominal.DayNumber, from.DayNumber, today.DayNumber }.Max();
        var last = Math.Min(through.DayNumber, occurrence.RollingEnd?.DayNumber ?? new DateOnly(occurrence.Year, occurrence.Month,
            DateTime.DaysInMonth(occurrence.Year, occurrence.Month)).DayNumber);
        return first > last ? [] : Enumerable.Range(first, last - first + 1).Select(DateOnly.FromDayNumber)
            .Where(d => d.DayOfWeek != DayOfWeek.Sunday).ToArray();
    }

    public static DateOnly[] Candidates(DateOnly preferred, DateOnly from, DateOnly through, DateOnly today) =>
        Enumerable.Range(-2, 5).Select(delta => preferred.DayNumber + delta)
            .Where(n => n >= DateOnly.MinValue.DayNumber && n <= DateOnly.MaxValue.DayNumber)
            .Select(DateOnly.FromDayNumber)
            .Where(d => d.Year == preferred.Year && d.Month == preferred.Month && d >= from && d <= through && d >= today && d.DayOfWeek != DayOfWeek.Sunday)
            .OrderBy(d => Math.Abs(d.DayNumber - preferred.DayNumber)).ThenBy(d => d).ToArray();
}
