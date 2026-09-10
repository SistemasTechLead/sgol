using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Sgol.BuildingBlocks.Identifiers;
using Sgol.BuildingBlocks.Versioning;
using Sgol.Configuration.Contracts;
using Sgol.Generation.Contracts;
using Sgol.Identity.Contracts;
using Sgol.Organization.Contracts;
using Sgol.Planning.Contracts;
using Sgol.Web.Infrastructure.Persistence.Auditing;
using Sgol.Web.Infrastructure.Persistence.Configuration;
using Sgol.Web.Infrastructure.Persistence.Generation;
using Xunit;

namespace Sgol.IntegrationTests;

public sealed partial class EvidencePolicyPersistenceTests
{
    [Fact]
    public async Task Hu027_EightPoliciesPublishAndSuccessorPreservesQueryableImmutableHistory()
    {
        var actor = await ResetAndSeedActorAsync("VALIDATION-DIRECTION", CanonicalRole.Direction);
        await using var context = CreateContext();
        var services = CreateServices(context, NewUuidGenerator());
        await PublishAllTaskDefinitionsAsync(services.Release, services.Task, actor);

        var initialRelease = await services.Release.CreateDraftAsync(NewRelease(actor));
        foreach (var taskCode in ValidationPolicyCatalog.All.Keys)
        {
            await services.Validation.PutAsync(NewValidationPolicy(actor, initialRelease.Id, taskCode));
        }

        await services.Release.PublishAsync(new PublishConfigurationReleaseCommand(
            actor,
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            initialRelease.Id,
            initialRelease.RowVersion,
            Now.AddHours(1),
            "Políticas de validación iniciales"));

        var current = await context.ValidationPolicyVersions.AsNoTracking()
            .Where(item => item.Status == VersionStatuses.Current)
            .ToListAsync();
        Assert.Equal(8, current.Count);
        foreach (var policy in current)
        {
            var taskCode = TaskDefinitionCatalog.All.Single(item => item.Id == policy.TaskDefinitionId).TaskCode;
            var approved = ValidationPolicyCatalog.Require(taskCode);
            Assert.True(policy.IsRequired);
            Assert.Equal(approved.ExecutorRole, policy.ExecutorRole);
            Assert.Equal(approved.ValidatorRole, policy.ValidatorRole);
            Assert.Equal(ValidationPolicyValues.ImmediateSuperior, policy.ValidatorRelation);
            Assert.Equal(
                ValidationPolicyValues.AllowedResults,
                policy.AllowedResults.RootElement.EnumerateArray().Select(item => item.GetString()!).ToArray());
        }

        var definitions = await services.Task.ListAsync(actor, Guid.CreateVersion7());
        Assert.All(definitions, definition =>
        {
            var policy = Assert.IsType<ValidationPolicyVersionDetails>(definition.CurrentValidationPolicy);
            Assert.Equal(definition.Current!.Id, policy.TaskDefinitionVersionId);
            Assert.Single(definition.ValidationPolicyHistory);
            Assert.Equal(policy.PolicyVersionId, definition.ValidationPolicyHistory[0].PolicyVersionId);
        });

        var old = definitions.Single(item => item.TaskCode == "TAR-0005").CurrentValidationPolicy!;
        var successorRelease = await services.Release.CreateDraftAsync(NewRelease(actor));
        var successor = await services.Validation.PutAsync(NewValidationPolicy(
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
            "Sustituir política de validación"));

        var detail = await services.Task.GetAsync(actor, Guid.CreateVersion7(), "TAR-0005");
        Assert.Equal(successor.PolicyVersionId, detail.CurrentValidationPolicy!.PolicyVersionId);
        Assert.Equal(2, detail.ValidationPolicyHistory.Count);
        Assert.Equal(VersionStatuses.Current, detail.ValidationPolicyHistory[0].Status);
        Assert.Equal(VersionStatuses.Superseded, detail.ValidationPolicyHistory[1].Status);
        Assert.Equal(detail.ValidationPolicyHistory[1].PolicyVersionId, detail.ValidationPolicyHistory[0].SupersedesPolicyVersionId);
        Assert.Contains(await context.AuditEvents.AsNoTracking().ToListAsync(), audit =>
            audit.Action == "VALIDATION_POLICY_PUBLISHED" &&
            audit.ResourceId == successor.PolicyVersionId &&
            audit.BeforeData != null &&
            audit.AfterData != null);
        Assert.Empty(context.ChangeTracker.Entries());
    }

