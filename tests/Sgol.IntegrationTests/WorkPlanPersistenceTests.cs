using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Sgol.BuildingBlocks.Identifiers;
using Sgol.BuildingBlocks.Time;
using Sgol.BuildingBlocks.Versioning;
using Sgol.Configuration.Contracts;
using Sgol.Generation.Contracts;
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

public sealed class WorkPlanPersistenceTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 9, 4, 19, 30, 0, TimeSpan.Zero);
    private readonly PostgreSqlContainer _postgres = PostgreSqlPersistenceTests.CreateContainerForTests();

    public Task InitializeAsync() => _postgres.StartAsync();

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    [Fact]
    public async Task EnsureCreatesThenRecoversOneEmptyDraftWithStableEtagAndNoUnrelatedEffects()
    {
        var seed = await ResetAndSeedAsync(CanonicalRole.Direction, includePeriod: true);
        await using var context = CreateContext();
        var service = CreateService(context);
        var key = Guid.CreateVersion7();

        var created = await service.EnsureAsync(Command(seed.ActorUserId, key));
        var replayed = await service.EnsureAsync(Command(seed.ActorUserId, key));
        var recoveredByFunctionalKey = await service.EnsureAsync(Command(seed.ActorUserId, Guid.CreateVersion7()));

        Assert.Equal(WorkPlanResults.Created, created.Result);
        Assert.Equal(WorkPlanResults.Recovered, replayed.Result);
        Assert.Equal(WorkPlanResults.Recovered, recoveredByFunctionalKey.Result);
        Assert.Equal(created.PlanId, replayed.PlanId);
        Assert.Equal(created.PlanId, recoveredByFunctionalKey.PlanId);
        Assert.Equal(WorkPlanStatuses.Draft, created.Status);
        Assert.Equal(1, created.RowVersion);
        Assert.Single(await context.WorkPlans.AsNoTracking().ToListAsync());
        Assert.Empty(await context.WorkObligations.AsNoTracking().ToListAsync());
        Assert.Empty(await context.AssignmentVersions.AsNoTracking().ToListAsync());
        Assert.Equal(2, await context.IdempotencyRecords.CountAsync());
        Assert.Equal(1, await context.AuditEvents.CountAsync(item => item.Action == "WORK_PLAN_ENSURED"));
        Assert.Equal(2, await context.AuditEvents.CountAsync(item => item.Action == "WORK_PLAN_RECOVERED"));
    }

    [Fact]
    public async Task SameKeyWithAnotherWeekUsesAnIndependentResourceScope()
    {
        var seed = await ResetAndSeedAsync(CanonicalRole.Administration, includePeriod: true);
        await using var context = CreateContext();
        var service = CreateService(context);
        var key = Guid.CreateVersion7();
        await service.EnsureAsync(Command(seed.ActorUserId, key));

        await Assert.ThrowsAsync<WorkPlanPeriodNotFoundException>(() =>
            service.EnsureAsync(Command(seed.ActorUserId, key) with { IsoWeek = 37 }));

        Assert.Equal(1, await context.WorkPlans.CountAsync());
        Assert.Equal(1, await context.IdempotencyRecords.CountAsync());
        Assert.DoesNotContain(await context.AuditEvents.AsNoTracking().ToListAsync(),
            item => item.Action == "IDEMPOTENCY_CONFLICT_REJECTED");
    }

    [Fact]
    public async Task InvalidWeekMissingPeriodAndMissingRoleAreAuditedIdempotentRejections()
    {
        var authorized = await ResetAndSeedAsync(CanonicalRole.Subcoordination, includePeriod: false);
        await using (var context = CreateContext())
        {
            var service = CreateService(context);
            await Assert.ThrowsAsync<WorkPlanIsoWeekInvalidException>(() =>
                service.EnsureAsync(Command(authorized.ActorUserId, Guid.CreateVersion7()) with { IsoWeek = 54 }));
            await Assert.ThrowsAsync<WorkPlanPeriodNotFoundException>(() =>
                service.EnsureAsync(Command(authorized.ActorUserId, Guid.CreateVersion7())));
            Assert.False(await context.WorkPlans.AnyAsync());
            Assert.Empty(await context.IdempotencyRecords.AsNoTracking().ToListAsync());
            Assert.Equal(2, await context.AuditEvents.CountAsync(item => item.Action == "WORK_PLAN_ENSURE_REJECTED"));
        }

        var denied = await ResetAndSeedAsync(
            roleCode: null,
            includePeriod: true,
            positionText: "DIRECCION");
        await using var deniedContext = CreateContext();
        await Assert.ThrowsAsync<WorkPlanAccessDeniedException>(() =>
            CreateService(deniedContext).EnsureAsync(Command(denied.ActorUserId, Guid.CreateVersion7())));
        Assert.False(await deniedContext.WorkPlans.AnyAsync());
        Assert.Empty(await deniedContext.IdempotencyRecords.AsNoTracking().ToListAsync());
        Assert.Contains(await deniedContext.AuditEvents.AsNoTracking().ToListAsync(),
            item => item.Outcome == "ACCESO_DENEGADO");
    }

    [Theory]
    [InlineData(CanonicalRole.Direction)]
    [InlineData(CanonicalRole.Administration)]
    [InlineData(CanonicalRole.Subcoordination)]
    [InlineData(CanonicalRole.SalesFloor)]
    public async Task EveryCanonicalPlanViewerCanEnsureWithoutPositionTextGrantingAuthority(string roleCode)
    {
        var seed = await ResetAndSeedAsync(roleCode, includePeriod: true, positionText: "Puesto sin autoridad");
        await using var context = CreateContext();

        var result = await CreateService(context)
            .EnsureAsync(Command(seed.ActorUserId, Guid.CreateVersion7()));

        Assert.Equal(WorkPlanStatuses.Draft, result.Status);
    }

    [Fact]
    public async Task InactiveAccountIsDeniedWithoutPlanEffect()
    {
        var seed = await ResetAndSeedAsync(CanonicalRole.Direction, includePeriod: true);
        await using var context = CreateContext();
        await context.AppUsers.Where(item => item.Id == seed.ActorUserId)
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.Status, AccountStatus.Inactive));

        await Assert.ThrowsAsync<WorkPlanAccessDeniedException>(() =>
            CreateService(context).EnsureAsync(Command(seed.ActorUserId, Guid.CreateVersion7())));

        Assert.False(await context.WorkPlans.AnyAsync());
        Assert.Contains(await context.AuditEvents.AsNoTracking().ToListAsync(),
            item => item.Outcome == "ACCESO_DENEGADO");
    }

    [Fact]
    public async Task ConcurrentDifferentAndSameKeysConvergeOnOnePostgreSqlIdentity()
    {
        var seed = await ResetAndSeedAsync(CanonicalRole.Direction, includePeriod: true);
        var sharedKey = Guid.CreateVersion7();
        await using var first = CreateContext();
        await using var second = CreateContext();
        await using var third = CreateContext();

        var results = await Task.WhenAll(
            CreateService(first).EnsureAsync(Command(seed.ActorUserId, sharedKey)),
            CreateService(second).EnsureAsync(Command(seed.ActorUserId, sharedKey)),
            CreateService(third).EnsureAsync(Command(seed.ActorUserId, Guid.CreateVersion7())));

        Assert.Single(results.Select(item => item.PlanId).Distinct());
        await using var verification = CreateContext();
        Assert.Equal(1, await verification.WorkPlans.CountAsync());
        Assert.Equal(2, await verification.IdempotencyRecords.CountAsync());
    }

    [Fact]
    public async Task AuditFailureRollsBackPlanAndIdempotency()
    {
        var seed = await ResetAndSeedAsync(CanonicalRole.Direction, includePeriod: true);
        await using var context = CreateContext();
        await context.Database.ExecuteSqlRawAsync(
            """
            CREATE OR REPLACE FUNCTION reject_work_plan_audit() RETURNS trigger AS $$
            BEGIN
                IF NEW.action = 'WORK_PLAN_ENSURED' THEN
                    RAISE EXCEPTION 'synthetic audit failure';
                END IF;
                RETURN NEW;
            END;
            $$ LANGUAGE plpgsql;
            CREATE TRIGGER reject_work_plan_audit BEFORE INSERT ON audit_event
            FOR EACH ROW EXECUTE FUNCTION reject_work_plan_audit();
            """);

        await Assert.ThrowsAsync<DbUpdateException>(() =>
            CreateService(context).EnsureAsync(Command(seed.ActorUserId, Guid.CreateVersion7())));

        context.ChangeTracker.Clear();
        Assert.False(await context.WorkPlans.AnyAsync());
        Assert.False(await context.IdempotencyRecords.AnyAsync());
    }

    [Fact]
    public async Task DraftContentIsDerivedAcrossTwoLevelsWithoutMembershipOrMutation()
    {
        var seed = await ResetAndSeedAsync(CanonicalRole.Direction, includePeriod: true);
        var obligationIds = await SeedTwoLevelObligationsAsync(seed);
        await using var context = CreateContext();
        var before = await context.WorkObligations.AsNoTracking()
            .OrderBy(item => item.Id).Select(item => new { item.Id, item.ExecutionStatus, item.RowVersion }).ToListAsync();

        var plan = await CreateService(context)
            .EnsureAsync(Command(seed.ActorUserId, Guid.CreateVersion7()));
        var derived = await context.WorkObligations.AsNoTracking()
            .Where(item => item.BranchId == plan.BranchId && item.PeriodId == plan.PeriodId)
            .Select(item => item.Id).ToListAsync();
        var after = await context.WorkObligations.AsNoTracking()
            .OrderBy(item => item.Id).Select(item => new { item.Id, item.ExecutionStatus, item.RowVersion }).ToListAsync();

        Assert.Equal(obligationIds.Order(), derived.Order());
        Assert.Equal(before, after);
        Assert.Equal(1, await context.WorkPlans.CountAsync());
        Assert.Empty(await context.AssignmentVersions.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task UniqueConstraintRejectsSecondPlanForBranchAndPeriod()
    {
        var seed = await ResetAndSeedAsync(CanonicalRole.Direction, includePeriod: true);
        await using var context = CreateContext();
        context.WorkPlans.AddRange(
            new WorkPlan(Guid.CreateVersion7(), BranchScope.LorettaId, seed.PeriodId!.Value),
            new WorkPlan(Guid.CreateVersion7(), BranchScope.LorettaId, seed.PeriodId.Value));

        var error = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        Assert.Equal("UX_work_plan_branch_period", (error.InnerException as PostgresException)?.ConstraintName);
    }

    private async Task<Seed> ResetAndSeedAsync(
        string? roleCode,
        bool includePeriod,
        string? positionText = null)
    {
        await using var context = CreateContext();
        await context.Database.EnsureDeletedAsync();
        await context.Database.MigrateAsync();
        var personId = Guid.CreateVersion7();
        var userId = Guid.CreateVersion7();
        context.People.Add(new Person
        {
            Id = personId,
            StableCode = $"PLAN-{userId:N}",
            DisplayName = "Persona sintética",
            CreatedAt = Now.AddDays(-30),
        });
        context.EmploymentVersions.Add(new EmploymentVersion(
            Guid.CreateVersion7(), personId, BranchScope.LorettaId, EmploymentStatus.Active,
            Now.AddDays(-30), positionText: positionText));
        context.AppUsers.Add(new AppUser
        {
            Id = userId,
            PersonId = personId,
            Status = AccountStatus.Active,
            MustChangePassword = false,
            MfaEnrolledAt = Now.AddDays(-30),
            SecurityStamp = $"synthetic-{userId:N}",
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
                ValidFrom = Now.AddDays(-30),
            });
        }

        Guid? periodId = null;
        if (includePeriod)
        {
            var range = WeekContract.Calculate(2026, 36);
            periodId = Guid.CreateVersion7();
            context.WeekPeriods.Add(new WeekPeriod(
                periodId.Value, BranchScope.LorettaId, 2026, 36,
                range.StartsOn, range.EndsOn, WeekContract.Current));
        }

        await context.SaveChangesAsync();
        return new Seed(userId, periodId);
    }

    private async Task<IReadOnlyList<Guid>> SeedTwoLevelObligationsAsync(Seed seed)
    {
        await using var context = CreateContext();
        var releaseId = Guid.CreateVersion7();
        var release = new ConfigurationRelease(releaseId, BranchScope.LorettaId);
        release.ApplyPublished(Published(releaseId), 1, seed.ActorUserId, Now.AddDays(-20));
        context.ConfigurationReleases.Add(release);
        var obligationIds = new List<Guid>();

        foreach (var item in new[]
                 {
                     (TaskCode: "TAR-0008", Role: CanonicalRole.Subcoordination),
                     (TaskCode: "TAR-0007", Role: CanonicalRole.SalesFloor),
                 })
        {
            var task = TaskDefinitionCatalog.Require(item.TaskCode);
            var taskVersionId = Guid.CreateVersion7();
            var taskVersion = new TaskDefinitionVersion(
                taskVersionId, task.Id, 1, 1, JsonDocument.Parse("{}"), releaseId);
            taskVersion.ApplyPublished(Published(taskVersionId), activeForNew: true);
            var policyId = Guid.CreateVersion7();
            var policy = new EligibilityPolicyVersion(
                policyId, task.Id, taskVersionId, releaseId, null, 1, item.Role, true, null);
            policy.ApplyPublished(Published(policyId));
            var ruleId = Guid.CreateVersion7();
            var rule = new ActivationRuleVersion(
                ruleId, task.Id, taskVersionId, releaseId, null, 1,
                ActivationModes.Manual, JsonDocument.Parse("null").RootElement,
                ActivationOriginSchemas.ManualReference);
            rule.ApplyPublished(Published(ruleId));
            context.TaskDefinitionVersions.Add(taskVersion);
            context.EligibilityPolicyVersions.Add(policy);
            context.ActivationRuleVersions.Add(rule);
            await context.SaveChangesAsync();

            var origin = $"PLAN-{item.TaskCode}";
            var request = new GenerationRequest(
                Guid.CreateVersion7(), Guid.CreateVersion7(), Hash(origin), ruleId,
                BranchScope.LorettaId, seed.PeriodId!.Value,
                ActivationOriginSchemas.ManualReference, origin, seed.ActorUserId, Now.AddDays(-1));
            context.GenerationRequests.Add(request);
            await context.SaveChangesAsync();
            var obligation = new WorkObligation(
                Guid.CreateVersion7(), taskVersionId, BranchScope.LorettaId,
                seed.PeriodId.Value, request.Id, origin);
            context.WorkObligations.Add(obligation);
            await context.SaveChangesAsync();
            request.LinkObligation(obligation.Id);
            await context.SaveChangesAsync();
            obligationIds.Add(obligation.Id);
        }

        return obligationIds;
    }

    private static EnsureWorkPlanCommand Command(Guid actorUserId, Guid key) => new(
        actorUserId, key, Guid.CreateVersion7(), BranchScope.LorettaId, 2026, 36);

    private static EfWorkPlanService CreateService(SgolDbContext context) => new(
        context,
        new AuditTransaction(context),
        new FixedClock(Now),
        new Uuid7Generator(new FixedClock(Now)));

    private SgolDbContext CreateContext() => new(
        new DbContextOptionsBuilder<SgolDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options);

    private static VersionRecord Published(Guid id) => new(
        id, VersionStatuses.Current, Now.AddDays(-30), null, "Configuración sintética", null, 2);

    private static string Hash(string value) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private sealed record Seed(Guid ActorUserId, Guid? PeriodId);

    private sealed class FixedClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; } = utcNow;
    }
}
