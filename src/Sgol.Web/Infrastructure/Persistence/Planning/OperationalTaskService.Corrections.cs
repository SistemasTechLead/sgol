using Microsoft.EntityFrameworkCore;

namespace Sgol.Web.Infrastructure.Persistence.Planning;

public sealed record CorrectionContext(DateTimeOffset? OriginalDelivery, string? Comment, string? Conflict);

public sealed partial class OperationalTaskService
{
    public async Task<Dictionary<Guid, CorrectionContext>> MyCorrectionsAsync(Guid actor, CancellationToken token)
    {
        var person = await PersonAsync(actor, token);
        var rows = await db.OperationalAssignments.AsNoTracking().Where(a => a.PersonId == person && a.ReassignedFromId != null).ToArrayAsync(token);
        var ids = rows.Select(a => a.ReassignedFromId!.Value).ToArray();
        var originals = await db.OperationalAssignments.IgnoreQueryFilters().AsNoTracking().Where(a => ids.Contains(a.Id))
            .Select(a => new { a.Id, a.CompletedAt }).ToDictionaryAsync(a => a.Id, a => a.CompletedAt, token);
        var ledger = await OperationalCapacityService.LedgerAsync(db, clock.UtcNow, token);
        return rows.ToDictionary(a => a.Id, a => new CorrectionContext(originals.GetValueOrDefault(a.ReassignedFromId!.Value),
            a.ReassignmentComment, CorrectionScheduling.Conflict(ledger, a)));
    }
}
