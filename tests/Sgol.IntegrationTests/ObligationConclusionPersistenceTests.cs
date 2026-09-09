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
using Sgol.Web.Infrastructure.Persistence;
using Sgol.Web.Infrastructure.Persistence.Auditing;
using Sgol.Web.Infrastructure.Persistence.Bootstrap;
using Sgol.Web.Infrastructure.Persistence.Configuration;
using Sgol.Web.Infrastructure.Persistence.Evidence;
using Sgol.Web.Infrastructure.Persistence.Execution;
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
        Assert.Empty(await context.OutboxEvents.AsNoTracking().ToListAsync());
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
        string taskCode = "TAR-0018")
    {
        await using (var reset = CreateContext())
        {
            await reset.Database.EnsureDeletedAsync();
            await reset.Database.MigrateAsync();
        }

        var configurationActor = await SeedActorAsync("HU022-DIRECTION", CanonicalRole.Direction);
        var actor = await SeedActorAsync("HU022-RESPONSIBLE", CanonicalRole.Subcoordination);
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
            if (requirement.RequirementCode == "CHECKLIST_COMPLETO")
            {
                var payload = JsonDocument.Parse("""
                    {"schemaVersion":1,"productCorrect":true,"zoneAndFamilyCorrect":true,"stableFormation":true,"labelsVisible":true,"alignmentConsistent":true,"occupancyJustified":true,"clean":true,"intact":true,"signageCorrect":true,"matchesPlanogramOrList":true}
                    """);
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
        clock,
        NewUuidGenerator());

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
