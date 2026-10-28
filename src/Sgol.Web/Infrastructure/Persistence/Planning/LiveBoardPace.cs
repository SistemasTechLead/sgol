namespace Sgol.Web.Infrastructure.Persistence.Planning;

public sealed record LiveBoardDeadline(DateTimeOffset DueAt, DateTimeOffset? CompletedAt);

public sealed record LiveBoardPace(int Total, int Expected, int Delivered)
{
    public const int GreenThreshold = 90;
    public const int YellowThreshold = 70;

    public decimal ExpectedPercent => Total == 0 ? 0 : 100m * Expected / Total;
    public string Color => Expected == 0 ? "neutral" :
        100L * Delivered >= (long)GreenThreshold * Expected ? "green" :
        100L * Delivered >= (long)YellowThreshold * Expected ? "yellow" : "red";

    public static LiveBoardPace For(IReadOnlyList<LiveBoardTask> tasks, DateTimeOffset now)
        => ForDeadlines(tasks.Select(t => new LiveBoardDeadline(t.DueAt, t.CompletedAt)).ToArray(), now);

    public static LiveBoardPace ForDeadlines(IReadOnlyList<LiveBoardDeadline> tasks, DateTimeOffset now)
    {
        // The fill, detail and color share the same cumulative delivery count.
        return new(tasks.Count, tasks.Count(t => t.DueAt <= now), tasks.Count(t => t.CompletedAt is not null));
    }
}
