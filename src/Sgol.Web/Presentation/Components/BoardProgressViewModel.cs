using System.Text.Json;
using Sgol.Web.Infrastructure.Persistence.Planning;

namespace Sgol.Web.Presentation.Components;

public sealed record BoardProgressViewModel(string Key, string Label, DateOnly StartsOn,
    IReadOnlyList<LiveBoardDeadline> Deadlines, DateTimeOffset Now, Guid? PersonId = null)
{
    public LiveBoardPace Pace => LiveBoardPace.ForDeadlines(Deadlines, Now);
    public int Delivered => Deadlines.Count(t => t.CompletedAt is not null);
    public int Percent => Deadlines.Count == 0 ? 0 : (int)(100L * Delivered / Deadlines.Count);
    public string Schedule => JsonSerializer.Serialize(Deadlines.GroupBy(t => t.DueAt.ToUnixTimeMilliseconds())
        .OrderBy(g => g.Key).Select(g => new long[] { g.Key, g.LongCount() }));
}
