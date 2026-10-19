using Sgol.Planning.Contracts;

namespace Sgol.Web.Infrastructure.Persistence.Planning;

public sealed record WeeklyBoardRow(string Name, string TaskCode, string PlanName, int LoadMinutes,
    IReadOnlyList<LiveBoardTask> Assignments)
{
    public IEnumerable<LiveBoardTask> ForDay(DateOnly day) => Assignments
        .Where(t => WeekContract.LocalToday(t.DueAt) == day)
        .OrderBy(t => t.DueAt).ThenBy(t => t.PersonName).ThenBy(t => t.Id);
}

public sealed record WeeklyBoardSnapshot(DateTimeOffset Now, DateOnly StartsOn, IReadOnlyList<LiveBoardTask> Tasks,
    IReadOnlyList<LiveBoardTask>? ActivationTasks = null)
{
    public IEnumerable<LiveBoardTask> Work => (ActivationTasks ?? Tasks).Where(t =>
        WeekContract.LocalToday(t.StartsAt) >= StartsOn && WeekContract.LocalToday(t.StartsAt) <= EndsOn);
    public IEnumerable<IGrouping<Guid, LiveBoardTask>> Workers => Work.GroupBy(t => t.PersonId)
        .OrderBy(g => g.First().PersonName).ThenBy(g => g.Key);
    public long ActivationLoad(DateOnly day, Guid? person = null) => Work.Where(t =>
        WeekContract.LocalToday(t.StartsAt) == day && (person == null || t.PersonId == person)).Sum(t => (long)t.LoadMinutes);
    public long ClosingLoad(DateOnly day) => Tasks.Where(t => WeekContract.LocalToday(t.DueAt) == day).Sum(t => (long)t.LoadMinutes);
    public static DateOnly Monday(DateOnly day) => day.AddDays(-(((int)day.DayOfWeek + 6) % 7));
    public DateOnly EndsOn => StartsOn.AddDays(6);
    public IEnumerable<DateOnly> Days => Enumerable.Range(0, 7).Select(StartsOn.AddDays);
    // Keep plan and assignment snapshots distinct, including different historical loads.
    public IEnumerable<WeeklyBoardRow> Rows => Tasks.GroupBy(t => new { t.PlanId, t.TaskId, t.Name, t.TaskCode, t.LoadMinutes, t.PlanName })
        .OrderBy(g => g.Key.PlanName).ThenBy(g => g.Key.Name).ThenBy(g => g.Key.TaskCode).ThenBy(g => g.Key.TaskId).ThenBy(g => g.Key.LoadMinutes)
        .Select(g => new WeeklyBoardRow(g.Key.Name, g.Key.TaskCode, g.Key.PlanName, g.Key.LoadMinutes, g.ToArray()));
}
