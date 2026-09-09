using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Sgol.Assignment.Contracts;
using Sgol.BuildingBlocks.Identifiers;
using Sgol.BuildingBlocks.Time;
using Sgol.BuildingBlocks.Versioning;
using Sgol.Configuration.Contracts;
using Sgol.Generation.Contracts;
using Sgol.Identity.Contracts;
using Sgol.Organization.Contracts;
using Sgol.Planning.Contracts;
using Sgol.Web.Infrastructure.Persistence;
using Sgol.Web.Infrastructure.Persistence.Assignment;
using Sgol.Web.Infrastructure.Persistence.Auditing;
using Sgol.Web.Infrastructure.Persistence.Bootstrap;
using Sgol.Web.Infrastructure.Persistence.Planning;
using Testcontainers.PostgreSql;
using Xunit;

namespace Sgol.IntegrationTests;

public sealed class PlanPublicationPersistenceTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 9, 4, 20, 30, 0, TimeSpan.Zero);
    private readonly PostgreSqlContainer _postgres = PostgreSqlPersistenceTests.CreateContainerForTests();

    public Task InitializeAsync() => _postgres.StartAsync();

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    [Theory]
    [InlineData(CanonicalRole.Direction, 3)]
    [InlineData(CanonicalRole.Administration, 3)]
    [InlineData(CanonicalRole.Subcoordination, 2)]
    public async Task CanonicalSuperiorPublishesOwnAccumulatedScopeOnly(string actorRole, int expectedCount)
    {
        var seed = await ResetAndSeedAsync(actorRole);
        await using var context = CreateContext();

        var result = await CreateService(context).PublishAsync(Command(seed, Guid.CreateVersion7(), 1));

        Assert.Equal(PlanPublicationResults.Initial, result.Result);
        Assert.Equal(actorRole, result.ScopeRole);
        Assert.Equal(expectedCount, result.Obligations.Count);
        Assert.All(result.Obligations, item =>
            Assert.True(RoleHierarchy.CanAccessLevel(actorRole, seed.Obligations[item.ObligationId].Role)));
        Assert.DoesNotContain(result.Obligations, item =>
            seed.Obligations[item.ObligationId].Role == CanonicalRole.Administration &&
            actorRole == CanonicalRole.Subcoordination);
        var plan = await context.WorkPlans.AsNoTracking().SingleAsync();
        Assert.Equal(seed.PlanId, plan.Id);
        Assert.Equal(WorkPlanStatuses.Published, plan.Status);
        Assert.Equal(2, plan.RowVersion);
        Assert.Single(await context.PlanVersions.AsNoTracking().ToListAsync());
        Assert.Equal(expectedCount, await context.PlanVersionObligations.CountAsync());
        var audit = await context.AuditEvents.AsNoTracking()
            .SingleAsync(item => item.Action == "WORK_PLAN_PUBLISHED");
        Assert.Equal(PlanPublicationResults.Initial, audit.Outcome);
        Assert.Equal(
            [
                "addedObligations", "obligationCount", "planId", "planStatus", "publicationId",
                "publishedAt", "publishedBy", "rowVersion", "schemaVersion", "scopeRole",
                "versionNo", "versionStatus",
            ],
            audit.AfterData!.RootElement.EnumerateObject().Select(item => item.Name).Order().ToArray());
    }

    [Fact]
    public async Task SalesFloorAndTextualAuthorityCannotPublish()
    {
        var seed = await ResetAndSeedAsync(CanonicalRole.SalesFloor, actorPosition: "Dirección general");
        await using var context = CreateContext();

        var error = await Assert.ThrowsAsync<PlanPublicationAccessDeniedException>(() =>
            CreateService(context).PublishAsync(Command(seed, Guid.CreateVersion7(), 1)));

        Assert.Equal("ACCESO_DENEGADO", error.ErrorCode);
        Assert.Empty(await context.PlanVersions.ToListAsync());
        Assert.Equal(WorkPlanStatuses.Draft, (await context.WorkPlans.SingleAsync()).Status);
        Assert.Contains(await context.AuditEvents.AsNoTracking().ToListAsync(),
            item => item.Action == "WORK_PLAN_PUBLICATION_REJECTED" && item.Outcome == "ACCESO_DENEGADO");
    }

    [Fact]
    public async Task InactiveOrRolelessActorIsRejectedWithoutPublication()
    {
        var inactive = await ResetAndSeedAsync(CanonicalRole.Direction);
        await using (var context = CreateContext())
        {
            await context.AppUsers.Where(item => item.Id == inactive.ActorUserId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.Status, AccountStatus.Inactive));
            await Assert.ThrowsAsync<PlanPublicationAccessDeniedException>(() =>
                CreateService(context).PublishAsync(Command(inactive, Guid.CreateVersion7(), 1)));
            Assert.Empty(await context.PlanVersions.AsNoTracking().ToListAsync());
        }

        var roleless = await ResetAndSeedAsync(CanonicalRole.Direction);
        await using var rolelessContext = CreateContext();
        await rolelessContext.RoleAssignmentVersions
            .Where(item => item.UserId == roleless.ActorUserId && item.Status == RoleAssignmentStatus.Active)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.Status, RoleAssignmentStatus.Superseded)
                .SetProperty(item => item.ValidTo, Now));
        await Assert.ThrowsAsync<PlanPublicationAccessDeniedException>(() =>
            CreateService(rolelessContext).PublishAsync(Command(roleless, Guid.CreateVersion7(), 1)));
        Assert.Empty(await rolelessContext.PlanVersions.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task LateObligationCreatesAccumulatedV2AndReplayPreservesV1IdentityAndEtag()
    {
        var seed = await ResetAndSeedAsync(CanonicalRole.Subcoordination);
        var firstKey = Guid.CreateVersion7();
        PlanPublicationResult first;
        await using (var firstContext = CreateContext())
        {
            first = await CreateService(firstContext).PublishAsync(Command(seed, firstKey, 1));
        }

        var late = await AddObligationAsync(seed, CanonicalRole.SalesFloor, assigned: true);
        PlanPublicationResult second;
        await using (var secondContext = CreateContext())
        {
            second = await CreateService(secondContext).PublishAsync(
                Command(seed, Guid.CreateVersion7(), 2));
        }

        Assert.Equal(PlanPublicationResults.Incremental, second.Result);
        Assert.Equal(first.Obligations.Count + 1, second.Obligations.Count);
        Assert.Equal(late, Assert.Single(second.AddedObligations).ObligationId);
        Assert.Equal(seed.PlanId, second.PlanId);
        Assert.Equal(3, second.RowVersion);

        await using var verification = CreateContext();
        var versions = await verification.PlanVersions.AsNoTracking()
            .OrderBy(item => item.VersionNo).ToListAsync();
        Assert.Equal(2, versions.Count);
        Assert.Equal(PlanVersionStatuses.Superseded, versions[0].Status);
        Assert.Equal(PlanVersionStatuses.Current, versions[1].Status);
        Assert.Equal(first.PublicationId, versions[0].Id);
        Assert.Equal(second.PublicationId, versions[1].Id);
        Assert.Single(await verification.WorkPlans.AsNoTracking().ToListAsync());

        var replay = await CreateService(verification).PublishAsync(Command(seed, firstKey, 1));
        Assert.Equal(PlanPublicationResults.Recovered, replay.Result);
        Assert.Equal(first.PublicationId, replay.PublicationId);
        Assert.Equal(2, replay.RowVersion);
        Assert.Equal(3, (await verification.WorkPlans.AsNoTracking().SingleAsync()).RowVersion);

        await Assert.ThrowsAsync<PlanPublicationNoChangesException>(() =>
            CreateService(verification).PublishAsync(Command(seed, Guid.CreateVersion7(), 3)));
        await Assert.ThrowsAsync<PlanPublicationIdempotencyConflictException>(() =>
            CreateService(verification).PublishAsync(Command(seed, firstKey, 3)));
    }

    [Fact]
    public async Task UnassignedApplicableObligationRejectsAtomicallyWhileConcludedUnpublishedIsExcluded()
    {
        var seed = await ResetAndSeedAsync(CanonicalRole.Subcoordination);
        var concluded = await AddObligationAsync(seed, CanonicalRole.SalesFloor, assigned: true);
        await using (var update = CreateContext())
        {
            await ObligationConclusionTestData.ConcludeAsync(update, concluded, Now);
        }

        var unassigned = await AddObligationAsync(seed, CanonicalRole.SalesFloor, assigned: false);
        await using var context = CreateContext();
        await Assert.ThrowsAsync<PlanPublicationUnassignedObligationException>(() =>
            CreateService(context).PublishAsync(Command(seed, Guid.CreateVersion7(), 1)));

        Assert.Empty(await context.PlanVersions.AsNoTracking().ToListAsync());
        Assert.Equal(1, (await context.WorkPlans.AsNoTracking().SingleAsync()).RowVersion);
        Assert.Equal(WorkObligationStatuses.Concluded,
            (await context.WorkObligations.AsNoTracking().SingleAsync(item => item.Id == concluded)).ExecutionStatus);
        Assert.False(await context.AssignmentVersions.AnyAsync(item => item.ObligationId == unassigned));
    }

    [Fact]
    public async Task ResponsibleWhoseCanonicalRoleChangedMakesNewAssignmentUnpublishable()
    {
        var seed = await ResetAndSeedAsync(CanonicalRole.Subcoordination);
        var floorUserId = seed.People[CanonicalRole.SalesFloor].UserId;
        await using (var update = CreateContext())
        {
            await update.RoleAssignmentVersions
                .Where(item => item.UserId == floorUserId && item.Status == RoleAssignmentStatus.Active)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(item => item.Status, RoleAssignmentStatus.Superseded)
                    .SetProperty(item => item.ValidTo, Now.AddMinutes(-1)));
            update.RoleAssignmentVersions.Add(new RoleAssignmentVersion
            {
                Id = Guid.CreateVersion7(),
                UserId = floorUserId,
                BranchId = BranchScope.LorettaId,
                RoleCode = CanonicalRole.Subcoordination,
                Status = RoleAssignmentStatus.Active,
                ValidFrom = Now.AddMinutes(-1),
            });
            await update.SaveChangesAsync();
        }

        await using var context = CreateContext();
        await Assert.ThrowsAsync<PlanPublicationAssignmentInvalidException>(() =>
            CreateService(context).PublishAsync(Command(seed, Guid.CreateVersion7(), 1)));
        Assert.Empty(await context.PlanVersions.AsNoTracking().ToListAsync());
        Assert.Equal(1, (await context.WorkPlans.AsNoTracking().SingleAsync()).RowVersion);
    }

    [Fact]
    public async Task RealEligibilityAndAutomaticAssignmentCanBePublishedThroughPersistedEvaluation()
    {
        var seed = await ResetAndSeedAsync(CanonicalRole.Direction);
        var late = await AddObligationAsync(seed, CanonicalRole.SalesFloor, assigned: false);
        await using var context = CreateContext();
        var responsible = seed.People[CanonicalRole.SalesFloor];
        context.AvailabilityDayVersions.Add(new AvailabilityDayVersion(
            Guid.CreateVersion7(),
            responsible.PersonId,
            BranchScope.LorettaId,
            DateOnly.FromDateTime(Now.UtcDateTime),
            isAvailable: true,
            seed.ActorUserId));
        await context.SaveChangesAsync();

        var evaluation = await CreateEligibilityService(context).EvaluateAsync(new EvaluateEligibilityCommand(
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            late,
            DateOnly.FromDateTime(Now.UtcDateTime),
            EligibilityDateSources.ManualRequest));
        var assignment = await CreateAssignmentService(context).AssignAsync(new AssignObligationCommand(
            Guid.CreateVersion7(),
            late,
            evaluation.EvaluationId,
            Guid.CreateVersion7()));

        var publication = await CreateService(context).PublishAsync(Command(seed, Guid.CreateVersion7(), 1));

        Assert.Equal(AutomaticAssignmentResults.Created, assignment.Result);
        Assert.Contains(publication.AddedObligations, item =>
            item.ObligationId == late && item.AssignmentVersionId == assignment.AssignmentId);
        var persisted = await context.AssignmentVersions.AsNoTracking()
            .SingleAsync(item => item.Id == assignment.AssignmentId);
        Assert.False(persisted.Explanation.RootElement.TryGetProperty(
            "eligibilityPolicyVersionId", out _));
    }

    [Fact]
    public async Task CorrectionWithCoherentDirectPolicyAndEvaluationRemainsPublishable()
    {
        var seed = await ResetAndSeedAsync(CanonicalRole.Direction);
        var target = seed.Obligations.Values.Single(item => item.Role == CanonicalRole.SalesFloor);
        await using var context = CreateContext();
        var automatic = await context.AssignmentVersions
            .SingleAsync(item => item.Id == target.AssignmentId);
        var evaluationId = automatic.Explanation.RootElement
            .GetProperty("eligibilityEvaluationId").GetGuid();
        automatic.Supersede();
        var correctionId = Guid.CreateVersion7();
        context.AssignmentVersions.Add(new AssignmentVersion(
            correctionId,
            target.ObligationId,
            automatic.PersonId,
            AssignmentVersionStatuses.Current,
            AssignmentTypes.Correction,
            JsonSerializer.SerializeToDocument(new
            {
                schemaVersion = 1,
                eligibilityEvaluationId = evaluationId,
                eligibilityPolicyVersionId = target.PolicyId,
            }, JsonSerializerOptions.Web),
            Now,
            "Corrección sintética",
            seed.ActorUserId,
            automatic.Id));
        await context.SaveChangesAsync();

        var publication = await CreateService(context).PublishAsync(Command(seed, Guid.CreateVersion7(), 1));

        Assert.Contains(publication.Obligations, item =>
            item.ObligationId == target.ObligationId && item.AssignmentVersionId == correctionId);
    }

    [Fact]
    public async Task AutomaticAssignmentWithMismatchedEvaluationPolicyRejectsAtomically()
    {
        var seed = await ResetAndSeedAsync(CanonicalRole.Direction);
        var target = seed.Obligations.Values.Single(item => item.Role == CanonicalRole.SalesFloor);
        var otherPolicy = seed.Obligations.Values
            .Single(item => item.Role == CanonicalRole.Subcoordination).PolicyId;
        await using var context = CreateContext();
        var assignment = await context.AssignmentVersions.AsNoTracking()
            .SingleAsync(item => item.Id == target.AssignmentId);
        var evaluationId = assignment.Explanation.RootElement
            .GetProperty("eligibilityEvaluationId").GetGuid();
        await context.EligibilityEvaluations
            .Where(item => item.Id == evaluationId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.PolicyVersionId, otherPolicy));

        await Assert.ThrowsAsync<PlanPublicationContentConflictException>(() =>
            CreateService(context).PublishAsync(Command(seed, Guid.CreateVersion7(), 1)));

        Assert.Empty(await context.PlanVersions.AsNoTracking().ToListAsync());
        Assert.Empty(await context.PlanVersionObligations.AsNoTracking().ToListAsync());
        var plan = await context.WorkPlans.AsNoTracking().SingleAsync();
        Assert.Equal(WorkPlanStatuses.Draft, plan.Status);
        Assert.Equal(1, plan.RowVersion);
    }

    [Fact]
    public async Task StaleEtagAndMissingPlanAreIdempotentAuditedRejectionsWithoutEffects()
    {
        var seed = await ResetAndSeedAsync(CanonicalRole.Direction);
        await using var context = CreateContext();
        var service = CreateService(context);
        var staleKey = Guid.CreateVersion7();

        await Assert.ThrowsAsync<PlanPublicationVersionConflictException>(() =>
            service.PublishAsync(Command(seed, staleKey, 9)));
        await Assert.ThrowsAsync<PlanPublicationVersionConflictException>(() =>
            service.PublishAsync(Command(seed, staleKey, 9)));
        await Assert.ThrowsAsync<PlanPublicationNotFoundException>(() =>
            service.PublishAsync(Command(seed with { PlanId = Guid.CreateVersion7() }, Guid.CreateVersion7(), 1)));

        Assert.Empty(await context.PlanVersions.AsNoTracking().ToListAsync());
        Assert.Equal(2, await context.IdempotencyRecords.CountAsync());
        Assert.Equal(3, await context.AuditEvents.CountAsync(item =>
            item.Action == "WORK_PLAN_PUBLICATION_REJECTED"));
    }

    [Fact]
    public async Task SameKeyConcurrencyCreatesOnePublicationAndAuditFailureRollsEverythingBack()
    {
        var seed = await ResetAndSeedAsync(CanonicalRole.Direction);
        var key = Guid.CreateVersion7();
        await using var first = CreateContext();
        await using var second = CreateContext();

        var results = await Task.WhenAll(
            CreateService(first).PublishAsync(Command(seed, key, 1)),
            CreateService(second).PublishAsync(Command(seed, key, 1)));

        Assert.Single(results.Select(item => item.PublicationId).Distinct());
        await using var verification = CreateContext();
        Assert.Single(await verification.PlanVersions.AsNoTracking().ToListAsync());
        Assert.Single(await verification.IdempotencyRecords.Where(item => item.Key == key).ToListAsync());

        var rollbackSeed = await ResetAndSeedAsync(CanonicalRole.Direction);
        await using var rollback = CreateContext();
        await rollback.Database.ExecuteSqlRawAsync(
            """
            CREATE OR REPLACE FUNCTION reject_plan_publication_audit() RETURNS trigger AS $$
            BEGIN
                IF NEW.action = 'WORK_PLAN_PUBLISHED' THEN
                    RAISE EXCEPTION 'synthetic publication audit failure';
                END IF;
                RETURN NEW;
            END;
            $$ LANGUAGE plpgsql;
            CREATE TRIGGER reject_plan_publication_audit BEFORE INSERT ON audit_event
            FOR EACH ROW EXECUTE FUNCTION reject_plan_publication_audit();
            """);

        await Assert.ThrowsAsync<DbUpdateException>(() =>
            CreateService(rollback).PublishAsync(Command(rollbackSeed, Guid.CreateVersion7(), 1)));
        rollback.ChangeTracker.Clear();
        Assert.Empty(await rollback.PlanVersions.AsNoTracking().ToListAsync());
        Assert.Empty(await rollback.PlanVersionObligations.AsNoTracking().ToListAsync());
        Assert.False(await rollback.IdempotencyRecords.AnyAsync(item => item.Scope.Contains("plan-publication")));
        var plan = await rollback.WorkPlans.AsNoTracking().SingleAsync();
        Assert.Equal(WorkPlanStatuses.Draft, plan.Status);
        Assert.Equal(1, plan.RowVersion);
    }

    [Fact]
    public async Task PostgreSqlEnforcesCurrentScopeAndAssignmentObligationIntegrity()
    {
        var seed = await ResetAndSeedAsync(CanonicalRole.Subcoordination);
        await using var context = CreateContext();
        var published = await CreateService(context).PublishAsync(Command(seed, Guid.CreateVersion7(), 1));
        context.ChangeTracker.Clear();

        context.PlanVersions.Add(new PlanVersion(
            Guid.CreateVersion7(), seed.PlanId, 2, CanonicalRole.Subcoordination,
            seed.ActorUserId, Now.AddMinutes(1), null, 3));
        var duplicate = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        Assert.Equal(PlanVersionConfiguration.CurrentScopeIndex,
            (duplicate.InnerException as PostgresException)?.ConstraintName);
        context.ChangeTracker.Clear();

        var admin = seed.Obligations.Single(item => item.Value.Role == CanonicalRole.Administration);
        var floor = seed.Obligations.Single(item => item.Value.Role == CanonicalRole.SalesFloor);
        context.PlanVersionObligations.Add(new PlanVersionObligation(
            published.PublicationId, admin.Key, floor.Value.AssignmentId));
        var mismatch = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        Assert.Contains("assignment_version", (mismatch.InnerException as PostgresException)?.ConstraintName);
    }

    private async Task<Seed> ResetAndSeedAsync(string actorRole, string? actorPosition = null)
    {
        await using var context = CreateContext();
        await context.Database.EnsureDeletedAsync();
        await context.Database.MigrateAsync();

        var people = new Dictionary<string, PersonState>();
        foreach (var role in new[]
                 {
                     CanonicalRole.Direction,
                     CanonicalRole.Administration,
                     CanonicalRole.Subcoordination,
                     CanonicalRole.SalesFloor,
                 })
        {
            people[role] = AddUser(context, role, role == actorRole ? actorPosition : null);
        }

        var periodId = Guid.CreateVersion7();
        var range = WeekContract.Calculate(2026, 36);
        context.WeekPeriods.Add(new WeekPeriod(
            periodId, BranchScope.LorettaId, 2026, 36,
            range.StartsOn, range.EndsOn, WeekContract.Current));
        var planId = Guid.CreateVersion7();
        context.WorkPlans.Add(new WorkPlan(planId, BranchScope.LorettaId, periodId));

        var releaseId = Guid.CreateVersion7();
        var release = new ConfigurationRelease(releaseId, BranchScope.LorettaId);
        release.ApplyPublished(Published(releaseId), 1, people[CanonicalRole.Direction].UserId, Now.AddDays(-20));
        context.ConfigurationReleases.Add(release);
        await context.SaveChangesAsync();

        var obligations = new Dictionary<Guid, ObligationState>();
        foreach (var definition in new[]
                 {
                     (TaskCode: "TAR-0026", Role: CanonicalRole.Administration),
                     (TaskCode: "TAR-0005", Role: CanonicalRole.Subcoordination),
                     (TaskCode: "TAR-0007", Role: CanonicalRole.SalesFloor),
                 })
        {
            var configuration = await AddConfigurationAsync(context, releaseId, definition.TaskCode, definition.Role);
            var state = await AddObligationCoreAsync(
                context, periodId, configuration, people[definition.Role],
                people[actorRole].UserId, assigned: true);
            obligations[state.ObligationId] = state;
        }

        return new Seed(
            people[actorRole].UserId,
            planId,
            periodId,
            people,
            obligations);
    }

    private async Task<Guid> AddObligationAsync(Seed seed, string role, bool assigned)
    {
        await using var context = CreateContext();
        var existing = seed.Obligations.Values.First(item => item.Role == role);
        var state = await AddObligationCoreAsync(
            context,
            seed.PeriodId,
            new ConfigurationState(existing.TaskVersionId, existing.PolicyId, existing.RuleId, role),
            seed.People[role],
            seed.ActorUserId,
            assigned);
        seed.Obligations[state.ObligationId] = state;
        return state.ObligationId;
    }

    private static PersonState AddUser(SgolDbContext context, string role, string? position)
    {
        var personId = Guid.CreateVersion7();
        var userId = Guid.CreateVersion7();
        context.People.Add(new Person
        {
            Id = personId,
            StableCode = $"PUB-{userId:N}",
            DisplayName = "Persona sintética",
            CreatedAt = Now.AddDays(-30),
        });
        context.EmploymentVersions.Add(new EmploymentVersion(
            Guid.CreateVersion7(), personId, BranchScope.LorettaId,
            EmploymentStatus.Active, Now.AddDays(-30), positionText: position));
        context.AppUsers.Add(new AppUser
        {
            Id = userId,
            PersonId = personId,
            Status = AccountStatus.Active,
            MustChangePassword = false,
            MfaEnrolledAt = Now.AddDays(-30),
            SecurityStamp = $"synthetic-{userId:N}",
        });
        context.RoleAssignmentVersions.Add(new RoleAssignmentVersion
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            BranchId = BranchScope.LorettaId,
            RoleCode = role,
            Status = RoleAssignmentStatus.Active,
            ValidFrom = Now.AddDays(-30),
        });
        return new PersonState(personId, userId);
    }

    private static async Task<ConfigurationState> AddConfigurationAsync(
        SgolDbContext context,
        Guid releaseId,
        string taskCode,
        string role)
    {
        var task = TaskDefinitionCatalog.Require(taskCode);
        var taskVersionId = Guid.CreateVersion7();
        var taskVersion = new TaskDefinitionVersion(
            taskVersionId, task.Id, 1, 1, JsonDocument.Parse("{}"), releaseId);
        taskVersion.ApplyPublished(Published(taskVersionId), activeForNew: true);
        var policyId = Guid.CreateVersion7();
        var policy = new EligibilityPolicyVersion(
            policyId, task.Id, taskVersionId, releaseId, null, 1, role, true, null);
        policy.ApplyPublished(Published(policyId));
        var ruleId = Guid.CreateVersion7();
        using var schedule = taskCode switch
        {
            "TAR-0005" => JsonDocument.Parse(
                """{"kind":"WORKING_DAY_WINDOWS","workingDaysOnly":true,"localTimes":["12:00","17:00"],"timeZone":"America/Mexico_City"}"""),
            "TAR-0026" => JsonDocument.Parse(
                """{"kind":"BUSINESS_DAYS_BEFORE_DUE_DATE","businessDaysBefore":3,"localTime":"08:30","timeZone":"America/Mexico_City","adjustDueDateToPreviousBusinessDay":true}"""),
            _ => JsonDocument.Parse("null"),
        };
        var activation = ActivationPolicyCatalog.Require(taskCode);
        var rule = new ActivationRuleVersion(
            ruleId, task.Id, taskVersionId, releaseId, null, 1,
            activation.Mode, schedule.RootElement, activation.OriginKeySchema);
        rule.ApplyPublished(Published(ruleId));
        context.TaskDefinitionVersions.Add(taskVersion);
        context.EligibilityPolicyVersions.Add(policy);
        context.ActivationRuleVersions.Add(rule);
        await context.SaveChangesAsync();
        return new ConfigurationState(taskVersionId, policyId, ruleId, role);
    }

    private static async Task<ObligationState> AddObligationCoreAsync(
        SgolDbContext context,
        Guid periodId,
        ConfigurationState configuration,
        PersonState responsible,
        Guid requestedBy,
        bool assigned)
    {
        var origin = $"PUB-{Guid.CreateVersion7():N}";
        var request = new GenerationRequest(
            Guid.CreateVersion7(), Guid.CreateVersion7(), Hash(origin), configuration.RuleId,
            BranchScope.LorettaId, periodId, ActivationOriginSchemas.ManualReference,
            origin, requestedBy, Now.AddMinutes(-5));
        context.GenerationRequests.Add(request);
        await context.SaveChangesAsync();
        var obligation = new WorkObligation(
            Guid.CreateVersion7(), configuration.TaskVersionId, BranchScope.LorettaId,
            periodId, request.Id, origin);
        context.WorkObligations.Add(obligation);
        await context.SaveChangesAsync();
        request.LinkObligation(obligation.Id);

        var assignmentId = Guid.Empty;
        if (assigned)
        {
            var evaluationId = Guid.CreateVersion7();
            context.EligibilityEvaluations.Add(new EligibilityEvaluation(
                evaluationId,
                Guid.CreateVersion7(),
                obligation.Id,
                Now.AddMinutes(-4),
                DateOnly.FromDateTime(Now.UtcDateTime),
                EligibilityDateSources.ManualRequest,
                configuration.PolicyId,
                JsonDocument.Parse("{}"),
                EligibilityResults.EligibleCandidates));
            assignmentId = Guid.CreateVersion7();
            context.AssignmentVersions.Add(new AssignmentVersion(
                assignmentId,
                obligation.Id,
                responsible.PersonId,
                AssignmentVersionStatuses.Current,
                AssignmentTypes.Automatic,
                JsonSerializer.SerializeToDocument(new
                {
                    schemaVersion = 1,
                    eligibilityEvaluationId = evaluationId,
                }, JsonSerializerOptions.Web),
                Now.AddMinutes(-4)));
        }

        await context.SaveChangesAsync();
        return new ObligationState(
            obligation.Id,
            assignmentId,
            configuration.TaskVersionId,
            configuration.PolicyId,
            configuration.RuleId,
            configuration.Role);
    }

    private static PublishWorkPlanCommand Command(Seed seed, Guid key, long rowVersion) => new(
        seed.ActorUserId, key, Guid.CreateVersion7(), seed.PlanId, rowVersion);

    private static EfPlanPublicationService CreateService(SgolDbContext context) => new(
        context,
        new AuditTransaction(context),
        new FixedClock(Now),
        new Uuid7Generator(new FixedClock(Now)));

    private static EfEligibilityEvaluationService CreateEligibilityService(SgolDbContext context) => new(
        context,
        new AuditTransaction(context),
        new FixedClock(Now),
        new Uuid7Generator(new FixedClock(Now)));

    private static EfAutomaticAssignmentService CreateAssignmentService(SgolDbContext context) => new(
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

    private sealed record PersonState(Guid PersonId, Guid UserId);
    private sealed record ConfigurationState(Guid TaskVersionId, Guid PolicyId, Guid RuleId, string Role);
    private sealed record ObligationState(
        Guid ObligationId,
        Guid AssignmentId,
        Guid TaskVersionId,
        Guid PolicyId,
        Guid RuleId,
        string Role);
    private sealed record Seed(
        Guid ActorUserId,
        Guid PlanId,
        Guid PeriodId,
        IReadOnlyDictionary<string, PersonState> People,
        Dictionary<Guid, ObligationState> Obligations);

    private sealed class FixedClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; } = utcNow;
    }
}
