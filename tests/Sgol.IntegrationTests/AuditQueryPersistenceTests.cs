using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Sgol.Assignment.Contracts;
using Sgol.Auditing.Contracts;
using Sgol.BuildingBlocks.Identifiers;
using Sgol.BuildingBlocks.Time;
using Sgol.BuildingBlocks.Versioning;
using Sgol.Configuration.Contracts;
using Sgol.Evidence.Contracts;
using Sgol.Execution.Contracts;
using Sgol.Generation.Contracts;
using Sgol.Identity.Contracts;
using Sgol.Notifications.Contracts;
using Sgol.Organization.Contracts;
using Sgol.Planning.Contracts;
using Sgol.Validation.Contracts;
using Sgol.Web.Infrastructure.Persistence;
using Sgol.Web.Infrastructure.Persistence.Auditing;
using Sgol.Web.Infrastructure.Persistence.Bootstrap;
using Testcontainers.PostgreSql;
using Xunit;

namespace Sgol.IntegrationTests;

public sealed class AuditQueryPersistenceTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 9, 11, 18, 0, 0, TimeSpan.Zero);
    private static readonly string[] OwnOrLowerRelations = ["OWN", "LOWER"];
    private readonly PostgreSqlContainer postgres = PostgreSqlPersistenceTests.CreateContainerForTests();
    private readonly EphemeralDataProtectionProvider protection = new();

    public Task InitializeAsync() => postgres.StartAsync();
    public async Task DisposeAsync() => await postgres.DisposeAsync();

    [Fact]
    public async Task CanonicalRolesReceiveOnlyOwnAndLowerHistoricalSubjects()
    {
        var scenario = await ResetAndSeedAsync();
        await using var context = CreateContext();
        var reader = Reader(context);
        var request = Request(scenario.Direction.UserId) with { Action = "AUDIT_EVENT_DELETE_ATTEMPTED" };
        var direction = await reader.ReadAsync(request);
        Assert.Equal(5, direction.Items.Count);

        var administration = await reader.ReadAsync(request with { ActorUserId = scenario.Administration.UserId });
        Assert.Equal(3, administration.Items.Count);
        Assert.DoesNotContain(administration.Items, item => item.Actor.UserId == scenario.Direction.UserId);
        Assert.DoesNotContain(administration.Items, item => item.Actor.UserId == scenario.AdministrationPeer.UserId);

        var subcoordination = await reader.ReadAsync(request with { ActorUserId = scenario.Subcoordination.UserId });
        Assert.Equal(2, subcoordination.Items.Count);
        Assert.All(subcoordination.Items, item => Assert.Contains(item.Scope.Relation, OwnOrLowerRelations));

        var sales = await reader.ReadAsync(request with { ActorUserId = scenario.Sales.UserId });
        Assert.Equal(scenario.Sales.UserId, Assert.Single(sales.Items).Actor.UserId);
        Assert.Equal("OWN", sales.Items[0].Scope.Relation);
    }

    [Fact]
    public async Task TraceUsesPersistedLinksReturnsFourStagesAndMinimizesPayload()
    {
        var scenario = await ResetAndSeedAsync();
        await using var context = CreateContext();
        var before = await CountsAsync(context);
        var page = await Reader(context).ReadAsync(Request(scenario.Direction.UserId) with
        {
            TraceObligationId = scenario.ObligationId,
        });

        Assert.Equal(4, page.Items.Count);
        Assert.Equal(page.Items.OrderBy(item => item.OccurredAt).Select(item => item.Id),
            page.Items.Select(item => item.Id));
        Assert.Equal(new AuditCompleteness(true, true, true, true), page.Completeness);
        Assert.Equal("DEPENDENCY", page.Items[0].Scope.Relation);
        Assert.All(page.Items, item =>
        {
            Assert.NotEqual(Guid.Empty, item.Actor.UserId);
            Assert.NotEqual(default, item.OccurredAt);
            Assert.NotNull(item.Change.After);
            Assert.DoesNotContain("passwordHash", item.Change.After!.Keys);
            Assert.DoesNotContain("signedUrl", item.Change.After.Keys);
            Assert.False(item.Reason.Provided);
        });
        Assert.Contains(page.Items, item => item.Change.After!.ContainsKey("status") &&
            item.Change.OmittedFieldCount == 2);
        Assert.Equal(before, await CountsAsync(context));
    }

    [Fact]
    public async Task CursorIsActorBoundAndConcurrentInsertAfterFenceDoesNotMixPages()
    {
        var scenario = await ResetAndSeedAsync();
        await using var context = CreateContext();
        var reader = Reader(context);
        var request = Request(scenario.Direction.UserId) with { Limit = 2 };
        var first = await reader.ReadAsync(request);
        Assert.Equal(2, first.Items.Count);
        Assert.NotNull(first.NextCursor);

        context.AuditEvents.Add(Audit(Guid.CreateVersion7(), scenario.Direction.UserId,
            "CONCURRENT", "AUDIT_EVENT", Guid.CreateVersion7(), Now.AddMinutes(1)));
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        var second = await reader.ReadAsync(request with { Cursor = first.NextCursor });
        Assert.DoesNotContain(second.Items, item => item.Action == "CONCURRENT");
        Assert.DoesNotContain(second.Items, item => first.Items.Any(previous => previous.Id == item.Id));
        await Assert.ThrowsAsync<AuditCursorInvalidException>(() => reader.ReadAsync(request with
        {
            ActorUserId = scenario.Administration.UserId,
            Cursor = first.NextCursor,
        }));
    }

    [Fact]
    public async Task DeleteSecurityWriterAddsOneExactEventAndTriggerRejectsChanges()
    {
        var scenario = await ResetAndSeedAsync();
        await using var context = CreateContext();
        var target = Guid.CreateVersion7();
        var correlation = Guid.CreateVersion7();
        var before = await context.AuditEvents.CountAsync();
        var writer = new EfAuditSecurityEventWriter(context, new AuditTransaction(context),
            new FixedClock(Now), new SequenceGenerator(Guid.CreateVersion7()));

        await writer.WriteDeleteAttemptAsync(scenario.Direction.UserId, correlation, target);

        Assert.Equal(before + 1, await context.AuditEvents.CountAsync());
        var audit = await context.AuditEvents.AsNoTracking().SingleAsync(item =>
            item.Action == "AUDIT_EVENT_DELETE_ATTEMPTED" && item.CorrelationId == correlation);
        Assert.Equal(scenario.Direction.UserId, audit.ActorUserId);
        Assert.Equal("USER", audit.ActorType);
        Assert.Equal("AUDIT_EVENT", audit.ResourceType);
        Assert.Equal(target, audit.ResourceId);
        Assert.Equal(BranchScope.LorettaId, audit.BranchId);
        Assert.Equal("METHOD_NOT_ALLOWED", audit.Reason);
        Assert.Equal("REJECTED", audit.Outcome);
        Assert.Null(audit.BeforeData); Assert.Null(audit.AfterData); Assert.Null(audit.SourceIpHash);

        var update = await Assert.ThrowsAsync<PostgresException>(() => context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE audit_event SET outcome = 'ALTERED' WHERE id = {audit.Id}"));
        Assert.Equal("55000", update.SqlState);
        var delete = await Assert.ThrowsAsync<PostgresException>(() => context.Database.ExecuteSqlInterpolatedAsync(
            $"DELETE FROM audit_event WHERE id = {audit.Id}"));
        Assert.Equal("55000", delete.SqlState);
        Assert.True(await context.AuditEvents.AsNoTracking().AnyAsync(item => item.Id == audit.Id));
    }

    [Fact]
    public async Task DetailConvergesForMissingAndOutOfHierarchyEvents()
    {
        var scenario = await ResetAndSeedAsync();
        await using var context = CreateContext();
        var peerEvent = await context.AuditEvents.AsNoTracking().SingleAsync(item =>
            item.Action == "AUDIT_EVENT_DELETE_ATTEMPTED" &&
            item.ActorUserId == scenario.AdministrationPeer.UserId);
        var reader = Reader(context);

        await Assert.ThrowsAsync<AuditEventNotFoundException>(() =>
            reader.FindAsync(scenario.Sales.UserId, peerEvent.Id));
        await Assert.ThrowsAsync<AuditEventNotFoundException>(() =>
            reader.FindAsync(scenario.Sales.UserId, Guid.CreateVersion7()));
    }

    [Fact]
    public async Task TraceWithHistoricalPeerSubjectFailsAsOneHiddenChain()
    {
        var scenario = await ResetAndSeedAsync();
        await using var context = CreateContext();
        var originalAssignmentId = await context.AssignmentVersions.AsNoTracking()
            .Where(item => item.ObligationId == scenario.ObligationId)
            .Select(item => item.Id)
            .SingleAsync();
        var peerAssignment = new AssignmentVersion(Guid.CreateVersion7(), scenario.ObligationId,
            scenario.AdministrationPeer.PersonId, AssignmentVersionStatuses.Superseded,
            AssignmentTypes.Correction, JsonDocument.Parse("{}"), Now.AddHours(-4).AddMinutes(-30),
            "Corrección histórica sintética", scenario.Direction.UserId, originalAssignmentId);
        InternalNoticeTestData.AddAssignmentWithNotice(context, peerAssignment);
        context.AuditEvents.Add(Audit(Guid.CreateVersion7(), scenario.AdministrationPeer.UserId,
            "ASSIGNMENT_CORRECTED", "ASSIGNMENT_VERSION", peerAssignment.Id,
            Now.AddHours(-4).AddMinutes(-30)));
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        await Assert.ThrowsAsync<AuditEventNotFoundException>(() => Reader(context).ReadAsync(
            Request(scenario.Administration.UserId) with { TraceObligationId = scenario.ObligationId }));
    }

    [Fact]
    public async Task DirectionPreservesOpaqueSystemAndInactiveHistoricalActors()
    {
        var scenario = await ResetAndSeedAsync();
        await using var context = CreateContext();
        var inactive = await context.AppUsers.SingleAsync(item => item.Id == scenario.Administration.UserId);
        inactive.Status = AccountStatus.Inactive;
        var systemEvent = new AuditEvent
        {
            Id = Guid.CreateVersion7(),
            OccurredAt = Now.AddHours(-2),
            ActorUserId = null,
            ActorType = "SYSTEM_LEGACY",
            Action = "UNKNOWN_HISTORICAL_ACTION",
            ResourceType = "UNKNOWN_HISTORICAL_RESOURCE",
            ResourceId = Guid.CreateVersion7(),
            BranchId = null,
            CorrelationId = Guid.CreateVersion7(),
            Outcome = "UNKNOWN_HISTORICAL_OUTCOME",
        };
        var inactiveActorEvent = Audit(Guid.CreateVersion7(), scenario.Administration.UserId,
            "HISTORICAL_ACTOR_EVENT", "UNKNOWN_HISTORICAL_RESOURCE", Guid.CreateVersion7(), Now.AddHours(-1));
        context.AuditEvents.AddRange(systemEvent, inactiveActorEvent);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var page = await Reader(context).ReadAsync(Request(scenario.Direction.UserId));
        var system = Assert.Single(page.Items, item => item.Id == systemEvent.Id);
        Assert.Equal("SYSTEM_LEGACY", system.Actor.Type);
        Assert.Null(system.Actor.UserId);
        Assert.Equal("UNKNOWN_HISTORICAL_ACTION", system.Action);
        Assert.Equal("UNKNOWN_HISTORICAL_RESOURCE", system.Resource.Type);
        Assert.Equal("UNKNOWN_HISTORICAL_OUTCOME", system.Outcome);
        Assert.Equal(scenario.Administration.UserId,
            Assert.Single(page.Items, item => item.Id == inactiveActorEvent.Id).Actor.UserId);
    }

    [Theory]
    [InlineData("account")]
    [InlineData("employment")]
    [InlineData("role")]
    public async Task ObsoleteReaderAuthorityIsDenied(string obsoletePart)
    {
        var scenario = await ResetAndSeedAsync();
        await using var context = CreateContext();
        if (obsoletePart == "account")
        {
            var account = await context.AppUsers.SingleAsync(item => item.Id == scenario.Direction.UserId);
            account.Status = AccountStatus.Inactive;
        }
        else if (obsoletePart == "employment")
        {
            var employment = await context.EmploymentVersions.SingleAsync(item =>
                item.PersonId == scenario.Direction.PersonId && item.ValidTo == null);
            context.EmploymentVersions.Add(employment.CreateSuccessor(
                Guid.CreateVersion7(), EmploymentStatus.Inactive, Now.AddMinutes(-1)));
        }
        else
        {
            var role = await context.RoleAssignmentVersions.SingleAsync(item =>
                item.UserId == scenario.Direction.UserId && item.ValidTo == null);
            role.Status = RoleAssignmentStatus.Superseded;
            role.ValidTo = Now.AddMinutes(-1);
        }
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        await Assert.ThrowsAsync<AuditAccessDeniedException>(() =>
            Reader(context).ReadAsync(Request(scenario.Direction.UserId)));
    }

    private async Task<Scenario> ResetAndSeedAsync()
    {
        await using var context = CreateContext();
        await context.Database.EnsureDeletedAsync();
        await context.Database.MigrateAsync();
        var direction = AddPerson(context, "AUD-DIR", CanonicalRole.Direction);
        var administration = AddPerson(context, "AUD-ADM", CanonicalRole.Administration);
        var administrationPeer = AddPerson(context, "AUD-ADM-PEER", CanonicalRole.Administration);
        var subcoordination = AddPerson(context, "AUD-SUB", CanonicalRole.Subcoordination);
        var sales = AddPerson(context, "AUD-SALES", CanonicalRole.SalesFloor);
        await context.SaveChangesAsync();

        foreach (var actor in new[] { direction, administration, administrationPeer, subcoordination, sales })
            context.AuditEvents.Add(Audit(Guid.CreateVersion7(), actor.UserId,
                "AUDIT_EVENT_DELETE_ATTEMPTED", "AUDIT_EVENT", Guid.CreateVersion7(), Now.AddHours(-8)));
        await context.SaveChangesAsync();

        var releaseId = Guid.CreateVersion7();
        var release = new ConfigurationRelease(releaseId, BranchScope.LorettaId);
        release.ApplyPublished(Published(releaseId), 1, direction.UserId, Now.AddDays(-2));
        context.ConfigurationReleases.Add(release);
        var task = TaskDefinitionCatalog.Require("TAR-0008");
        var taskVersionId = Guid.CreateVersion7();
        var taskVersion = new TaskDefinitionVersion(taskVersionId, task.Id, 1, 1,
            JsonDocument.Parse("{}"), releaseId);
        taskVersion.ApplyPublished(Published(taskVersionId), true);
        var ruleId = Guid.CreateVersion7();
        var rule = new ActivationRuleVersion(ruleId, task.Id, taskVersionId, releaseId, null, 1,
            ActivationModes.Manual, JsonDocument.Parse("null").RootElement, ActivationOriginSchemas.ManualReference);
        rule.ApplyPublished(Published(ruleId));
        var week = WeekContract.Calculate(2026, 37);
        var period = new WeekPeriod(Guid.CreateVersion7(), BranchScope.LorettaId, 2026, 37,
            week.StartsOn, week.EndsOn, WeekContract.Current);
        context.TaskDefinitionVersions.Add(taskVersion);
        context.ActivationRuleVersions.Add(rule);
        context.WeekPeriods.Add(period);
        await context.SaveChangesAsync();

        var evidencePolicyId = await ObligationConclusionTestData.EnsurePolicyAsync(context, taskVersionId, Now.AddDays(-2));
        var validationDefinition = ValidationPolicyCatalog.Require(task.TaskCode);
        var validationPolicyId = Guid.CreateVersion7();
        var validationPolicy = new ValidationPolicyVersion(validationPolicyId, task.Id, taskVersionId, releaseId,
            null, 1, true, validationDefinition.ExecutorRole, ValidationPolicyValues.ImmediateSuperior,
            validationDefinition.ValidatorRole, ValidationPolicyValues.AllowedResults);
        validationPolicy.ApplyPublished(Published(validationPolicyId));
        context.ValidationPolicyVersions.Add(validationPolicy);
        await context.SaveChangesAsync();

        var generation = new GenerationRequest(Guid.CreateVersion7(), Guid.CreateVersion7(), new string('a', 64),
            ruleId, BranchScope.LorettaId, period.Id, ActivationOriginSchemas.ManualReference,
            "AUDIT-TRACE", direction.UserId, Now.AddHours(-7));
        context.GenerationRequests.Add(generation);
        await context.SaveChangesAsync();
        var obligation = new WorkObligation(Guid.CreateVersion7(), taskVersionId, BranchScope.LorettaId,
            period.Id, generation.Id, "AUDIT-TRACE", evidencePolicyId, validationPolicyId);
        context.WorkObligations.Add(obligation);
        await context.SaveChangesAsync();
        generation.LinkObligation(obligation.Id);
        var assignment = new AssignmentVersion(Guid.CreateVersion7(), obligation.Id, sales.PersonId,
            AssignmentVersionStatuses.Current, AssignmentTypes.Automatic, JsonDocument.Parse("{}"), Now.AddHours(-6));
        context.AssignmentVersions.Add(assignment);
        context.InternalNotices.Add(new InternalNotice(
            Guid.CreateVersion7(), sales.UserId, assignment.Id, assignment.AssignedAt));
        await context.SaveChangesAsync();

        var validationAt = Now.AddHours(-4);
        await ObligationConclusionTestData.ConcludeAsync(context, obligation.Id, validationAt);
        var evidence = await context.EvidenceItems.AsNoTracking()
            .Where(item => item.ObligationId == obligation.Id)
            .OrderBy(item => item.CreatedAt).ThenBy(item => item.Id)
            .FirstAsync();
        var snapshot = await context.EvidenceReviewSnapshots.AsNoTracking()
            .SingleAsync(item => item.ObligationId == obligation.Id);
        var validation = new ValidationRequirement(Guid.CreateVersion7(), obligation.Id, validationPolicyId,
            validationAt);
        validation.Resolve(validationAt, validation.RowVersion);
        var decision = new ValidationDecisionVersion(Guid.CreateVersion7(), validation.Id, 1,
            ValidationResults.Incomplete, "Fundamento sintético", ValidationAuthorityTypes.Ordinary,
            administration.UserId, administration.PersonId, CanonicalRole.Administration, assignment.Id,
            sales.PersonId, validationAt, null, null, snapshot.Id);
        context.ValidationRequirements.Add(validation);
        context.ValidationDecisionVersions.Add(decision);
        await context.SaveChangesAsync();

        context.AuditEvents.AddRange(
            Audit(Guid.CreateVersion7(), direction.UserId, "CONFIGURATION_RELEASE_PUBLISHED",
                "TASK_DEFINITION_VERSION", taskVersionId, Now.AddHours(-7)),
            Audit(Guid.CreateVersion7(), direction.UserId, "ASSIGNMENT_CREATED",
                "ASSIGNMENT_VERSION", assignment.Id, Now.AddHours(-6)),
            Audit(Guid.CreateVersion7(), sales.UserId, "EVIDENCE_CONTRIBUTED",
                "EVIDENCE_ITEM", evidence.Id, Now.AddHours(-5)),
            Audit(Guid.CreateVersion7(), administration.UserId, "VALIDATION_DECISION_ISSUED",
                "VALIDATION_REQUIREMENT", validation.Id, validationAt));
        await context.SaveChangesAsync();
        return new(direction, administration, administrationPeer, subcoordination, sales, obligation.Id);
    }

    private static SeededPerson AddPerson(SgolDbContext context, string code, string role)
    {
        var personId = Guid.CreateVersion7(); var userId = Guid.CreateVersion7();
        context.People.Add(new Person { Id = personId, StableCode = code, DisplayName = $"Persona {code}", CreatedAt = Now.AddDays(-30) });
        context.EmploymentVersions.Add(new EmploymentVersion(Guid.CreateVersion7(), personId,
            BranchScope.LorettaId, EmploymentStatus.Active, Now.AddDays(-30)));
        context.AppUsers.Add(new AppUser
        {
            Id = userId,
            PersonId = personId,
            Status = AccountStatus.Active,
            MustChangePassword = false,
            MfaEnrolledAt = Now.AddDays(-30),
            SecurityStamp = $"audit-{userId:N}",
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
        return new(personId, userId);
    }

    private static AuditEvent Audit(Guid id, Guid actor, string action, string resourceType, Guid resourceId,
        DateTimeOffset occurredAt) => new()
        {
            Id = id,
            OccurredAt = occurredAt,
            ActorUserId = actor,
            ActorType = "USER",
            Action = action,
            ResourceType = resourceType,
            ResourceId = resourceId,
            BranchId = BranchScope.LorettaId,
            CorrelationId = Guid.CreateVersion7(),
            Outcome = "SUCCESS",
            AfterData = JsonDocument.Parse("{\"schemaVersion\":1,\"status\":\"VIGENTE\",\"passwordHash\":\"secret\",\"signedUrl\":\"https://secret\"}"),
        };

    private static AuditQueryRequest Request(Guid actor) => new(actor, Now.AddDays(-1), Now.AddDays(1), null,
        null, null, null, null, null, BranchScope.LorettaCode, null, null, null, 100);
    private EfAuditEventReader Reader(SgolDbContext context) => new(context, new FixedClock(Now), protection);
    private SgolDbContext CreateContext() => new(new DbContextOptionsBuilder<SgolDbContext>()
        .UseNpgsql(postgres.GetConnectionString()).Options);
    private static VersionRecord Published(Guid id) => new(id, VersionStatuses.Current, Now.AddDays(-2),
        null, "Configuración sintética", null, 2);
    private static async Task<Counts> CountsAsync(SgolDbContext context) => new(
        await context.AuditEvents.CountAsync(), await context.WorkObligations.CountAsync(),
        await context.AssignmentVersions.CountAsync(), await context.EvidenceItems.CountAsync(),
        await context.ValidationRequirements.CountAsync());

    private sealed record SeededPerson(Guid PersonId, Guid UserId);
    private sealed record Scenario(SeededPerson Direction, SeededPerson Administration,
        SeededPerson AdministrationPeer, SeededPerson Subcoordination, SeededPerson Sales, Guid ObligationId);
    private sealed record Counts(int Audits, int Obligations, int Assignments, int Evidence, int Validations);
    private sealed class FixedClock(DateTimeOffset value) : IClock { public DateTimeOffset UtcNow { get; } = value; }
    private sealed class SequenceGenerator(params Guid[] values) : IUuidGenerator
    {
        private int index;
        public Guid NewUuid() => values[index++];
    }
}
