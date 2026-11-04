using Microsoft.EntityFrameworkCore;
using Sgol.Identity.Contracts;

namespace Sgol.Web.Infrastructure.Persistence.Planning;

public sealed partial class OperationalSupervisionService
{
    // Intersect the requested queue with the actor's existing authorized query.
    // These are overlapping review queues, not an exclusive assignment of reviewers.
    public static IQueryable<SupervisedTask> ReviewQueue(IQueryable<SupervisedTask> query, string reviewer)
    {
        var roles = RolesFor(reviewer);
        return query.Where(t => roles.Contains(t.Role) && t.Assignment.CompletedAt != null &&
            (t.Assignment.ReviewStatus == null || t.Assignment.ReviewStatus == "PENDIENTE_REVISION") &&
            (reviewer != CanonicalRole.Direction || t.Role == CanonicalRole.Subcoordination || t.Assignment.SpecializationId != null));
    }

    public async Task<int> ReviewQueueCountAsync(Guid actor, string reviewer, CancellationToken token)
    {
        var role = await AuthorizeAsync(actor, token);
        return await ReviewQueue(Query(role, actor), reviewer).CountAsync(token);
    }
}
