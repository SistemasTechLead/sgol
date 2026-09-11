using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Sgol.Assignment.Contracts;
using Sgol.BuildingBlocks.Identifiers;
using Sgol.BuildingBlocks.Time;
using Sgol.BuildingBlocks.Versioning;
using Sgol.Configuration.Contracts;
using Sgol.Evidence.Contracts;
using Sgol.Execution.Contracts;
using Sgol.Generation.Contracts;
using Sgol.Identity.Contracts;
using Sgol.Organization.Contracts;
using Sgol.Planning.Contracts;
using Sgol.Reporting.Contracts;
using Sgol.Web.Infrastructure.Persistence;
using Sgol.Web.Infrastructure.Persistence.Auditing;
using Sgol.Web.Infrastructure.Persistence.Bootstrap;
using Sgol.Web.Infrastructure.Persistence.Configuration;
using Sgol.Web.Infrastructure.Persistence.Evidence;
using Sgol.Web.Infrastructure.Persistence.Execution;
using Sgol.Web.Infrastructure.Persistence.Validation;
using Sgol.Validation.Contracts;
using Sgol.Web.Infrastructure.Persistence.Generation;
using Sgol.Web.Infrastructure.Persistence.Versioning;
using Testcontainers.PostgreSql;
using Xunit;

namespace Sgol.IntegrationTests;

public sealed class ObligationConclusionPersistenceTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 9, 8, 22, 30, 0, TimeSpan.Zero);
    private readonly PostgreSqlContainer postgres = PostgreSqlPersistenceTests.CreateContainerForTests();

    public Task InitializeAsync() => postgres.StartAsync();
    public async Task DisposeAsync() => await postgres.DisposeAsync();

    [Fact]
    public async Task CompleteEvidenceCreatesOneExactResultTransitionsOnceAndReplays()
    {
        var fixture = await ResetAndCreateObligationAsync(withCompleteEvidence: true);
        await using var context = CreateContext();
        var service = ConclusionService(context, new FixedClock(Now));
        var command = new ConcludeObligationCommand(
            fixture.ActorId, Guid.CreateVersion7(), Guid.CreateVersion7(), fixture.ObligationId, 1);

        var first = await service.ConcludeAsync(command);
        var replay = await service.ConcludeAsync(command);
        await Assert.ThrowsAsync<ObligationConclusionIdempotencyConflictException>(() =>
            service.ConcludeAsync(command with { ExpectedRowVersion = 2 }));
        await Assert.ThrowsAsync<ObligationAlreadyConcludedException>(() => service.ConcludeAsync(new(
            fixture.ActorId, Guid.CreateVersion7(), Guid.CreateVersion7(), fixture.ObligationId, 2)));

        Assert.Equal(first.ObligationId, replay.ObligationId);
        Assert.Equal(first.RowVersion, replay.RowVersion);
        Assert.Equal(first.ExecutionResult.Id, replay.ExecutionResult.Id);
        Assert.Equal(first.ExecutionResult.EvidenceReviewSnapshotId, replay.ExecutionResult.EvidenceReviewSnapshotId);
        Assert.True(JsonElement.DeepEquals(
            first.ExecutionResult.ResultPayload.RootElement,
            replay.ExecutionResult.ResultPayload.RootElement));
        Assert.Equal(WorkObligationStatuses.Concluded, first.ExecutionStatus);
        Assert.Equal(Now, first.ConcludedAt);
        Assert.Equal(fixture.ActorId, first.ConcludedBy);
        Assert.Equal(2, first.RowVersion);
        Assert.Equal(ObligationConclusionResultCodes.Concluded, first.ExecutionResult.ResultCode);
        Assert.Equal("{\"schemaVersion\":1}", first.ExecutionResult.ResultPayload.RootElement.GetRawText());
        Assert.Equal(first.ConcludedAt, first.ExecutionResult.RecordedAt);
        Assert.Equal(first.ConcludedBy, first.ExecutionResult.RecordedBy);
        Assert.Single(await context.ExecutionResults.AsNoTracking()
            .Where(item => item.ObligationId == fixture.ObligationId).ToListAsync());
        var snapshot = await context.EvidenceReviewSnapshots.AsNoTracking()
            .SingleAsync(item => item.Id == first.ExecutionResult.EvidenceReviewSnapshotId);
        Assert.Equal(EvidenceReviewResults.Complete, snapshot.Result);
        Assert.Equal(fixture.PolicyId, snapshot.EvidencePolicyVersionId);
        Assert.Empty(snapshot.MissingRequirements.RootElement.EnumerateArray());
        Assert.Contains(await context.AuditEvents.AsNoTracking().ToListAsync(), audit =>
            audit.Action == "OBLIGATION_CONCLUDED" && audit.ResourceId == fixture.ObligationId);
        var requirement = await context.ValidationRequirements.AsNoTracking()
            .SingleAsync(item => item.ObligationId == fixture.ObligationId);
        Assert.Equal(ValidationStatuses.Pending, requirement.Status);
        Assert.Equal(first.ConcludedAt, requirement.CreatedAt);
        Assert.Empty(await context.OutboxEvents.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task ValidValidatorIssuesReplaysAndReplacesWithoutChangingExecution()
    {
        var fixture = await ResetAndCreateObligationAsync(withCompleteEvidence: true, taskCode: "TAR-0008");
        await using (var conclusionContext = CreateContext())
        {
            await ConclusionService(conclusionContext, new FixedClock(Now)).ConcludeAsync(new(
                fixture.ActorId, Guid.CreateVersion7(), Guid.CreateVersion7(), fixture.ObligationId, 1));
        }
        var validator = await SeedActorAsync("HU028-VALIDATOR", CanonicalRole.Administration);
        await using var context = CreateContext();
        var service = new EfValidationDecisionService(context,
            new EfEvidenceConclusionReviewService(context, NewUuidGenerator()), new FixedClock(Now.AddMinutes(20)), NewUuidGenerator());
        var issue = new IssueValidationDecisionCommand(validator, Guid.CreateVersion7(), Guid.CreateVersion7(),
            fixture.ObligationId, 1, ValidationResults.NotFulfilled, "La evidencia vigente no acredita el criterio.", null);

        var selfValidation = await Assert.ThrowsAsync<ValidationDecisionException>(() => service.IssueAsync(issue with
        {
            ActorUserId = fixture.ActorId,
            IdempotencyKey = Guid.CreateVersion7(),
        }));
        Assert.Equal("AUTOVALIDACION_NO_PERMITIDA", selfValidation.Code);
        Assert.Empty(await context.ValidationDecisionVersions.AsNoTracking().ToListAsync());

        var first = await service.IssueAsync(issue);
        var replay = await service.IssueAsync(issue);

        Assert.True(replay.Replayed);
        Assert.Equal(first.Decision.DecisionVersionId, replay.Decision.DecisionVersionId);
        Assert.Equal(WorkObligationStatuses.Concluded, first.History.ExecutionStatus);
        Assert.Equal(ValidationStatuses.Current, first.Decision.Status);

        var duplicate = await Assert.ThrowsAsync<ValidationDecisionException>(() => service.IssueAsync(issue with
        {
            IdempotencyKey = Guid.CreateVersion7(),
        }));
        Assert.Equal("DECISION_VALIDACION_YA_EXISTE", duplicate.Code);
        var stale = await Assert.ThrowsAsync<ValidationDecisionException>(() => service.ReplaceAsync(new(
            validator, Guid.CreateVersion7(), Guid.CreateVersion7(), first.Decision.DecisionVersionId, 2,
            ValidationResults.Fulfilled, "La evidencia vigente acredita el criterio.", "Corrección motivada.")));
        Assert.Equal("VERSION_CONFLICT", stale.Code);
        Assert.Single(await context.ValidationDecisionVersions.AsNoTracking().ToListAsync());

        var replacement = await service.ReplaceAsync(new(validator, Guid.CreateVersion7(), Guid.CreateVersion7(),
            first.Decision.DecisionVersionId, 1, ValidationResults.Fulfilled,
            "La evidencia vigente acredita el criterio.", "Se corrige la decisión después de una segunda revisión."));
        Assert.Equal(2, replacement.History.RowVersion);
        Assert.Equal(2, replacement.History.Decisions.Count);
        Assert.Single(replacement.History.Decisions, item => item.Status == ValidationStatuses.Current);
        Assert.Single(replacement.History.Decisions, item => item.Status == ValidationStatuses.Superseded);
        Assert.Equal(WorkObligationStatuses.Concluded,
            await context.WorkObligations.AsNoTracking().Where(item => item.Id == fixture.ObligationId)
                .Select(item => item.ExecutionStatus).SingleAsync());
        Assert.Single(await context.ExecutionResults.AsNoTracking().Where(item => item.ObligationId == fixture.ObligationId).ToListAsync());
        Assert.Equal([2, 1], replacement.History.Decisions.Select(item => item.VersionNo));
        Assert.Contains(await context.AuditEvents.AsNoTracking().ToListAsync(), item => item.Action == "VALIDATION_DECISION_REPLACED");
    }

    [Fact]
    public async Task Hu031ReadsOnlyInferiorsAndPendingAuthorityWithoutWriteEffects()
    {
        var fixture = await ResetAndCreateObligationAsync(withCompleteEvidence: true, taskCode: "TAR-0008");
        await using (var conclusionContext = CreateContext())
        {
            await ConclusionService(conclusionContext, new FixedClock(Now)).ConcludeAsync(new(
                fixture.ActorId, Guid.CreateVersion7(), Guid.CreateVersion7(), fixture.ObligationId, 1));
        }
        var administration = await SeedActorAsync("HU031-ADMIN", CanonicalRole.Administration);
        var direction = await SeedActorAsync("HU031-DIRECTION", CanonicalRole.Direction);
        var sales = await SeedActorAsync("HU031-SALES", CanonicalRole.SalesFloor);

        await using var context = CreateContext();
        var before = new
        {
            Requirements = await context.ValidationRequirements.CountAsync(),
            Decisions = await context.ValidationDecisionVersions.CountAsync(),
            Audits = await context.AuditEvents.CountAsync(),
            Idempotency = await context.IdempotencyRecords.CountAsync(),
            Outbox = await context.OutboxEvents.CountAsync(),
        };
        var reader = new EfObligationQueryReader(context, new FixedClock(Now.AddMinutes(20)));

        var supervision = await reader.ReadSupervisionAsync(new(
            administration, CanonicalRole.Subcoordination, null, 2026, 37,
            WorkObligationStatuses.Concluded, null, 25));
        var supervised = Assert.Single(supervision.Items);
        Assert.Equal(fixture.ObligationId, supervised.Obligation.ObligationId);
        Assert.Equal(CanonicalRole.Subcoordination, supervised.ResponsibleLevel);
        Assert.NotEmpty(supervised.CurrentEvidence);
        Assert.Equal(ValidationStatuses.Pending, supervised.Validation.Requirement?.Status);
        Assert.Null(supervised.Validation.CurrentDecision);

        var ordinary = Assert.Single((await reader.ReadPendingValidationsAsync(new(
            administration, null, null, 2026, 37, null, 25))).Items);
        Assert.Equal(ValidationAuthorityTypes.Ordinary, ordinary.AvailableAuthority.AuthorityType);
        Assert.Equal(PendingMaterializationStatuses.Materialized, ordinary.MaterializationStatus);
        Assert.Equal("\"1\"", ordinary.DecisionEtag);

        var escalated = Assert.Single((await reader.ReadPendingValidationsAsync(new(
            direction, null, null, null, null, null, 25))).Items);
        Assert.Equal(ValidationAuthorityTypes.Escalation, escalated.AvailableAuthority.AuthorityType);
        Assert.True(escalated.AvailableAuthority.EscalationReasonRequired);

        Assert.Empty((await reader.ReadSupervisionAsync(new(
            fixture.ActorId, null, null, null, null, null, null, 25))).Items);
        await Assert.ThrowsAsync<SupervisionAccessDeniedException>(() => reader.ReadSupervisionAsync(new(
            sales, null, null, null, null, null, null, 25)));

        var after = new
        {
            Requirements = await context.ValidationRequirements.CountAsync(),
            Decisions = await context.ValidationDecisionVersions.CountAsync(),
            Audits = await context.AuditEvents.CountAsync(),
            Idempotency = await context.IdempotencyRecords.CountAsync(),
            Outbox = await context.OutboxEvents.CountAsync(),
        };
        Assert.Equal(before, after);
        Assert.Empty(context.ChangeTracker.Entries());
    }

    [Fact]
    public async Task Hu031DerivesLegacyPendingWithoutMaterializingRequirement()
    {
        var fixture = await ResetAndCreateObligationAsync(withCompleteEvidence: true, taskCode: "TAR-0008");
        await using (var conclusionContext = CreateContext())
        {
            var conclusion = new EfObligationConclusionService(
                conclusionContext,
                new EfEvidenceConclusionReviewService(conclusionContext, NewUuidGenerator()),
                new NoopValidationRequirementWriter(),
                new FixedClock(Now),
                NewUuidGenerator());
            await conclusion.ConcludeAsync(new(
                fixture.ActorId, Guid.CreateVersion7(), Guid.CreateVersion7(), fixture.ObligationId, 1));
        }
        var administration = await SeedActorAsync("HU031-DERIVED", CanonicalRole.Administration);
        await using var context = CreateContext();
        var reader = new EfObligationQueryReader(context, new FixedClock(Now.AddMinutes(20)));

        var pending = Assert.Single((await reader.ReadPendingValidationsAsync(new(
            administration, null, null, null, null, null, 25))).Items);

        Assert.Equal(PendingMaterializationStatuses.Derived, pending.MaterializationStatus);
        Assert.Null(pending.ValidationRequirement);
        Assert.Equal("\"2\"", pending.DecisionEtag);
        Assert.Empty(await context.ValidationRequirements.AsNoTracking().ToListAsync());
        Assert.Empty(await context.ValidationDecisionVersions.AsNoTracking().ToListAsync());
        Assert.Empty(context.ChangeTracker.Entries());
    }

    [Fact]
    public async Task Hu029ReconcilesFiveIndicatorsWithCurrentScopeAndNoReadEffects()
    {
        var fixture = await ResetAndCreateObligationAsync(withCompleteEvidence: false, taskCode: "TAR-0008");
        var administration = await SeedActorAsync("HU029-ADMIN", CanonicalRole.Administration);
        var administrationPeer = await SeedActorAsync("HU029-ADMIN-PEER", CanonicalRole.Administration);
        var direction = await SeedActorAsync("HU029-DIRECTION", CanonicalRole.Direction);
        var sales = await SeedActorAsync("HU029-SALES-ZERO", CanonicalRole.SalesFloor);

        await using (var setup = CreateContext())
        {
            await AddIndicatorObligationAsync(setup, fixture, administration, "hu029-pending", concluded: false);
            await AddIndicatorObligationAsync(setup, fixture, administration, "hu029-concluded", concluded: true);
            await AddIndicatorObligationAsync(setup, fixture, administration, "hu029-fulfilled",
                concluded: true, validationResult: ValidationResults.Fulfilled);
            await AddIndicatorObligationAsync(setup, fixture, administration, "hu029-not-fulfilled",
                concluded: true, validationResult: ValidationResults.NotFulfilled);
            await AddIndicatorObligationAsync(setup, fixture, administration, "hu029-replaced",
                concluded: true, validationResult: ValidationResults.Fulfilled,
                supersededResult: ValidationResults.NotFulfilled);
            await AddIndicatorObligationAsync(setup, fixture, administration, "hu029-policy-null",
                concluded: true, validationPolicy: false);
            await AddIndicatorObligationAsync(setup, fixture, administration, "hu029-unassigned",
                concluded: false, assigned: false);
        }

        await using var context = CreateContext();
        var before = new
        {
            Obligations = await context.WorkObligations.CountAsync(),
            Assignments = await context.AssignmentVersions.CountAsync(),
            Requirements = await context.ValidationRequirements.CountAsync(),
            Decisions = await context.ValidationDecisionVersions.CountAsync(),
            Audits = await context.AuditEvents.CountAsync(),
            Idempotency = await context.IdempotencyRecords.CountAsync(),
            Outbox = await context.OutboxEvents.CountAsync(),
        };
        var reader = new EfObligationQueryReader(context, new FixedClock(Now.AddMinutes(30)));
        var responsiblePersonId = await UserPersonId(context, fixture.ActorId);
        var administrationPeerPersonId = await UserPersonId(context, administrationPeer);
        var salesPersonId = await UserPersonId(context, sales);

        var result = await reader.ReadAsync(new(
            administration, 2026, 37, null, null, null, 100));

        Assert.Equal(7, result.Snapshot.BaseObligationsCount);
        Assert.Equal(new IndicatorCount(2, 7), result.Snapshot.Pending);
        Assert.Equal(new IndicatorCount(5, 7), result.Snapshot.Concluded);
        Assert.Equal(new IndicatorCount(3, 7), result.Snapshot.Validated);
        Assert.Equal(new IndicatorCount(1, 7), result.Snapshot.NonCompliant);
        Assert.Equal(2, result.Snapshot.ActiveLoadByPerson.Denominator);
        Assert.Equal(2, result.Snapshot.ActiveLoadByPerson.Items.Sum(item => item.Count));
        Assert.Contains(result.Snapshot.ActiveLoadByPerson.Items,
            item => item.Person.Id == responsiblePersonId && item.Count == 2);
        Assert.DoesNotContain(result.Snapshot.ActiveLoadByPerson.Items,
            item => item.Person.StableCode == "HU029-ADMIN-PEER");
        Assert.Contains(result.Snapshot.ActiveLoadByPerson.Items,
            item => item.Person.Id == salesPersonId && item.Count == 0);
        Assert.Equal(
            [CanonicalRole.Administration, CanonicalRole.Subcoordination, CanonicalRole.SalesFloor],
            result.Snapshot.Scope.IncludedLevels);
        Assert.Equal(Now.AddMinutes(30), result.QueriedAt);
        Assert.Null(result.NextCursor);

        var firstPage = await reader.ReadAsync(new(
            administration, 2026, 37, null, null, null, 2));
        Assert.Equal(2, firstPage.Snapshot.ActiveLoadByPerson.Items.Count);
        Assert.NotNull(firstPage.NextCursor);
        var secondPage = await reader.ReadAsync(new(
            administration, 2026, 37, null, null, firstPage.NextCursor, 2));
        Assert.DoesNotContain(secondPage.Snapshot.ActiveLoadByPerson.Items,
            item => firstPage.Snapshot.ActiveLoadByPerson.Items.Any(previous => previous.Person.Id == item.Person.Id));
        Assert.Equal(firstPage.Snapshot.Pending, secondPage.Snapshot.Pending);

        var hiddenPeer = await reader.ReadAsync(new(
            administration, 2026, 37, null, administrationPeerPersonId, null, 25));
        Assert.Equal(0, hiddenPeer.Snapshot.BaseObligationsCount);
        Assert.Null(hiddenPeer.Snapshot.Scope.ResponsiblePersonId);
        Assert.Empty(hiddenPeer.Snapshot.ActiveLoadByPerson.Items);

        var hidden = await reader.ReadAsync(new(
            administration, 2026, 37, CanonicalRole.Direction, null, null, 25));
        Assert.Equal(0, hidden.Snapshot.BaseObligationsCount);
        Assert.Empty(hidden.Snapshot.Scope.IncludedLevels);
        Assert.Empty(hidden.Snapshot.ActiveLoadByPerson.Items);

        var ownSales = await reader.ReadAsync(new(
            sales, 2026, 37, null, null, null, 25));
        Assert.Equal(0, ownSales.Snapshot.BaseObligationsCount);
        Assert.Equal(salesPersonId, Assert.Single(ownSales.Snapshot.ActiveLoadByPerson.Items).Person.Id);
        Assert.Equal([CanonicalRole.SalesFloor], ownSales.Snapshot.Scope.IncludedLevels);

        var limitedDirection = await reader.ReadAsync(new(
            direction, 2026, 37, null, null, null, 100));
        Assert.Equal(7, limitedDirection.Snapshot.BaseObligationsCount);
        Assert.Equal(2, limitedDirection.Snapshot.Pending.Count);

        var emptyPeriod = await reader.ReadAsync(new(
            administration, 2026, 38, null, null, null, 100));
        Assert.Equal(new IndicatorCount(0, 0), emptyPeriod.Snapshot.Pending);
        Assert.All(emptyPeriod.Snapshot.ActiveLoadByPerson.Items, item => Assert.Equal(0, item.Count));

        var after = new
        {
            Obligations = await context.WorkObligations.CountAsync(),
            Assignments = await context.AssignmentVersions.CountAsync(),
            Requirements = await context.ValidationRequirements.CountAsync(),
            Decisions = await context.ValidationDecisionVersions.CountAsync(),
            Audits = await context.AuditEvents.CountAsync(),
            Idempotency = await context.IdempotencyRecords.CountAsync(),
            Outbox = await context.OutboxEvents.CountAsync(),
        };
        Assert.Equal(before, after);
        Assert.Empty(context.ChangeTracker.Entries());
    }

    [Fact]
    public async Task EscalationAndDirectionSelfValidationRequireTheirReasonAndAreAudited()
    {
        var escalatedFixture = await ResetAndCreateObligationAsync(withCompleteEvidence: true, taskCode: "TAR-0008");
        await using (var conclusionContext = CreateContext())
        {
            await ConclusionService(conclusionContext, new FixedClock(Now)).ConcludeAsync(new(
                escalatedFixture.ActorId, Guid.CreateVersion7(), Guid.CreateVersion7(), escalatedFixture.ObligationId, 1));
        }
        var direction = await SeedActorAsync("HU028-ESCALATION", CanonicalRole.Direction);
        await using (var context = CreateContext())
        {
            var service = ValidationService(context, Now.AddMinutes(20));
            var missingReason = await Assert.ThrowsAsync<ValidationDecisionException>(() => service.IssueAsync(new(
                direction, Guid.CreateVersion7(), Guid.CreateVersion7(), escalatedFixture.ObligationId, 1,
                ValidationResults.Incomplete, "La evidencia requiere revisión adicional.", null)));
            Assert.Equal("MOTIVO_REQUERIDO", missingReason.Code);
            Assert.Empty(await context.ValidationDecisionVersions.AsNoTracking().ToListAsync());

            var escalated = await service.IssueAsync(new(direction, Guid.CreateVersion7(), Guid.CreateVersion7(),
                escalatedFixture.ObligationId, 1, ValidationResults.Incomplete,
                "La evidencia requiere revisión adicional.", "Ausencia justificada del superior inmediato."));
            Assert.Equal(ValidationAuthorityTypes.Escalation, escalated.Decision.AuthorityType);
            Assert.Contains(await context.AuditEvents.AsNoTracking().ToListAsync(), item =>
                item.Action == "VALIDATION_DECISION_ESCALATED" && item.ResourceId == escalated.History.ValidationRequirement!.RequirementId);
        }

        var selfFixture = await ResetAndCreateObligationAsync(withCompleteEvidence: true, taskCode: "TAR-0008",
            responsibleRole: CanonicalRole.Direction);
        await using (var conclusionContext = CreateContext())
        {
            await ConclusionService(conclusionContext, new FixedClock(Now)).ConcludeAsync(new(
                selfFixture.ActorId, Guid.CreateVersion7(), Guid.CreateVersion7(), selfFixture.ObligationId, 1));
        }
        await using (var context = CreateContext())
        {
            var selfValidated = await ValidationService(context, Now.AddMinutes(20)).IssueAsync(new(
                selfFixture.ActorId, Guid.CreateVersion7(), Guid.CreateVersion7(), selfFixture.ObligationId, 1,
                ValidationResults.Fulfilled, "La evidencia vigente acredita el criterio.", null));
            Assert.Equal(ValidationAuthorityTypes.DirectionSelfValidation, selfValidated.Decision.AuthorityType);
            Assert.Contains(await context.AuditEvents.AsNoTracking().ToListAsync(), item =>
                item.Action == "VALIDATION_DIRECTION_SELF_VALIDATED" && item.ResourceId == selfValidated.History.ValidationRequirement!.RequirementId);
        }
    }

    [Fact]
    public async Task ConcurrentIssueAndReplacementLeaveOneCurrentLinearHistory()
    {
        var fixture = await ResetAndCreateObligationAsync(withCompleteEvidence: true, taskCode: "TAR-0008");
        await using (var conclusionContext = CreateContext())
        {
            await ConclusionService(conclusionContext, new FixedClock(Now)).ConcludeAsync(new(
                fixture.ActorId, Guid.CreateVersion7(), Guid.CreateVersion7(), fixture.ObligationId, 1));
        }
        var validator = await SeedActorAsync("HU028-RACE", CanonicalRole.Administration);
        await using var issueContextA = CreateContext();
        await using var issueContextB = CreateContext();
        var issueA = CaptureAsync(() => ValidationService(issueContextA, Now.AddMinutes(20)).IssueAsync(new(
            validator, Guid.CreateVersion7(), Guid.CreateVersion7(), fixture.ObligationId, 1,
            ValidationResults.Incomplete, "Primera intención concurrente.", null)));
        var issueB = CaptureAsync(() => ValidationService(issueContextB, Now.AddMinutes(20)).IssueAsync(new(
            validator, Guid.CreateVersion7(), Guid.CreateVersion7(), fixture.ObligationId, 1,
            ValidationResults.NotFulfilled, "Segunda intención concurrente.", null)));
        var issueOutcomes = await Task.WhenAll(issueA, issueB);
        var issued = Assert.Single(issueOutcomes, item => item.Result is not null).Result!;
        var issueFailure = Assert.Single(issueOutcomes, item => item.Error is not null).Error!;
        Assert.True(issueFailure.Code is "DECISION_VALIDACION_YA_EXISTE" or "VALIDACION_CONCURRENCIA_CONFLICTO");

        await using var replacementContextA = CreateContext();
        await using var replacementContextB = CreateContext();
        var replacementA = CaptureAsync(() => ValidationService(replacementContextA, Now.AddMinutes(21)).ReplaceAsync(new(
            validator, Guid.CreateVersion7(), Guid.CreateVersion7(), issued.Decision.DecisionVersionId, 1,
            ValidationResults.Fulfilled, "Primera sustitución concurrente.", "Corrección concurrente A.")));
        var replacementB = CaptureAsync(() => ValidationService(replacementContextB, Now.AddMinutes(21)).ReplaceAsync(new(
            validator, Guid.CreateVersion7(), Guid.CreateVersion7(), issued.Decision.DecisionVersionId, 1,
            ValidationResults.Fulfilled, "Segunda sustitución concurrente.", "Corrección concurrente B.")));
        var replacementOutcomes = await Task.WhenAll(replacementA, replacementB);
        Assert.Single(replacementOutcomes, item => item.Result is not null);
        var replacementFailure = Assert.Single(replacementOutcomes, item => item.Error is not null).Error!;
        Assert.True(replacementFailure.Code is "DECISION_VALIDACION_NO_ENCONTRADA" or "VALIDACION_CONCURRENCIA_CONFLICTO");

        await using var verification = CreateContext();
        var requirement = await verification.ValidationRequirements.AsNoTracking().SingleAsync();
        var decisions = await verification.ValidationDecisionVersions.AsNoTracking().OrderBy(item => item.VersionNo).ToListAsync();
        Assert.Equal(2, requirement.RowVersion);
        Assert.Equal(2, decisions.Count);
        Assert.Single(decisions, item => item.Status == ValidationStatuses.Current);
        Assert.Single(decisions, item => item.Status == ValidationStatuses.Superseded);
        Assert.Equal(decisions[0].Id, decisions[1].SupersedesId);
    }

    [Fact]
    public async Task IncompleteWrongResponsibleAndStaleVersionHaveNoConclusionEffects()
    {
        var fixture = await ResetAndCreateObligationAsync(withCompleteEvidence: false);
        var deniedActors = new[]
        {
            await SeedActorAsync("HU022-SUPERIOR", CanonicalRole.Direction),
            await SeedActorAsync("HU022-PEER", CanonicalRole.Subcoordination),
            await SeedActorAsync("HU022-LOWER", CanonicalRole.SalesFloor),
        };
        await using var context = CreateContext();
        var service = ConclusionService(context, new FixedClock(Now));

        foreach (var deniedActor in deniedActors)
        {
            await Assert.ThrowsAsync<ObligationConclusionNotFoundException>(() => service.ConcludeAsync(new(
                deniedActor, Guid.CreateVersion7(), Guid.CreateVersion7(), fixture.ObligationId, 1)));
        }
        await Assert.ThrowsAsync<ObligationConclusionVersionConflictException>(() => service.ConcludeAsync(new(
            fixture.ActorId, Guid.CreateVersion7(), Guid.CreateVersion7(), fixture.ObligationId, 2)));
        var missing = await Assert.ThrowsAsync<ObligationEvidenceMissingException>(() => service.ConcludeAsync(new(
            fixture.ActorId, Guid.CreateVersion7(), Guid.CreateVersion7(), fixture.ObligationId, 1)));

        Assert.NotEmpty(missing.RequirementCodes);
        var obligation = await context.WorkObligations.AsNoTracking().SingleAsync(item => item.Id == fixture.ObligationId);
        Assert.Equal(WorkObligationStatuses.Pending, obligation.ExecutionStatus);
        Assert.Equal(1, obligation.RowVersion);
        Assert.Null(obligation.ConcludedAt);
        Assert.Null(obligation.ConcludedBy);
        Assert.Empty(await context.ExecutionResults.AsNoTracking().ToListAsync());
        Assert.Empty(await context.EvidenceReviewSnapshots.AsNoTracking().ToListAsync());
        Assert.DoesNotContain(await context.AuditEvents.AsNoTracking().ToListAsync(), audit =>
            audit.Action == "OBLIGATION_CONCLUDED");
    }

    [Fact]
    public async Task DifferentKeysRaceToOneResultAndPostgreSqlRejectsMutation()
    {
        var fixture = await ResetAndCreateObligationAsync(withCompleteEvidence: true);
        await using var firstContext = CreateContext();
        await using var secondContext = CreateContext();
        var attempts = await Task.WhenAll(
            Record.ExceptionAsync(() => ConclusionService(firstContext, new FixedClock(Now)).ConcludeAsync(new(
                fixture.ActorId, Guid.CreateVersion7(), Guid.CreateVersion7(), fixture.ObligationId, 1))),
            Record.ExceptionAsync(() => ConclusionService(secondContext, new FixedClock(Now)).ConcludeAsync(new(
                fixture.ActorId, Guid.CreateVersion7(), Guid.CreateVersion7(), fixture.ObligationId, 1))));

        Assert.Single(attempts, exception => exception is null);
        Assert.Single(attempts, exception => exception is ObligationConclusionException);
        await using var verification = CreateContext();
        var result = Assert.Single(await verification.ExecutionResults.AsNoTracking().ToListAsync());
        var update = await Assert.ThrowsAsync<PostgresException>(() => verification.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE execution_result SET result_code = {"ALTERADA"} WHERE id = {result.Id}
            """));
        Assert.Equal(PostgresErrorCodes.CheckViolation, update.SqlState);
        var direct = await Assert.ThrowsAsync<PostgresException>(() => verification.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE work_obligation SET concluded_at = NULL WHERE id = {fixture.ObligationId}
            """));
        Assert.Equal(PostgresErrorCodes.CheckViolation, direct.SqlState);
    }

    [Fact]
    public async Task ObsoleteSnapshotIsNotLinkedAndAuditFailureRollsBackConclusion()
    {
        var fixture = await ResetAndCreateObligationAsync(withCompleteEvidence: true);
        Guid oldSnapshotId;
        await using (var snapshotContext = CreateContext())
        {
            await using var transaction = await snapshotContext.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            _ = await snapshotContext.WorkObligations
                .FromSqlInterpolated($"SELECT * FROM work_obligation WHERE id = {fixture.ObligationId} FOR UPDATE")
                .AsTracking().SingleAsync();
            var review = await new EfEvidenceConclusionReviewService(snapshotContext, NewUuidGenerator()).ReviewAsync(new(
                fixture.ActorId, Guid.CreateVersion7(), fixture.ObligationId, Now));
            oldSnapshotId = review.SnapshotId!.Value;
            await snapshotContext.SaveChangesAsync();
            await transaction.CommitAsync();
        }

        await using (var replacementContext = CreateContext())
        {
            var item = await replacementContext.EvidenceItems.AsTracking()
                .SingleAsync(entry => entry.ObligationId == fixture.ObligationId && entry.RequirementCode == "CHECKLIST_COMPLETO");
            var current = await replacementContext.EvidenceVersions.AsTracking()
                .SingleAsync(version => version.EvidenceItemId == item.Id && version.Status == EvidenceVersionStatuses.Current);
            current.Supersede();
            item.Advance(item.RowVersion);
            using var replacement = JsonDocument.Parse("""
                {"schemaVersion":1,"productCorrect":true,"zoneAndFamilyCorrect":true,"stableFormation":true,"labelsVisible":true,"alignmentConsistent":true,"occupancyJustified":true,"clean":true,"intact":true,"signageCorrect":true,"matchesPlanogramOrList":true}
                """);
            replacementContext.EvidenceVersions.Add(new EvidenceVersion(
                Guid.CreateVersion7(), item.Id, 2, replacement, fixture.ActorId, Now.AddMinutes(10),
                "Corrección sintética", current.Id));
            await replacementContext.SaveChangesAsync();
        }

        await using (var conclusionContext = CreateContext())
        {
            var result = await ConclusionService(conclusionContext, new FixedClock(Now.AddMinutes(11))).ConcludeAsync(new(
                fixture.ActorId, Guid.CreateVersion7(), Guid.CreateVersion7(), fixture.ObligationId, 1));
            Assert.NotEqual(oldSnapshotId, result.ExecutionResult.EvidenceReviewSnapshotId);
        }

        var rollbackFixture = await ResetAndCreateObligationAsync(withCompleteEvidence: true);
        var duplicateAuditId = Guid.CreateVersion7();
        await using (var setup = CreateContext())
        {
            setup.AuditEvents.Add(new AuditEvent
            {
                Id = duplicateAuditId,
                OccurredAt = Now,
                ActorUserId = rollbackFixture.ActorId,
                ActorType = "APP_USER",
                Action = "SYNTHETIC_EXISTING_EVENT",
                ResourceType = "WORK_OBLIGATION",
                ResourceId = rollbackFixture.ObligationId,
                BranchId = BranchScope.LorettaId,
                CorrelationId = Guid.CreateVersion7(),
                Outcome = "SUCCESS",
            });
            await setup.SaveChangesAsync();
        }

        await using (var failingContext = CreateContext())
        {
            var service = new EfObligationConclusionService(
                failingContext,
                new EfEvidenceConclusionReviewService(failingContext, NewUuidGenerator()),
                new NoopValidationRequirementWriter(),
                new FixedClock(Now),
                new SequenceUuidGenerator(Guid.CreateVersion7(), duplicateAuditId));
            await Assert.ThrowsAsync<ObligationConclusionInconsistentException>(() => service.ConcludeAsync(new(
                rollbackFixture.ActorId, Guid.CreateVersion7(), Guid.CreateVersion7(), rollbackFixture.ObligationId, 1)));
        }

        await using var verification = CreateContext();
        var pending = await verification.WorkObligations.AsNoTracking()
            .SingleAsync(item => item.Id == rollbackFixture.ObligationId);
        Assert.Equal(WorkObligationStatuses.Pending, pending.ExecutionStatus);
        Assert.Empty(await verification.ExecutionResults.AsNoTracking().ToListAsync());
        Assert.DoesNotContain(await verification.IdempotencyRecords.AsNoTracking().ToListAsync(), record =>
            record.ResourceType == "EXECUTION_RESULT");
    }

    private sealed class NoopValidationRequirementWriter : IValidationRequirementWriter
    {
        public Task EnsureAsync(EnsureValidationRequirementCommand command, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    [Fact]
    public async Task Tar0092CurrentFEnt001AloneDeterminesWhetherConditionalPhotoBlocks()
    {
        var noDifference = await ResetAndCreateObligationAsync(false, "TAR-0092");
        await using (var context = CreateContext())
        {
            await AddTar0092EvidenceAsync(context, noDifference, hasDifference: false);
            var conclusion = await ConclusionService(context, new FixedClock(Now)).ConcludeAsync(new(
                noDifference.ActorId, Guid.CreateVersion7(), Guid.CreateVersion7(), noDifference.ObligationId, 1));
            Assert.Equal(WorkObligationStatuses.Concluded, conclusion.ExecutionStatus);
        }

        var difference = await ResetAndCreateObligationAsync(false, "TAR-0092");
        await using (var context = CreateContext())
        {
            await AddTar0092EvidenceAsync(context, difference, hasDifference: true);
            var missing = await Assert.ThrowsAsync<ObligationEvidenceMissingException>(() =>
                ConclusionService(context, new FixedClock(Now)).ConcludeAsync(new(
                    difference.ActorId, Guid.CreateVersion7(), Guid.CreateVersion7(), difference.ObligationId, 1)));
            Assert.Contains("FOTO_DIFERENCIA_DANO", missing.RequirementCodes);
            Assert.Equal(WorkObligationStatuses.Pending,
                await context.WorkObligations.AsNoTracking().Where(item => item.Id == difference.ObligationId)
                    .Select(item => item.ExecutionStatus).SingleAsync());
        }
    }

    private async Task<Fixture> ResetAndCreateObligationAsync(
        bool withCompleteEvidence,
        string taskCode = "TAR-0018",
        string? responsibleRole = null)
    {
        await using (var reset = CreateContext())
        {
            await reset.Database.EnsureDeletedAsync();
            await reset.Database.MigrateAsync();
        }

        var configurationActor = await SeedActorAsync("HU022-DIRECTION", CanonicalRole.Direction);
        var actor = await SeedActorAsync("HU022-RESPONSIBLE", responsibleRole ?? ValidationPolicyCatalog.Require(taskCode).ExecutorRole);
        await using var context = CreateContext();
        var audit = new AuditTransaction(context);
        var generator = NewUuidGenerator();
        var releaseService = new EfConfigurationReleaseService(
            context, audit, new VersioningTransaction(context, audit), new FixedClock(Now), generator);
        var taskService = new EfTaskDefinitionService(context, audit, releaseService, new FixedClock(Now), generator);
        var policyService = new EfEvidencePolicyService(context, audit, new FixedClock(Now), generator);
        var definitionsRelease = await releaseService.CreateDraftAsync(NewRelease(configurationActor));
        using var empty = JsonDocument.Parse("{}");
        foreach (var task in TaskDefinitionCatalog.All)
        {
            await taskService.CreateVersionAsync(new(
                configurationActor, Guid.CreateVersion7(), Guid.CreateVersion7(), task.TaskCode,
                definitionsRelease.Id, 1, empty.RootElement));
        }
        await releaseService.PublishAsync(new(
            configurationActor, Guid.CreateVersion7(), Guid.CreateVersion7(), definitionsRelease.Id,
            definitionsRelease.RowVersion, Now.AddMinutes(1), "HU-022 definitions"));

        var policyRelease = await releaseService.CreateDraftAsync(NewRelease(configurationActor));
        foreach (var pair in EvidencePolicyCatalog.All)
        {
            await policyService.PutAsync(new(
                configurationActor, Guid.CreateVersion7(), Guid.CreateVersion7(), pair.Key, policyRelease.Id,
                pair.Value.Select(item => new EvidenceRequirementInput(item.Code, item.Kind, item.ConditionCode)).ToArray(), null));
        }
        await releaseService.PublishAsync(new(
            configurationActor, Guid.CreateVersion7(), Guid.CreateVersion7(), policyRelease.Id,
            policyRelease.RowVersion, Now.AddMinutes(2), "HU-022 evidence policies"));

        var validationPolicyService = new EfValidationPolicyService(context, new AuditTransaction(context),
            new FixedClock(Now), generator);
        var validationRelease = await releaseService.CreateDraftAsync(NewRelease(configurationActor));
        foreach (var pair in ValidationPolicyCatalog.All)
        {
            await validationPolicyService.PutAsync(new(configurationActor, Guid.CreateVersion7(), Guid.CreateVersion7(),
                pair.Key, validationRelease.Id, true, pair.Value.ExecutorRole, ValidationPolicyValues.ImmediateSuperior,
                pair.Value.ValidatorRole, ValidationPolicyValues.AllowedResults, null));
        }
        await releaseService.PublishAsync(new(configurationActor, Guid.CreateVersion7(), Guid.CreateVersion7(), validationRelease.Id,
            validationRelease.RowVersion, Now.AddMinutes(3), "HU-028 validation policies"));

        var taskDefinition = TaskDefinitionCatalog.Require(taskCode);
        var taskVersion = await context.TaskDefinitionVersions.AsNoTracking()
            .SingleAsync(item => item.TaskDefinitionId == taskDefinition.Id && item.Status == VersionStatuses.Current);
        var policy = await context.EvidencePolicyVersions.AsNoTracking()
            .SingleAsync(item => item.TaskDefinitionId == taskDefinition.Id && item.Status == VersionStatuses.Current);
        using var noSchedule = JsonDocument.Parse("null");
        var rule = new ActivationRuleVersion(
            Guid.CreateVersion7(), taskDefinition.Id, taskVersion.Id, policyRelease.Id, null, 1,
            ActivationModes.Manual, noSchedule.RootElement, ActivationOriginSchemas.ManualReference);
        var rulePlan = VersioningRules.PlanPublication(
            rule.ToVersionRecord(), null, [], rule.RowVersion, Now.AddMinutes(2), "HU-022 activation");
        rule.ApplyPublished(rulePlan.Published);
        var week = WeekContract.Calculate(2026, 37);
        var period = new WeekPeriod(
            Guid.CreateVersion7(), BranchScope.LorettaId, 2026, 37, week.StartsOn, week.EndsOn, WeekContract.Current);
        var request = new GenerationRequest(
            Guid.CreateVersion7(), Guid.CreateVersion7(), new string('a', 64), rule.Id,
            BranchScope.LorettaId, period.Id, ActivationOriginSchemas.ManualReference,
            $"HU022-{Guid.CreateVersion7():N}", actor, Now.AddMinutes(3));
        context.ActivationRuleVersions.Add(rule);
        context.WeekPeriods.Add(period);
        context.GenerationRequests.Add(request);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var obligation = await new EfWorkObligationMaterializer(
            context, new AuditTransaction(context), new FixedClock(Now.AddMinutes(3)), NewUuidGenerator())
            .MaterializeAsync(new(request.Id, Guid.CreateVersion7()));
        var personId = await context.AppUsers.AsNoTracking()
            .Where(user => user.Id == actor).Select(user => user.PersonId).SingleAsync();
        using var explanation = JsonDocument.Parse("{}");
        InternalNoticeTestData.AddAssignmentWithNotice(context, new AssignmentVersion(
            Guid.CreateVersion7(), obligation.ObligationId, personId, AssignmentVersionStatuses.Current,
            AssignmentTypes.Automatic, explanation, Now.AddMinutes(4)));
        await context.SaveChangesAsync();

        if (withCompleteEvidence)
        {
            await AddCompleteEvidenceAsync(context, actor, obligation.ObligationId, policy.Id);
        }

        return new(actor, obligation.ObligationId, policy.Id);
    }

    private static async Task AddIndicatorObligationAsync(
        SgolDbContext context,
        Fixture fixture,
        Guid validatorUserId,
        string origin,
        bool concluded,
        string? validationResult = null,
        string? supersededResult = null,
        bool validationPolicy = true,
        bool assigned = true)
    {
        var template = await context.WorkObligations.AsNoTracking()
            .SingleAsync(item => item.Id == fixture.ObligationId);
        var templateRequest = await context.GenerationRequests.AsNoTracking()
            .SingleAsync(item => item.Id == template.GenerationRequestId);
        var responsiblePersonId = await UserPersonId(context, fixture.ActorId);
        var requestId = Guid.CreateVersion7();
        var obligationId = Guid.CreateVersion7();
        var request = new GenerationRequest(
            requestId,
            Guid.CreateVersion7(),
            Convert.ToHexStringLower(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(origin))),
            templateRequest.RuleVersionId,
            BranchScope.LorettaId,
            template.PeriodId,
            ActivationOriginSchemas.ManualReference,
            origin,
            fixture.ActorId,
            Now.AddMinutes(5));
        var obligation = new WorkObligation(
            obligationId,
            template.TaskDefinitionVersionId,
            BranchScope.LorettaId,
            template.PeriodId,
            requestId,
            origin,
            template.EvidencePolicyVersionId,
            validationPolicy ? template.ValidationPolicyVersionId : null);
        context.GenerationRequests.Add(request);
        await context.SaveChangesAsync();

        context.WorkObligations.Add(obligation);
        Guid? assignmentId = null;
        if (assigned)
        {
            assignmentId = Guid.CreateVersion7();
            InternalNoticeTestData.AddAssignmentWithNotice(context, new AssignmentVersion(
                assignmentId.Value,
                obligationId,
                responsiblePersonId,
                AssignmentVersionStatuses.Current,
                AssignmentTypes.Automatic,
                JsonDocument.Parse("{}"),
                Now.AddMinutes(6)));
        }
        await context.SaveChangesAsync();
        request.LinkObligation(obligationId);
        await context.SaveChangesAsync();

        if (concluded)
        {
            await ObligationConclusionTestData.ConcludeAsync(context, obligationId, Now.AddMinutes(10));
        }

        if (validationResult is null) return;
        Assert.True(concluded);
        Assert.True(assigned);
        Assert.NotNull(template.ValidationPolicyVersionId);
        var service = new EfValidationDecisionService(
            context,
            new EfEvidenceConclusionReviewService(context, NewUuidGenerator()),
            new FixedClock(Now.AddMinutes(15)),
            NewUuidGenerator());
        var issue = await service.IssueAsync(new(
            validatorUserId,
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            obligationId,
            2,
            supersededResult ?? validationResult,
            "La evidencia vigente acredita el criterio sintético.",
            null));

        if (supersededResult is not null)
        {
            await service.ReplaceAsync(new(
                validatorUserId,
                Guid.CreateVersion7(),
                Guid.CreateVersion7(),
                issue.Decision.DecisionVersionId,
                issue.History.RowVersion,
                validationResult,
                "La evidencia vigente acredita el criterio sintético.",
                "Sustitución sintética motivada."));
        }
    }

    private static Task<Guid> UserPersonId(SgolDbContext context, Guid userId) =>
        context.AppUsers.AsNoTracking().Where(user => user.Id == userId)
            .Select(user => user.PersonId).SingleAsync();

    private static async Task AddCompleteEvidenceAsync(
        SgolDbContext context, Guid actor, Guid obligationId, Guid policyId)
    {
        var requirements = await context.EvidenceRequirementVersions.AsNoTracking()
            .Where(item => item.PolicyVersionId == policyId).OrderBy(item => item.Ordinal).ToListAsync();
        foreach (var requirement in requirements)
        {
            var item = new EvidenceItem(
                Guid.CreateVersion7(), obligationId, policyId, requirement.Id,
                requirement.RequirementCode, requirement.Kind, Now.AddMinutes(5));
            context.EvidenceItems.Add(item);
            if (requirement.Kind is EvidenceRequirementKinds.StructuredData or EvidenceRequirementKinds.DigitalRecord or EvidenceRequirementKinds.StructuredChecklist)
            {
                var payload = CompleteStructuredPayload(requirement.RequirementCode);
                context.EvidenceVersions.Add(new EvidenceVersion(
                    Guid.CreateVersion7(), item.Id, 1, payload, actor, Now.AddMinutes(5)));
                continue;
            }

            var file = CreateCleanFile(actor, obligationId, policyId, requirement, item.Id);
            context.FileObjects.Add(file);
            context.EvidenceVersions.Add(new EvidenceVersion(
                Guid.CreateVersion7(), item.Id, 1, file.Id, actor, Now.AddMinutes(8)));
        }
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
    }

    private static JsonDocument CompleteStructuredPayload(string requirementCode) => JsonDocument.Parse(requirementCode switch
    {
        "CHECKLIST_COMPLETO" => """
            {"schemaVersion":1,"productCorrect":true,"zoneAndFamilyCorrect":true,"stableFormation":true,"labelsVisible":true,"alignmentConsistent":true,"occupancyJustified":true,"clean":true,"intact":true,"signageCorrect":true,"matchesPlanogramOrList":true}
            """,
        "SECUENCIA" => """{"schemaVersion":1,"sequenceSummary":"Secuencia sintética"}""",
        "DECISION" => """{"schemaVersion":1,"decisionSummary":"Decisión sintética","decidedAt":"2026-09-07T20:00:00Z"}""",
        "FUNDAMENTO" => """{"schemaVersion":1,"foundationSummary":"Fundamento sintético"}""",
        "AVISO_INTERNO" => """{"schemaVersion":1,"noticeReference":"AVI-01","notifiedAt":"2026-09-07T20:00:00Z"}""",
        _ => throw new InvalidOperationException($"No existe payload sintético completo para {requirementCode}."),
    });

    private static async Task AddTar0092EvidenceAsync(
        SgolDbContext context,
        Fixture fixture,
        bool hasDifference)
    {
        var requirements = await context.EvidenceRequirementVersions.AsNoTracking()
            .Where(item => item.PolicyVersionId == fixture.PolicyId).OrderBy(item => item.Ordinal).ToListAsync();
        foreach (var requirement in requirements.Where(item => item.RequirementCode != "FOTO_DIFERENCIA_DANO"))
        {
            var item = new EvidenceItem(
                Guid.CreateVersion7(), fixture.ObligationId, fixture.PolicyId, requirement.Id,
                requirement.RequirementCode, requirement.Kind, Now.AddMinutes(5));
            context.EvidenceItems.Add(item);
            if (requirement.RequirementCode == "F_ENT_001")
            {
                var payload = JsonDocument.Parse($$"""
                    {"schemaVersion":1,"formCode":"F-ENT-001","formReference":"HU022-FENT","completedAt":"2026-09-08T22:20:00Z","hasDifference":{{hasDifference.ToString().ToLowerInvariant()}},"hasDamage":false}
                    """);
                context.EvidenceVersions.Add(new EvidenceVersion(
                    Guid.CreateVersion7(), item.Id, 1, payload, fixture.ActorId, Now.AddMinutes(5)));
            }
            else
            {
                var file = CreateCleanFile(
                    fixture.ActorId, fixture.ObligationId, fixture.PolicyId, requirement, item.Id, "NOTA");
                context.FileObjects.Add(file);
                context.EvidenceVersions.Add(new EvidenceVersion(
                    Guid.CreateVersion7(), item.Id, 1, file.Id, fixture.ActorId, Now.AddMinutes(8)));
            }
        }

        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
    }

    private static FileObject CreateCleanFile(
        Guid actor,
        Guid obligationId,
        Guid policyId,
        EvidenceRequirementVersion requirement,
        Guid itemId,
        string? documentSubtype = null)
    {
        var hash = Convert.ToHexStringLower(SHA256.HashData(Guid.CreateVersion7().ToByteArray()));
        var photograph = requirement.Kind == EvidenceRequirementKinds.Photograph;
        var mediaType = photograph ? "image/png" : "application/pdf";
        var file = new FileObject(
            Guid.CreateVersion7(), BranchScope.LorettaId, obligationId, policyId, requirement.Id,
            requirement.RequirementCode, requirement.Kind, documentSubtype,
            $"v1/{hash[..2]}/{hash[2..4]}/{hash}", photograph ? "evidence.png" : "evidence.pdf",
            mediaType, 128, hash, actor, Now.AddMinutes(5));
        file.ConfirmUpload(Now.AddMinutes(6));
        file.MarkClean(mediaType, "synthetic", Now.AddMinutes(7));
        file.Link(itemId, Now.AddMinutes(8));
        return file;
    }

    private async Task<Guid> SeedActorAsync(string code, string role)
    {
        await using var context = CreateContext();
        var personId = Guid.CreateVersion7();
        var userId = Guid.CreateVersion7();
        context.People.Add(new Person
        {
            Id = personId,
            StableCode = code,
            DisplayName = "Persona sintética HU-022",
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
            SecurityStamp = $"hu022-{userId:N}",
        });
        context.IdentityCredentials.Add(new IdentityCredential
        {
            UserId = userId,
            UserName = $"hu022.{userId:N}",
            NormalizedUserName = $"HU022.{userId:N}",
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

    private static EfObligationConclusionService ConclusionService(SgolDbContext context, IClock clock) => new(
        context,
        new EfEvidenceConclusionReviewService(context, NewUuidGenerator()),
        new EfValidationRequirementWriter(context, NewUuidGenerator()),
        clock,
        NewUuidGenerator());

    private static EfValidationDecisionService ValidationService(SgolDbContext context, DateTimeOffset at) => new(
        context,
        new EfEvidenceConclusionReviewService(context, NewUuidGenerator()),
        new FixedClock(at),
        NewUuidGenerator());

    private static async Task<(ValidationMutationResult? Result, ValidationDecisionException? Error)> CaptureAsync(
        Func<Task<ValidationMutationResult>> action)
    {
        try { return (await action(), null); }
        catch (ValidationDecisionException exception) { return (null, exception); }
    }

    private SgolDbContext CreateContext() => new(
        new DbContextOptionsBuilder<SgolDbContext>().UseNpgsql(postgres.GetConnectionString()).Options);

    private static CreateConfigurationReleaseCommand NewRelease(Guid actor) =>
        new(actor, Guid.CreateVersion7(), Guid.CreateVersion7());

    private static Uuid7Generator NewUuidGenerator() => new(new FixedClock(Now));

    private sealed record Fixture(Guid ActorId, Guid ObligationId, Guid PolicyId);

    private sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow { get; } = now;
    }

    private sealed class SequenceUuidGenerator(params Guid[] values) : IUuidGenerator
    {
        private readonly Queue<Guid> values = new(values);

        public Guid NewUuid() => values.Dequeue();
    }
}
