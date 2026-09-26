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

public sealed class EligibilityPolicyPersistenceTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 9, 5, 12, 0, 0, TimeSpan.Zero);
    private readonly PostgreSqlContainer _postgres = PostgreSqlPersistenceTests.CreateContainerForTests();

    public Task InitializeAsync() => _postgres.StartAsync();
    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    [Fact]
    public async Task EightPoliciesPublishAtomicallyAndSuccessorPreservesExactTaskSnapshotHistoryAndAudit()
    {
        var actor = await ResetAndSeedActorAsync("POLICY-DIRECTION", CanonicalRole.Direction);
        await using var context = CreateContext();
        var services = CreateServices(context, NewUuidGenerator());
        await PublishAllTaskDefinitionsAsync(services.Release, services.Task, actor);

        var initialRelease = await services.Release.CreateDraftAsync(NewRelease(actor));
        var drafts = new List<EligibilityPolicyVersionDetails>();
        foreach (var pair in EligibilityPolicyCatalog.All)
        {
            drafts.Add(await services.Policy.PutAsync(NewPolicy(actor, initialRelease.Id, pair.Key, pair.Value)));
        }

        await services.Release.PublishAsync(new PublishConfigurationReleaseCommand(
            actor, Guid.CreateVersion7(), Guid.CreateVersion7(), initialRelease.Id,
            initialRelease.RowVersion, Now.AddDays(1), "Políticas iniciales"));

        var current = await services.Policy.ListAsync(actor, Guid.CreateVersion7());
        Assert.Equal(8, current.Count);
        var eligibilityRead = await services.Policy.GetAsync(actor, Guid.CreateVersion7(), "TAR-0005");
        Assert.Single(eligibilityRead.History);
        Assert.Equal(VersionStatuses.Current, eligibilityRead.Current?.Status);
        Assert.Equal(EligibilityPolicyCatalog.All.OrderBy(item => item.Key),
            current.OrderBy(item => item.TaskCode).Select(item => new KeyValuePair<string, string>(item.TaskCode, item.RequiredRole)));
        Assert.All(current, policy =>
        {
            Assert.True(policy.RequiresAvailability);
            Assert.Null(policy.RequiredShift);
            Assert.Equal(VersionStatuses.Current, policy.Status);
        });
        var definitions = await services.Task.ListAsync(actor, Guid.CreateVersion7());
        Assert.All(definitions, definition => Assert.NotNull(definition.CurrentEligibilityPolicy));
        Assert.All(definitions, definition =>
            Assert.Equal(definition.Current!.Id, definition.CurrentEligibilityPolicy!.TaskDefinitionVersionId));

        var old = current.Single(item => item.TaskCode == "TAR-0005");
        var successorRelease = await services.Release.CreateDraftAsync(NewRelease(actor));
        var successor = await services.Policy.PutAsync(NewPolicy(
            actor, successorRelease.Id, old.TaskCode, old.RequiredRole, expected: old.RowVersion));
        await services.Release.PublishAsync(new PublishConfigurationReleaseCommand(
            actor, Guid.CreateVersion7(), Guid.CreateVersion7(), successorRelease.Id,
            successorRelease.RowVersion, Now.AddDays(2), "Sustituir política"));

        var history = await context.EligibilityPolicyVersions.AsNoTracking()
            .Where(item => item.TaskDefinitionId == TaskDefinitionCatalog.Require("TAR-0005").Id)
            .OrderBy(item => item.VersionNo)
            .ToListAsync();
        Assert.Equal(2, history.Count);
        var eligibilityHistoryRead = await services.Policy.GetAsync(actor, Guid.CreateVersion7(), "TAR-0005");
        Assert.Equal(2, eligibilityHistoryRead.History.Count);
        Assert.Equal(history[1].Id, eligibilityHistoryRead.Current?.Id);
        Assert.Equal(VersionStatuses.Superseded, history[0].Status);
        Assert.Equal(VersionStatuses.Current, history[1].Status);
        Assert.Equal(history[0].Id, history[1].SupersedesId);
        Assert.Equal(old.TaskDefinitionVersionId, successor.TaskDefinitionVersionId);
        Assert.Contains(await context.AuditEvents.AsNoTracking().ToListAsync(), audit =>
            audit.Action == "ELIGIBILITY_POLICY_PUBLISHED" && audit.ResourceId == successor.Id &&
            audit.BeforeData != null && audit.AfterData != null);

        var concurrencyBase = (await services.Policy.ListAsync(actor, Guid.CreateVersion7()))
            .Single(item => item.TaskCode == "TAR-0005");
        var releaseA = await services.Release.CreateDraftAsync(NewRelease(actor));
        var releaseB = await services.Release.CreateDraftAsync(NewRelease(actor));
        await services.Policy.PutAsync(NewPolicy(
            actor, releaseA.Id, concurrencyBase.TaskCode, concurrencyBase.RequiredRole, expected: concurrencyBase.RowVersion));
        await services.Policy.PutAsync(NewPolicy(
            actor, releaseB.Id, concurrencyBase.TaskCode, concurrencyBase.RequiredRole, expected: concurrencyBase.RowVersion));
        await services.Release.PublishAsync(new PublishConfigurationReleaseCommand(
            actor, Guid.CreateVersion7(), Guid.CreateVersion7(), releaseA.Id,
            releaseA.RowVersion, Now.AddDays(3), "Concurrencia ganadora"));
        await Assert.ThrowsAsync<VersionConflictException>(() => services.Release.PublishAsync(
            new PublishConfigurationReleaseCommand(
                actor, Guid.CreateVersion7(), Guid.CreateVersion7(), releaseB.Id,
                releaseB.RowVersion, Now.AddDays(4), "Concurrencia obsoleta")));
        Assert.Single(await context.EligibilityPolicyVersions.AsNoTracking()
            .Where(item => item.TaskDefinitionId == TaskDefinitionCatalog.Require("TAR-0005").Id &&
                item.Status == VersionStatuses.Current)
            .ToListAsync());
        Assert.Empty(context.ChangeTracker.Entries());
    }

    [Fact]
    public async Task ValidationAuthorizationConcurrencyAndIdempotencyFailuresHaveNoPolicyEffect()
    {
        var direction = await ResetAndSeedActorAsync("POLICY-DIRECTION", CanonicalRole.Direction);
        await using var context = CreateContext();
        var services = CreateServices(context, NewUuidGenerator());
        await PublishAllTaskDefinitionsAsync(services.Release, services.Task, direction);
        var release = await services.Release.CreateDraftAsync(NewRelease(direction));

        await Assert.ThrowsAsync<TaskDefinitionNotMvpException>(() => services.Policy.PutAsync(
            NewPolicy(direction, release.Id, "TAR-0195", "SUBCOORDINACION")));
        await Assert.ThrowsAsync<EligibilityPolicyValidationException>(() => services.Policy.PutAsync(
            NewPolicy(direction, release.Id, "TAR-0005", "Director")));
        await Assert.ThrowsAsync<EligibilityPolicyValidationException>(() => services.Policy.PutAsync(
            NewPolicy(direction, release.Id, "TAR-0005", "DIRECCION")));
        await Assert.ThrowsAsync<EligibilityPolicyValidationException>(() => services.Policy.PutAsync(
            NewPolicy(direction, release.Id, "TAR-0005", "SUBCOORDINACION", availability: false)));
        await Assert.ThrowsAsync<EligibilityPolicyValidationException>(() => services.Policy.PutAsync(
            NewPolicy(direction, release.Id, "TAR-0005", "SUBCOORDINACION", shift: "MATUTINO")));

        foreach (var role in new[] { CanonicalRole.Administration, CanonicalRole.Subcoordination, CanonicalRole.SalesFloor })
        {
            var denied = await SeedActorAsync($"POLICY-{role}", role);
            await Assert.ThrowsAsync<EligibilityPolicyAccessDeniedException>(() =>
                services.Policy.GetAsync(denied, Guid.CreateVersion7(), "TAR-0005"));
            await Assert.ThrowsAsync<EligibilityPolicyAccessDeniedException>(() => services.Policy.PutAsync(
                NewPolicy(denied, release.Id, "TAR-0005", "SUBCOORDINACION")));
        }
        var inactive = await SeedActorAsync("POLICY-INACTIVE", CanonicalRole.Direction, accountActive: false);
        var outsideLoretta = await SeedActorAsync("POLICY-OUTSIDE", CanonicalRole.Direction, hasEmployment: false);
        await Assert.ThrowsAsync<EligibilityPolicyAccessDeniedException>(() => services.Policy.PutAsync(
            NewPolicy(inactive, release.Id, "TAR-0005", "SUBCOORDINACION")));
        await Assert.ThrowsAsync<EligibilityPolicyAccessDeniedException>(() => services.Policy.PutAsync(
            NewPolicy(outsideLoretta, release.Id, "TAR-0005", "SUBCOORDINACION")));

        var command = NewPolicy(direction, release.Id, "TAR-0005", "SUBCOORDINACION");
        var created = await services.Policy.PutAsync(command);
        var replay = await services.Policy.PutAsync(command);
        Assert.Equal(created.Id, replay.Id);
        Assert.False(created.Replayed);
        Assert.True(replay.Replayed);
        await Assert.ThrowsAsync<EligibilityPolicyIdempotencyConflictException>(() => services.Policy.PutAsync(
            command with { ExpectedRowVersion = 1 }));
        await Assert.ThrowsAsync<VersionConflictException>(() => services.Policy.PutAsync(
            NewPolicy(direction, release.Id, "TAR-0005", "SUBCOORDINACION")));
        await Assert.ThrowsAsync<EligibilityPolicyCoverageException>(() => services.Release.PublishAsync(
            new PublishConfigurationReleaseCommand(
                direction, Guid.CreateVersion7(), Guid.CreateVersion7(), release.Id,
                release.RowVersion, Now.AddDays(1), "Catálogo incompleto")));

        Assert.Single(await context.EligibilityPolicyVersions.AsNoTracking().ToListAsync());
        Assert.Contains(await context.AuditEvents.AsNoTracking().ToListAsync(), audit =>
            audit.Action == "ELIGIBILITY_POLICY_ACCESS_DENIED" && audit.Outcome == "DENIED");
        Assert.DoesNotContain(await context.AuditEvents.AsNoTracking().ToListAsync(), audit =>
            audit.Action == "ELIGIBILITY_POLICY_PUBLISHED");
        Assert.Empty(context.ChangeTracker.Entries());
    }

    [Fact]
    public async Task PostgreSqlRejectsInvalidAvailabilityShiftExactVersionAndSecondCurrentPolicy()
    {
        var actor = await ResetAndSeedActorAsync("POLICY-DB", CanonicalRole.Direction);
        await using var context = CreateContext();
        var services = CreateServices(context, NewUuidGenerator());
        await PublishAllTaskDefinitionsAsync(services.Release, services.Task, actor);
        var definition = TaskDefinitionCatalog.Require("TAR-0005");
        var version = await context.TaskDefinitionVersions.AsNoTracking()
            .SingleAsync(item => item.TaskDefinitionId == definition.Id && item.Status == VersionStatuses.Current);
        var release = await services.Release.CreateDraftAsync(NewRelease(actor));

        await Assert.ThrowsAsync<PostgresException>(() => context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO eligibility_policy_version
                (id, task_definition_id, task_definition_version_id, release_id, version_no, required_role,
                 requires_availability, required_shift, status, row_version)
            VALUES ({Guid.CreateVersion7()}, {definition.Id}, {version.Id}, {release.Id}, 1, {"SUBCOORDINACION"},
                    {false}, {null}, {"BORRADOR"}, 1)
            """));
        await Assert.ThrowsAsync<PostgresException>(() => context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO eligibility_policy_version
                (id, task_definition_id, task_definition_version_id, release_id, version_no, required_role,
                 requires_availability, required_shift, status, row_version)
            VALUES ({Guid.CreateVersion7()}, {definition.Id}, {version.Id}, {release.Id}, 2, {"SUBCOORDINACION"},
                    {true}, {"MATUTINO"}, {"BORRADOR"}, 1)
            """));
        await Assert.ThrowsAsync<PostgresException>(() => context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO eligibility_policy_version
                (id, task_definition_id, task_definition_version_id, release_id, version_no, required_role,
                 requires_availability, required_shift, status, row_version)
            VALUES ({Guid.CreateVersion7()}, {TaskDefinitionCatalog.Require("TAR-0007").Id}, {version.Id}, {release.Id}, 3,
                    {"PISO_VENTAS"}, {true}, {null}, {"BORRADOR"}, 1)
            """));

        var secondRelease = await services.Release.CreateDraftAsync(NewRelease(actor));
        await context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO eligibility_policy_version
                (id, task_definition_id, task_definition_version_id, release_id, version_no, required_role,
                 requires_availability, required_shift, status, effective_from, reason, row_version)
            VALUES ({Guid.CreateVersion7()}, {definition.Id}, {version.Id}, {release.Id}, 4, {"SUBCOORDINACION"},
                    {true}, {null}, {"VIGENTE"}, {Now.AddDays(1)}, {"Sintética"}, 2)
            """);
        await Assert.ThrowsAsync<PostgresException>(() => context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO eligibility_policy_version
                (id, task_definition_id, task_definition_version_id, release_id, version_no, required_role,
                 requires_availability, required_shift, status, effective_from, reason, row_version)
            VALUES ({Guid.CreateVersion7()}, {definition.Id}, {version.Id}, {secondRelease.Id}, 5, {"SUBCOORDINACION"},
                    {true}, {null}, {"VIGENTE"}, {Now.AddDays(2)}, {"Sintética dos"}, 2)
            """));
    }

    [Fact]
    public async Task AuditFailureRollsBackPolicyAndIdempotencyInTheSameTransaction()
    {
        var actor = await ResetAndSeedActorAsync("POLICY-ROLLBACK", CanonicalRole.Direction);
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
            ResourceType = "ELIGIBILITY_POLICY_VERSION",
            BranchId = BranchScope.LorettaId,
            CorrelationId = Guid.CreateVersion7(),
            Outcome = "SUCCESS",
        });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var policyId = Guid.CreateVersion7();
        var failing = new EfEligibilityPolicyService(
            context,
            new AuditTransaction(context),
            new FixedClock(Now),
            new SequenceUuidGenerator(policyId, duplicateAuditId));
        await Assert.ThrowsAsync<DbUpdateException>(() => failing.PutAsync(
            NewPolicy(actor, release.Id, "TAR-0005", "SUBCOORDINACION")));

        Assert.False(await context.EligibilityPolicyVersions.AsNoTracking().AnyAsync(item => item.Id == policyId));
        Assert.False(await context.IdempotencyRecords.AsNoTracking().AnyAsync(item => item.ResourceId == policyId));
        Assert.Empty(context.ChangeTracker.Entries());
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
            new EfEligibilityPolicyService(context, audit, new FixedClock(Now), generator));
    }

    private static CreateConfigurationReleaseCommand NewRelease(Guid actor) =>
        new(actor, Guid.CreateVersion7(), Guid.CreateVersion7());

    private static PutEligibilityPolicyCommand NewPolicy(
        Guid actor,
        Guid releaseId,
        string taskCode,
        string role,
        Guid? key = null,
        Guid? correlation = null,
        long? expected = null,
        bool availability = true,
        string? shift = null) => new(
            actor, key ?? Guid.CreateVersion7(), correlation ?? Guid.CreateVersion7(), taskCode,
            releaseId, role, availability, shift, expected);

    private static Uuid7Generator NewUuidGenerator() => new(new FixedClock(Now));

    private sealed record Services(
        EfConfigurationReleaseService Release,
        EfTaskDefinitionService Task,
        EfEligibilityPolicyService Policy);

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
