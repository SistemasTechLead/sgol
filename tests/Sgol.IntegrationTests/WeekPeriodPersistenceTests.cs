using Microsoft.EntityFrameworkCore;
using Sgol.BuildingBlocks.Identifiers;
using Sgol.BuildingBlocks.Time;
using Sgol.Identity.Contracts;
using Sgol.Organization.Contracts;
using Sgol.Planning.Contracts;
using Sgol.Web.Infrastructure.Persistence;
using Sgol.Web.Infrastructure.Persistence.Auditing;
using Sgol.Web.Infrastructure.Persistence.Bootstrap;
using Sgol.Web.Infrastructure.Persistence.Planning;
using Testcontainers.PostgreSql;
using Xunit;

namespace Sgol.IntegrationTests;

public sealed class WeekPeriodPersistenceTests : IAsyncLifetime
{
    private static readonly DateTimeOffset CurrentWeekNow =
        new(2026, 9, 9, 18, 0, 0, TimeSpan.Zero);
    private readonly PostgreSqlContainer _postgres = PostgreSqlPersistenceTests.CreateContainerForTests();

    public Task InitializeAsync() => _postgres.StartAsync();

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    [Fact]
    public async Task SameIsoWeekMaterializesOneStableLorettaPeriodWithoutUnrelatedEffects()
    {
        var actorUserId = await ResetAndSeedActorAsync("WEEK-STABLE", CanonicalRole.Direction);
        await using var context = CreateContext();
        var service = CreateService(context, CurrentWeekNow);
        var calendarCount = await context.CalendarDayVersions.CountAsync();
        var releaseCount = await context.ConfigurationReleases.CountAsync();
        var personCount = await context.People.CountAsync();
        var roleCount = await context.RoleAssignmentVersions.CountAsync();
        var accountCount = await context.AppUsers.CountAsync();

        var first = await service.GetAsync(actorUserId, Guid.CreateVersion7(), 2026, 37);
        var second = await service.GetAsync(actorUserId, Guid.CreateVersion7(), 2026, 37);

        Assert.Equal(first, second);
        Assert.Equal(BranchScope.LorettaCode, first.BranchCode);
        Assert.Equal(new DateOnly(2026, 9, 7), first.StartsOn);
        Assert.Equal(new DateOnly(2026, 9, 13), first.EndsOn);
        Assert.Equal(WeekContract.Current, first.DerivedStatus);
        Assert.Equal(1, await context.WeekPeriods.CountAsync());
        Assert.Equal(calendarCount, await context.CalendarDayVersions.CountAsync());
        Assert.Equal(releaseCount, await context.ConfigurationReleases.CountAsync());
        Assert.Equal(personCount, await context.People.CountAsync());
        Assert.Equal(roleCount, await context.RoleAssignmentVersions.CountAsync());
        Assert.Equal(accountCount, await context.AppUsers.CountAsync());
        Assert.Empty(context.ChangeTracker.Entries());
    }

    [Fact]
    public async Task ElapsedWeekRemainsQueryableAndRefreshesOnlyItsDerivedStatus()
    {
        var actorUserId = await ResetAndSeedActorAsync("WEEK-ELAPSED", CanonicalRole.Administration);
        WeekPeriodDetails current;
        await using (var currentContext = CreateContext())
        {
            current = await CreateService(currentContext, CurrentWeekNow)
                .GetAsync(actorUserId, Guid.CreateVersion7(), 2026, 37);
        }

        await using var elapsedContext = CreateContext();
        var elapsed = await CreateService(
                elapsedContext,
                new DateTimeOffset(2026, 9, 14, 6, 0, 0, TimeSpan.Zero))
            .GetAsync(actorUserId, Guid.CreateVersion7(), 2026, 37);

        Assert.Equal(current.Id, elapsed.Id);
        Assert.Equal(WeekContract.Current, current.DerivedStatus);
        Assert.Equal(WeekContract.Elapsed, elapsed.DerivedStatus);
        Assert.Equal(1, await elapsedContext.WeekPeriods.CountAsync());
        Assert.Equal(2, await elapsedContext.AuditEvents.CountAsync(
            audit => audit.ResourceType == "WEEK_PERIOD" && audit.Outcome == "SUCCESS"));
        Assert.Empty(elapsedContext.ChangeTracker.Entries());
    }

    [Fact]
    public async Task EveryCanonicalRoleHasPlanViewAndObtainsTheSamePeriod()
    {
        var actors = new[]
        {
            await ResetAndSeedActorAsync("WEEK-DIR", CanonicalRole.Direction),
            await SeedActorAsync("WEEK-ADM", CanonicalRole.Administration),
            await SeedActorAsync("WEEK-SUB", CanonicalRole.Subcoordination),
            await SeedActorAsync("WEEK-PISO", CanonicalRole.SalesFloor),
        };

        await using var context = CreateContext();
        var service = CreateService(context, CurrentWeekNow);
        var periods = new List<WeekPeriodDetails>();
        foreach (var actor in actors)
        {
            periods.Add(await service.GetAsync(actor, Guid.CreateVersion7(), 2026, 37));
        }

        Assert.Single(periods.Select(period => period.Id).Distinct());
        Assert.Equal(1, await context.WeekPeriods.CountAsync());
        Assert.Empty(context.ChangeTracker.Entries());
    }

