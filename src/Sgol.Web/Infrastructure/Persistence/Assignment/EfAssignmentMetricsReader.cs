using Microsoft.EntityFrameworkCore;
using Sgol.Assignment.Contracts;
using Sgol.Generation.Contracts;
using Sgol.Organization.Contracts;

namespace Sgol.Web.Infrastructure.Persistence.Assignment;

internal static class EfAssignmentMetricsReader
{
    public static async Task<IReadOnlyDictionary<Guid, AssignmentMetrics>> ReadAsync(
        SgolDbContext dbContext,
        IReadOnlyCollection<Guid> personIds,
        DateTimeOffset calculatedAt,
        CancellationToken cancellationToken)
    {
        if (personIds.Count == 0)
        {
            return new Dictionary<Guid, AssignmentMetrics>();
        }

        var ids = personIds.ToArray();
        var loads = await (
            from assignment in dbContext.AssignmentVersions.AsNoTracking()
            join obligation in dbContext.WorkObligations.AsNoTracking()
                on assignment.ObligationId equals obligation.Id
            where ids.Contains(assignment.PersonId) &&
                assignment.Status == AssignmentVersionStatuses.Current &&
                assignment.AssignedAt <= calculatedAt &&
                obligation.BranchId == BranchScope.LorettaId &&
                obligation.ExecutionStatus == WorkObligationStatuses.Pending
            group assignment by assignment.PersonId into assignments
            select new { PersonId = assignments.Key, Count = assignments.Count() })
            .ToDictionaryAsync(item => item.PersonId, item => item.Count, cancellationToken);

        var lastAutomaticAssignments = await dbContext.AssignmentVersions.AsNoTracking()
            .Where(assignment =>
                ids.Contains(assignment.PersonId) &&
                assignment.AssignmentType == AssignmentTypes.Automatic &&
                assignment.AssignedAt <= calculatedAt)
            .GroupBy(assignment => assignment.PersonId)
            .Select(assignments => new
            {
                PersonId = assignments.Key,
                LastAssignedAt = assignments.Max(assignment => assignment.AssignedAt),
            })
            .ToDictionaryAsync(item => item.PersonId, item => item.LastAssignedAt, cancellationToken);

        return ids.ToDictionary(
            personId => personId,
            personId => new AssignmentMetrics(
                loads.GetValueOrDefault(personId),
                lastAutomaticAssignments.TryGetValue(personId, out var lastAssignedAt)
                    ? lastAssignedAt
                    : null));
    }
}

internal sealed record AssignmentMetrics(int ActiveLoad, DateTimeOffset? LastAutoAssignmentAt);