    [Fact]
    public async Task Hu027_RejectionsReplayAndAuditFailureLeaveNoPartialEffect()
    {
        var direction = await ResetAndSeedActorAsync("VALIDATION-WRITES", CanonicalRole.Direction);
        await using var context = CreateContext();
        var services = CreateServices(context, NewUuidGenerator());
        await PublishAllTaskDefinitionsAsync(services.Release, services.Task, direction);
        var release = await services.Release.CreateDraftAsync(NewRelease(direction));

        await Assert.ThrowsAsync<TaskDefinitionNotMvpException>(() => services.Validation.PutAsync(
            NewValidationPolicy(direction, release.Id, "TAR-9999")));
        await Assert.ThrowsAsync<ValidationPolicyValidationException>(() => services.Validation.PutAsync(
            NewValidationPolicy(direction, release.Id, "TAR-0005") with { ExecutorRole = "GERENTE" }));
        await Assert.ThrowsAsync<ValidationPolicyValidationException>(() => services.Validation.PutAsync(
            NewValidationPolicy(direction, release.Id, "TAR-0005") with { ValidatorRole = "SUBCOORDINACION" }));
        await Assert.ThrowsAsync<ValidationPolicyValidationException>(() => services.Validation.PutAsync(
            NewValidationPolicy(direction, release.Id, "TAR-0005") with { ValidatorRelation = "PAR" }));
        await Assert.ThrowsAsync<ValidationPolicyValidationException>(() => services.Validation.PutAsync(
            NewValidationPolicy(direction, release.Id, "TAR-0005") with { AllowedResults = ["CUMPLIDA", "INCOMPLETA"] }));

        foreach (var role in new[] { CanonicalRole.Administration, CanonicalRole.Subcoordination, CanonicalRole.SalesFloor })
        {
            var denied = await SeedActorAsync($"VALIDATION-{role}", role);
            await Assert.ThrowsAsync<ValidationPolicyAccessDeniedException>(() => services.Validation.PutAsync(
                NewValidationPolicy(denied, release.Id, "TAR-0005")));
        }

        var command = NewValidationPolicy(direction, release.Id, "TAR-0005");
        var created = await services.Validation.PutAsync(command);
        var replay = await services.Validation.PutAsync(command with
        {
            AllowedResults = ValidationPolicyValues.AllowedResults.Reverse().ToArray(),
        });
        Assert.Equal(created.PolicyVersionId, replay.PolicyVersionId);
        await Assert.ThrowsAsync<ValidationPolicyIdempotencyConflictException>(() => services.Validation.PutAsync(
            command with { ExpectedRowVersion = 1 }));
        await Assert.ThrowsAsync<ValidationPolicyOverlapException>(() => services.Validation.PutAsync(
            NewValidationPolicy(direction, release.Id, "TAR-0005")));
        await Assert.ThrowsAsync<ValidationPolicyIdempotencyConflictException>(() => services.Validation.PutAsync(
            NewValidationPolicy(direction, release.Id, "TAR-0007", key: command.IdempotencyKey)));
        await Assert.ThrowsAsync<ValidationPolicyCoverageException>(() => services.Release.PublishAsync(
            new PublishConfigurationReleaseCommand(
                direction,
                Guid.CreateVersion7(),
                Guid.CreateVersion7(),
                release.Id,
                release.RowVersion,
                Now.AddHours(1),
                "Catálogo incompleto")));
        Assert.Single(await context.ValidationPolicyVersions.AsNoTracking().ToListAsync());
        Assert.DoesNotContain(await context.AuditEvents.AsNoTracking().ToListAsync(), audit =>
            audit.Action == "VALIDATION_POLICY_PUBLISHED");

        var secondRelease = await services.Release.CreateDraftAsync(NewRelease(direction));
        var duplicateAuditId = Guid.CreateVersion7();
        context.AuditEvents.Add(new AuditEvent
        {
            Id = duplicateAuditId,
            OccurredAt = Now,
            ActorUserId = direction,
            ActorType = "APP_USER",
            Action = "SYNTHETIC_EXISTING_EVENT",
            ResourceType = "VALIDATION_POLICY_VERSION",
            BranchId = BranchScope.LorettaId,
            CorrelationId = Guid.CreateVersion7(),
            Outcome = "SUCCESS",
        });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        var failedPolicyId = Guid.CreateVersion7();
        var failing = new EfValidationPolicyService(
            context,
            new AuditTransaction(context),
            new FixedClock(Now),
            new SequenceUuidGenerator(failedPolicyId, duplicateAuditId));
        await Assert.ThrowsAsync<DbUpdateException>(() => failing.PutAsync(
            NewValidationPolicy(direction, secondRelease.Id, "TAR-0007")));
        Assert.False(await context.ValidationPolicyVersions.AsNoTracking().AnyAsync(item => item.Id == failedPolicyId));
        Assert.False(await context.IdempotencyRecords.AsNoTracking().AnyAsync(item => item.ResourceId == failedPolicyId));
        Assert.Contains(await context.AuditEvents.AsNoTracking().ToListAsync(), audit =>
            audit.Action == "VALIDATION_POLICY_ACCESS_DENIED" && audit.Outcome == "DENIED");
        Assert.Empty(context.ChangeTracker.Entries());
    }

