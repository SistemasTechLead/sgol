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
using Sgol.Web.Infrastructure.Persistence.Generation;
using Sgol.Web.Infrastructure.Persistence.Identity;
using Testcontainers.PostgreSql;
using Xunit;

namespace Sgol.IntegrationTests;

public sealed class GenerationRequestPersistenceTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 9, 3, 21, 0, 0, TimeSpan.Zero);
    private readonly PostgreSqlContainer _postgres = PostgreSqlPersistenceTests.CreateContainerForTests();

    public Task InitializeAsync() => _postgres.StartAsync();

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    [Fact]
    public async Task AcceptedReplayAndConflictAreAuditedWithoutCreatingAnObligation()
    {
        var scenario = await ResetAndSeedAsync(CanonicalRole.Administration);
        var key = Guid.CreateVersion7();
        await using var context = CreateContext();
        var service = CreateService(context);

        var accepted = await service.CreateAsync(Command(scenario, key));
        var recovered = await service.CreateAsync(Command(scenario, key));
        await Assert.ThrowsAsync<GenerationRequestIdempotencyConflictException>(() =>
            service.CreateAsync(Command(scenario, key, originReference: "different-reference")));

        Assert.Equal(GenerationRequestResults.Accepted, accepted.Result);
        Assert.Equal(GenerationRequestResults.Recovered, recovered.Result);
        Assert.Equal(accepted.GenerationRequestId, recovered.GenerationRequestId);
        Assert.Null(accepted.ObligationId);
        Assert.Single(await context.GenerationRequests.AsNoTracking().ToListAsync());
        Assert.Single(await context.IdempotencyRecords.AsNoTracking()
            .Where(item => item.ResourceType == "GENERATION_REQUEST")
            .ToListAsync());
        Assert.Contains(await context.AuditEvents.AsNoTracking().ToListAsync(), item =>
            item.Action == "GENERATION_REQUEST_ACCEPTED" && item.Outcome == "SUCCESS");
        Assert.Contains(await context.AuditEvents.AsNoTracking().ToListAsync(), item =>
            item.Action == "GENERATION_REQUEST_IDEMPOTENCY_CONFLICT" && item.Outcome == "CONFLICT");
        Assert.Empty(await context.WorkObligations.AsNoTracking().ToListAsync());
        Assert.Empty(context.ChangeTracker.Entries());
    }

    [Fact]
    public async Task AcceptedRequestMaterializesPendingAuditedObligationWithApprovedSnapshot()
    {
        var scenario = await ResetAndSeedAsync(CanonicalRole.Direction);
        await using var context = CreateContext();
        var request = await CreateService(context).CreateAsync(Command(scenario, Guid.CreateVersion7()));

        var result = await CreateMaterializer(context).MaterializeAsync(new(
            request.GenerationRequestId,
            Guid.CreateVersion7()));

        Assert.Equal(request.GenerationRequestId, result.GenerationRequestId);
        Assert.Equal(scenario.TaskDefinitionVersionId, result.TaskDefinitionVersionId);
        Assert.Equal(BranchScope.LorettaId, result.BranchId);
        Assert.Equal(scenario.PeriodId, result.PeriodId);
        Assert.Equal("synthetic-reference", result.OriginReference);
        Assert.Equal(WorkObligationStatuses.Pending, result.ExecutionStatus);
        var storedRequest = await context.GenerationRequests.AsNoTracking()
            .SingleAsync(item => item.Id == request.GenerationRequestId);
        var stored = await context.WorkObligations.AsNoTracking().SingleAsync();
        Assert.Equal(stored.Id, storedRequest.ObligationId);
        Assert.Null(stored.InputPayload);
        Assert.Null(stored.DueAt);
        Assert.Contains(await context.AuditEvents.AsNoTracking().ToListAsync(), item =>
            item.Action == "WORK_OBLIGATION_CREATED" &&
            item.ResourceId == stored.Id &&
            item.ActorType == "SYSTEM" &&
            item.Outcome == "SUCCESS");
        Assert.Empty(await context.AssignmentVersions.AsNoTracking().ToListAsync());
        Assert.DoesNotContain(
            context.Model.GetEntityTypes(),
            item => item.ClrType.Name is "WorkPlan" or "PlanVersionObligation");
    }

    [Fact]
    public async Task RetryAndDuplicateFunctionalKeyRecoverSameUnchangedObligation()
    {
        var scenario = await ResetAndSeedAsync(CanonicalRole.Direction);
        await using var context = CreateContext();
        var requestService = CreateService(context);
        var firstRequest = await requestService.CreateAsync(Command(scenario, Guid.CreateVersion7()));
        var sameFunctionalRequest = await requestService.CreateAsync(Command(scenario, Guid.CreateVersion7()));
        var materializer = CreateMaterializer(context);

        var created = await materializer.MaterializeAsync(new(firstRequest.GenerationRequestId, Guid.CreateVersion7()));
        var before = await context.WorkObligations.AsNoTracking().SingleAsync();
        var recovered = await materializer.MaterializeAsync(new(sameFunctionalRequest.GenerationRequestId, Guid.CreateVersion7()));
        var after = await context.WorkObligations.AsNoTracking().SingleAsync();

        Assert.Equal(firstRequest.GenerationRequestId, sameFunctionalRequest.GenerationRequestId);
        Assert.Equal(created.ObligationId, recovered.ObligationId);
        Assert.Equal(before.Id, after.Id);
        Assert.Equal(before.TaskDefinitionVersionId, after.TaskDefinitionVersionId);
        Assert.Equal(before.BranchId, after.BranchId);
        Assert.Equal(before.PeriodId, after.PeriodId);
        Assert.Equal(before.OriginReference, after.OriginReference);
        Assert.Equal(before.ExecutionStatus, after.ExecutionStatus);
        Assert.Equal(before.RowVersion, after.RowVersion);
        Assert.Equal(1, await context.WorkObligations.CountAsync());
    }

    [Fact]
    public async Task TwentyConcurrentMaterializationsCreateOnePostgreSqlObligation()
    {
        var scenario = await ResetAndSeedAsync(CanonicalRole.Direction);
        Guid requestId;
        await using (var requestContext = CreateContext())
        {
            requestId = (await CreateService(requestContext)
                .CreateAsync(Command(scenario, Guid.CreateVersion7()))).GenerationRequestId;
        }

        var results = await Task.WhenAll(Enumerable.Range(0, 20).Select(async _ =>
        {
            await using var context = CreateContext();
            return await CreateMaterializer(context).MaterializeAsync(new(requestId, Guid.CreateVersion7()));
        }));

        Assert.Single(results.Select(item => item.ObligationId).Distinct());
        await using var verification = CreateContext();
        Assert.Equal(1, await verification.WorkObligations.CountAsync());
        Assert.Equal(
            results[0].ObligationId,
            await verification.GenerationRequests
                .Where(item => item.Id == requestId)
                .Select(item => item.ObligationId)
                .SingleAsync());
    }

    [Fact]
    public async Task PostgreSqlRejectsDivergentRequestObligationLink()
    {
        var scenario = await ResetAndSeedAsync(CanonicalRole.Direction);
        await using var context = CreateContext();
        var requestService = CreateService(context);
        var firstRequest = await requestService.CreateAsync(Command(
            scenario,
            Guid.CreateVersion7(),
            originReference: "first-origin"));
        var secondRequest = await requestService.CreateAsync(Command(
            scenario,
            Guid.CreateVersion7(),
            originReference: "second-origin"));
        var materializer = CreateMaterializer(context);
        var first = await materializer.MaterializeAsync(new(firstRequest.GenerationRequestId, Guid.CreateVersion7()));
        var second = await materializer.MaterializeAsync(new(secondRequest.GenerationRequestId, Guid.CreateVersion7()));

        var exception = await Assert.ThrowsAsync<PostgresException>(() =>
            context.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE generation_request SET obligation_id = {second.ObligationId} WHERE id = {firstRequest.GenerationRequestId}"));

        Assert.Equal(PostgresErrorCodes.UniqueViolation, exception.SqlState);
        Assert.Equal("UX_generation_request_obligation_id", exception.ConstraintName);
        Assert.Equal(
            first.ObligationId,
            await context.GenerationRequests.AsNoTracking()
                .Where(item => item.Id == firstRequest.GenerationRequestId)
                .Select(item => item.ObligationId)
                .SingleAsync());
    }

    [Fact]
    public async Task AuditFailureBeforeCommitLeavesNoObligationLinkOrPartialAudit()
    {
        var scenario = await ResetAndSeedAsync(CanonicalRole.Direction);
        await using var context = CreateContext();
        var request = await CreateService(context).CreateAsync(Command(scenario, Guid.CreateVersion7()));
        var duplicateAuditId = Guid.CreateVersion7();
        context.AuditEvents.Add(new AuditEvent
        {
            Id = duplicateAuditId,
            OccurredAt = Now,
            ActorType = "SYSTEM",
            Action = "SYNTHETIC_EXISTING_EVENT",
            ResourceType = "WORK_OBLIGATION",
            CorrelationId = Guid.CreateVersion7(),
            Outcome = "SUCCESS",
        });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        var auditCount = await context.AuditEvents.CountAsync();
        var materializer = CreateMaterializer(
            context,
            new SequenceUuidGenerator(Guid.CreateVersion7(), duplicateAuditId));

        await Assert.ThrowsAsync<DbUpdateException>(() => materializer.MaterializeAsync(new(
            request.GenerationRequestId,
            Guid.CreateVersion7())));

        Assert.False(await context.WorkObligations.AsNoTracking().AnyAsync());
        Assert.Null(await context.GenerationRequests.AsNoTracking()
            .Where(item => item.Id == request.GenerationRequestId)
            .Select(item => item.ObligationId)
            .SingleAsync());
        Assert.Equal(auditCount, await context.AuditEvents.CountAsync());
        Assert.Empty(context.ChangeTracker.Entries());
    }

    [Fact]
    public async Task MissingAndRejectedRequestsHaveNoMaterializationEffects()
    {
        var scenario = await ResetAndSeedAsync(CanonicalRole.Direction);
        await using var context = CreateContext();
        var materializer = CreateMaterializer(context);

        await Assert.ThrowsAsync<GenerationRequestNotFoundException>(() =>
            materializer.MaterializeAsync(new(Guid.CreateVersion7(), Guid.CreateVersion7())));

        var request = await CreateService(context).CreateAsync(Command(scenario, Guid.CreateVersion7()));
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE generation_request SET result = {"RECHAZADA"} WHERE id = {request.GenerationRequestId}");
        context.ChangeTracker.Clear();
        var auditCount = await context.AuditEvents.CountAsync();

        await Assert.ThrowsAsync<GenerationRequestNotAcceptedException>(() =>
            materializer.MaterializeAsync(new(request.GenerationRequestId, Guid.CreateVersion7())));

        Assert.False(await context.WorkObligations.AsNoTracking().AnyAsync());
        Assert.Null(await context.GenerationRequests.AsNoTracking()
            .Where(item => item.Id == request.GenerationRequestId)
            .Select(item => item.ObligationId)
            .SingleAsync());
        Assert.Equal(auditCount, await context.AuditEvents.CountAsync());
    }

    [Fact]
    public async Task TwentyConcurrentRequestsCreateOnePostgreSqlGenerationRequest()
    {
        var scenario = await ResetAndSeedAsync(CanonicalRole.Direction);
        var key = Guid.CreateVersion7();

        var results = await Task.WhenAll(Enumerable.Range(0, 20).Select(async _ =>
        {
            await using var context = CreateContext();
            return await CreateService(context).CreateAsync(Command(scenario, key));
        }));

        Assert.Single(results.Select(item => item.GenerationRequestId).Distinct());
        Assert.Single(results, item => item.Result == GenerationRequestResults.Accepted);
        Assert.Equal(19, results.Count(item => item.Result == GenerationRequestResults.Recovered));
        await using var verification = CreateContext();
        Assert.Equal(1, await verification.GenerationRequests.CountAsync());
        Assert.Equal(1, await verification.IdempotencyRecords.CountAsync(
            item => item.ResourceType == "GENERATION_REQUEST"));
    }

    [Fact]
    public async Task AuditFailureBeforeCommitLeavesNoGenerationOrIdempotencyEffects()
    {
        var scenario = await ResetAndSeedAsync(CanonicalRole.Direction);
        await using var context = CreateContext();
        var duplicateAuditId = Guid.CreateVersion7();
        context.AuditEvents.Add(new AuditEvent
        {
            Id = duplicateAuditId,
            OccurredAt = Now,
            ActorUserId = scenario.ActorUserId,
            ActorType = "APP_USER",
            Action = "SYNTHETIC_EXISTING_EVENT",
            ResourceType = "GENERATION_REQUEST",
            BranchId = BranchScope.LorettaId,
            CorrelationId = Guid.CreateVersion7(),
            Outcome = "SUCCESS",
        });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var requestId = Guid.CreateVersion7();
        var service = CreateService(context, new SequenceUuidGenerator(requestId, duplicateAuditId));
        await Assert.ThrowsAsync<DbUpdateException>(() =>
            service.CreateAsync(Command(scenario, Guid.CreateVersion7())));

        Assert.False(await context.GenerationRequests.AsNoTracking().AnyAsync());
        Assert.False(await context.IdempotencyRecords.AsNoTracking().AnyAsync(
            item => item.ResourceType == "GENERATION_REQUEST"));
        Assert.Empty(context.ChangeTracker.Entries());
    }

    [Fact]
    public async Task PermissionScopeTaskRuleAndOriginFailuresHaveNoGenerationEffect()
    {
        var denied = await ResetAndSeedAsync(CanonicalRole.SalesFloor);
        await using (var deniedContext = CreateContext())
        {
            await Assert.ThrowsAsync<GenerationRequestAccessDeniedException>(() =>
                CreateService(deniedContext).CreateAsync(Command(denied, Guid.CreateVersion7())));
        }

        var allowed = await ResetAndSeedAsync(CanonicalRole.Direction);
        await using (var invalidContext = CreateContext())
        {
            var service = CreateService(invalidContext);
            await Assert.ThrowsAsync<GenerationRequestAccessDeniedException>(() =>
                service.CreateAsync(Command(allowed, Guid.CreateVersion7()) with { BranchId = Guid.CreateVersion7() }));
            await Assert.ThrowsAsync<GenerationRequestOriginInvalidException>(() =>
                service.CreateAsync(Command(allowed, Guid.CreateVersion7(), originReference: " ")));
        }

        var inactive = await ResetAndSeedAsync(CanonicalRole.Direction, activeForNew: false);
        await using (var inactiveContext = CreateContext())
        {
            await Assert.ThrowsAsync<GenerationRequestTaskInactiveException>(() =>
                CreateService(inactiveContext).CreateAsync(Command(inactive, Guid.CreateVersion7())));
        }

        var recurring = await ResetAndSeedAsync(
            CanonicalRole.Direction,
            taskCode: "TAR-0005",
            mode: ActivationModes.Recurring,
            originType: ActivationOriginSchemas.WorkingDayWindow);
        await using (var recurringContext = CreateContext())
        {
            await Assert.ThrowsAsync<GenerationRequestRuleNotManualException>(() =>
                CreateService(recurringContext).CreateAsync(Command(
                    recurring,
                    Guid.CreateVersion7(),
                    originType: ActivationOriginSchemas.WorkingDayWindow)));
        }

        await using var verification = CreateContext();
        Assert.False(await verification.GenerationRequests.AnyAsync());
    }

    [Fact]
    public async Task GetAllowsCreatorAndSuperiorButHidesHorizontalAccess()
    {
        var scenario = await ResetAndSeedAsync(CanonicalRole.Subcoordination);
        var direction = await SeedActorAsync("GEN-DIRECTION", CanonicalRole.Direction);
        var peer = await SeedActorAsync("GEN-PEER", CanonicalRole.Subcoordination);
        await using var context = CreateContext();
        var service = CreateService(context);
        var created = await service.CreateAsync(Command(scenario, Guid.CreateVersion7()));

        var own = await service.GetAsync(
            scenario.ActorUserId, Guid.CreateVersion7(), created.GenerationRequestId);
        var superior = await service.GetAsync(
            direction, Guid.CreateVersion7(), created.GenerationRequestId);
        await Assert.ThrowsAsync<GenerationRequestNotFoundException>(() =>
            service.GetAsync(peer, Guid.CreateVersion7(), created.GenerationRequestId));

        Assert.Equal(created.GenerationRequestId, own.GenerationRequestId);
        Assert.Equal(created.GenerationRequestId, superior.GenerationRequestId);
        Assert.Contains(await context.AuditEvents.AsNoTracking().ToListAsync(), item =>
            item.Action == "GENERATION_REQUEST_READ_ACCESS_DENIED" && item.Outcome == "DENIED");
    }

    private async Task<Scenario> ResetAndSeedAsync(
        string actorRole,
        bool activeForNew = true,
        string taskCode = "TAR-0007",
        string mode = ActivationModes.Manual,
        string originType = ActivationOriginSchemas.ManualReference)
    {
        await using var context = CreateContext();
        await context.Database.EnsureDeletedAsync();
        await context.Database.MigrateAsync();
        var actor = await SeedActorAsync("GEN-ACTOR", actorRole, context);
        var releaseId = Guid.CreateVersion7();
        var release = new ConfigurationRelease(releaseId, BranchScope.LorettaId);
        release.ApplyPublished(
            Published(releaseId),
            versionNo: 1,
            actor,
            Now);

        var task = TaskDefinitionCatalog.Require(taskCode);
        var taskVersionId = Guid.CreateVersion7();
        using var empty = JsonDocument.Parse("{}");
        var taskVersion = new TaskDefinitionVersion(
            taskVersionId, task.Id, 1, 1, empty, releaseId);
        taskVersion.ApplyPublished(Published(taskVersionId), activeForNew);

        var eligibilityId = Guid.CreateVersion7();
        var eligibility = new EligibilityPolicyVersion(
            eligibilityId,
            task.Id,
            taskVersionId,
            releaseId,
            null,
            1,
            EligibilityPolicyCatalog.RequireRole(taskCode),
            true,
            null);
        eligibility.ApplyPublished(Published(eligibilityId));

        var ruleId = Guid.CreateVersion7();
        using var schedule = mode == ActivationModes.Manual
            ? JsonDocument.Parse("null")
            : JsonDocument.Parse(
                """{"kind":"WORKING_DAY_WINDOWS","workingDaysOnly":true,"localTimes":["12:00","17:00"],"timeZone":"America/Mexico_City"}""");
        var rule = new ActivationRuleVersion(
            ruleId,
            task.Id,
            taskVersionId,
            releaseId,
            null,
            1,
            mode,
            schedule.RootElement,
            originType);
        rule.ApplyPublished(Published(ruleId));

        var week = WeekContract.Calculate(2026, 36);
        var period = new WeekPeriod(
            Guid.CreateVersion7(),
            BranchScope.LorettaId,
            2026,
            36,
            week.StartsOn,
            week.EndsOn,
            WeekContract.Current);
        context.ConfigurationReleases.Add(release);
        context.TaskDefinitionVersions.Add(taskVersion);
        context.EligibilityPolicyVersions.Add(eligibility);
        context.ActivationRuleVersions.Add(rule);
        context.WeekPeriods.Add(period);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        return new Scenario(actor, ruleId, taskVersionId, period.Id, originType);
    }

    private async Task<Guid> SeedActorAsync(
        string stableCode,
        string roleCode,
        SgolDbContext? existingContext = null)
    {
        var ownsContext = existingContext is null;
        var context = existingContext ?? CreateContext();
        try
        {
            var personId = Guid.CreateVersion7();
            var userId = Guid.CreateVersion7();
            context.People.Add(new Person
            {
                Id = personId,
                StableCode = stableCode,
                DisplayName = "Persona sintética",
                CreatedAt = Now,
            });
            context.EmploymentVersions.Add(new EmploymentVersion(
                Guid.CreateVersion7(), personId, BranchScope.LorettaId, EmploymentStatus.Active, Now));
            context.AppUsers.Add(new AppUser
            {
                Id = userId,
                PersonId = personId,
                Status = AccountStatus.Active,
                MustChangePassword = false,
                MfaEnrolledAt = Now,
                SecurityStamp = $"synthetic-{userId:N}",
            });
            context.IdentityCredentials.Add(new IdentityCredential
            {
                UserId = userId,
                UserName = $"user.{userId:N}",
                NormalizedUserName = $"USER.{userId:N}",
                PasswordHash = "synthetic-hash-not-a-secret",
            });
            context.RoleAssignmentVersions.Add(new RoleAssignmentVersion
            {
                Id = Guid.CreateVersion7(),
                UserId = userId,
                BranchId = BranchScope.LorettaId,
                RoleCode = roleCode,
                Status = RoleAssignmentStatus.Active,
                ValidFrom = Now,
            });
            await context.SaveChangesAsync();
            context.ChangeTracker.Clear();
            return userId;
        }
        finally
        {
            if (ownsContext)
            {
                await context.DisposeAsync();
            }
        }
    }

    private SgolDbContext CreateContext() => new(
        new DbContextOptionsBuilder<SgolDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options);

    private static EfGenerationRequestService CreateService(
        SgolDbContext context,
        IUuidGenerator? uuidGenerator = null)
    {
        var clock = new FixedClock(Now);
        var generator = uuidGenerator ?? new Uuid7Generator(clock);
        var audit = new AuditTransaction(context);
        var hierarchy = new EfRoleAssignmentService(context, audit, clock, generator);
        return new EfGenerationRequestService(context, audit, hierarchy, clock, generator);
    }

    private static EfWorkObligationMaterializer CreateMaterializer(
        SgolDbContext context,
        IUuidGenerator? uuidGenerator = null)
    {
        var clock = new FixedClock(Now);
        return new EfWorkObligationMaterializer(
            context,
            new AuditTransaction(context),
            clock,
            uuidGenerator ?? new Uuid7Generator(clock));
    }

    private static CreateGenerationRequestCommand Command(
        Scenario scenario,
        Guid key,
        string originReference = "synthetic-reference",
        string? originType = null) => new(
            scenario.ActorUserId,
            key,
            Guid.CreateVersion7(),
            scenario.RuleVersionId,
            BranchScope.LorettaId,
            scenario.PeriodId,
            originType ?? scenario.OriginType,
            originReference);

    private static VersionRecord Published(Guid id) => new(
        id,
        VersionStatuses.Current,
        Now,
        null,
        "Configuración sintética",
        null,
        2);

    private sealed record Scenario(
        Guid ActorUserId,
        Guid RuleVersionId,
        Guid TaskDefinitionVersionId,
        Guid PeriodId,
        string OriginType);

    private sealed class FixedClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; } = utcNow;
    }

    private sealed class SequenceUuidGenerator(params Guid[] values) : IUuidGenerator
    {
        private readonly Queue<Guid> _values = new(values);

        public Guid NewUuid() => _values.Dequeue();
    }
}
