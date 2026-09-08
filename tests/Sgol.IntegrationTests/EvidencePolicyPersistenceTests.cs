using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Sgol.Assignment.Contracts;
using Sgol.BuildingBlocks.Identifiers;
using Sgol.BuildingBlocks.Time;
using Sgol.BuildingBlocks.Versioning;
using Sgol.Configuration.Contracts;
using Sgol.Evidence.Contracts;
using Sgol.Generation.Contracts;
using Sgol.Identity.Contracts;
using Sgol.Organization.Contracts;
using Sgol.Planning.Contracts;
using Sgol.Web.Infrastructure.Persistence;
using Sgol.Web.Infrastructure.Persistence.Auditing;
using Sgol.Web.Infrastructure.Persistence.Bootstrap;
using Sgol.Web.Infrastructure.Persistence.Configuration;
using Sgol.Web.Infrastructure.Persistence.Evidence;
using Sgol.Web.Infrastructure.Persistence.Generation;
using Sgol.Web.Infrastructure.Persistence.Versioning;
using Testcontainers.PostgreSql;
using Xunit;

namespace Sgol.IntegrationTests;

public sealed class EvidencePolicyPersistenceTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 9, 5, 12, 0, 0, TimeSpan.Zero);
    private readonly PostgreSqlContainer _postgres = PostgreSqlPersistenceTests.CreateContainerForTests();

    public Task InitializeAsync() => _postgres.StartAsync();
    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    [Fact]
    public async Task EightPoliciesPublishExactlyAndSuccessorPreservesHistoryAndCurrentProjection()
    {
        var actor = await ResetAndSeedActorAsync("EVIDENCE-DIRECTION", CanonicalRole.Direction);
        await using var context = CreateContext();
        var services = CreateServices(context, NewUuidGenerator());
        await PublishAllTaskDefinitionsAsync(services.Release, services.Task, actor);

        var initialRelease = await services.Release.CreateDraftAsync(NewRelease(actor));
        foreach (var pair in EvidencePolicyCatalog.All)
        {
            await services.Policy.PutAsync(NewPolicy(actor, initialRelease.Id, pair.Key));
        }

        await services.Release.PublishAsync(new PublishConfigurationReleaseCommand(
            actor,
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            initialRelease.Id,
            initialRelease.RowVersion,
            Now.AddHours(1),
            "Políticas de evidencia iniciales"));

        var current = await context.EvidencePolicyVersions.AsNoTracking()
            .Where(item => item.Status == VersionStatuses.Current)
            .OrderBy(item => item.TaskDefinitionId)
            .ToListAsync();
        Assert.Equal(8, current.Count);
        Assert.Equal(27, await context.EvidenceRequirementVersions.AsNoTracking().CountAsync());
        Assert.All(await context.EvidenceRequirementVersions.AsNoTracking().ToListAsync(), item => Assert.True(item.IsRequired));

        var definitions = await services.Task.ListAsync(actor, Guid.CreateVersion7());
        Assert.All(definitions, definition =>
        {
            var policy = Assert.IsType<EvidencePolicyVersionDetails>(definition.CurrentEvidencePolicy);
            Assert.Equal(definition.Current!.Id, policy.TaskDefinitionVersionId);
            Assert.Equal(EvidencePolicyCatalog.Require(definition.TaskCode).Count, policy.Requirements.Count);
            Assert.All(policy.Requirements, requirement => Assert.True(requirement.IsRequired));
        });
        var tar0092 = definitions.Single(item => item.TaskCode == "TAR-0092").CurrentEvidencePolicy!;
        var conditional = Assert.Single(tar0092.Requirements, item => item.Code == "FOTO_DIFERENCIA_DANO");
        Assert.Equal(EvidenceConditionCodes.DifferenceOrDamage, conditional.Condition!.Code);
        var finalPhoto = definitions.Single(item => item.TaskCode == "TAR-0018").CurrentEvidencePolicy!
            .Requirements.Single(item => item.Code == "FOTOGRAFIA_FINAL");
        Assert.Null(finalPhoto.Condition);

        var old = definitions.Single(item => item.TaskCode == "TAR-0005").CurrentEvidencePolicy!;
        var successorRelease = await services.Release.CreateDraftAsync(NewRelease(actor));
        var successorDraft = await services.Policy.PutAsync(NewPolicy(
            actor,
            successorRelease.Id,
            "TAR-0005",
            expected: old.RowVersion));
        await services.Release.PublishAsync(new PublishConfigurationReleaseCommand(
            actor,
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            successorRelease.Id,
            successorRelease.RowVersion,
            Now.AddHours(2),
            "Sustituir política de evidencia"));

        var history = await context.EvidencePolicyVersions.AsNoTracking()
            .Where(item => item.TaskDefinitionId == TaskDefinitionCatalog.Require("TAR-0005").Id)
            .OrderBy(item => item.VersionNo)
            .ToListAsync();
        Assert.Equal(2, history.Count);
        Assert.Equal(VersionStatuses.Superseded, history[0].Status);
        Assert.Equal(VersionStatuses.Current, history[1].Status);
        Assert.Equal(history[0].Id, history[1].SupersedesId);
        Assert.Equal(old.TaskDefinitionVersionId, successorDraft.TaskDefinitionVersionId);
        Assert.Equal(2, await context.EvidenceRequirementVersions.AsNoTracking()
            .CountAsync(item => item.PolicyVersionId == history[0].Id));
        Assert.Contains(await context.AuditEvents.AsNoTracking().ToListAsync(), audit =>
            audit.Action == "EVIDENCE_POLICY_PUBLISHED" &&
            audit.ResourceId == successorDraft.PolicyVersionId &&
            audit.BeforeData != null &&
            audit.AfterData != null);
        Assert.Empty(context.ChangeTracker.Entries());
    }

    [Fact]
    public async Task InvalidUnauthorizedAndIdempotentWritesHaveNoPartialPolicyEffect()
    {
        var direction = await ResetAndSeedActorAsync("EVIDENCE-DIRECTION", CanonicalRole.Direction);
        await using var context = CreateContext();
        var services = CreateServices(context, NewUuidGenerator());
        await PublishAllTaskDefinitionsAsync(services.Release, services.Task, direction);
        var release = await services.Release.CreateDraftAsync(NewRelease(direction));

        await Assert.ThrowsAsync<TaskDefinitionNotMvpException>(() => services.Policy.PutAsync(
            NewPolicy(direction, release.Id, "TAR-9999")));
        await Assert.ThrowsAsync<EvidencePolicyValidationException>(() => services.Policy.PutAsync(
            NewPolicy(direction, release.Id, "TAR-0005") with { Requirements = [] }));
        var duplicate = Inputs("TAR-0005").ToList();
        duplicate[1] = duplicate[0];
        await Assert.ThrowsAsync<EvidencePolicyValidationException>(() => services.Policy.PutAsync(
            NewPolicy(direction, release.Id, "TAR-0005") with { Requirements = duplicate }));
        await Assert.ThrowsAsync<EvidencePolicyValidationException>(() => services.Policy.PutAsync(
            NewPolicy(direction, release.Id, "TAR-0092") with
            {
                Requirements = Inputs("TAR-0092")
                    .Select(item => item.Code == "FOTO_DIFERENCIA_DANO"
                        ? item with { ConditionCode = EvidenceConditionCodes.Always }
                        : item)
                    .ToArray(),
            }));

        foreach (var role in new[] { CanonicalRole.Administration, CanonicalRole.Subcoordination, CanonicalRole.SalesFloor })
        {
            var denied = await SeedActorAsync($"EVIDENCE-{role}", role);
            await Assert.ThrowsAsync<EvidencePolicyAccessDeniedException>(() => services.Policy.PutAsync(
                NewPolicy(denied, release.Id, "TAR-0005")));
        }
        var inactive = await SeedActorAsync("EVIDENCE-INACTIVE", CanonicalRole.Direction, accountActive: false);
        var outside = await SeedActorAsync("EVIDENCE-OUTSIDE", CanonicalRole.Direction, hasEmployment: false);
        await Assert.ThrowsAsync<EvidencePolicyAccessDeniedException>(() => services.Policy.PutAsync(
            NewPolicy(inactive, release.Id, "TAR-0005")));
        await Assert.ThrowsAsync<EvidencePolicyAccessDeniedException>(() => services.Policy.PutAsync(
            NewPolicy(outside, release.Id, "TAR-0005")));

        var command = NewPolicy(direction, release.Id, "TAR-0005");
        var created = await services.Policy.PutAsync(command);
        var replay = await services.Policy.PutAsync(command);
        Assert.Equal(created.PolicyVersionId, replay.PolicyVersionId);
        await Assert.ThrowsAsync<EvidencePolicyIdempotencyConflictException>(() => services.Policy.PutAsync(
            command with { ExpectedRowVersion = 1 }));
        await Assert.ThrowsAsync<EvidencePolicyOverlapException>(() => services.Policy.PutAsync(
            NewPolicy(direction, release.Id, "TAR-0005")));
        await Assert.ThrowsAsync<EvidencePolicyCoverageException>(() => services.Release.PublishAsync(
            new PublishConfigurationReleaseCommand(
                direction,
                Guid.CreateVersion7(),
                Guid.CreateVersion7(),
                release.Id,
                release.RowVersion,
                Now.AddHours(1),
                "Catálogo incompleto")));

        Assert.Single(await context.EvidencePolicyVersions.AsNoTracking().ToListAsync());
        Assert.Equal(2, await context.EvidenceRequirementVersions.AsNoTracking().CountAsync());
        Assert.Contains(await context.AuditEvents.AsNoTracking().ToListAsync(), audit =>
            audit.Action == "EVIDENCE_POLICY_ACCESS_DENIED" && audit.Outcome == "DENIED");
        Assert.DoesNotContain(await context.AuditEvents.AsNoTracking().ToListAsync(), audit =>
            audit.Action == "EVIDENCE_POLICY_PUBLISHED");
        Assert.Empty(context.ChangeTracker.Entries());
    }

    [Fact]
    public async Task ConcurrentDraftCreationLeavesExactlyOneCompletePolicy()
    {
        var actor = await ResetAndSeedActorAsync("EVIDENCE-CONCURRENT", CanonicalRole.Direction);
        Guid releaseId;
        await using (var setupContext = CreateContext())
        {
            var setup = CreateServices(setupContext, NewUuidGenerator());
            await PublishAllTaskDefinitionsAsync(setup.Release, setup.Task, actor);
            var release = await setup.Release.CreateDraftAsync(NewRelease(actor));
            releaseId = release.Id;
        }

        await using var firstContext = CreateContext();
        await using var secondContext = CreateContext();
        var first = CreateServices(firstContext, NewUuidGenerator());
        var second = CreateServices(secondContext, NewUuidGenerator());
        var results = await Task.WhenAll(
            Record.ExceptionAsync(() => first.Policy.PutAsync(NewPolicy(actor, releaseId, "TAR-0005"))),
            Record.ExceptionAsync(() => second.Policy.PutAsync(NewPolicy(actor, releaseId, "TAR-0005"))));

        Assert.Single(results, exception => exception is null);
        Assert.Single(results, exception => exception is EvidencePolicyOverlapException);
        await using var verification = CreateContext();
        var policy = Assert.Single(await verification.EvidencePolicyVersions.AsNoTracking().ToListAsync());
        Assert.Equal(2, await verification.EvidenceRequirementVersions.AsNoTracking()
            .CountAsync(item => item.PolicyVersionId == policy.Id));
        Assert.Contains(await verification.AuditEvents.AsNoTracking().ToListAsync(), audit =>
            audit.Action == "EVIDENCE_POLICY_REJECTED" && audit.Outcome == "REJECTED");
    }

    [Fact]
    public async Task AuditFailureRollsBackHeaderRequirementsAndIdempotencyTogether()
    {
        var actor = await ResetAndSeedActorAsync("EVIDENCE-ROLLBACK", CanonicalRole.Direction);
        await using var context = CreateContext();
        var normal = CreateServices(context, NewUuidGenerator());
        await PublishAllTaskDefinitionsAsync(normal.Release, normal.Task, actor);
        var release = await normal.Release.CreateDraftAsync(NewRelease(actor));
        var duplicateAuditId = Guid.CreateVersion7();
        context.AuditEvents.Add(new AuditEvent
        {
            Id = duplicateAuditId,
            OccurredAt = Now,
            ActorUserId = actor,
            ActorType = "APP_USER",
            Action = "SYNTHETIC_EXISTING_EVENT",
            ResourceType = "EVIDENCE_POLICY_VERSION",
            BranchId = BranchScope.LorettaId,
            CorrelationId = Guid.CreateVersion7(),
            Outcome = "SUCCESS",
        });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var policyId = Guid.CreateVersion7();
        var failing = new EfEvidencePolicyService(
            context,
            new AuditTransaction(context),
            new FixedClock(Now),
            new SequenceUuidGenerator(policyId, Guid.CreateVersion7(), Guid.CreateVersion7(), duplicateAuditId));
        await Assert.ThrowsAsync<DbUpdateException>(() => failing.PutAsync(
            NewPolicy(actor, release.Id, "TAR-0005")));

        Assert.False(await context.EvidencePolicyVersions.AsNoTracking().AnyAsync(item => item.Id == policyId));
        Assert.False(await context.EvidenceRequirementVersions.AsNoTracking().AnyAsync(item => item.PolicyVersionId == policyId));
        Assert.False(await context.IdempotencyRecords.AsNoTracking().AnyAsync(item => item.ResourceId == policyId));
        Assert.Empty(context.ChangeTracker.Entries());
    }

    [Fact]
    public async Task PostgreSqlEnforcesClosedCatalogAndObligationCapturesImmutableApplicablePolicy()
    {
        var actor = await ResetAndSeedActorAsync("EVIDENCE-SNAPSHOT", CanonicalRole.Direction);
        await using var context = CreateContext();
        var services = CreateServices(context, NewUuidGenerator());
        await PublishAllTaskDefinitionsAsync(services.Release, services.Task, actor);
        var release = await services.Release.CreateDraftAsync(NewRelease(actor));
        foreach (var taskCode in EvidencePolicyCatalog.All.Keys)
        {
            await services.Policy.PutAsync(NewPolicy(actor, release.Id, taskCode));
        }
        await services.Release.PublishAsync(new PublishConfigurationReleaseCommand(
            actor,
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            release.Id,
            release.RowVersion,
            Now.AddHours(1),
            "Políticas aplicables"));

        var task = TaskDefinitionCatalog.Require("TAR-0007");
        var taskVersion = await context.TaskDefinitionVersions.AsNoTracking()
            .SingleAsync(item => item.TaskDefinitionId == task.Id && item.Status == VersionStatuses.Current);
        var policy = await context.EvidencePolicyVersions.AsNoTracking()
            .SingleAsync(item => item.TaskDefinitionId == task.Id && item.Status == VersionStatuses.Current);
        var successorRelease = await services.Release.CreateDraftAsync(NewRelease(actor));
        await services.Policy.PutAsync(NewPolicy(
            actor,
            successorRelease.Id,
            "TAR-0007",
            expected: policy.RowVersion));
        await services.Release.PublishAsync(new PublishConfigurationReleaseCommand(
            actor,
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            successorRelease.Id,
            successorRelease.RowVersion,
            Now.AddHours(2),
            "Sustitución posterior a la obligación"));

        using var noSchedule = JsonDocument.Parse("null");
        var rule = new ActivationRuleVersion(
            Guid.CreateVersion7(),
            task.Id,
            taskVersion.Id,
            release.Id,
            null,
            1,
            ActivationModes.Manual,
            noSchedule.RootElement,
            ActivationOriginSchemas.ManualReference);
        var rulePlan = VersioningRules.PlanPublication(
            rule.ToVersionRecord(),
            null,
            [],
            rule.RowVersion,
            Now.AddHours(1),
            "Regla sintética");
        rule.ApplyPublished(rulePlan.Published);
        context.ActivationRuleVersions.Add(rule);
        var week = WeekContract.Calculate(2026, 36);
        var period = new WeekPeriod(
            Guid.CreateVersion7(),
            BranchScope.LorettaId,
            2026,
            36,
            week.StartsOn,
            week.EndsOn,
            WeekContract.Current);
        context.WeekPeriods.Add(period);
        var request = new GenerationRequest(
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            new string('a', 64),
            rule.Id,
            BranchScope.LorettaId,
            period.Id,
            ActivationOriginSchemas.ManualReference,
            "SNAPSHOT-001",
            actor,
            Now.AddHours(1).AddMinutes(1));
        context.GenerationRequests.Add(request);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var materializer = new EfWorkObligationMaterializer(
            context,
            new AuditTransaction(context),
            new FixedClock(Now.AddHours(1).AddMinutes(1)),
            NewUuidGenerator(Now.AddHours(1).AddMinutes(1)));
        var obligation = await materializer.MaterializeAsync(new MaterializeWorkObligationCommand(
            request.Id,
            Guid.CreateVersion7()));
        Assert.Equal(policy.Id, obligation.EvidencePolicyVersionId);

        Assert.Equal(
            policy.Id,
            await context.WorkObligations.AsNoTracking()
                .Where(item => item.Id == obligation.ObligationId)
                .Select(item => item.EvidencePolicyVersionId)
                .SingleAsync());
        Assert.NotEqual(
            policy.Id,
            await context.EvidencePolicyVersions.AsNoTracking()
                .Where(item => item.TaskDefinitionId == task.Id && item.Status == VersionStatuses.Current)
                .Select(item => item.Id)
                .SingleAsync());

        var duplicateAuditId = Guid.CreateVersion7();
        context.AuditEvents.Add(new AuditEvent
        {
            Id = duplicateAuditId,
            OccurredAt = Now,
            ActorUserId = actor,
            ActorType = "APP_USER",
            Action = "SYNTHETIC_EXISTING_EVENT",
            ResourceType = "EVIDENCE_REVIEW_SNAPSHOT",
            ResourceId = obligation.ObligationId,
            BranchId = BranchScope.LorettaId,
            CorrelationId = Guid.CreateVersion7(),
            Outcome = "SUCCESS",
        });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        var failedSnapshotIds = new[] { Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7() };
        var failingReview = new EfEvidenceReviewService(
            context,
            new FixedClock(Now.AddHours(3)),
            new SequenceUuidGenerator(
                failedSnapshotIds[0], duplicateAuditId,
                failedSnapshotIds[1], duplicateAuditId,
                failedSnapshotIds[2], duplicateAuditId));
        await Assert.ThrowsAsync<EvidenceReviewFailedException>(() => failingReview.ReviewAsync(
            new(actor, Guid.CreateVersion7(), obligation.ObligationId)));
        Assert.False(await context.EvidenceReviewSnapshots.AsNoTracking()
            .AnyAsync(snapshot => failedSnapshotIds.Contains(snapshot.Id)));

        var responsible = await SeedActorAsync("EVIDENCE-REVIEW-RESPONSIBLE", CanonicalRole.Subcoordination);
        var superior = await SeedActorAsync("EVIDENCE-REVIEW-SUPERIOR", CanonicalRole.Administration);
        var peer = await SeedActorAsync("EVIDENCE-REVIEW-PEER", CanonicalRole.Subcoordination);
        var lower = await SeedActorAsync("EVIDENCE-REVIEW-LOWER", CanonicalRole.SalesFloor);
        var responsiblePerson = await context.AppUsers.AsNoTracking()
            .Where(user => user.Id == responsible).Select(user => user.PersonId).SingleAsync();
        using var assignmentExplanation = JsonDocument.Parse("{}");
        context.AssignmentVersions.Add(new AssignmentVersion(
            Guid.CreateVersion7(), obligation.ObligationId, responsiblePerson, AssignmentVersionStatuses.Current,
            AssignmentTypes.Automatic, assignmentExplanation, Now.AddHours(2)));
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var reviewService = new EfEvidenceReviewService(
            context,
            new FixedClock(Now.AddHours(3)),
            NewUuidGenerator(Now.AddHours(3)));
        var firstReview = await reviewService.ReviewAsync(new(responsible, Guid.CreateVersion7(), obligation.ObligationId));
        var repeatedReview = await reviewService.ReviewAsync(new(superior, Guid.CreateVersion7(), obligation.ObligationId));
        var directionReview = await reviewService.ReviewAsync(new(actor, Guid.CreateVersion7(), obligation.ObligationId));
        Assert.Equal(EvidenceReviewResults.Incomplete, firstReview.Result);
        Assert.Equal(4, firstReview.MissingRequirements.Count);
        Assert.Equal(policy.Id, firstReview.EvidencePolicyVersionId);
        Assert.Equal(firstReview.SnapshotId, repeatedReview.SnapshotId);
        Assert.Equal(firstReview.SnapshotId, directionReview.SnapshotId);
        await Assert.ThrowsAsync<EvidenceReviewObligationNotFoundException>(() => reviewService.ReviewAsync(
            new(peer, Guid.CreateVersion7(), obligation.ObligationId)));
        await Assert.ThrowsAsync<EvidenceReviewObligationNotFoundException>(() => reviewService.ReviewAsync(
            new(lower, Guid.CreateVersion7(), obligation.ObligationId)));
        Assert.Single(await context.EvidenceReviewSnapshots.AsNoTracking()
            .Where(snapshot => snapshot.ObligationId == obligation.ObligationId).ToListAsync());
        Assert.Contains(await context.AuditEvents.AsNoTracking().ToListAsync(), audit =>
            audit.Action == "EVIDENCE_REVIEW_SNAPSHOT_CREATED" && audit.ResourceId == firstReview.SnapshotId);
        Assert.Equal(WorkObligationStatuses.Pending,
            await context.WorkObligations.AsNoTracking().Where(item => item.Id == obligation.ObligationId)
                .Select(item => item.ExecutionStatus).SingleAsync());

        var releaseRequirement = await context.EvidenceRequirementVersions.AsNoTracking()
            .SingleAsync(item => item.PolicyVersionId == policy.Id && item.RequirementCode == "LIBERACION");
        var evidenceItem = new EvidenceItem(
            Guid.CreateVersion7(), obligation.ObligationId, policy.Id, releaseRequirement.Id,
            releaseRequirement.RequirementCode, releaseRequirement.Kind, Now.AddHours(3));
        using var firstRelease = JsonDocument.Parse(
            """{"schemaVersion":1,"releasedAt":"2026-09-08T16:00:00Z","releaseReference":"LIB-01"}""");
        var firstVersion = new EvidenceVersion(
            Guid.CreateVersion7(), evidenceItem.Id, 1, firstRelease, responsible, Now.AddHours(3));
        context.EvidenceItems.Add(evidenceItem);
        context.EvidenceVersions.Add(firstVersion);
        await context.SaveChangesAsync();

        var afterContribution = await reviewService.ReviewAsync(new(responsible, Guid.CreateVersion7(), obligation.ObligationId));
        Assert.NotEqual(firstReview.SnapshotId, afterContribution.SnapshotId);
        Assert.Equal(3, afterContribution.MissingRequirements.Count);
        Assert.Equal(4, firstReview.MissingRequirements.Count);

        firstVersion.Supersede();
        evidenceItem.Advance(evidenceItem.RowVersion);
        using var replacementRelease = JsonDocument.Parse(
            """{"schemaVersion":1,"releasedAt":"2026-09-08T17:00:00Z","releaseReference":"LIB-02"}""");
        var replacementVersion = new EvidenceVersion(
            Guid.CreateVersion7(), evidenceItem.Id, 2, replacementRelease, responsible, Now.AddHours(4),
            "Corrección sintética", firstVersion.Id);
        context.EvidenceVersions.Add(replacementVersion);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        await using var concurrentContextOne = CreateContext();
        await using var concurrentContextTwo = CreateContext();
        var concurrentOne = new EfEvidenceReviewService(
            concurrentContextOne, new FixedClock(Now.AddHours(4)), NewUuidGenerator(Now.AddHours(4)));
        var concurrentTwo = new EfEvidenceReviewService(
            concurrentContextTwo, new FixedClock(Now.AddHours(4)), NewUuidGenerator(Now.AddHours(4)));
        var concurrentResults = await Task.WhenAll(
            concurrentOne.ReviewAsync(new(actor, Guid.CreateVersion7(), obligation.ObligationId)),
            concurrentTwo.ReviewAsync(new(actor, Guid.CreateVersion7(), obligation.ObligationId)));
        Assert.Equal(concurrentResults[0].SnapshotId, concurrentResults[1].SnapshotId);
        Assert.NotEqual(afterContribution.SnapshotId, concurrentResults[0].SnapshotId);
        var concurrentSnapshot = await context.EvidenceReviewSnapshots.AsNoTracking()
            .SingleAsync(snapshot => snapshot.Id == concurrentResults[0].SnapshotId);
        Assert.Equal(replacementVersion.Id, Assert.Single(concurrentSnapshot.EvidenceVersionIds));
        Assert.Equal(3, await context.EvidenceReviewSnapshots.AsNoTracking()
            .CountAsync(snapshot => snapshot.ObligationId == obligation.ObligationId));

        var updateException = await Assert.ThrowsAsync<PostgresException>(() => context.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE evidence_review_snapshot SET result = {"COMPLETA"} WHERE id = {firstReview.SnapshotId}
            """));
        Assert.Equal(PostgresErrorCodes.CheckViolation, updateException.SqlState);
        var deleteException = await Assert.ThrowsAsync<PostgresException>(() => context.Database.ExecuteSqlInterpolatedAsync($"""
            DELETE FROM evidence_review_snapshot WHERE id = {firstReview.SnapshotId}
            """));
        Assert.Equal(PostgresErrorCodes.CheckViolation, deleteException.SqlState);

        var historical = new WorkObligation(
            Guid.CreateVersion7(),
            taskVersion.Id,
            BranchScope.LorettaId,
            period.Id,
            Guid.CreateVersion7(),
            "HISTORICAL-NULL");
        var historicalRequest = new GenerationRequest(
            historical.GenerationRequestId,
            Guid.CreateVersion7(),
            new string('b', 64),
            rule.Id,
            BranchScope.LorettaId,
            period.Id,
            ActivationOriginSchemas.ManualReference,
            historical.OriginReference,
            actor,
            Now);
        context.GenerationRequests.Add(historicalRequest);
        await context.SaveChangesAsync();
        context.WorkObligations.Add(historical);
        historicalRequest.LinkObligation(historical.Id);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        Assert.Null(historical.EvidencePolicyVersionId);

        await Assert.ThrowsAsync<PostgresException>(() => context.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE work_obligation
            SET evidence_policy_version_id = {policy.Id}
            WHERE id = {historical.Id}
            """));
        await Assert.ThrowsAsync<PostgresException>(() => context.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE evidence_requirement_catalog
            SET kind = {EvidenceRequirementKinds.Photograph}
            WHERE task_definition_id = {task.Id} AND requirement_code = {"LIBERACION"}
            """));

        var policyForInvalidRow = await context.EvidencePolicyVersions.AsNoTracking()
            .SingleAsync(item => item.TaskDefinitionId == task.Id && item.Status == VersionStatuses.Current);
        await Assert.ThrowsAsync<PostgresException>(() => context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO evidence_requirement_version
                (id, policy_version_id, task_definition_id, requirement_code, kind, condition_code, ordinal, is_required)
            VALUES ({Guid.CreateVersion7()}, {policyForInvalidRow.Id}, {task.Id}, {"DESCONOCIDO"},
                    {EvidenceRequirementKinds.DigitalRecord}, {EvidenceConditionCodes.Always}, 99, {true})
            """));
        await Assert.ThrowsAsync<PostgresException>(() => context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO evidence_requirement_version
                (id, policy_version_id, task_definition_id, requirement_code, kind, condition_code, ordinal, is_required)
            VALUES ({Guid.CreateVersion7()}, {policyForInvalidRow.Id}, {task.Id}, {"LIBERACION"},
                    {EvidenceRequirementKinds.DigitalRecord}, {EvidenceConditionCodes.Always}, 99, {false})
            """));
    }

    private static async Task PublishAllTaskDefinitionsAsync(
        EfConfigurationReleaseService releaseService,
        EfTaskDefinitionService taskService,
        Guid actor)
    {
        var release = await releaseService.CreateDraftAsync(NewRelease(actor));
        using var empty = JsonDocument.Parse("{}");
        foreach (var task in TaskDefinitionCatalog.All)
        {
            await taskService.CreateVersionAsync(new CreateTaskDefinitionVersionCommand(
                actor,
                Guid.CreateVersion7(),
                Guid.CreateVersion7(),
                task.TaskCode,
                release.Id,
                1,
                empty.RootElement));
        }

        await releaseService.PublishAsync(new PublishConfigurationReleaseCommand(
            actor,
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            release.Id,
            release.RowVersion,
            Now,
            "Definiciones iniciales"));
    }

    private async Task ResetAsync()
    {
        await using var context = CreateContext();
        await context.Database.EnsureDeletedAsync();
        await context.Database.MigrateAsync();
    }

    private async Task<Guid> ResetAndSeedActorAsync(string code, string role)
    {
        await ResetAsync();
        return await SeedActorAsync(code, role);
    }

    private async Task<Guid> SeedActorAsync(
        string code,
        string role,
        bool accountActive = true,
        bool hasEmployment = true)
    {
        await using var context = CreateContext();
        var personId = Guid.CreateVersion7();
        var userId = Guid.CreateVersion7();
        context.People.Add(new Person
        {
            Id = personId,
            StableCode = code,
            DisplayName = "Persona sintética",
            CreatedAt = Now,
        });
        if (hasEmployment)
        {
            context.EmploymentVersions.Add(new EmploymentVersion(
                Guid.CreateVersion7(),
                personId,
                BranchScope.LorettaId,
                EmploymentStatus.Active,
                Now));
        }
        context.AppUsers.Add(new AppUser
        {
            Id = userId,
            PersonId = personId,
            Status = accountActive ? AccountStatus.Active : AccountStatus.Inactive,
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
            RoleCode = role,
            Status = RoleAssignmentStatus.Active,
            ValidFrom = Now,
        });
        await context.SaveChangesAsync();
        return userId;
    }

    private SgolDbContext CreateContext() => new(
        new DbContextOptionsBuilder<SgolDbContext>().UseNpgsql(_postgres.GetConnectionString()).Options);

    private static Services CreateServices(SgolDbContext context, IUuidGenerator generator)
    {
        var audit = new AuditTransaction(context);
        var release = new EfConfigurationReleaseService(
            context,
            audit,
            new VersioningTransaction(context, audit),
            new FixedClock(Now),
            generator);
        return new Services(
            release,
            new EfTaskDefinitionService(context, audit, release, new FixedClock(Now), generator),
            new EfEvidencePolicyService(context, audit, new FixedClock(Now), generator));
    }

    private static CreateConfigurationReleaseCommand NewRelease(Guid actor) =>
        new(actor, Guid.CreateVersion7(), Guid.CreateVersion7());

    private static PutEvidencePolicyCommand NewPolicy(
        Guid actor,
        Guid releaseId,
        string taskCode,
        Guid? key = null,
        Guid? correlation = null,
        long? expected = null) => new(
        actor,
        key ?? Guid.CreateVersion7(),
        correlation ?? Guid.CreateVersion7(),
        taskCode,
        releaseId,
        Inputs(taskCode),
        expected);

    private static EvidenceRequirementInput[] Inputs(string taskCode) =>
        EvidencePolicyCatalog.Require(taskCode)
            .Select(item => new EvidenceRequirementInput(item.Code, item.Kind, item.ConditionCode))
            .ToArray();

    private static Uuid7Generator NewUuidGenerator(DateTimeOffset? now = null) =>
        new(new FixedClock(now ?? Now));

    private sealed record Services(
        EfConfigurationReleaseService Release,
        EfTaskDefinitionService Task,
        EfEvidencePolicyService Policy);

    private sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow { get; } = now;
    }

    private sealed class SequenceUuidGenerator(params Guid[] values) : IUuidGenerator
    {
        private readonly Queue<Guid> _values = new(values);

        public Guid NewUuid() => _values.Dequeue();
    }
}