    [Fact]
    public async Task Hu027_ConcurrentDraftCreationLeavesOnePolicyAndOneAuditedRejection()
    {
        var actor = await ResetAndSeedActorAsync("VALIDATION-CONCURRENT", CanonicalRole.Direction);
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
            Record.ExceptionAsync(() => first.Validation.PutAsync(
                NewValidationPolicy(actor, releaseId, "TAR-0005"))),
            Record.ExceptionAsync(() => second.Validation.PutAsync(
                NewValidationPolicy(actor, releaseId, "TAR-0005"))));

        Assert.Single(results, exception => exception is null);
        Assert.Single(results, exception => exception is ValidationPolicyOverlapException);
        await using var verification = CreateContext();
        Assert.Single(await verification.ValidationPolicyVersions.AsNoTracking().ToListAsync());
        Assert.Contains(await verification.AuditEvents.AsNoTracking().ToListAsync(), audit =>
            audit.Action == "VALIDATION_POLICY_REJECTED" && audit.Outcome == "REJECTED");
    }

    [Fact]
    public async Task Hu027_ObligationCapturesExactPolicyAndPostgreSqlProtectsHistoryAndNoBackfill()
    {
        var actor = await ResetAndSeedActorAsync("VALIDATION-SNAPSHOT", CanonicalRole.Direction);
        await using var context = CreateContext();
        var services = CreateServices(context, NewUuidGenerator());
        await PublishAllTaskDefinitionsAsync(services.Release, services.Task, actor);
        var release = await services.Release.CreateDraftAsync(NewRelease(actor));
        foreach (var taskCode in ValidationPolicyCatalog.All.Keys)
        {
            await services.Validation.PutAsync(NewValidationPolicy(actor, release.Id, taskCode));
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
        var policy = await context.ValidationPolicyVersions.AsNoTracking()
            .SingleAsync(item => item.TaskDefinitionId == task.Id && item.Status == VersionStatuses.Current);
        using var noSchedule = JsonDocument.Parse("null");
        var rule = new ActivationRuleVersion(
            Guid.CreateVersion7(), task.Id, taskVersion.Id, release.Id, null, 1,
            ActivationModes.Manual, noSchedule.RootElement, ActivationOriginSchemas.ManualReference);
        var rulePlan = VersioningRules.PlanPublication(
            rule.ToVersionRecord(), null, [], rule.RowVersion, Now.AddHours(1), "Regla sintética");
        rule.ApplyPublished(rulePlan.Published);
        context.ActivationRuleVersions.Add(rule);
        var week = WeekContract.Calculate(2026, 36);
        var period = new WeekPeriod(
            Guid.CreateVersion7(), BranchScope.LorettaId, 2026, 36, week.StartsOn, week.EndsOn, WeekContract.Current);
        context.WeekPeriods.Add(period);
        var request = new GenerationRequest(
            Guid.CreateVersion7(), Guid.CreateVersion7(), new string('c', 64), rule.Id,
            BranchScope.LorettaId, period.Id, ActivationOriginSchemas.ManualReference,
            "VALIDATION-SNAPSHOT-001", actor, Now.AddHours(1).AddMinutes(1));
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
        Assert.Equal(policy.Id, obligation.ValidationPolicyVersionId);

        var historicalRequest = new GenerationRequest(
            Guid.CreateVersion7(), Guid.CreateVersion7(), new string('d', 64), rule.Id,
            BranchScope.LorettaId, period.Id, ActivationOriginSchemas.ManualReference,
            "VALIDATION-HISTORICAL-NULL", actor, Now);
        var historical = new WorkObligation(
            Guid.CreateVersion7(), taskVersion.Id, BranchScope.LorettaId, period.Id,
            historicalRequest.Id, historicalRequest.OriginReference);
        context.GenerationRequests.Add(historicalRequest);
        await context.SaveChangesAsync();
        context.WorkObligations.Add(historical);
        historicalRequest.LinkObligation(historical.Id);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        Assert.Null(historical.ValidationPolicyVersionId);

        await Assert.ThrowsAsync<PostgresException>(() => context.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE work_obligation
            SET validation_policy_version_id = {policy.Id}
            WHERE id = {historical.Id}
            """));
        await Assert.ThrowsAsync<PostgresException>(() => context.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE validation_policy_version
            SET validator_role = {"PISO_VENTAS"}
            WHERE id = {policy.Id}
            """));
        var otherPolicy = await context.ValidationPolicyVersions.AsNoTracking()
            .SingleAsync(item =>
                item.TaskDefinitionId == TaskDefinitionCatalog.Require("TAR-0005").Id &&
                item.Status == VersionStatuses.Current);
        var wrongRequest = new GenerationRequest(
            Guid.CreateVersion7(), Guid.CreateVersion7(), new string('e', 64), rule.Id,
            BranchScope.LorettaId, period.Id, ActivationOriginSchemas.ManualReference,
            "WRONG-POLICY", actor, Now.AddHours(1));
        context.GenerationRequests.Add(wrongRequest);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        await Assert.ThrowsAsync<PostgresException>(() => context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO work_obligation
                (id, task_definition_version_id, branch_id, period_id, generation_request_id,
                 origin_reference, validation_policy_version_id, execution_status, row_version)
            VALUES ({Guid.CreateVersion7()}, {taskVersion.Id}, {BranchScope.LorettaId}, {period.Id},
                    {wrongRequest.Id}, {"WRONG-POLICY"}, {otherPolicy.Id}, {WorkObligationStatuses.Pending}, 1)
            """));
    }

    private static PutValidationPolicyCommand NewValidationPolicy(
        Guid actor,
        Guid releaseId,
        string taskCode,
        Guid? key = null,
        long? expected = null)
    {
        var policy = ValidationPolicyCatalog.Require(taskCode);
        return new PutValidationPolicyCommand(
            actor,
            key ?? Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            taskCode,
            releaseId,
            true,
            policy.ExecutorRole,
            ValidationPolicyValues.ImmediateSuperior,
            policy.ValidatorRole,
            ValidationPolicyValues.AllowedResults,
            expected);
    }
}
