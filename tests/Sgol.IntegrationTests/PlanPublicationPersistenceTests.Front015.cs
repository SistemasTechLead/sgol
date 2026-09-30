using Microsoft.EntityFrameworkCore;
using Sgol.Execution.Contracts;
using Sgol.Identity.Contracts;
using Sgol.Planning.Contracts;
using Sgol.Web.Infrastructure.Persistence.Execution;
using Xunit;

namespace Sgol.IntegrationTests;

// Focused PostgreSQL regressions prepared for the integration milestone.
public sealed partial class PlanPublicationPersistenceTests
{
    [Theory]
    [InlineData(CanonicalRole.Direction, 3)]
    [InlineData(CanonicalRole.Administration, 3)]
    [InlineData(CanonicalRole.Subcoordination, 2)]
    [InlineData(CanonicalRole.SalesFloor, 1)]
    public async Task Front015ReadsOneDraftAcrossLevelsWithoutAuditOrIdempotencyWrites(string role, int visibleCount)
    {
        var seed = await ResetAndSeedAsync(role);
        await using var context = CreateContext();
        var beforeAudit = await context.AuditEvents.CountAsync();
        var beforeIdempotency = await context.IdempotencyRecords.CountAsync();
        var reader = new EfObligationQueryReader(context, new FixedClock(Now));
        var plan = await reader.ReadPlanAsync(seed.ActorUserId, 2026, 36);
        var versions = await reader.ReadVersionsAsync(new(seed.ActorUserId, seed.PlanId));
        var obligations = await reader.ListAsync(new ObligationListRequest(seed.ActorUserId, seed.PeriodId,
            null, null, null, null, null, 50));
        Assert.Equal(seed.PlanId, plan.Plan.PlanId); Assert.Equal("BORRADOR", plan.Plan.Status);
        Assert.Equal(visibleCount, obligations.Items.Count); Assert.Empty(versions.Versions);
        Assert.Null(versions.NextCursor); Assert.Equal(1, versions.RowVersion);
        Assert.Equal(beforeAudit, await context.AuditEvents.CountAsync());
        Assert.Equal(beforeIdempotency, await context.IdempotencyRecords.CountAsync());
        Assert.Single(await context.WorkPlans.ToListAsync()); Assert.Empty(await context.PlanVersions.ToListAsync());
    }

    [Fact]
    public async Task Front015V2HistoryPaginatesAndKeepsFrozenV1WithCurrentPlanEtag()
    {
        var seed = await ResetAndSeedAsync(CanonicalRole.Subcoordination);
        PlanPublicationResult first;
        await using (var context = CreateContext()) first = await CreateService(context).PublishAsync(Command(seed, Guid.CreateVersion7(), 1));
        await AddObligationAsync(seed, CanonicalRole.SalesFloor, assigned: true);
        PlanPublicationResult second;
        await using (var context = CreateContext()) second = await CreateService(context).PublishAsync(Command(seed, Guid.CreateVersion7(), 2));
        await using var verification = CreateContext(); var reader = new EfObligationQueryReader(verification, new FixedClock(Now));
        var page = await reader.ReadVersionsAsync(new(seed.ActorUserId, seed.PlanId, Limit: 1));
        Assert.Equal(second.PublicationId, Assert.Single(page.Versions).PublicationId); Assert.NotNull(page.NextCursor);
        var older = await reader.ReadVersionsAsync(new(seed.ActorUserId, seed.PlanId, Limit: 1, Cursor: page.NextCursor));
        Assert.Equal(first.PublicationId, Assert.Single(older.Versions).PublicationId); Assert.Null(older.NextCursor);
        var snapshot = await reader.ReadVersionsAsync(new(seed.ActorUserId, seed.PlanId, first.PublicationId, 1));
        Assert.Equal(3, snapshot.RowVersion); Assert.Equal(2, snapshot.Snapshot!.Publication.PlanRowVersion);
        Assert.Equal("SUSTITUIDA", snapshot.Snapshot.Publication.VersionStatus); Assert.Single(snapshot.Snapshot.Obligations);
        Assert.NotNull(snapshot.NextCursor);
        var remainder = await reader.ReadVersionsAsync(new(seed.ActorUserId, seed.PlanId, first.PublicationId, 1, snapshot.NextCursor));
        var frozenPairs = snapshot.Snapshot.Obligations.Concat(remainder.Snapshot!.Obligations).OrderBy(p => p.ObligationId).ToArray();
        Assert.Equal(first.Obligations.OrderBy(p => p.ObligationId).ToArray(), frozenPairs);
        Assert.Null(remainder.NextCursor);
    }