    [Fact]
    public async Task InvalidWeekAndActorWithoutPlanViewDoNotMaterializeAPeriod()
    {
        var actorWithoutRole = await ResetAndSeedActorAsync("WEEK-DENIED", roleCode: null);
        await using var context = CreateContext();
        var service = CreateService(context, CurrentWeekNow);

        await Assert.ThrowsAsync<WeekValidationException>(() =>
            service.GetAsync(actorWithoutRole, Guid.CreateVersion7(), 2021, 53));
        await Assert.ThrowsAsync<WeekAccessDeniedException>(() =>
            service.GetAsync(actorWithoutRole, Guid.CreateVersion7(), 2026, 37));

        Assert.False(await context.WeekPeriods.AnyAsync());
        Assert.Contains(
            await context.AuditEvents.AsNoTracking().ToListAsync(),
            audit => audit.Action == "WEEK_PERIOD_ACCESS_DENIED" && audit.Outcome == "DENIED");
        Assert.Empty(context.ChangeTracker.Entries());
    }

    [Fact]
    public async Task ConcurrentQueriesRecoverTheSinglePostgreSqlPeriod()
    {
        var actorUserId = await ResetAndSeedActorAsync("WEEK-RACE", CanonicalRole.Subcoordination);
        await using var firstContext = CreateContext();
        await using var secondContext = CreateContext();

        var results = await Task.WhenAll(
            CreateService(firstContext, CurrentWeekNow)
                .GetAsync(actorUserId, Guid.CreateVersion7(), 2026, 37),
            CreateService(secondContext, CurrentWeekNow)
                .GetAsync(actorUserId, Guid.CreateVersion7(), 2026, 37));

        Assert.Equal(results[0].Id, results[1].Id);
        await using var verification = CreateContext();
        Assert.Equal(1, await verification.WeekPeriods.CountAsync());
        Assert.Equal(1, await verification.AuditEvents.CountAsync(
            audit => audit.Action == "WEEK_PERIOD_MATERIALIZED"));
        Assert.Empty(firstContext.ChangeTracker.Entries());
        Assert.Empty(secondContext.ChangeTracker.Entries());
    }

    private async Task<Guid> ResetAndSeedActorAsync(string stableCode, string? roleCode)
    {
        await using var context = CreateContext();
        await context.Database.EnsureDeletedAsync();
        await context.Database.MigrateAsync();
        return await SeedActorAsync(stableCode, roleCode);
    }

    private async Task<Guid> SeedActorAsync(string stableCode, string? roleCode)
    {
        await using var context = CreateContext();
        var personId = Guid.CreateVersion7();
        var userId = Guid.CreateVersion7();
        context.People.Add(new Person
        {
            Id = personId,
            StableCode = stableCode,
            DisplayName = "Persona sintética",
            CreatedAt = CurrentWeekNow,
        });
        context.EmploymentVersions.Add(new EmploymentVersion(
            Guid.CreateVersion7(),
            personId,
            BranchScope.LorettaId,
            EmploymentStatus.Active,
            CurrentWeekNow));
        context.AppUsers.Add(new AppUser
        {
            Id = userId,
            PersonId = personId,
            Status = AccountStatus.Active,
            MustChangePassword = false,
            MfaEnrolledAt = CurrentWeekNow,
            SecurityStamp = $"synthetic-stamp-{userId:N}",
        });
        context.IdentityCredentials.Add(new IdentityCredential
        {
            UserId = userId,
            UserName = $"user.{userId:N}",
            NormalizedUserName = $"USER.{userId:N}",
            PasswordHash = "synthetic-hash-not-a-secret",
        });
        if (roleCode is not null)
        {
            context.RoleAssignmentVersions.Add(new RoleAssignmentVersion
            {
                Id = Guid.CreateVersion7(),
                UserId = userId,
                BranchId = BranchScope.LorettaId,
                RoleCode = roleCode,
                Status = RoleAssignmentStatus.Active,
                ValidFrom = CurrentWeekNow,
            });
        }

        await context.SaveChangesAsync();
        return userId;
    }

    private SgolDbContext CreateContext() => new(
        new DbContextOptionsBuilder<SgolDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options);

    private static EfWeekPeriodService CreateService(
        SgolDbContext context,
        DateTimeOffset now) => new(
            context,
            new AuditTransaction(context),
            new FixedClock(now),
            new Uuid7Generator(new FixedClock(now)));

    private sealed class FixedClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; } = utcNow;
    }
}
