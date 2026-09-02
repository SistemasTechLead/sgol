using Microsoft.EntityFrameworkCore;
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

public sealed class ConfigurationReleasePersistenceTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 9, 3, 12, 0, 0, TimeSpan.Zero);
    private readonly PostgreSqlContainer _postgres = PostgreSqlPersistenceTests.CreateContainerForTests();

    public Task InitializeAsync() => _postgres.StartAsync();

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    [Fact]
    public async Task DraftInitialAndSuccessorPublicationPreserveHistoryAuditAndUnrelatedFacts()
    {
        var actorUserId = await ResetAndSeedActorAsync("CONFIG-CYCLE", CanonicalRole.Direction);
        await using var context = CreateContext();
        var service = CreateService(context, NewUuidGenerator());
        var personCount = await context.People.CountAsync();
        var roleCount = await context.RoleAssignmentVersions.CountAsync();
        var firstDraftCommand = CreateCommand(actorUserId);
        var firstDraft = await service.CreateDraftAsync(firstDraftCommand);
        var draftReplay = await service.CreateDraftAsync(firstDraftCommand);
        var secondDraft = await service.CreateDraftAsync(CreateCommand(actorUserId));

        var publicationKey = Guid.CreateVersion7();
        var firstCommand = PublishCommand(
            actorUserId,
            firstDraft.Id,
            firstDraft.RowVersion,
            Now,
            "Publicación inicial",
            publicationKey);
        var first = await service.PublishAsync(firstCommand);
        var replay = await service.PublishAsync(firstCommand);
        var second = await service.PublishAsync(PublishCommand(
            actorUserId,
            secondDraft.Id,
            secondDraft.RowVersion,
            Now.AddDays(7),
            "Publicación sucesora"));

        var history = await context.ConfigurationReleases.AsNoTracking()
            .OrderBy(release => release.VersionNo)
            .ToListAsync();
        Assert.Equal(2, history.Count);
        Assert.Equal(VersionStatuses.Superseded, history[0].Status);
        Assert.Equal(Now.AddDays(7), history[0].EffectiveTo);
        Assert.Equal(VersionStatuses.Current, history[1].Status);
        Assert.Equal(first.Id, history[1].SupersedesId);
        Assert.Equal(2, second.VersionNo);
        Assert.Equal(firstDraft, draftReplay);
        Assert.Equal(first, replay);
        Assert.Single(history, release => release.Status == VersionStatuses.Current);

        var publicationAudits = await context.AuditEvents.AsNoTracking()
            .Where(audit => audit.Action == "CONFIGURATION_RELEASE_PUBLISHED")
            .OrderBy(audit => audit.OccurredAt)
            .ToListAsync();
        Assert.Equal(2, publicationAudits.Count);
        Assert.All(publicationAudits, audit =>
        {
            Assert.NotNull(audit.BeforeData);
            Assert.NotNull(audit.AfterData);
            AssertSafeAudit(audit);
        });
        Assert.Equal(personCount, await context.People.CountAsync());
        Assert.Equal(roleCount, await context.RoleAssignmentVersions.CountAsync());
        Assert.Empty(context.ChangeTracker.Entries());
    }

    [Fact]
    public async Task PermissionRoleScopeStateReasonAndEtagFailuresHaveNoFunctionalEffect()
    {
        var deniedActor = await ResetAndSeedActorAsync("CONFIG-DENIED", CanonicalRole.Administration);
        var missingScopedRole = await SeedActorAsync("CONFIG-NO-SCOPE", roleCode: null);
        var inactiveEmployment = await SeedActorAsync(
            "CONFIG-INACTIVE-EMPLOYMENT",
            CanonicalRole.Direction,
            employmentStatus: EmploymentStatus.Inactive);
        var inactiveAccount = await SeedActorAsync(
            "CONFIG-INACTIVE-ACCOUNT",
            CanonicalRole.Direction,
            accountStatus: AccountStatus.Inactive);
        await using var deniedContext = CreateContext();
        var deniedService = CreateService(deniedContext, NewUuidGenerator());
        foreach (var actor in new[] { deniedActor, missingScopedRole, inactiveEmployment, inactiveAccount })
        {
            await Assert.ThrowsAsync<ConfigurationAccessDeniedException>(() =>
                deniedService.CreateDraftAsync(CreateCommand(actor)));
        }
        Assert.False(await deniedContext.ConfigurationReleases.AnyAsync());
        Assert.Contains(
            await deniedContext.AuditEvents.AsNoTracking().ToListAsync(),
            audit => audit.Action == "CONFIGURATION_RELEASE_ACCESS_DENIED" && audit.Outcome == "DENIED");

        var direction = await SeedActorAsync("CONFIG-DIRECTION", CanonicalRole.Direction);
        await using var context = CreateContext();
        var service = CreateService(context, NewUuidGenerator());
        var draft = await service.CreateDraftAsync(CreateCommand(direction));
        await Assert.ThrowsAsync<VersioningValidationException>(() => service.PublishAsync(PublishCommand(
            direction,
            draft.Id,
            draft.RowVersion,
            Now,
            " ")));
        await Assert.ThrowsAsync<VersionConflictException>(() => service.PublishAsync(PublishCommand(
            direction,
            draft.Id,
            draft.RowVersion + 1,
            Now,
            "ETag obsoleto")));

        var published = await service.PublishAsync(PublishCommand(
            direction,
            draft.Id,
            draft.RowVersion,
            Now,
            "Publicación válida"));
        await Assert.ThrowsAsync<VersioningStateException>(() => service.PublishAsync(PublishCommand(
            direction,
            published.Id,
            published.RowVersion,
            Now.AddDays(1),
            "Estado inválido")));

        Assert.Single(await context.ConfigurationReleases.AsNoTracking().ToListAsync());
        Assert.Single(await context.AuditEvents.AsNoTracking()
            .Where(audit => audit.Action == "CONFIGURATION_RELEASE_PUBLISHED")
            .ToListAsync());
        Assert.Empty(context.ChangeTracker.Entries());
    }

    [Fact]
    public async Task ConcurrentPublicationsAreSerializedAndLeaveOneCurrentRelease()
    {
        var actorUserId = await ResetAndSeedActorAsync("CONFIG-RACE", CanonicalRole.Direction);
        ConfigurationReleaseDetails firstDraft;
        ConfigurationReleaseDetails secondDraft;
        await using (var setupContext = CreateContext())
        {
            var setupService = CreateService(setupContext, NewUuidGenerator());
            firstDraft = await setupService.CreateDraftAsync(CreateCommand(actorUserId));
            secondDraft = await setupService.CreateDraftAsync(CreateCommand(actorUserId));
        }

        await using var firstContext = CreateContext();
        await using var secondContext = CreateContext();
        var firstService = CreateService(firstContext, NewUuidGenerator());
        var secondService = CreateService(secondContext, NewUuidGenerator());
        var results = await Task.WhenAll(
            CaptureAsync(() => firstService.PublishAsync(PublishCommand(
                actorUserId,
                firstDraft.Id,
                firstDraft.RowVersion,
                Now,
                "Carrera uno"))),
            CaptureAsync(() => secondService.PublishAsync(PublishCommand(
                actorUserId,
                secondDraft.Id,
                secondDraft.RowVersion,
                Now,
                "Carrera dos"))));

        Assert.Single(results, result => result is ConfigurationReleaseDetails);
        Assert.Single(results, result => result is VersioningOverlapException or VersionConflictException);
        await using var verification = CreateContext();
        Assert.Single(await verification.ConfigurationReleases.AsNoTracking()
            .Where(release => release.Status == VersionStatuses.Current)
            .ToListAsync());
        Assert.Empty(firstContext.ChangeTracker.Entries());
        Assert.Empty(secondContext.ChangeTracker.Entries());
    }

    [Fact]
    public async Task AuditFailureRollsBackPublicationAndLeavesTrackerClean()
    {
        var actorUserId = await ResetAndSeedActorAsync("CONFIG-ROLLBACK", CanonicalRole.Direction);
        await using var context = CreateContext();
        var draftService = CreateService(context, NewUuidGenerator());
        var draft = await draftService.CreateDraftAsync(CreateCommand(actorUserId));
        var duplicateAuditId = Guid.CreateVersion7();
        context.AuditEvents.Add(NewExistingAudit(duplicateAuditId, actorUserId));
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var failingService = CreateService(context, new SequenceUuidGenerator(duplicateAuditId));
        await Assert.ThrowsAsync<DbUpdateException>(() => failingService.PublishAsync(PublishCommand(
            actorUserId,
            draft.Id,
            draft.RowVersion,
            Now,
            "Debe revertirse")));

        var persisted = await context.ConfigurationReleases.AsNoTracking().SingleAsync();
        Assert.Equal(VersionStatuses.Draft, persisted.Status);
        Assert.Null(persisted.VersionNo);
        Assert.Equal(1, persisted.RowVersion);
        Assert.Single(await context.AuditEvents.AsNoTracking()
            .Where(audit => audit.Id == duplicateAuditId)
            .ToListAsync());
        Assert.Empty(context.ChangeTracker.Entries());
    }

    private async Task<Guid> ResetAndSeedActorAsync(string stableCode, string roleCode)
    {
        await using var context = CreateContext();
        await context.Database.EnsureDeletedAsync();
        await context.Database.MigrateAsync();
        return await SeedActorAsync(stableCode, roleCode);
    }

    private async Task<Guid> SeedActorAsync(
        string stableCode,
        string? roleCode,
        string employmentStatus = EmploymentStatus.Active,
        string accountStatus = AccountStatus.Active)
    {
        await using var context = CreateContext();
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
            Guid.CreateVersion7(),
            personId,
            BranchScope.LorettaId,
            employmentStatus,
            Now));
        context.AppUsers.Add(new AppUser
        {
            Id = userId,
            PersonId = personId,
            Status = accountStatus,
            MustChangePassword = false,
            MfaEnrolledAt = Now,
            SecurityStamp = $"synthetic-stamp-{userId:N}",
        });
        context.IdentityCredentials.Add(new IdentityCredential
        {
            UserId = userId,
            UserName = $"user.{userId:N}",
            NormalizedUserName = $"USER.{userId:N}",
            PasswordHash = "synthetic-hash-not-a-secret",
        });
        if (roleCode is not null)
        {
            context.RoleAssignmentVersions.Add(new RoleAssignmentVersion
            {
                Id = Guid.CreateVersion7(),
                UserId = userId,
                BranchId = BranchScope.LorettaId,
                RoleCode = roleCode,
                Status = RoleAssignmentStatus.Active,
                ValidFrom = Now,
            });
        }
        await context.SaveChangesAsync();
        return userId;
    }

    private SgolDbContext CreateContext() => new(
        new DbContextOptionsBuilder<SgolDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options);

    private static EfConfigurationReleaseService CreateService(
        SgolDbContext context,
        IUuidGenerator uuidGenerator) => new(
            context,
            new AuditTransaction(context),
            new VersioningTransaction(context, new AuditTransaction(context)),
            new FixedClock(Now),
            uuidGenerator);

    private static CreateConfigurationReleaseCommand CreateCommand(Guid actorUserId) => new(
        actorUserId,
        Guid.CreateVersion7(),
        Guid.CreateVersion7());

    private static PublishConfigurationReleaseCommand PublishCommand(
        Guid actorUserId,
        Guid releaseId,
        long rowVersion,
        DateTimeOffset effectiveFrom,
        string reason,
        Guid? idempotencyKey = null) => new(
            actorUserId,
            idempotencyKey ?? Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            releaseId,
            rowVersion,
            effectiveFrom,
            reason);

    private static AuditEvent NewExistingAudit(Guid id, Guid actorUserId) => new()
    {
        Id = id,
        OccurredAt = Now,
        ActorUserId = actorUserId,
        ActorType = "APP_USER",
        Action = "SYNTHETIC_EXISTING_EVENT",
        ResourceType = "CONFIGURATION_RELEASE",
        BranchId = BranchScope.LorettaId,
        CorrelationId = Guid.CreateVersion7(),
        Outcome = "SUCCESS",
    };

    private static void AssertSafeAudit(AuditEvent audit)
    {
        var text = (audit.BeforeData?.RootElement.GetRawText() ?? string.Empty) +
            (audit.AfterData?.RootElement.GetRawText() ?? string.Empty);
        Assert.DoesNotContain("password", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("securityStamp", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("totp", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("recovery", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("connection", text, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<object> CaptureAsync(Func<Task<ConfigurationReleaseDetails>> action)
    {
        try
        {
            return await action();
        }
        catch (Exception exception)
        {
            return exception;
        }
    }

    private static Uuid7Generator NewUuidGenerator() => new(new FixedClock(Now));

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
