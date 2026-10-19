using Microsoft.EntityFrameworkCore;
using Sgol.Execution.Contracts;
using Sgol.Organization.Contracts;
using Sgol.Planning.Contracts;

namespace Sgol.Web.Infrastructure.Persistence.Execution;

// Planning reads reuse the established obligation scope inside the same read-only transaction.
public sealed partial class EfObligationQueryReader
{
    public async Task<WorkPlanRead> ReadPlanAsync(Guid actorUserId, int isoYear, int isoWeek,
        CancellationToken cancellationToken = default)
    {
        WeekRange range;
        try { range = WeekContract.Calculate(isoYear, isoWeek); }
        catch (WeekValidationException) { throw new WorkPlanIsoWeekInvalidException(); }
        await using var transaction = await BeginReadOnlyAsync(cancellationToken);
        var queriedAt = clock.UtcNow;
        await GetPlanActorAsync(actorUserId, queriedAt, cancellationToken);
        var period = await dbContext.WeekPeriods.AsNoTracking().SingleOrDefaultAsync(p =>
            p.BranchId == BranchScope.LorettaId && p.IsoYear == isoYear && p.IsoWeek == isoWeek, cancellationToken)
            ?? throw new WorkPlanPeriodNotFoundException();
        if (period.StartsOn != range.StartsOn || period.EndsOn != range.EndsOn)
            throw new WorkPlanPeriodIncompatibleException();
        var plan = await dbContext.WorkPlans.AsNoTracking().SingleOrDefaultAsync(p =>
            p.BranchId == BranchScope.LorettaId && p.PeriodId == period.Id, cancellationToken)
            ?? throw new PlanPublicationNotFoundException();
        var result = new WorkPlanRead(new(plan.Id, plan.BranchId, BranchScope.LorettaCode,
            period.Id, isoYear, isoWeek, plan.Status, plan.RowVersion), queriedAt);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    public async Task<PlanVersionsRead> ReadVersionsAsync(PlanVersionsRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.PlanId == Guid.Empty || request.PublicationId == Guid.Empty || request.Limit is < 1 or > 100)
            throw new PlanQueryException("CONSULTA_PLAN_INVALIDA", 400);
        await using var transaction = await BeginReadOnlyAsync(cancellationToken);
        var queriedAt = clock.UtcNow;
        var actor = await GetPlanActorAsync(request.ActorUserId, queriedAt, cancellationToken);
        if (request.Cursor is { } cursor && (cursor.ActorUserId != request.ActorUserId ||
            cursor.RoleCode != actor.RoleCode || cursor.PlanId != request.PlanId ||
            cursor.PublicationId != request.PublicationId || cursor.Limit != request.Limit ||
            (request.PublicationId is null ? cursor.AfterVersionNo is null or < 1 || cursor.AfterObligationId is not null
                : cursor.AfterObligationId is null || cursor.AfterObligationId == Guid.Empty || cursor.AfterVersionNo is not null)))
            throw new PlanQueryException("CURSOR_INVALIDO", 400);
        var plan = await dbContext.WorkPlans.AsNoTracking().SingleOrDefaultAsync(p =>
            p.Id == request.PlanId && p.BranchId == BranchScope.LorettaId, cancellationToken)
            ?? throw new PlanPublicationNotFoundException();
        var visibleIds = BuildVisibleRows(actor, queriedAt).Where(o => o.PeriodId == plan.PeriodId)
            .Select(o => o.ObligationId);
        var memberships = dbContext.PlanVersionObligations.AsNoTracking()
            .Where(m => visibleIds.Contains(m.ObligationId));
        var visibleVersions = dbContext.PlanVersions.AsNoTracking().Where(v => v.PlanId == plan.Id &&
            memberships.Any(m => m.PlanVersionId == v.Id));
        IQueryable<PlanVersionDetails> ProjectVersions(IQueryable<PlanVersion> source) => source.Select(v => new PlanVersionDetails(v.Id, v.PlanId, v.VersionNo,
            v.Status, v.ScopeRole, v.PublishedBy, v.PublishedAt,
            v.SupersedesId != null && visibleVersions.Any(previous => previous.Id == v.SupersedesId)
                ? v.SupersedesId : null, v.PlanRowVersion));
        PlanReadCursor? next = null;
        List<PlanVersionDetails> versions = [];
        PlanSnapshotDetails? snapshot = null;
        if (request.PublicationId is { } publicationId)
        {
            var publication = await ProjectVersions(visibleVersions.Where(v => v.Id == publicationId)).SingleOrDefaultAsync(cancellationToken)
                ?? throw new PlanQueryException("PUBLICACION_NO_ENCONTRADA", 404);
            var pairs = memberships.Where(m => m.PlanVersionId == publicationId);
            if (request.Cursor?.AfterObligationId is { } after) pairs = pairs.Where(m => m.ObligationId.CompareTo(after) > 0);
            var items = await pairs.OrderBy(m => m.ObligationId).Take(request.Limit + 1)
                .Select(m => new PlanPublicationItem(m.ObligationId, m.AssignmentVersionId)).ToListAsync(cancellationToken);
            if (items.Count > request.Limit)
            {
                items.RemoveAt(items.Count - 1);
                next = new(request.ActorUserId, actor.RoleCode, plan.Id, publicationId,
                    request.Limit, null, items[^1].ObligationId);
            }
            snapshot = new(publication, items);
        }
        else
        {
            var pageQuery = visibleVersions;
            if (request.Cursor?.AfterVersionNo is { } after) pageQuery = pageQuery.Where(v => v.VersionNo < after);
            versions = await ProjectVersions(pageQuery.OrderByDescending(v => v.VersionNo).ThenBy(v => v.Id)
                .Take(request.Limit + 1)).ToListAsync(cancellationToken);
            if (versions.Count > request.Limit)
            {
                versions.RemoveAt(versions.Count - 1);
                next = new(request.ActorUserId, actor.RoleCode, plan.Id, null, request.Limit, versions[^1].VersionNo, null);
            }
        }
        await transaction.CommitAsync(cancellationToken);
        return new(versions, snapshot, next, plan.RowVersion, queriedAt);
    }

    private async Task<ActorAccess> GetPlanActorAsync(Guid actorUserId, DateTimeOffset queriedAt, CancellationToken token)
    {
        try { return await GetActorAsync(actorUserId, queriedAt, token); }
        catch (ObligationQueryAccessDeniedException) { throw new WorkPlanAccessDeniedException(); }
    }
}
