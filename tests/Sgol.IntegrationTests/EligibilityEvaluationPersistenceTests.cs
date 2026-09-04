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
using Testcontainers.PostgreSql;
using Xunit;

namespace Sgol.IntegrationTests;

public sealed class EligibilityEvaluationPersistenceTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 9, 3, 21, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly EligibilityDate = new(2026, 9, 3);
    private readonly PostgreSqlContainer _postgres = PostgreSqlPersistenceTests.CreateContainerForTests();

    public Task InitializeAsync() => _postgres.StartAsync();

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    [Fact]
    public async Task EvaluationPersistsExactPolicyCompleteReasonsSnapshotAndAuditWithoutSideEffects()
    {
        var scenario = await ResetAndSeedAsync();
        await using var context = CreateContext();
        var beforeObligation = await context.WorkObligations.AsNoTracking().SingleAsync();
        var beforePeople = await context.People.AsNoTracking().OrderBy(item => item.Id).ToListAsync();
        var beforeEmployments = await context.EmploymentVersions.AsNoTracking().OrderBy(item => item.Id).ToListAsync();
        var beforeRoles = await context.RoleAssignmentVersions.AsNoTracking().OrderBy(item => item.Id).ToListAsync();
        var beforeAvailability = await context.AvailabilityDayVersions.AsNoTracking().OrderBy(item => item.Id).ToListAsync();

        var result = await CreateService(context).EvaluateAsync(new(
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            scenario.ObligationId,
            EligibilityDate,
            EligibilityDateSources.ManualRequest));

        Assert.Equal(EligibilityResults.EligibleCandidates, result.Result);
        Assert.Equal(scenario.PolicyVersionId, result.PolicyVersionId);
        Assert.Equal(scenario.EligiblePersonId, Assert.Single(result.Candidates, item => item.IsEligible).PersonId);
        Assert.Contains(result.Candidates, item =>
            item.PersonId == scenario.MissingAvailabilityPersonId &&
            item.Reasons.Contains(EligibilityExclusionReasons.AvailabilityMissing));
        Assert.Contains(result.Candidates, item =>
            item.PersonId == scenario.WrongRolePersonId &&
            item.Reasons.Contains(EligibilityExclusionReasons.RequiredRoleMismatch) &&
            item.Reasons.Contains(EligibilityExclusionReasons.AvailabilityNotPositive));
        Assert.Contains(result.Candidates, item =>
            item.PersonId == scenario.InactivePersonId &&
            item.Reasons.Contains(EligibilityExclusionReasons.InactivePerson));
        Assert.All(result.Candidates, item =>
        {
            Assert.Null(item.ActiveLoad);
            Assert.Null(item.LastAutoAssignmentAt);
            Assert.Null(item.Rank);
        });
        Assert.Null(result.WinnerPersonId);
        Assert.Equal(result.Candidates.Count, await context.EligibilityCandidates.CountAsync());
        var snapshot = await context.EligibilityEvaluations.AsNoTracking().SingleAsync();
        Assert.Equal(scenario.ObligationId, snapshot.InputSnapshot.RootElement.GetProperty("obligationId").GetGuid());
        Assert.Equal(
            scenario.TaskDefinitionVersionId,
            snapshot.InputSnapshot.RootElement.GetProperty("obligationTaskDefinitionVersionId").GetGuid());
        Assert.Contains(await context.AuditEvents.AsNoTracking().ToListAsync(), item =>
            item.Action == "ELIGIBILITY_EVALUATED" && item.ResourceId == result.EvaluationId);
        Assert.Equal(beforeObligation.ExecutionStatus, (await context.WorkObligations.AsNoTracking().SingleAsync()).ExecutionStatus);
        Assert.Equal(beforeObligation.RowVersion, (await context.WorkObligations.AsNoTracking().SingleAsync()).RowVersion);
        Assert.Equal(beforePeople.Count, await context.People.CountAsync());
        Assert.Equal(beforeEmployments.Count, await context.EmploymentVersions.CountAsync());
        Assert.Equal(beforeRoles.Count, await context.RoleAssignmentVersions.CountAsync());
        Assert.Equal(beforeAvailability.Count, await context.AvailabilityDayVersions.CountAsync());
        Assert.Empty(await context.AssignmentVersions.AsNoTracking().ToListAsync());
        Assert.Contains(context.Model.GetEntityTypes(), item => item.ClrType.Name == "WorkPlan");
        Assert.Contains(context.Model.GetEntityTypes(), item => item.ClrType.Name == "PlanVersion");
        Assert.Contains(context.Model.GetEntityTypes(), item => item.ClrType.Name == "PlanVersionObligation");
        Assert.Empty(await context.WorkPlans.AsNoTracking().ToListAsync());
        Assert.Empty(await context.PlanVersions.AsNoTracking().ToListAsync());
        Assert.Empty(await context.PlanVersionObligations.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task ZeroEligibleReplayAndIntentionalReevaluationPreserveImmutableSnapshots()
    {
        var scenario = await ResetAndSeedAsync(includeEligible: false);
        await using var context = CreateContext();
        var service = CreateService(context);
        var requestId = Guid.CreateVersion7();
        var command = new EvaluateEligibilityCommand(
            requestId,
            Guid.CreateVersion7(),
            scenario.ObligationId,
            EligibilityDate,
            EligibilityDateSources.ManualRequest);

        var first = await service.EvaluateAsync(command);
        var replay = await service.EvaluateAsync(command);
        var reevaluation = await service.EvaluateAsync(command with
        {
            EvaluationRequestId = Guid.CreateVersion7(),
            CorrelationId = Guid.CreateVersion7(),
        });

        Assert.Equal(EligibilityResults.NoEligibleCandidate, first.Result);
        Assert.Equal(first.EvaluationId, replay.EvaluationId);
        Assert.True(replay.Replayed);
        Assert.NotEqual(first.EvaluationId, reevaluation.EvaluationId);
        Assert.Equal(2, await context.EligibilityEvaluations.CountAsync());
        Assert.All(await context.EligibilityEvaluations.AsNoTracking().ToListAsync(), item =>
            Assert.Null(item.WinnerPersonId));
        Assert.Empty(await context.AssignmentVersions.AsNoTracking().ToListAsync());
        Assert.Equal(WorkObligationStatuses.Pending, (await context.WorkObligations.AsNoTracking().SingleAsync()).ExecutionStatus);

        await Assert.ThrowsAsync<EligibilityEvaluationIdempotencyConflictException>(() =>
            service.EvaluateAsync(command with { EligibilityDateSource = EligibilityDateSources.ScheduledOccurrence }));
    }

    [Fact]
    public async Task InvalidDateMissingOrNonPendingObligationHaveNoEvaluationEffects()
    {
        var scenario = await ResetAndSeedAsync();
        await using var context = CreateContext();
        var service = CreateService(context);

        await Assert.ThrowsAsync<EligibilityDateInvalidException>(() => service.EvaluateAsync(new(
            Guid.CreateVersion7(), Guid.CreateVersion7(), scenario.ObligationId,
            EligibilityDate.AddDays(1), EligibilityDateSources.ManualRequest)));
        await Assert.ThrowsAsync<EligibilityEvaluationNotFoundException>(() => service.EvaluateAsync(new(
            Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(),
            EligibilityDate, EligibilityDateSources.ManualRequest)));
        var concluded = WorkObligationStatuses.Concluded;
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE work_obligation SET execution_status = {concluded}, concluded_at = {Now}, concluded_by = {scenario.ActorPersonId} WHERE id = {scenario.ObligationId}");
        context.ChangeTracker.Clear();
        await Assert.ThrowsAsync<EligibilityObligationNotPendingException>(() => service.EvaluateAsync(new(
            Guid.CreateVersion7(), Guid.CreateVersion7(), scenario.ObligationId,
            EligibilityDate, EligibilityDateSources.ManualRequest)));

        Assert.Empty(await context.EligibilityEvaluations.AsNoTracking().ToListAsync());
        Assert.Empty(await context.EligibilityCandidates.AsNoTracking().ToListAsync());
        Assert.Empty(await context.IdempotencyRecords.AsNoTracking()
            .Where(item => item.ResourceType == "ELIGIBILITY_EVALUATION")
            .ToListAsync());
    }

    [Fact]
    public async Task AuditFailureRollsBackEvaluationCandidatesAndIdempotency()
    {
        var scenario = await ResetAndSeedAsync();
        await using var context = CreateContext();
        var duplicateAuditId = Guid.CreateVersion7();
        context.AuditEvents.Add(new AuditEvent
        {
            Id = duplicateAuditId,
            OccurredAt = Now,
            ActorType = "SYSTEM",
            Action = "SYNTHETIC_EXISTING_EVENT",
            ResourceType = "ELIGIBILITY_EVALUATION",
            CorrelationId = Guid.CreateVersion7(),
            Outcome = "SUCCESS",
        });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        var auditCount = await context.AuditEvents.CountAsync();
        var service = CreateService(
            context,
            new SequenceUuidGenerator(Guid.CreateVersion7(), duplicateAuditId));

        await Assert.ThrowsAsync<DbUpdateException>(() => service.EvaluateAsync(new(
            Guid.CreateVersion7(), Guid.CreateVersion7(), scenario.ObligationId,
            EligibilityDate, EligibilityDateSources.ManualRequest)));

        Assert.Empty(await context.EligibilityEvaluations.AsNoTracking().ToListAsync());
        Assert.Empty(await context.EligibilityCandidates.AsNoTracking().ToListAsync());
        Assert.Equal(auditCount, await context.AuditEvents.CountAsync());
        Assert.Empty(await context.IdempotencyRecords.AsNoTracking()
            .Where(item => item.ResourceType == "ELIGIBILITY_EVALUATION")
            .ToListAsync());
    }

    [Fact]
    public async Task PostgreSqlEnforcesNoWinnerNoRankingAndRestrictRelations()
    {
        var scenario = await ResetAndSeedAsync();
        await using var context = CreateContext();
        var evaluated = await CreateService(context).EvaluateAsync(new(
            Guid.CreateVersion7(), Guid.CreateVersion7(), scenario.ObligationId,
            EligibilityDate, EligibilityDateSources.ManualRequest));

        var rankException = await Assert.ThrowsAsync<PostgresException>(() =>
            context.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE eligibility_candidate SET rank = {1} WHERE evaluation_id = {evaluated.EvaluationId}"));
        Assert.Equal(PostgresErrorCodes.CheckViolation, rankException.SqlState);
        var winnerException = await Assert.ThrowsAsync<PostgresException>(() =>
            context.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE eligibility_evaluation SET winner_person_id = {scenario.EligiblePersonId} WHERE id = {evaluated.EvaluationId}"));
        Assert.Equal(PostgresErrorCodes.CheckViolation, winnerException.SqlState);
        var unapprovedReason = "RAZON_INVENTADA";
        var reasonException = await Assert.ThrowsAsync<PostgresException>(() =>
            context.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE eligibility_candidate SET reasons = jsonb_build_array({unapprovedReason}) WHERE evaluation_id = {evaluated.EvaluationId}"));
        Assert.Equal(PostgresErrorCodes.CheckViolation, reasonException.SqlState);
        var deleteException = await Assert.ThrowsAsync<PostgresException>(() =>
            context.Database.ExecuteSqlInterpolatedAsync(
                $"DELETE FROM work_obligation WHERE id = {scenario.ObligationId}"));
        Assert.Equal(PostgresErrorCodes.RestrictViolation, deleteException.SqlState);
    }

    [Fact]
    public async Task ExplanationAllowsVisibleHierarchyAndHidesHigherTargetFromLowerRole()
    {
        var scenario = await ResetAndSeedAsync();
        await using var context = CreateContext();
        var service = CreateService(context);
        var evaluated = await service.EvaluateAsync(new(
            Guid.CreateVersion7(), Guid.CreateVersion7(), scenario.ObligationId,
            EligibilityDate, EligibilityDateSources.ManualRequest));

        var visible = await service.GetLatestAsync(
            scenario.ActorUserId, Guid.CreateVersion7(), scenario.ObligationId);
        await Assert.ThrowsAsync<EligibilityEvaluationNotFoundException>(() =>
            service.GetLatestAsync(
                scenario.LowerViewerUserId, Guid.CreateVersion7(), scenario.ObligationId));
        await Assert.ThrowsAsync<EligibilityEvaluationAccessDeniedException>(() =>
            service.GetLatestAsync(
                Guid.CreateVersion7(), Guid.CreateVersion7(), scenario.ObligationId));

        Assert.Equal(evaluated.EvaluationId, visible.EvaluationId);
    }

    private async Task<Scenario> ResetAndSeedAsync(bool includeEligible = true)
    {
        await using var context = CreateContext();
        await context.Database.EnsureDeletedAsync();
        await context.Database.MigrateAsync();

        var actor = AddPerson(context, "EVAL-DIRECTION", EmploymentStatus.Active, CanonicalRole.Direction, true);
        var eligible = AddPerson(context, "EVAL-ELIGIBLE", EmploymentStatus.Active, CanonicalRole.Subcoordination, includeEligible);
        var missingAvailability = AddPerson(context, "EVAL-NO-DAY", EmploymentStatus.Active, CanonicalRole.Subcoordination, null);
        var wrongRole = AddPerson(context, "EVAL-WRONG-ROLE", EmploymentStatus.Active, CanonicalRole.Administration, false);
        var inactive = AddPerson(context, "EVAL-INACTIVE", EmploymentStatus.Inactive, CanonicalRole.Subcoordination, true);
        var lowerViewer = AddPerson(context, "EVAL-LOWER-VIEWER", EmploymentStatus.Active, CanonicalRole.SalesFloor, true);
        context.AvailabilityDayVersions.Add(new AvailabilityDayVersion(
            Guid.CreateVersion7(),
            missingAvailability.PersonId,
            BranchScope.LorettaId,
            EligibilityDate.AddDays(1),
            true,
            missingAvailability.UserId));
        await context.SaveChangesAsync();

        var releaseId = Guid.CreateVersion7();
        var release = new ConfigurationRelease(releaseId, BranchScope.LorettaId);
        release.ApplyPublished(Published(releaseId), 1, actor.UserId, Now);
        var task = TaskDefinitionCatalog.Require("TAR-0008");
        var taskVersionId = Guid.CreateVersion7();
        using var payload = JsonDocument.Parse("{}");
        var taskVersion = new TaskDefinitionVersion(taskVersionId, task.Id, 1, 1, payload, releaseId);
        taskVersion.ApplyPublished(Published(taskVersionId), activeForNew: true);
        var policyId = Guid.CreateVersion7();
        var policy = new EligibilityPolicyVersion(
            policyId, task.Id, taskVersionId, releaseId, null, 1,
            CanonicalRole.Subcoordination, true, null);
        policy.ApplyPublished(Published(policyId));
        var ruleId = Guid.CreateVersion7();
        using var schedule = JsonDocument.Parse("null");
        var rule = new ActivationRuleVersion(
            ruleId, task.Id, taskVersionId, releaseId, null, 1,
            ActivationModes.Manual, schedule.RootElement, ActivationOriginSchemas.ManualReference);
        rule.ApplyPublished(Published(ruleId));
        var week = WeekContract.Calculate(2026, 36);
        var period = new WeekPeriod(
            Guid.CreateVersion7(), BranchScope.LorettaId, 2026, 36,
            week.StartsOn, week.EndsOn, WeekContract.Current);
        var requestId = Guid.CreateVersion7();
        var request = new GenerationRequest(
            requestId, Guid.CreateVersion7(), new string('a', 64), ruleId,
            BranchScope.LorettaId, period.Id, ActivationOriginSchemas.ManualReference,
            "synthetic-eligibility-origin", actor.UserId, Now);
        var obligation = new WorkObligation(
            Guid.CreateVersion7(), taskVersionId, BranchScope.LorettaId,
            period.Id, requestId, "synthetic-eligibility-origin");
        context.ConfigurationReleases.Add(release);
        context.TaskDefinitionVersions.Add(taskVersion);
        context.EligibilityPolicyVersions.Add(policy);
        context.ActivationRuleVersions.Add(rule);
        context.WeekPeriods.Add(period);
        context.GenerationRequests.Add(request);
        await context.SaveChangesAsync();
        context.WorkObligations.Add(obligation);
        await context.SaveChangesAsync();
        request.LinkObligation(obligation.Id);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        return new Scenario(
            actor.UserId,
            actor.PersonId,
            lowerViewer.UserId,
            obligation.Id,
            taskVersionId,
            policyId,
            eligible.PersonId,
            missingAvailability.PersonId,
            wrongRole.PersonId,
            inactive.PersonId);
    }

    private static SeededPerson AddPerson(
        SgolDbContext context,
        string stableCode,
        string employmentStatus,
        string roleCode,
        bool? availability)
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
            Guid.CreateVersion7(), personId, BranchScope.LorettaId, employmentStatus, Now));
        context.AppUsers.Add(new AppUser
        {
            Id = userId,
            PersonId = personId,
            Status = AccountStatus.Active,
            MustChangePassword = false,
            MfaEnrolledAt = Now,
            SecurityStamp = $"synthetic-{userId:N}",
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
        if (availability is bool isAvailable)
        {
            context.AvailabilityDayVersions.Add(new AvailabilityDayVersion(
                Guid.CreateVersion7(), personId, BranchScope.LorettaId,
                EligibilityDate, isAvailable, userId));
        }

        return new SeededPerson(personId, userId);
    }

    private SgolDbContext CreateContext() => new(
        new DbContextOptionsBuilder<SgolDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options);

    private static EfEligibilityEvaluationService CreateService(
        SgolDbContext context,
        IUuidGenerator? generator = null)
    {
        var clock = new FixedClock(Now);
        return new EfEligibilityEvaluationService(
            context,
            new AuditTransaction(context),
            clock,
            generator ?? new Uuid7Generator(clock));
    }

    private static VersionRecord Published(Guid id) => new(
        id, VersionStatuses.Current, Now, null, "Configuración sintética", null, 2);

    private sealed record SeededPerson(Guid PersonId, Guid UserId);

    private sealed record Scenario(
        Guid ActorUserId,
        Guid ActorPersonId,
        Guid LowerViewerUserId,
        Guid ObligationId,
        Guid TaskDefinitionVersionId,
        Guid PolicyVersionId,
        Guid EligiblePersonId,
        Guid MissingAvailabilityPersonId,
        Guid WrongRolePersonId,
        Guid InactivePersonId);

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
