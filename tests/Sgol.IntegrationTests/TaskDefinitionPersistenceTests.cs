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

public sealed class TaskDefinitionPersistenceTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 9, 4, 12, 0, 0, TimeSpan.Zero);
    private readonly PostgreSqlContainer _postgres = PostgreSqlPersistenceTests.CreateContainerForTests();

    public Task InitializeAsync() => _postgres.StartAsync();

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    [Fact]
    public async Task Migration_SeedsExactlyEightCanonicalIdentitiesAndNoVersions()
    {
        await ResetAsync();
        await using var context = CreateContext();

        var definitions = await context.TaskDefinitions.AsNoTracking().OrderBy(item => item.TaskCode).ToListAsync();
        Assert.Equal(TaskDefinitionCatalog.All.Select(item => item.TaskCode), definitions.Select(item => item.TaskCode));
        Assert.Equal(TaskDefinitionCatalog.All.Select(item => item.Name), definitions.Select(item => item.Name));
        Assert.Equal(8, definitions.Select(item => item.Id).Distinct().Count());
        Assert.False(await context.TaskDefinitionVersions.AnyAsync());
        Assert.Empty(context.ChangeTracker.Entries());
    }

    [Fact]
    public async Task DatabaseRejectsNinthOptionalAndExcludedCodes()
    {
        await ResetAsync();
        foreach (var code in new[] { "TAR-0001", "TAR-0195", "TAR-0202", "T221" })
        {
            await using var context = CreateContext();
            await Assert.ThrowsAsync<PostgresException>(() => context.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO task_definition (id, task_code, name) VALUES ({Guid.CreateVersion7()}, {code}, {"No autorizada"})"));
        }
    }

    [Fact]
    public async Task V1V2DeactivationAndReactivationPreserveHistoryAndAudit()
    {
        var actor = await ResetAndSeedActorAsync("TASK-CYCLE", CanonicalRole.Direction);
        await using var context = CreateContext();
        var (releaseService, taskService) = CreateServices(context, NewUuidGenerator());
        var peopleBefore = await context.People.CountAsync();
        var rolesBefore = await context.RoleAssignmentVersions.CountAsync();

        var v1 = await CreateAndPublishAsync(releaseService, taskService, actor, "TAR-0005", Now, "V1");
        var v2 = await CreateAndPublishAsync(releaseService, taskService, actor, "TAR-0005", Now.AddDays(1), "V2");
        var deactivationRelease = await releaseService.CreateDraftAsync(NewRelease(actor));
        var deactivationCommand = new DeactivateTaskDefinitionCommand(
            actor,
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            "TAR-0005",
            deactivationRelease.Id,
            v2.RowVersion,
            Now.AddDays(2),
            "Desactivar nuevas");
        var inactive = await taskService.DeactivateNewAsync(deactivationCommand);
        var inactiveReplay = await taskService.DeactivateNewAsync(deactivationCommand);
        var v4 = await CreateAndPublishAsync(releaseService, taskService, actor, "TAR-0005", Now.AddDays(3), "Reactivar");

        var history = await context.TaskDefinitionVersions.AsNoTracking()
            .Where(item => item.TaskDefinitionId == TaskDefinitionCatalog.Require("TAR-0005").Id)
            .OrderBy(item => item.VersionNo)
            .ToListAsync();
        Assert.Equal([1, 2, 3, 4], history.Select(item => item.VersionNo));
        Assert.Equal(VersionStatuses.Superseded, history[0].Status);
        Assert.Equal(VersionStatuses.Superseded, history[1].Status);
        Assert.Equal(VersionStatuses.Superseded, history[2].Status);
        Assert.Equal(VersionStatuses.Current, history[3].Status);
        Assert.Equal(v1.Id, history[1].SupersedesId);
        Assert.Equal(v2.Id, history[2].SupersedesId);
        Assert.Equal(inactive.Id, inactiveReplay.Id);
        Assert.Equal(inactive.Status, inactiveReplay.Status);
        Assert.Equal(inactive.Id, v4.SupersedesId);
        Assert.Contains(await context.AuditEvents.AsNoTracking().ToListAsync(),
            audit => audit.Action == "TASK_DEFINITION_DEACTIVATED_NEW" && audit.Reason == "Desactivar nuevas");
        Assert.Equal(peopleBefore, await context.People.CountAsync());
        Assert.Equal(rolesBefore, await context.RoleAssignmentVersions.CountAsync());
        Assert.Empty(context.ChangeTracker.Entries());
    }

    [Fact]
    public async Task PermissionScopeAndStateFailuresHaveNoFunctionalEffect()
    {
        var denied = await ResetAndSeedActorAsync("TASK-DENIED", CanonicalRole.Administration);
        await using var context = CreateContext();
        var (releaseService, taskService) = CreateServices(context, NewUuidGenerator());
        var readableCatalog = await taskService.ListAsync(denied, Guid.CreateVersion7());
        Assert.Equal(8, readableCatalog.Count);
        Assert.All(readableCatalog, definition => Assert.Null(definition.Current));
        await Assert.ThrowsAsync<TaskDefinitionAccessDeniedException>(() =>
            taskService.CreateVersionAsync(NewCreate(denied, Guid.CreateVersion7(), "TAR-0005")));
        Assert.False(await context.TaskDefinitionVersions.AnyAsync());
        Assert.Contains(await context.AuditEvents.AsNoTracking().ToListAsync(),
            audit => audit.Action == "TASK_DEFINITION_ACCESS_DENIED" && audit.Outcome == "DENIED");

        var direction = await SeedActorAsync("TASK-DIRECTION", CanonicalRole.Direction);
        var release = await releaseService.CreateDraftAsync(NewRelease(direction));
        await Assert.ThrowsAsync<TaskDefinitionNotMvpException>(() =>
            taskService.CreateVersionAsync(NewCreate(direction, release.Id, "TAR-0001")));
        await Assert.ThrowsAsync<TaskDefinitionNotMvpException>(() =>
            taskService.CreateVersionAsync(NewCreate(direction, release.Id, "TAR-0195")));
        Assert.False(await context.TaskDefinitionVersions.AnyAsync());
        Assert.Empty(context.ChangeTracker.Entries());
    }

    [Fact]
    public async Task DuplicateDraftAndStaleEtagAreRejectedWithoutSecondVersion()
    {
        var actor = await ResetAndSeedActorAsync("TASK-CONCURRENCY", CanonicalRole.Direction);
        await using var context = CreateContext();
        var (releaseService, taskService) = CreateServices(context, NewUuidGenerator());
        var release = await releaseService.CreateDraftAsync(NewRelease(actor));
        using var empty = JsonDocument.Parse("{}");
        var draft = await taskService.CreateVersionAsync(NewCreate(actor, release.Id, "TAR-0005", empty.RootElement));
        await Assert.ThrowsAsync<VersionConflictException>(() =>
            taskService.CreateVersionAsync(NewCreate(actor, release.Id, "TAR-0005", empty.RootElement)));
        await Assert.ThrowsAsync<VersionConflictException>(() =>
            taskService.PublishVersionAsync(new PublishTaskDefinitionVersionCommand(
                actor, Guid.CreateVersion7(), Guid.CreateVersion7(), "TAR-0005", draft.Id,
                draft.RowVersion + 1, Now, "ETag obsoleto")));
        Assert.Single(await context.TaskDefinitionVersions.AsNoTracking().ToListAsync());
        Assert.Equal(VersionStatuses.Draft, (await context.TaskDefinitionVersions.AsNoTracking().SingleAsync()).Status);
        Assert.Empty(context.ChangeTracker.Entries());
    }

    [Fact]
    public async Task AuditFailureRollsBackDraftAndIdempotencyRecord()
    {
        var actor = await ResetAndSeedActorAsync("TASK-ROLLBACK", CanonicalRole.Direction);
        await using var context = CreateContext();
        var normal = CreateServices(context, NewUuidGenerator());
        var release = await normal.Release.CreateDraftAsync(NewRelease(actor));
        var duplicateAuditId = Guid.CreateVersion7();
        context.AuditEvents.Add(NewExistingAudit(duplicateAuditId, actor));
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var generatedVersionId = Guid.CreateVersion7();
        var failing = CreateServices(context, new SequenceUuidGenerator(generatedVersionId, duplicateAuditId));
        await Assert.ThrowsAsync<DbUpdateException>(() =>
            failing.Task.CreateVersionAsync(NewCreate(actor, release.Id, "TAR-0005")));

        Assert.False(await context.TaskDefinitionVersions.AsNoTracking().AnyAsync(item => item.Id == generatedVersionId));
        Assert.False(await context.IdempotencyRecords.AsNoTracking().AnyAsync(item => item.ResourceId == generatedVersionId));
        Assert.Empty(context.ChangeTracker.Entries());
    }

    private static async Task<TaskDefinitionVersionDetails> CreateAndPublishAsync(
        EfConfigurationReleaseService releaseService,
        EfTaskDefinitionService taskService,
        Guid actor,
        string taskCode,
        DateTimeOffset effectiveFrom,
        string reason)
    {
        var release = await releaseService.CreateDraftAsync(NewRelease(actor));
        var draft = await taskService.CreateVersionAsync(NewCreate(actor, release.Id, taskCode));
        return await taskService.PublishVersionAsync(new PublishTaskDefinitionVersionCommand(
            actor,
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            taskCode,
            draft.Id,
            draft.RowVersion,
            effectiveFrom,
            reason));
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

    private async Task<Guid> SeedActorAsync(string code, string role)
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
            RoleCode = role,
            Status = RoleAssignmentStatus.Active,
            ValidFrom = Now,
        });
        await context.SaveChangesAsync();
        return userId;
    }

    private SgolDbContext CreateContext() => new(
        new DbContextOptionsBuilder<SgolDbContext>().UseNpgsql(_postgres.GetConnectionString()).Options);

    private static (EfConfigurationReleaseService Release, EfTaskDefinitionService Task) CreateServices(
        SgolDbContext context,
        IUuidGenerator generator)
    {
        var audit = new AuditTransaction(context);
        var release = new EfConfigurationReleaseService(
            context,
            audit,
            new VersioningTransaction(context, audit),
            new FixedClock(Now),
            generator);
        var task = new EfTaskDefinitionService(context, audit, release, new FixedClock(Now), generator);
        return (release, task);
    }

    private static CreateConfigurationReleaseCommand NewRelease(Guid actor) =>
        new(actor, Guid.CreateVersion7(), Guid.CreateVersion7());

    private static CreateTaskDefinitionVersionCommand NewCreate(
        Guid actor,
        Guid releaseId,
        string taskCode,
        JsonElement payload = default)
    {
        if (payload.ValueKind == JsonValueKind.Undefined)
        {
            using var empty = JsonDocument.Parse("{}");
            payload = empty.RootElement.Clone();
        }

        return new CreateTaskDefinitionVersionCommand(
            actor, Guid.CreateVersion7(), Guid.CreateVersion7(), taskCode, releaseId, 1, payload);
    }

    private static AuditEvent NewExistingAudit(Guid id, Guid actor) => new()
    {
        Id = id,
        OccurredAt = Now,
        ActorUserId = actor,
        ActorType = "APP_USER",
        Action = "SYNTHETIC_EXISTING_EVENT",
        ResourceType = "TASK_DEFINITION_VERSION",
        BranchId = BranchScope.LorettaId,
        CorrelationId = Guid.CreateVersion7(),
        Outcome = "SUCCESS",
    };

    private static Uuid7Generator NewUuidGenerator() => new(new FixedClock(Now));

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