    [Fact]
    public async Task Front015FiltersHiddenPairsBeforePaginationAndHidesAnotherPlan()
    {
        var seed = await ResetAndSeedAsync(CanonicalRole.Direction);
        PlanPublicationResult publication;
        await using (var context = CreateContext()) publication = await CreateService(context).PublishAsync(Command(seed, Guid.CreateVersion7(), 1));
        await using var verification = CreateContext(); var reader = new EfObligationQueryReader(verification, new FixedClock(Now));
        var floor = seed.People[CanonicalRole.SalesFloor].UserId;
        var snapshot = await reader.ReadVersionsAsync(new(floor, seed.PlanId, publication.PublicationId, 1));
        var pair = Assert.Single(snapshot.Snapshot!.Obligations);
        Assert.Equal(seed.Obligations.Single(p => p.Value.Role == CanonicalRole.SalesFloor).Key, pair.ObligationId);
        Assert.Null(snapshot.NextCursor);
        var otherPlan = await Assert.ThrowsAsync<PlanPublicationNotFoundException>(() => reader.ReadVersionsAsync(new(floor, Guid.CreateVersion7())));
        Assert.Equal("PLAN_NO_ENCONTRADO", otherPlan.ErrorCode);
        var hidden = await Assert.ThrowsAsync<PlanQueryException>(() => reader.ReadVersionsAsync(new(floor, seed.PlanId, Guid.CreateVersion7())));
        Assert.Equal("PUBLICACION_NO_ENCONTRADA", hidden.ErrorCode);
    }

    [Fact]
    public async Task Front015CursorCannotMoveBetweenActorLimitVariantOrRole()
    {
        var seed = await ResetAndSeedAsync(CanonicalRole.Subcoordination);
        PlanPublicationResult publication;
        await using (var context = CreateContext()) publication = await CreateService(context).PublishAsync(Command(seed, Guid.CreateVersion7(), 1));
        await using var verification = CreateContext(); var reader = new EfObligationQueryReader(verification, new FixedClock(Now));
        var page = await reader.ReadVersionsAsync(new(seed.ActorUserId, seed.PlanId, publication.PublicationId, 1));
        Assert.NotNull(page.NextCursor);
        foreach (var request in new[]
        {
            new PlanVersionsRequest(seed.People[CanonicalRole.Direction].UserId, seed.PlanId, publication.PublicationId, 1, page.NextCursor),
            new PlanVersionsRequest(seed.ActorUserId, seed.PlanId, publication.PublicationId, 2, page.NextCursor),
            new PlanVersionsRequest(seed.ActorUserId, seed.PlanId, null, 1, page.NextCursor),
        })
        {
            var error = await Assert.ThrowsAsync<PlanQueryException>(() => reader.ReadVersionsAsync(request));
            Assert.Equal("CURSOR_INVALIDO", error.ErrorCode);
        }
        await verification.RoleAssignmentVersions.Where(r => r.UserId == seed.ActorUserId && r.Status == RoleAssignmentStatus.Active)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.RoleCode, CanonicalRole.SalesFloor));
        var changed = await Assert.ThrowsAsync<PlanQueryException>(() => reader.ReadVersionsAsync(new(seed.ActorUserId, seed.PlanId, publication.PublicationId, 1, page.NextCursor)));
        Assert.Equal("CURSOR_INVALIDO", changed.ErrorCode);
    }
}
