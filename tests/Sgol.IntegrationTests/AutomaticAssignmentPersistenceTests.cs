using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Sgol.Assignment.Contracts;
using Sgol.BuildingBlocks.Identifiers;
using Sgol.BuildingBlocks.Time;
using Sgol.BuildingBlocks.Versioning;
using Sgol.Configuration.Contracts;
using Sgol.Generation.Contracts;
using Sgol.Identity.Contracts;
using Sgol.Notifications.Contracts;
using Sgol.Organization.Contracts;
using Sgol.Planning.Contracts;
using Sgol.Web.Infrastructure.Persistence;
using Sgol.Web.Infrastructure.Persistence.Assignment;
using Sgol.Web.Infrastructure.Persistence.Auditing;
using Sgol.Web.Infrastructure.Persistence.Bootstrap;
using Sgol.Web.Infrastructure.Persistence.Notifications;
using Testcontainers.PostgreSql;
using Xunit;

namespace Sgol.IntegrationTests;

public sealed class AutomaticAssignmentPersistenceTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 4, 18, 0, 0, TimeSpan.Zero);
    private static readonly string[] ExplanationProperties =
    [
        "schemaVersion",
        "eligibilityEvaluationId",
        "calculatedAt",
        "orderingRules",
        "candidates",
        "winner",
        "decisiveRule",
    ];
    private readonly PostgreSqlContainer _postgres = PostgreSqlPersistenceTests.CreateContainerForTests();

    public Task InitializeAsync() => _postgres.StartAsync();

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    [Fact]
    public async Task AssignmentUsesDerivedLoadAndPersistsExactExplanationAuditAndNoOtherEffects()
    {
        var seed = await ResetAsync();
        await using var context = CreateContext();
        var lowerLoad = AddPerson(context, "ASSIGN-020", "Auxiliar");
        var higherLoad = AddPerson(context, "ASSIGN-010", "Auxiliar");
        var excludedSimilarPosition = AddPerson(context, "ASSIGN-001", "Subcoordinador");
        await context.SaveChangesAsync();

        var active = await AddObligationAsync(context, seed, "active-load");
        AddAutomatic(context, active, higherLoad.Id, AssignmentVersionStatuses.Current, Now.AddDays(-5));
        var concluded = await AddObligationAsync(context, seed, "concluded-load");
        AddAutomatic(context, concluded, lowerLoad.Id, AssignmentVersionStatuses.Current, Now.AddDays(-3));
        await ObligationConclusionTestData.ConcludeAsync(context, concluded, Now.AddMinutes(-1));
        var corrected = await AddObligationAsync(context, seed, "corrected-load");
        var original = AddAutomatic(
            context,
            corrected,
            lowerLoad.Id,
            AssignmentVersionStatuses.Superseded,
            Now.AddDays(-2));
        AddCorrection(context, corrected, lowerLoad.Id, Now.AddDays(-1), seed.SystemUserId, original);
        await ObligationConclusionTestData.ConcludeAsync(context, corrected, Now.AddMinutes(-1));

        var target = await AddObligationAsync(context, seed, "target-load");
        var evaluation = AddEvaluation(
            context,
            target,
            seed.PolicyId,
            EligibilityResults.EligibleCandidates,
            (lowerLoad, true),
            (higherLoad, true),
            (excludedSimilarPosition, false));
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        var before = await CountsAsync(context);
        var command = new AssignObligationCommand(
            Guid.CreateVersion7(), target, evaluation.Id, Guid.CreateVersion7());

        var result = await Service(context).AssignAsync(command);

        Assert.Equal(AutomaticAssignmentResults.Created, result.Result);
        Assert.Equal(lowerLoad.Id, result.WinnerPersonId);
        var assignment = await context.AssignmentVersions.AsNoTracking()
            .SingleAsync(item => item.Id == result.AssignmentId);
        Assert.Equal(AssignmentVersionStatuses.Current, assignment.Status);
        Assert.Equal(AssignmentTypes.Automatic, assignment.AssignmentType);
        Assert.Null(assignment.AssignedBy);
        Assert.Null(assignment.Reason);
        Assert.Null(assignment.SupersedesId);
        Assert.Equal(Now, assignment.AssignedAt);
        var notice = await context.InternalNotices.AsNoTracking().SingleAsync(item => item.ResourceId == assignment.Id);
        Assert.Equal(InternalNoticeTypes.ObligationAssigned, notice.NoticeType);
        Assert.Equal(InternalNoticeResourceTypes.AssignmentVersion, notice.ResourceType);
        Assert.Equal(Now, notice.CreatedAt);
        Assert.Null(notice.ReadAt);
        Assert.Equal(lowerLoad.Id, await context.AppUsers.AsNoTracking()
            .Where(user => user.Id == notice.RecipientUserId).Select(user => user.PersonId).SingleAsync());
        Assert.Equal(WorkObligationStatuses.Pending,
            (await context.WorkObligations.AsNoTracking().SingleAsync(item => item.Id == target)).ExecutionStatus);

        var explanation = assignment.Explanation.RootElement;
        Assert.Equal(
            ExplanationProperties.Order(StringComparer.Ordinal),
            explanation.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
        Assert.Equal(1, explanation.GetProperty("schemaVersion").GetInt32());
        Assert.Equal(evaluation.Id, explanation.GetProperty("eligibilityEvaluationId").GetGuid());
        Assert.Equal(Now, explanation.GetProperty("calculatedAt").GetDateTimeOffset());
        Assert.Equal(2, explanation.GetProperty("candidates").GetArrayLength());
        var winner = explanation.GetProperty("winner");
        Assert.Equal(lowerLoad.Id, winner.GetProperty("personId").GetGuid());
        Assert.Equal(AutomaticAssignmentDecisiveRules.ActiveLoad,
            explanation.GetProperty("decisiveRule").GetString());
        Assert.DoesNotContain(
            explanation.GetProperty("candidates").EnumerateArray(),
            item => item.GetProperty("personId").GetGuid() == excludedSimilarPosition.Id);

        Assert.All(await context.EligibilityCandidates.AsNoTracking()
            .Where(item => item.EvaluationId == evaluation.Id).ToListAsync(), item =>
        {
            Assert.Null(item.ActiveLoad);
            Assert.Null(item.LastAutoAssignmentAt);
            Assert.Null(item.Rank);
        });
        Assert.Null((await context.EligibilityEvaluations.AsNoTracking()
            .SingleAsync(item => item.Id == evaluation.Id)).WinnerPersonId);
        var audit = await context.AuditEvents.AsNoTracking()
            .SingleAsync(item => item.RequestId == command.AssignmentRequestId.ToString("D"));
        Assert.Equal("SYSTEM", audit.ActorType);
        Assert.Null(audit.ActorUserId);
        Assert.Equal("AUTOMATIC_ASSIGNMENT_CREATED", audit.Action);
        Assert.Equal(AutomaticAssignmentResults.Created, audit.Outcome);

        var after = await CountsAsync(context);
        Assert.Equal(before with
        {
            Assignments = before.Assignments + 1,
            Idempotency = before.Idempotency + 1,
            Audits = before.Audits + 1,
        }, after);
        Assert.Contains(context.Model.GetEntityTypes(), entity => entity.ClrType.Name == "WorkPlan");
        Assert.Contains(context.Model.GetEntityTypes(), entity => entity.ClrType.Name == "PlanVersion");
        Assert.Contains(context.Model.GetEntityTypes(), entity => entity.ClrType.Name == "PlanVersionObligation");
        Assert.Empty(await context.WorkPlans.AsNoTracking().ToListAsync());
        Assert.Empty(await context.PlanVersions.AsNoTracking().ToListAsync());
        Assert.Empty(await context.PlanVersionObligations.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task AssignmentTieUsesOldestHistoricalAutomaticAndIgnoresNewerCorrection()
    {
        var seed = await ResetAsync();
        await using var context = CreateContext();
        var oldest = AddPerson(context, "ASSIGN-020");
        var newer = AddPerson(context, "ASSIGN-010");
        await context.SaveChangesAsync();

        var oldHistory = await AddObligationAsync(context, seed, "old-history");
        var oldAutomatic = AddAutomatic(
            context,
            oldHistory,
            oldest.Id,
            AssignmentVersionStatuses.Superseded,
            Now.AddDays(-10));
        AddCorrection(context, oldHistory, oldest.Id, Now.AddHours(-1), seed.SystemUserId, oldAutomatic);
        await ObligationConclusionTestData.ConcludeAsync(context, oldHistory, Now.AddMinutes(-1));
        var newHistory = await AddObligationAsync(context, seed, "new-history");
        AddAutomatic(context, newHistory, newer.Id, AssignmentVersionStatuses.Current, Now.AddDays(-5));
        await ObligationConclusionTestData.ConcludeAsync(context, newHistory, Now.AddMinutes(-1));
        var target = await AddObligationAsync(context, seed, "target-wait");
        var evaluation = AddEvaluation(
            context,
            target,
            seed.PolicyId,
            EligibilityResults.EligibleCandidates,
            (oldest, true),
            (newer, true));
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var result = await Service(context).AssignAsync(new(
            Guid.CreateVersion7(), target, evaluation.Id, Guid.CreateVersion7()));

        Assert.Equal(oldest.Id, result.WinnerPersonId);
        var explanation = (await context.AssignmentVersions.AsNoTracking()
            .SingleAsync(item => item.Id == result.AssignmentId)).Explanation.RootElement;
        Assert.Equal(AutomaticAssignmentDecisiveRules.LastAutoAssignmentAt,
            explanation.GetProperty("decisiveRule").GetString());
        var oldestRank = explanation.GetProperty("candidates").EnumerateArray().First();
        Assert.Equal(Now.AddDays(-10), oldestRank.GetProperty("lastAutoAssignmentAt").GetDateTimeOffset());
    }

    [Fact]
    public async Task NeverAssignedAndThenStableCodeResolveRemainingTies()
    {
        var seed = await ResetAsync();
        await using var context = CreateContext();
        var never = AddPerson(context, "ASSIGN-900");
        var assigned = AddPerson(context, "ASSIGN-001");
        await context.SaveChangesAsync();
        var history = await AddObligationAsync(context, seed, "history");
        AddAutomatic(context, history, assigned.Id, AssignmentVersionStatuses.Current, Now.AddDays(-30));
        await ObligationConclusionTestData.ConcludeAsync(context, history, Now.AddMinutes(-1));
        var target = await AddObligationAsync(context, seed, "target-never");
        var evaluation = AddEvaluation(
            context,
            target,
            seed.PolicyId,
            EligibilityResults.EligibleCandidates,
            (never, true),
            (assigned, true));
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var neverResult = await Service(context).AssignAsync(new(
            Guid.CreateVersion7(), target, evaluation.Id, Guid.CreateVersion7()));

        Assert.Equal(never.Id, neverResult.WinnerPersonId);

        await ResetAsync();
        await using var tieContext = CreateContext();
        var tieSeed = await LoadSeedAsync(tieContext);
        var firstCode = AddPerson(tieContext, "ASSIGN-002");
        var secondCode = AddPerson(tieContext, "ASSIGN-010");
        await tieContext.SaveChangesAsync();
        var tieTarget = await AddObligationAsync(tieContext, tieSeed, "target-code");
        var tieEvaluation = AddEvaluation(
            tieContext,
            tieTarget,
            tieSeed.PolicyId,
            EligibilityResults.EligibleCandidates,
            (secondCode, true),
            (firstCode, true));
        await tieContext.SaveChangesAsync();
        tieContext.ChangeTracker.Clear();

        var tieResult = await Service(tieContext).AssignAsync(new(
            Guid.CreateVersion7(), tieTarget, tieEvaluation.Id, Guid.CreateVersion7()));

        Assert.Equal(firstCode.Id, tieResult.WinnerPersonId);
    }

    [Fact]
    public async Task RetriesRecoverAndConflictingIdentityOrEvaluationHasNoAssignmentEffect()
    {
        var seed = await ResetAsync();
        await using var context = CreateContext();
        var only = AddPerson(context, "ASSIGN-001");
        await context.SaveChangesAsync();
        var target = await AddObligationAsync(context, seed, "target-retry");
        var evaluation = AddEvaluation(
            context,
            target,
            seed.PolicyId,
            EligibilityResults.EligibleCandidates,
            (only, true));
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        var requestId = Guid.CreateVersion7();
        var command = new AssignObligationCommand(requestId, target, evaluation.Id, Guid.CreateVersion7());
        var service = Service(context);

        var created = await service.AssignAsync(command);
        var replayed = await service.AssignAsync(command with { CorrelationId = Guid.CreateVersion7() });
        var anotherRequest = await service.AssignAsync(command with
        {
            AssignmentRequestId = Guid.CreateVersion7(),
            CorrelationId = Guid.CreateVersion7(),
        });

        Assert.Equal(AutomaticAssignmentResults.Created, created.Result);
        Assert.Equal(AutomaticAssignmentResults.Recovered, replayed.Result);
        Assert.Equal(AutomaticAssignmentResults.Recovered, anotherRequest.Result);
        Assert.Equal(created.AssignmentId, replayed.AssignmentId);
        Assert.Equal(created.AssignmentId, anotherRequest.AssignmentId);
        Assert.Equal(1, await context.AssignmentVersions.CountAsync(item =>
            item.ObligationId == target && item.Status == AssignmentVersionStatuses.Current));
        Assert.Equal(1, await context.InternalNotices.CountAsync(item => item.ResourceId == created.AssignmentId));

        var otherTarget = await AddObligationAsync(context, seed, "target-conflict");
        var otherEvaluation = AddEvaluation(
            context,
            otherTarget,
            seed.PolicyId,
            EligibilityResults.EligibleCandidates,
            (only, true));
        await context.SaveChangesAsync();
        var independentResource = await service.AssignAsync(
            command with
            {
                ObligationId = otherTarget,
                EligibilityEvaluationId = otherEvaluation.Id,
                CorrelationId = Guid.CreateVersion7(),
            });
        Assert.Equal(AutomaticAssignmentResults.Created, independentResource.Result);
        Assert.NotEqual(created.AssignmentId, independentResource.AssignmentId);
        Assert.Equal(1, await context.AssignmentVersions.CountAsync(item =>
            item.ObligationId == otherTarget && item.Status == AssignmentVersionStatuses.Current));

        var newerEvaluation = AddEvaluation(
            context,
            target,
            seed.PolicyId,
            EligibilityResults.EligibleCandidates,
            (only, true),
            evaluatedAt: Now.AddMinutes(1));
        await context.SaveChangesAsync();
        await Assert.ThrowsAsync<AutomaticAssignmentAlreadyExistsException>(() => service.AssignAsync(new(
            Guid.CreateVersion7(), target, newerEvaluation.Id, Guid.CreateVersion7())));
    }

    [Fact]
    public async Task NoCandidateAndInvalidSnapshotsAreAuditedWithoutAssignment()
    {
        var seed = await ResetAsync();
        await using var context = CreateContext();
        var excluded = AddPerson(context, "ASSIGN-001", "Puesto parecido");
        await context.SaveChangesAsync();
        var target = await AddObligationAsync(context, seed, "target-none");
        var evaluation = AddEvaluation(
            context,
            target,
            seed.PolicyId,
            EligibilityResults.NoEligibleCandidate,
            (excluded, false));
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        var requestId = Guid.CreateVersion7();
        var command = new AssignObligationCommand(requestId, target, evaluation.Id, Guid.CreateVersion7());
        var service = Service(context);

        var first = await service.AssignAsync(command);
        var replay = await service.AssignAsync(command with { CorrelationId = Guid.CreateVersion7() });

        Assert.Equal(AutomaticAssignmentResults.NoEligibleCandidate, first.Result);
        Assert.Equal(AutomaticAssignmentResults.NoEligibleCandidate, replay.Result);
        Assert.Null(first.AssignmentId);
        Assert.Empty(await context.AssignmentVersions.AsNoTracking()
            .Where(item => item.ObligationId == target).ToListAsync());
        Assert.Equal(1, await context.IdempotencyRecords.CountAsync(item => item.Key == requestId));
        Assert.Equal(2, await context.AuditEvents.CountAsync(item =>
            item.RequestId == requestId.ToString("D") &&
            item.Action == "AUTOMATIC_ASSIGNMENT_NOT_CREATED"));

        var otherTarget = await AddObligationAsync(context, seed, "other-target");
        await context.SaveChangesAsync();
        await Assert.ThrowsAsync<AutomaticAssignmentEvaluationMismatchException>(() => service.AssignAsync(new(
            Guid.CreateVersion7(), otherTarget, evaluation.Id, Guid.CreateVersion7())));

        var oldEvaluation = AddEvaluation(
            context,
            otherTarget,
            seed.PolicyId,
            EligibilityResults.NoEligibleCandidate,
            (excluded, false),
            evaluatedAt: Now.AddMinutes(-2));
        var latestEvaluation = AddEvaluation(
            context,
            otherTarget,
            seed.PolicyId,
            EligibilityResults.NoEligibleCandidate,
            (excluded, false),
            evaluatedAt: Now.AddMinutes(-1));
        await context.SaveChangesAsync();
        await Assert.ThrowsAsync<AutomaticAssignmentEvaluationStaleException>(() => service.AssignAsync(new(
            Guid.CreateVersion7(), otherTarget, oldEvaluation.Id, Guid.CreateVersion7())));
        Assert.NotEqual(oldEvaluation.Id, latestEvaluation.Id);
        Assert.Empty(await context.AssignmentVersions.AsNoTracking()
            .Where(item => item.ObligationId == otherTarget).ToListAsync());

        await Assert.ThrowsAsync<AutomaticAssignmentEvaluationNotFoundException>(() => service.AssignAsync(new(
            Guid.CreateVersion7(), otherTarget, Guid.CreateVersion7(), Guid.CreateVersion7())));

        var incompatibleTarget = await AddObligationAsync(context, seed, "incompatible-target");
        var incompatibleEvaluation = AddEvaluation(
            context,
            incompatibleTarget,
            seed.PolicyId,
            EligibilityResults.EligibleCandidates,
            (excluded, false));
        await context.SaveChangesAsync();
        await Assert.ThrowsAsync<AutomaticAssignmentEvaluationIncompatibleException>(() => service.AssignAsync(new(
            Guid.CreateVersion7(), incompatibleTarget, incompatibleEvaluation.Id, Guid.CreateVersion7())));

        var concludedTarget = await AddObligationAsync(context, seed, "concluded-target");
        var concludedEvaluation = AddEvaluation(
            context,
            concludedTarget,
            seed.PolicyId,
            EligibilityResults.EligibleCandidates,
            (excluded, true));
        await context.SaveChangesAsync();
        await ObligationConclusionTestData.ConcludeAsync(
            context, concludedTarget, Now.AddMinutes(-1), excluded.Id);
        await Assert.ThrowsAsync<AutomaticAssignmentObligationNotAssignableException>(() => service.AssignAsync(new(
            Guid.CreateVersion7(), concludedTarget, concludedEvaluation.Id, Guid.CreateVersion7())));
        Assert.Empty(await context.AssignmentVersions.AsNoTracking()
            .Where(item => item.ObligationId == incompatibleTarget)
            .ToListAsync());
        Assert.Single(await context.AssignmentVersions.AsNoTracking()
            .Where(item => item.ObligationId == concludedTarget &&
                item.Status == AssignmentVersionStatuses.Current)
            .ToListAsync());
    }

    [Fact]
    public async Task AuditFailureRollsBackAssignmentAndIdempotencyTogether()
    {
        var seed = await ResetAsync();
        await using var context = CreateContext();
        var only = AddPerson(context, "ASSIGN-001");
        await context.SaveChangesAsync();
        var target = await AddObligationAsync(context, seed, "target-rollback");
        var evaluation = AddEvaluation(
            context,
            target,
            seed.PolicyId,
            EligibilityResults.EligibleCandidates,
            (only, true));
        var duplicateAuditId = Guid.CreateVersion7();
        context.AuditEvents.Add(new AuditEvent
        {
            Id = duplicateAuditId,
            OccurredAt = Now.AddMinutes(-1),
            ActorType = "SYSTEM",
            Action = "SYNTHETIC_EXISTING_EVENT",
            ResourceType = "WORK_OBLIGATION",
            ResourceId = target,
            BranchId = BranchScope.LorettaId,
            CorrelationId = Guid.CreateVersion7(),
            Outcome = "SUCCESS",
        });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        var assignmentId = Guid.CreateVersion7();
        var requestId = Guid.CreateVersion7();
        var service = Service(context, new SequenceUuidGenerator(assignmentId, duplicateAuditId));

        await Assert.ThrowsAsync<DbUpdateException>(() => service.AssignAsync(new(
            requestId, target, evaluation.Id, Guid.CreateVersion7())));

        Assert.False(await context.AssignmentVersions.AsNoTracking().AnyAsync(item => item.Id == assignmentId));
        Assert.False(await context.IdempotencyRecords.AsNoTracking().AnyAsync(item => item.Key == requestId));
        Assert.Equal(1, await context.AuditEvents.CountAsync(item => item.Id == duplicateAuditId));
    }

    [Fact]
    public async Task ConcurrentRequestsCreateOneCurrentAssignmentAndConcurrentObligationsRereadLoad()
    {
        var seed = await ResetAsync();
        await using (var arrange = CreateContext())
        {
            var first = AddPerson(arrange, "ASSIGN-001");
            var second = AddPerson(arrange, "ASSIGN-002");
            await arrange.SaveChangesAsync();
            var sameTarget = await AddObligationAsync(arrange, seed, "same-target");
            var sameEvaluation = AddEvaluation(
                arrange,
                sameTarget,
                seed.PolicyId,
                EligibilityResults.EligibleCandidates,
                (first, true),
                (second, true));
            var targetA = await AddObligationAsync(arrange, seed, "target-a");
            var evaluationA = AddEvaluation(
                arrange,
                targetA,
                seed.PolicyId,
                EligibilityResults.EligibleCandidates,
                (first, true),
                (second, true));
            var targetB = await AddObligationAsync(arrange, seed, "target-b");
            var evaluationB = AddEvaluation(
                arrange,
                targetB,
                seed.PolicyId,
                EligibilityResults.EligibleCandidates,
                (first, true),
                (second, true));
            await arrange.SaveChangesAsync();

            var sameCommands = new[]
            {
                new AssignObligationCommand(Guid.CreateVersion7(), sameTarget, sameEvaluation.Id, Guid.CreateVersion7()),
                new AssignObligationCommand(Guid.CreateVersion7(), sameTarget, sameEvaluation.Id, Guid.CreateVersion7()),
            };
            var sameResults = await Task.WhenAll(sameCommands.Select(ExecuteInNewContextAsync));
            Assert.Single(sameResults.Select(item => item.AssignmentId).Distinct());

            var loadResults = await Task.WhenAll(
                ExecuteInNewContextAsync(new(
                    Guid.CreateVersion7(), targetA, evaluationA.Id, Guid.CreateVersion7())),
                ExecuteInNewContextAsync(new(
                    Guid.CreateVersion7(), targetB, evaluationB.Id, Guid.CreateVersion7())));
            Assert.Equal(2, loadResults.Select(item => item.WinnerPersonId).Distinct().Count());
        }

        await using var verify = CreateContext();
        Assert.All(
            await verify.WorkObligations.AsNoTracking().ToListAsync(),
            obligation => Assert.Equal(WorkObligationStatuses.Pending, obligation.ExecutionStatus));
        Assert.All(
            await verify.AssignmentVersions.AsNoTracking().ToListAsync(),
            assignment => Assert.Equal(AssignmentVersionStatuses.Current, assignment.Status));
    }

    private async Task<AutomaticAssignmentResult> ExecuteInNewContextAsync(AssignObligationCommand command)
    {
        await using var context = CreateContext();
        return await Service(context).AssignAsync(command);
    }

    private async Task<Seed> ResetAsync()
    {
        await using var context = CreateContext();
        await context.Database.EnsureDeletedAsync();
        await context.Database.MigrateAsync();

        var systemPerson = AddPerson(context, "ASSIGN-SYSTEM");
        var systemUserId = context.AppUsers.Local.Single(user => user.PersonId == systemPerson.Id).Id;
        await context.SaveChangesAsync();

        var releaseId = Guid.CreateVersion7();
        var release = new ConfigurationRelease(releaseId, BranchScope.LorettaId);
        release.ApplyPublished(Published(releaseId), 1, systemUserId, Now.AddDays(-30));
        var task = TaskDefinitionCatalog.Require("TAR-0008");
        var taskVersionId = Guid.CreateVersion7();
        using var payload = JsonDocument.Parse("{}");
        var taskVersion = new TaskDefinitionVersion(taskVersionId, task.Id, 1, 1, payload, releaseId);
        taskVersion.ApplyPublished(Published(taskVersionId), activeForNew: true);
        var policyId = Guid.CreateVersion7();
        var policy = new EligibilityPolicyVersion(
            policyId,
            task.Id,
            taskVersionId,
            releaseId,
            null,
            1,
            CanonicalRole.Subcoordination,
            true,
            null);
        policy.ApplyPublished(Published(policyId));
        var ruleId = Guid.CreateVersion7();
        using var schedule = JsonDocument.Parse("null");
        var rule = new ActivationRuleVersion(
            ruleId,
            task.Id,
            taskVersionId,
            releaseId,
            null,
            1,
            ActivationModes.Manual,
            schedule.RootElement,
            ActivationOriginSchemas.ManualReference);
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
        context.EligibilityPolicyVersions.Add(policy);
        context.ActivationRuleVersions.Add(rule);
        context.WeekPeriods.Add(period);
        await context.SaveChangesAsync();
        return new Seed(systemUserId, taskVersionId, policyId, ruleId, period.Id);
    }

    private static async Task<Seed> LoadSeedAsync(SgolDbContext context)
    {
        var policy = await context.EligibilityPolicyVersions.AsNoTracking().SingleAsync();
        var rule = await context.ActivationRuleVersions.AsNoTracking().SingleAsync();
        var period = await context.WeekPeriods.AsNoTracking().SingleAsync();
        var systemUser = await context.AppUsers.AsNoTracking().SingleAsync();
        return new Seed(systemUser.Id, policy.TaskDefinitionVersionId, policy.Id, rule.Id, period.Id);
    }

    private static Person AddPerson(
        SgolDbContext context,
        string stableCode,
        string? positionText = null)
    {
        var person = new Person
        {
            Id = Guid.CreateVersion7(),
            StableCode = stableCode,
            DisplayName = $"Persona {stableCode}",
            CreatedAt = Now.AddDays(-30),
        };
        context.People.Add(person);
        context.EmploymentVersions.Add(new EmploymentVersion(
            Guid.CreateVersion7(),
            person.Id,
            BranchScope.LorettaId,
            EmploymentStatus.Active,
            Now.AddDays(-30),
            positionText: positionText));
        context.AppUsers.Add(new AppUser
        {
            Id = Guid.CreateVersion7(),
            PersonId = person.Id,
            Status = AccountStatus.Active,
            MustChangePassword = false,
            MfaEnrolledAt = Now.AddDays(-30),
            SecurityStamp = $"automatic-assignment-{person.Id:N}",
        });
        return person;
    }

    private static async Task<Guid> AddObligationAsync(
        SgolDbContext context,
        Seed seed,
        string origin)
    {
        var requestId = Guid.CreateVersion7();
        var request = new GenerationRequest(
            requestId,
            Guid.CreateVersion7(),
            Hash(origin),
            seed.RuleId,
            BranchScope.LorettaId,
            seed.PeriodId,
            ActivationOriginSchemas.ManualReference,
            origin,
            seed.SystemUserId,
            Now.AddDays(-1));
        context.GenerationRequests.Add(request);
        await context.SaveChangesAsync();
        var evidencePolicyId = await ObligationConclusionTestData.EnsurePolicyAsync(
            context, seed.TaskVersionId, Now.AddDays(-1));
        var obligation = new WorkObligation(
            Guid.CreateVersion7(),
            seed.TaskVersionId,
            BranchScope.LorettaId,
            seed.PeriodId,
            request.Id,
            origin,
            evidencePolicyId);
        context.WorkObligations.Add(obligation);
        await context.SaveChangesAsync();
        request.LinkObligation(obligation.Id);
        await context.SaveChangesAsync();
        return obligation.Id;
    }

    private static EligibilityEvaluation AddEvaluation(
        SgolDbContext context,
        Guid obligationId,
        Guid policyId,
        string result,
        (Person Person, bool Eligible) first,
        (Person Person, bool Eligible)? second = null,
        (Person Person, bool Eligible)? third = null,
        DateTimeOffset? evaluatedAt = null)
    {
        var evaluation = new EligibilityEvaluation(
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            obligationId,
            evaluatedAt ?? Now.AddMinutes(-1),
            new DateOnly(2026, 9, 4),
            EligibilityDateSources.ManualRequest,
            policyId,
            JsonDocument.Parse("{}"),
            result);
        context.EligibilityEvaluations.Add(evaluation);
        foreach (var candidate in new[] { first, second, third }.Where(item => item is not null))
        {
            var value = candidate!.Value;
            context.EligibilityCandidates.Add(new EligibilityCandidate(
                evaluation.Id,
                value.Person.Id,
                value.Person.StableCode,
                value.Eligible,
                JsonDocument.Parse(value.Eligible ? "[]" : "[\"ROL_REQUERIDO_NO_COINCIDE\"]")));
        }

        return evaluation;
    }

    private static Guid AddAutomatic(
        SgolDbContext context,
        Guid obligationId,
        Guid personId,
        string status,
        DateTimeOffset assignedAt)
    {
        var id = Guid.CreateVersion7();
        InternalNoticeTestData.AddAssignmentWithNotice(context, new AssignmentVersion(
            id,
            obligationId,
            personId,
            status,
            AssignmentTypes.Automatic,
            JsonDocument.Parse("{}"),
            assignedAt));
        return id;
    }

    private static void AddCorrection(
        SgolDbContext context,
        Guid obligationId,
        Guid personId,
        DateTimeOffset assignedAt,
        Guid assignedBy,
        Guid supersedesId) =>
        InternalNoticeTestData.AddAssignmentWithNotice(context, new AssignmentVersion(
            Guid.CreateVersion7(),
            obligationId,
            personId,
            AssignmentVersionStatuses.Current,
            AssignmentTypes.Correction,
            JsonDocument.Parse("{}"),
            assignedAt,
            "Corrección sintética",
            assignedBy,
            supersedesId));

    private static async Task<Counts> CountsAsync(SgolDbContext context) => new(
        await context.WorkObligations.CountAsync(),
        await context.AssignmentVersions.CountAsync(),
        await context.EligibilityEvaluations.CountAsync(),
        await context.EligibilityCandidates.CountAsync(),
        await context.People.CountAsync(),
        await context.EmploymentVersions.CountAsync(),
        await context.AppUsers.CountAsync(),
        await context.RoleAssignmentVersions.CountAsync(),
        await context.AvailabilityDayVersions.CountAsync(),
        await context.IdempotencyRecords.CountAsync(),
        await context.AuditEvents.CountAsync());

    private static EfAutomaticAssignmentService Service(
        SgolDbContext context,
        IUuidGenerator? uuidGenerator = null) =>
        new(
            context,
            new AuditTransaction(context),
            new FixedClock(Now),
            uuidGenerator ?? new TestUuidGenerator(),
            new EfInternalNoticeWriter(context, new TestUuidGenerator()));

    private SgolDbContext CreateContext() => new(
        new DbContextOptionsBuilder<SgolDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options);

    private static VersionRecord Published(Guid id) => new(
        id,
        VersionStatuses.Current,
        Now.AddDays(-30),
        null,
        "Configuración sintética",
        null,
        2);

    private static string Hash(string value) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private sealed record Seed(
        Guid SystemUserId,
        Guid TaskVersionId,
        Guid PolicyId,
        Guid RuleId,
        Guid PeriodId);

    private sealed record Counts(
        int Obligations,
        int Assignments,
        int Evaluations,
        int Candidates,
        int People,
        int Employments,
        int AppUsers,
        int Roles,
        int Availability,
        int Idempotency,
        int Audits);

    private sealed class FixedClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; } = utcNow;
    }

    private sealed class TestUuidGenerator : IUuidGenerator
    {
        public Guid NewUuid() => Guid.CreateVersion7();
    }

    private sealed class SequenceUuidGenerator(params Guid[] values) : IUuidGenerator
    {
        private readonly Queue<Guid> _values = new(values);

        public Guid NewUuid() => _values.Dequeue();
    }
}
