using Microsoft.EntityFrameworkCore;

namespace Sgol.Web.Infrastructure.Persistence.Planning;

public sealed record OperationalLifecycleEntry(string Kind, DateTimeOffset? At, Guid? ActorId,
    string? Comment, string? Conclusion, string? Decision, string? ActorName = null);

public sealed partial class OperationalTaskService
{
    public async Task<IReadOnlyList<OperationalLifecycleEntry>> LifecycleHistoryAsync(Guid actor, Guid id, CancellationToken token)
    {
        var person = await PersonAsync(actor, token);
        if (!await db.OperationalAssignments.AnyAsync(a => a.Id == id && a.PersonId == person, token))
            await new OperationalSupervisionService(db, clock, direction).AuthorizeAssignmentAsync(actor, id, token);
        var state = await db.OperationalAssignments.AsNoTracking().Where(a => a.Id == id)
            .Select(a => new { a.CompletedAt, a.CompletedBy, a.CompletionKind, a.Comment, a.Conclusion,
                a.ReviewStatus, a.ReviewedAt, a.ReviewedBy, a.ReviewComment, a.ReviewDecision,
                a.ReassignedFromId, a.ReassignmentComment, a.StartsAt }).SingleAsync(token);
        var events = new List<OperationalLifecycleEntry>();
        if (state.ReassignedFromId is not null)
            events.Add(new("REASIGNACION", state.StartsAt, null, state.ReassignmentComment, null, null));
        if (state.CompletedAt is not null)
            events.Add(new(state.CompletionKind ?? "HISTORICO", state.CompletedAt, state.CompletedBy, state.Comment, state.Conclusion, null));
        if (state.ReviewedAt is not null || state.ReviewStatus is "CONCLUIDA" or "NO_SATISFECHA" or "REASIGNADA")
            events.Add(new("REVISION", state.ReviewedAt, state.ReviewedBy, state.ReviewComment, null, state.ReviewDecision ?? "HISTORICO"));
        var actors = events.Where(e => e.ActorId != null).Select(e => e.ActorId!.Value).Distinct().ToArray();
        var names = await (from u in db.AppUsers.AsNoTracking() join p in db.People.AsNoTracking() on u.PersonId equals p.Id
            where actors.Contains(u.Id) select new { u.Id, p.DisplayName }).ToDictionaryAsync(u => u.Id, u => u.DisplayName, token);
        return events.Select(e => e with { ActorName = e.ActorId is { } uid ? names.GetValueOrDefault(uid) : null })
            .OrderBy(e => e.At ?? DateTimeOffset.MaxValue).ToArray();
    }
}
