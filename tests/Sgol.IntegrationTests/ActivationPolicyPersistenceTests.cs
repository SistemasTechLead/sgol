using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Sgol.BuildingBlocks.Identifiers;
using Sgol.BuildingBlocks.Time;
using Sgol.BuildingBlocks.Versioning;
using Sgol.Configuration.Contracts;
using Sgol.Identity.Contracts;
using Sgol.Organization.Contracts;
using Sgol.Web.Infrastructure.Persistence;
using Sgol.Web.Infrastructure.Persistence.Auditing;
using Sgol.Web.Infrastructure.Persistence.Bootstrap;
using Sgol.Web.Infrastructure.Persistence.Configuration;
using Sgol.Web.Infrastructure.Persistence.Versioning;
using Testcontainers.PostgreSql;
using Xunit;

namespace Sgol.IntegrationTests;

public sealed class ActivationPolicyPersistenceTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 9, 6, 12, 0, 0, TimeSpan.Zero);
    private static readonly string[] ExpectedSalesTimes = ["12:00", "17:00"];
    private static readonly string[] UnsupportedModes = ["EVENTO", "CONDICION", "INTEGRACION_EXTERNA", "DESCONOCIDO"];
    private static readonly string[] DeniedRoles =
        [CanonicalRole.Administration, CanonicalRole.Subcoordination, CanonicalRole.SalesFloor];
    private readonly PostgreSqlContainer _postgres = PostgreSqlPersistenceTests.CreateContainerForTests();

    public Task InitializeAsync() => _postgres.StartAsync();
    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    [Fact]
    public async Task EightPoliciesPublishAtomicallyAndSuccessorPreservesExactTaskSnapshotHistoryAndAudit()
    {
        var actor = await ResetAndSeedActorAsync("ACTIVATION-DIRECTION", CanonicalRole.Direction);
        await using var context = CreateContext();
        var services = CreateServices(context, NewUuidGenerator());
        await PublishAllTaskDefinitionsAsync(services.Release, services.Task, actor);
        var taskVersions = await CurrentTaskVersionsAsync(context);

        var initialRelease = await services.Release.CreateDraftAsync(NewRelease(actor));
        foreach (var taskCode in ActivationPolicyCatalog.All.Keys)
        {
            await services.Policy.PutAsync(NewPolicy(
                actor, initialRelease.Id, taskCode, taskVersions[taskCode]));
        }

        await services.Release.PublishAsync(new PublishConfigurationReleaseCommand(
            actor, Guid.CreateVersion7(), Guid.CreateVersion7(), initialRelease.Id,
            initialRelease.RowVersion, Now.AddDays(1), "Políticas de activación iniciales"));

        var current = await context.ActivationRuleVersions.AsNoTracking()
            .Where(item => item.Status == VersionStatuses.Current)
            .OrderBy(item => item.TaskDefinitionId)
            .ToListAsync();
        Assert.Equal(8, current.Count);
        Assert.All(current, policy => Assert.Equal(taskVersions.Values.Single(id => id == policy.TaskDefinitionVersionId), policy.TaskDefinitionVersionId));
        var sales = current.Single(item => item.TaskDefinitionId == TaskDefinitionCatalog.Require("TAR-0005").Id);
        Assert.Equal(ExpectedSalesTimes,
            sales.Schedule.RootElement.GetProperty("localTimes").EnumerateArray().Select(item => item.GetString()));
        var service = current.Single(item => item.TaskDefinitionId == TaskDefinitionCatalog.Require("TAR-0026").Id);
        Assert.Equal(3, service.Schedule.RootElement.GetProperty("businessDaysBefore").GetInt32());
        Assert.True(service.Schedule.RootElement.GetProperty("adjustDueDateToPreviousBusinessDay").GetBoolean());

        var originalTaskVersionIds = await context.TaskDefinitionVersions.AsNoTracking()
            .Select(item => item.Id)
            .OrderBy(id => id)
            .ToListAsync();
        var successorRelease = await services.Release.CreateDraftAsync(NewRelease(actor));
        await services.Policy.PutAsync(NewPolicy(
            actor, successorRelease.Id, "TAR-0026", taskVersions["TAR-0026"],
            expected: service.RowVersion,
            localTime: "09:15"));
        await services.Release.PublishAsync(new PublishConfigurationReleaseCommand(
            actor, Guid.CreateVersion7(), Guid.CreateVersion7(), successorRelease.Id,
            successorRelease.RowVersion, Now.AddDays(2), "Cambiar hora local"));

        var history = await context.ActivationRuleVersions.AsNoTracking()
            .Where(item => item.TaskDefinitionId == TaskDefinitionCatalog.Require("TAR-0026").Id)
            .OrderBy(item => item.VersionNo)
            .ToListAsync();
        Assert.Equal(2, history.Count);
        Assert.Equal(VersionStatuses.Superseded, history[0].Status);
        Assert.Equal(VersionStatuses.Current, history[1].Status);
        Assert.Equal(history[0].Id, history[1].SupersedesId);
        Assert.Equal(taskVersions["TAR-0026"], history[1].TaskDefinitionVersionId);
        Assert.Equal(originalTaskVersionIds, await context.TaskDefinitionVersions.AsNoTracking()
            .Select(item => item.Id)
            .OrderBy(id => id)
            .ToListAsync());
        Assert.Contains(await context.AuditEvents.AsNoTracking().ToListAsync(), audit =>
            audit.Action == "ACTIVATION_POLICY_PUBLISHED" && audit.ResourceId == history[1].Id &&
            audit.BeforeData != null && audit.AfterData != null);

        var releaseA = await services.Release.CreateDraftAsync(NewRelease(actor));
        var releaseB = await services.Release.CreateDraftAsync(NewRelease(actor));
        await services.Policy.PutAsync(NewPolicy(
            actor, releaseA.Id, "TAR-0026", taskVersions["TAR-0026"],
            expected: history[1].RowVersion, localTime: "10:00"));
        await services.Policy.PutAsync(NewPolicy(
            actor, releaseB.Id, "TAR-0026", taskVersions["TAR-0026"],
            expected: history[1].RowVersion, localTime: "11:00"));
        await services.Release.PublishAsync(new PublishConfigurationReleaseCommand(
            actor, Guid.CreateVersion7(), Guid.CreateVersion7(), releaseA.Id,
            releaseA.RowVersion, Now.AddDays(3), "Concurrencia ganadora"));
        await Assert.ThrowsAsync<VersionConflictException>(() => services.Release.PublishAsync(
            new PublishConfigurationReleaseCommand(
                actor, Guid.CreateVersion7(), Guid.CreateVersion7(), releaseB.Id,
                releaseB.RowVersion, Now.AddDays(4), "Concurrencia obsoleta")));
        Assert.Single(await context.ActivationRuleVersions.AsNoTracking()
            .Where(item =>
                item.TaskDefinitionId == TaskDefinitionCatalog.Require("TAR-0026").Id &&
                item.Status == VersionStatuses.Current)
            .ToListAsync());
        Assert.Empty(context.ChangeTracker.Entries());
    }

    [Fact]
    public async Task ValidationAuthorizationExactVersionConcurrencyAndIdempotencyFailuresHaveNoEffect()
    {
        var direction = await ResetAndSeedActorAsync("ACTIVATION-DIRECTION", CanonicalRole.Direction);
        await using var context = CreateContext();
        var services = CreateServices(context, NewUuidGenerator());
        await PublishAllTaskDefinitionsAsync(services.Release, services.Task, direction);
        var taskVersions = await CurrentTaskVersionsAsync(context);
        var release = await services.Release.CreateDraftAsync(NewRelease(direction));

        await Assert.ThrowsAsync<TaskDefinitionNotMvpException>(() => services.Policy.PutAsync(
            NewPolicy(direction, release.Id, "TAR-0195", Guid.CreateVersion7())));
        foreach (var unsupported in UnsupportedModes)
        {
            await Assert.ThrowsAsync<ActivationPolicyValidationException>(() => services.Policy.PutAsync(
                NewPolicy(direction, release.Id, "TAR-0007", taskVersions["TAR-0007"], mode: unsupported)));
        }
        await Assert.ThrowsAsync<ActivationPolicyValidationException>(() => services.Policy.PutAsync(
            NewPolicy(direction, release.Id, "TAR-0007", taskVersions["TAR-0007"], mode: ActivationModes.Recurring)));
        await Assert.ThrowsAsync<ActivationPolicyDefinitionPreconditionException>(() => services.Policy.PutAsync(
            NewPolicy(direction, release.Id, "TAR-0007", Guid.CreateVersion7())));

        foreach (var role in DeniedRoles)
        {
            var denied = await SeedActorAsync($"ACTIVATION-{role}", role);
            await Assert.ThrowsAsync<ActivationPolicyAccessDeniedException>(() => services.Policy.PutAsync(
                NewPolicy(denied, release.Id, "TAR-0007", taskVersions["TAR-0007"])));
        }
        var inactiveActor = await SeedActorAsync("ACTIVATION-INACTIVE", CanonicalRole.Direction, accountActive: false);
        var outsideLoretta = await SeedActorAsync("ACTIVATION-OUTSIDE", CanonicalRole.Direction, hasEmployment: false);
        await Assert.ThrowsAsync<ActivationPolicyAccessDeniedException>(() => services.Policy.PutAsync(
            NewPolicy(inactiveActor, release.Id, "TAR-0007", taskVersions["TAR-0007"])));
        await Assert.ThrowsAsync<ActivationPolicyAccessDeniedException>(() => services.Policy.PutAsync(
            NewPolicy(outsideLoretta, release.Id, "TAR-0007", taskVersions["TAR-0007"])));

        var command = NewPolicy(direction, release.Id, "TAR-0007", taskVersions["TAR-0007"]);
        var created = await services.Policy.PutAsync(command);
        var replay = await services.Policy.PutAsync(command);
        Assert.Equal(created.Id, replay.Id);
        await Assert.ThrowsAsync<ActivationPolicyIdempotencyConflictException>(() => services.Policy.PutAsync(
            command with { ExpectedRowVersion = 1 }));
        await Assert.ThrowsAsync<VersionConflictException>(() => services.Policy.PutAsync(
            NewPolicy(direction, release.Id, "TAR-0007", taskVersions["TAR-0007"], expected: 1)));
        await Assert.ThrowsAsync<ActivationPolicyCoverageException>(() => services.Release.PublishAsync(
            new PublishConfigurationReleaseCommand(
                direction, Guid.CreateVersion7(), Guid.CreateVersion7(), release.Id,
                release.RowVersion, Now.AddDays(1), "Catálogo incompleto")));

        Assert.Single(await context.ActivationRuleVersions.AsNoTracking().ToListAsync());
        Assert.Contains(await context.AuditEvents.AsNoTracking().ToListAsync(), audit =>
            audit.Action == "ACTIVATION_POLICY_ACCESS_DENIED" && audit.Outcome == "DENIED");
        Assert.DoesNotContain(await context.AuditEvents.AsNoTracking().ToListAsync(), audit =>
            audit.Action == "ACTIVATION_POLICY_PUBLISHED");
        Assert.Empty(context.ChangeTracker.Entries());
    }

    [Fact]
    public async Task InactiveTaskVersionCannotReceiveActivationPolicy()
    {
        var actor = await ResetAndSeedActorAsync("ACTIVATION-INACTIVE-TASK", CanonicalRole.Direction);
        await using var context = CreateContext();
        var services = CreateServices(context, NewUuidGenerator());
        await PublishAllTaskDefinitionsAsync(services.Release, services.Task, actor);
        var taskVersions = await CurrentTaskVersionsAsync(context);
        var taskVersion = await context.TaskDefinitionVersions.AsNoTracking()
            .SingleAsync(item => item.Id == taskVersions["TAR-0007"]);
        var release = await services.Release.CreateDraftAsync(NewRelease(actor));
        await services.Task.DeactivateNewAsync(new DeactivateTaskDefinitionCommand(
            actor, Guid.CreateVersion7(), Guid.CreateVersion7(), "TAR-0007", release.Id,
            taskVersion.RowVersion, Now.AddDays(1), "Inactivar para nuevas"));
        var activationRelease = await services.Release.CreateDraftAsync(NewRelease(actor));

        await Assert.ThrowsAsync<ActivationPolicyDefinitionPreconditionException>(() => services.Policy.PutAsync(
            NewPolicy(actor, activationRelease.Id, "TAR-0007", taskVersions["TAR-0007"])));
        Assert.Empty(await context.ActivationRuleVersions.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task PostgreSqlRejectsInvalidModeScheduleSchemaExactVersionAndSecondCurrentPolicy()
    {
        var actor = await ResetAndSeedActorAsync("ACTIVATION-DB", CanonicalRole.Direction);
        await using var context = CreateContext();
        var services = CreateServices(context, NewUuidGenerator());
        await PublishAllTaskDefinitionsAsync(services.Release, services.Task, actor);
        var taskVersions = await CurrentTaskVersionsAsync(context);
        var definition = TaskDefinitionCatalog.Require("TAR-0007");
        var release = await services.Release.CreateDraftAsync(NewRelease(actor));

        await Assert.ThrowsAsync<PostgresException>(() => InsertRuleAsync(
            context, definition.Id, taskVersions["TAR-0007"], release.Id, 1,
            "EVENTO", "null", ActivationOriginSchemas.ManualReference));
        await Assert.ThrowsAsync<PostgresException>(() => InsertRuleAsync(
            context, definition.Id, taskVersions["TAR-0007"], release.Id, 2,
            ActivationModes.Manual, "{}", ActivationOriginSchemas.ManualReference));
        await Assert.ThrowsAsync<PostgresException>(() => InsertRuleAsync(
            context, definition.Id, taskVersions["TAR-0007"], release.Id, 3,
            ActivationModes.Manual, "null", ActivationOriginSchemas.ServiceDueDateReference));
        await Assert.ThrowsAsync<PostgresException>(() => InsertRuleAsync(
            context, definition.Id, taskVersions["TAR-0005"], release.Id, 4,
            ActivationModes.Manual, "null", ActivationOriginSchemas.ManualReference));
        var serviceDefinition = TaskDefinitionCatalog.Require("TAR-0026");
        await Assert.ThrowsAsync<PostgresException>(() => InsertRuleAsync(
            context, serviceDefinition.Id, taskVersions["TAR-0026"], release.Id, 5,
            ActivationModes.Recurring,
            """{"kind":"BUSINESS_DAYS_BEFORE_DUE_DATE","businessDaysBefore":3,"timeZone":"America/Mexico_City","adjustDueDateToPreviousBusinessDay":true}""",
            ActivationOriginSchemas.ServiceDueDateReference));
        await Assert.ThrowsAsync<PostgresException>(() => InsertRuleAsync(
            context, serviceDefinition.Id, taskVersions["TAR-0026"], release.Id, 6,
            ActivationModes.Recurring,
            """{"kind":"BUSINESS_DAYS_BEFORE_DUE_DATE","businessDaysBefore":3,"localTime":"08:30","timeZone":"America/Mexico_City","adjustDueDateToPreviousBusinessDay":true,"externalSystem":"ERP"}""",
            ActivationOriginSchemas.ServiceDueDateReference));

        var secondRelease = await services.Release.CreateDraftAsync(NewRelease(actor));
        await InsertRuleAsync(
            context, definition.Id, taskVersions["TAR-0007"], release.Id, 7,
            ActivationModes.Manual, "null", ActivationOriginSchemas.ManualReference,
            status: VersionStatuses.Current, effectiveFrom: Now.AddDays(1), reason: "Sintética", rowVersion: 2);
        await Assert.ThrowsAsync<PostgresException>(() => InsertRuleAsync(
            context, definition.Id, taskVersions["TAR-0007"], secondRelease.Id, 8,
            ActivationModes.Manual, "null", ActivationOriginSchemas.ManualReference,
            status: VersionStatuses.Current, effectiveFrom: Now.AddDays(2), reason: "Sintética dos", rowVersion: 2));
    }

    [Fact]
    public async Task AuditFailureRollsBackPolicyAndIdempotencyInTheSameTransaction()
    {
        var actor = await ResetAndSeedActorAsync("ACTIVATION-ROLLBACK", CanonicalRole.Direction);
        await using var context = CreateContext();
        var normal = CreateServices(context, NewUuidGenerator());
        await PublishAllTaskDefinitionsAsync(normal.Release, normal.Task, actor);
        var taskVersions = await CurrentTaskVersionsAsync(context);
        var release = await normal.Release.CreateDraftAsync(NewRelease(actor));
        var duplicateAuditId = Guid.CreateVersion7();
        context.AuditEvents.Add(new AuditEvent
        {
            Id = duplicateAuditId,
            OccurredAt = Now,
            ActorUserId = actor,
            ActorType = "APP_USER",
            Action = "SYNTHETIC_EXISTING_EVENT",
            ResourceType = "ACTIVATION_RULE_VERSION",
            BranchId = BranchScope.LorettaId,
            CorrelationId = Guid.CreateVersion7(),
            Outcome = "SUCCESS",
        });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var policyId = Guid.CreateVersion7();
        var failing = new EfActivationPolicyService(
            context,
            new AuditTransaction(context),
            new FixedClock(Now),
            new SequenceUuidGenerator(policyId, duplicateAuditId));
        await Assert.ThrowsAsync<DbUpdateException>(() => failing.PutAsync(
            NewPolicy(actor, release.Id, "TAR-0007", taskVersions["TAR-0007"])));

        Assert.False(await context.ActivationRuleVersions.AsNoTracking().AnyAsync(item => item.Id == policyId));
        Assert.False(await context.IdempotencyRecords.AsNoTracking().AnyAsync(item => item.ResourceId == policyId));
        Assert.Empty(context.ChangeTracker.Entries());
    }

    private static async Task InsertRuleAsync(
        SgolDbContext context,
        Guid taskDefinitionId,
        Guid taskDefinitionVersionId,
        Guid releaseId,
        int versionNo,
        string mode,
        string schedule,
        string originKeySchema,
        string status = VersionStatuses.Draft,
        DateTimeOffset? effectiveFrom = null,
        string? reason = null,
        long rowVersion = 1) =>
        await context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO activation_rule_version
                (id, task_definition_id, task_definition_version_id, release_id, version_no, mode,
                 schedule, origin_key_schema, status, effective_from, reason, row_version)
            VALUES ({Guid.CreateVersion7()}, {taskDefinitionId}, {taskDefinitionVersionId}, {releaseId}, {versionNo},
                    {mode}, {schedule}::jsonb, {originKeySchema}, {status}, {effectiveFrom}, {reason}, {rowVersion})
            """);

    private static async Task<Dictionary<string, Guid>> CurrentTaskVersionsAsync(SgolDbContext context)
    {
        var versions = await context.TaskDefinitionVersions.AsNoTracking()
            .Where(item => item.Status == VersionStatuses.Current)
            .ToDictionaryAsync(item => item.TaskDefinitionId, item => item.Id);
        return TaskDefinitionCatalog.All.ToDictionary(item => item.TaskCode, item => versions[item.Id]);
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
                actor, Guid.CreateVersion7(), Guid.CreateVersion7(), task.TaskCode, release.Id, 1, empty.RootElement));
        }

        await releaseService.PublishAsync(new PublishConfigurationReleaseCommand(
            actor, Guid.CreateVersion7(), Guid.CreateVersion7(), release.Id, release.RowVersion, Now, "Definiciones iniciales"));
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
        context.People.Add(new Person { Id = personId, StableCode = code, DisplayName = "Persona sintética", CreatedAt = Now });
        if (hasEmployment)
        {
            context.EmploymentVersions.Add(new EmploymentVersion(
                Guid.CreateVersion7(), personId, BranchScope.LorettaId, EmploymentStatus.Active, Now));
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
            context, audit, new VersioningTransaction(context, audit), new FixedClock(Now), generator);
        return new Services(
            release,
            new EfTaskDefinitionService(context, audit, release, new FixedClock(Now), generator),
            new EfActivationPolicyService(context, audit, new FixedClock(Now), generator));
    }

    private static CreateConfigurationReleaseCommand NewRelease(Guid actor) =>
        new(actor, Guid.CreateVersion7(), Guid.CreateVersion7());

    private static PutActivationPolicyCommand NewPolicy(
        Guid actor,
        Guid releaseId,
        string taskCode,
        Guid taskDefinitionVersionId,
        Guid? key = null,
        Guid? correlation = null,
        long? expected = null,
        string? mode = null,
        string localTime = "08:30")
    {
        var definition = ActivationPolicyCatalog.Require(taskCode);
        var selectedMode = mode ?? definition.Mode;
        using var schedule = Schedule(taskCode, selectedMode, localTime);
        return new PutActivationPolicyCommand(
            actor,
            key ?? Guid.CreateVersion7(),
            correlation ?? Guid.CreateVersion7(),
            taskCode,
            taskDefinitionVersionId,
            releaseId,
            selectedMode,
            schedule.RootElement.Clone(),
            definition.OriginKeySchema,
            expected);
    }

    private static JsonDocument Schedule(string taskCode, string mode, string localTime)
    {
        if (mode != ActivationModes.Recurring)
        {
            return JsonDocument.Parse("null");
        }

        return taskCode == "TAR-0005"
            ? JsonDocument.Parse(
                """{"kind":"WORKING_DAY_WINDOWS","workingDaysOnly":true,"localTimes":["12:00","17:00"],"timeZone":"America/Mexico_City"}""")
            : JsonDocument.Parse(
                $$"""{"kind":"BUSINESS_DAYS_BEFORE_DUE_DATE","businessDaysBefore":3,"localTime":"{{localTime}}","timeZone":"America/Mexico_City","adjustDueDateToPreviousBusinessDay":true}""");
    }

    private static Uuid7Generator NewUuidGenerator() => new(new FixedClock(Now));

    private sealed record Services(
        EfConfigurationReleaseService Release,
        EfTaskDefinitionService Task,
        EfActivationPolicyService Policy);

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
