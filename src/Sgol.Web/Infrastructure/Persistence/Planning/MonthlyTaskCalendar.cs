using System.Globalization;
using Sgol.Planning.Contracts;

namespace Sgol.Web.Infrastructure.Persistence.Planning;

internal sealed record MonthlyOccurrence(int Year, int Month, int PreferredDay, DateOnly? ValidDate)
{
    public string Key(Guid task) => $"{task:N}:M:{Year:0000}{Month:00}:{PreferredDay:00}";
}

internal static class MonthlyTaskCalendar
{
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

    public static DateOnly[] Candidates(DateOnly preferred, DateOnly from, DateOnly through, DateOnly today) =>
        Enumerable.Range(-2, 5).Select(delta => preferred.DayNumber + delta)
            .Where(n => n >= DateOnly.MinValue.DayNumber && n <= DateOnly.MaxValue.DayNumber)
            .Select(DateOnly.FromDayNumber)
            .Where(d => d.Year == preferred.Year && d.Month == preferred.Month && d >= from && d <= through && d >= today && d.DayOfWeek != DayOfWeek.Sunday)
            .OrderBy(d => Math.Abs(d.DayNumber - preferred.DayNumber)).ThenBy(d => d).ToArray();
}
