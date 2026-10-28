using Microsoft.EntityFrameworkCore;
using Sgol.Identity.Contracts;
using Sgol.Organization.Contracts;
namespace Sgol.Web.Infrastructure.Persistence.Planning;
public sealed partial class OperationalCarryoverService
{
    private async Task<IQueryable<OperationalCarryoverRequest>> AuthorizedInboxAsync(Guid actor, CancellationToken token)
    {
        var role = await supervision.AuthorizeAsync(actor, token);
        var person = await Person(actor, token);
        var ids = await supervision.VisibleIdsAsync(actor, token);
        var query = db.Set<OperationalCarryoverRequest>().FromSqlInterpolated($"""
            SELECT * FROM operational_carryover_request r
            WHERE r."BranchId" = {BranchScope.LorettaId} AND r."PersonId" <> {person}
            AND NOT EXISTS (SELECT 1 FROM jsonb_array_elements(r."SnapshotJson") i
                WHERE i->>'AssignmentId' IS NULL OR NOT ((i->>'AssignmentId')::uuid = ANY({ids})))
            """).AsNoTracking();
        if (role != CanonicalRole.Direction)
        {
            var people = await query.Where(r => r.Kind == "POSPONER").Select(r => r.PersonId).Distinct().ToArrayAsync(token);
            var allowed = new List<Guid>();
            foreach (var id in people) if ((await ImmediateSupervisors(id, token)).Contains(actor)) allowed.Add(id);
            query = query.Where(r => r.Kind != "POSPONER" || allowed.Contains(r.PersonId));
        }
        return query;
    }
    public async Task<(OperationalCarryoverRequest[] Items, int Total, int Page)> InboxPageAsync(Guid actor, bool history, int page, CancellationToken token)
    {
        var query = await AuthorizedInboxAsync(actor, token);
        query = history ? query.Where(r => r.State != "PENDIENTE") : query.Where(r => r.State == "PENDIENTE");
        var total = await query.CountAsync(token);
        page = Math.Clamp(page, 1, Math.Max(1, (total + 9) / 10));
        return (await query.OrderBy(r => r.SubmittedAt).ThenBy(r => r.Id).Skip((page - 1) * 10).Take(10).ToArrayAsync(token), total, page);
    }
}
