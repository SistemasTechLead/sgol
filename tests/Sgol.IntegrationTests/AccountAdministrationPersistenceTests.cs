using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Sgol.BuildingBlocks.Identifiers;
using Sgol.BuildingBlocks.Time;
using Sgol.Identity.Contracts;
using Sgol.Organization.Contracts;
using Sgol.Web.Infrastructure.Persistence;
using Sgol.Web.Infrastructure.Persistence.Auditing;
using Sgol.Web.Infrastructure.Persistence.Bootstrap;
using Sgol.Web.Infrastructure.Persistence.Identity;
using Testcontainers.PostgreSql;
using Xunit;

namespace Sgol.IntegrationTests;

public sealed class AccountAdministrationPersistenceTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 9, 2, 12, 0, 0, TimeSpan.Zero);
    private const string InitialPassword = "synthetic-account-password-one";
    private const string ReactivationPassword = "synthetic-account-password-two";
    private readonly PostgreSqlContainer _postgres = PostgreSqlPersistenceTests.CreateContainerForTests();

    public Task InitializeAsync() => _postgres.StartAsync();

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    [Fact]
    public async Task DirectionCreatesIndividualAccountWithoutChangingEmploymentOrRole()
    {
        var actorUserId = await ResetAndSeedActorAsync(BootstrapContract.DirectionRoleCode);
        var personId = await SeedPersonAsync("PER-ACCOUNT-001", EmploymentStatus.Active);
        await using var context = CreateContext();
        var service = CreateService(context, NewUuidGenerator(Now), Now);

        var result = await service.CreateAsync(CreateCommand(actorUserId, personId, "person.one"));

        var user = await context.AppUsers.AsNoTracking().SingleAsync(item => item.Id == result.Account.Id);
        var credential = await context.IdentityCredentials.AsNoTracking().SingleAsync(item => item.UserId == user.Id);
        var audit = await context.AuditEvents.AsNoTracking().SingleAsync(item => item.ResourceId == user.Id);
        Assert.Equal(personId, user.PersonId);
        Assert.Equal(AccountStatus.Active, user.Status);
        Assert.True(user.MustChangePassword);
        Assert.Equal("person.one", credential.UserName);
        Assert.NotEqual(InitialPassword, credential.PasswordHash);
        Assert.False(await context.RoleAssignmentVersions.AnyAsync(item => item.UserId == user.Id));
        Assert.Single(await context.EmploymentVersions
            .AsNoTracking()
            .Where(item => item.PersonId == personId)
            .ToListAsync());
        Assert.Equal("USER_CREATED", audit.Action);
        Assert.Null(audit.BeforeData);
        Assert.DoesNotContain(InitialPassword, audit.AfterData!.RootElement.GetRawText(), StringComparison.Ordinal);
        Assert.False(audit.AfterData.RootElement.TryGetProperty("password", out _));
        Assert.False(audit.AfterData.RootElement.TryGetProperty("passwordHash", out _));
        Assert.False(audit.AfterData.RootElement.TryGetProperty("securityStamp", out _));
        Assert.Empty(context.ChangeTracker.Entries());
    }

    [Fact]
    public async Task MissingOrInactiveLorettaPersonIsRejectedWithoutAccountEffect()
    {
        var actorUserId = await ResetAndSeedActorAsync(BootstrapContract.DirectionRoleCode);
        var inactivePersonId = await SeedPersonAsync("PER-INACTIVE", EmploymentStatus.Inactive);
        await using var context = CreateContext();
        var service = CreateService(context, NewUuidGenerator(Now), Now);

        await Assert.ThrowsAsync<AccountPersonNotFoundException>(() =>
            service.CreateAsync(CreateCommand(actorUserId, Guid.CreateVersion7(), "missing.person")));
        await Assert.ThrowsAsync<AccountPersonOutOfScopeException>(() =>
            service.CreateAsync(CreateCommand(actorUserId, inactivePersonId, "inactive.person")));

        Assert.Equal(1, await context.AppUsers.CountAsync());
        Assert.False(await context.IdentityCredentials.AnyAsync(item => item.UserName != "actor.synthetic"));
        Assert.Empty(context.ChangeTracker.Entries());
    }

    [Fact]
    public async Task DuplicatePersonOrAccessIdentifierIsRejectedWithoutSharedOrPartialAccount()
    {
        var actorUserId = await ResetAndSeedActorAsync(BootstrapContract.DirectionRoleCode);
        var firstPersonId = await SeedPersonAsync("PER-DUP-001", EmploymentStatus.Active);
        var secondPersonId = await SeedPersonAsync("PER-DUP-002", EmploymentStatus.Active);
        await using var context = CreateContext();
        var service = CreateService(context, NewUuidGenerator(Now), Now);

        var first = await service.CreateAsync(CreateCommand(actorUserId, firstPersonId, "unique.access"));
        await Assert.ThrowsAsync<AccountConflictException>(() =>
            service.CreateAsync(CreateCommand(actorUserId, firstPersonId, "other.access")));
        await Assert.ThrowsAsync<AccountConflictException>(() =>
            service.CreateAsync(CreateCommand(actorUserId, secondPersonId, "UNIQUE.ACCESS")));

        Assert.Equal(1, await context.AppUsers.CountAsync(item => item.Id != actorUserId));
        Assert.Equal(first.Account.Id, await context.AppUsers
            .Where(item => item.PersonId == firstPersonId)
            .Select(item => item.Id)
            .SingleAsync());
        Assert.False(await context.AppUsers.AnyAsync(item => item.PersonId == secondPersonId));
        Assert.Equal(1, await context.AuditEvents.CountAsync(item => item.ResourceId == first.Account.Id));
        Assert.Empty(context.ChangeTracker.Entries());
    }

    [Fact]
    public async Task DeactivateAndReactivatePreserveAuditHistoryAndRotateSecureActivation()
    {
        var actorUserId = await ResetAndSeedActorAsync(BootstrapContract.DirectionRoleCode);
        var personId = await SeedPersonAsync("PER-CYCLE-001", EmploymentStatus.Active);
        await using var context = CreateContext();
        var service = CreateService(context, NewUuidGenerator(Now), Now);
        var created = await service.CreateAsync(CreateCommand(actorUserId, personId, "cycle.person"));
        var stampBefore = await context.AppUsers
            .Where(item => item.Id == created.Account.Id)
            .Select(item => item.SecurityStamp)
            .SingleAsync();

        var deactivated = await service.DeactivateAsync(StatusCommand(
            actorUserId,
            created.Account.Id,
            "Baja sintética"));
        var reactivated = await service.ReactivateAsync(StatusCommand(
            actorUserId,
            created.Account.Id,
            "Reactivación sintética",
            ReactivationPassword));

        var user = await context.AppUsers.AsNoTracking().SingleAsync(item => item.Id == created.Account.Id);
        var credential = await context.IdentityCredentials.AsNoTracking().SingleAsync(item => item.UserId == user.Id);
        var audits = await context.AuditEvents
            .AsNoTracking()
            .Where(item => item.ResourceId == user.Id)
            .OrderBy(item => item.OccurredAt)
            .ThenBy(item => item.Action)
            .ToListAsync();
        Assert.Equal(AccountStatus.Inactive, deactivated.Account.Status);
        Assert.Equal(AccountStatus.Active, reactivated.Account.Status);
        Assert.Equal(AccountStatus.Active, user.Status);
        Assert.True(user.MustChangePassword);
        Assert.NotEqual(stampBefore, user.SecurityStamp);
        Assert.Equal(
            PasswordVerificationResult.Success,
            new PasswordHasher<AppUser>().VerifyHashedPassword(user, credential.PasswordHash, ReactivationPassword));
        Assert.Equal(3, audits.Count);
        Assert.Contains(audits, item =>
            item.Action == "USER_DEACTIVATED" &&
            item.BeforeData!.RootElement.GetProperty("status").GetString() == AccountStatus.Active &&
            item.AfterData!.RootElement.GetProperty("status").GetString() == AccountStatus.Inactive);
        Assert.Contains(audits, item =>
            item.Action == "USER_REACTIVATED" &&
            item.BeforeData!.RootElement.GetProperty("status").GetString() == AccountStatus.Inactive &&
            item.AfterData!.RootElement.GetProperty("status").GetString() == AccountStatus.Active);
        Assert.All(audits, item => Assert.DoesNotContain(
            ReactivationPassword,
            (item.BeforeData?.RootElement.GetRawText() ?? string.Empty) +
            (item.AfterData?.RootElement.GetRawText() ?? string.Empty),
            StringComparison.Ordinal));
        Assert.Empty(context.ChangeTracker.Entries());
    }

    [Fact]
    public async Task ActorWithoutUserAdminAuthorityIsDeniedAuditedAndHasNoEffect()
    {
        var actorUserId = await ResetAndSeedActorAsync("ADMINISTRACION");
        var personId = await SeedPersonAsync("PER-DENIED", EmploymentStatus.Active);
        await using var context = CreateContext();
        var service = CreateService(context, NewUuidGenerator(Now), Now);

        await Assert.ThrowsAsync<AccountAccessDeniedException>(() =>
            service.CreateAsync(CreateCommand(actorUserId, personId, "denied.person")));

        Assert.False(await context.AppUsers.AnyAsync(item => item.PersonId == personId));
        Assert.Contains(await context.AuditEvents.AsNoTracking().ToListAsync(), item =>
            item.Action == "USER_ADMIN_ACCESS_DENIED" && item.Outcome == "DENIED");
        Assert.Empty(context.ChangeTracker.Entries());
    }

    [Fact]
    public async Task SameKeyReplaysSameBodyConflictsOnDifferentBodyAndWorksAcrossOperationScopes()
    {
        var actorUserId = await ResetAndSeedActorAsync(BootstrapContract.DirectionRoleCode);
        var personId = await SeedPersonAsync("PER-IDEMPOTENT", EmploymentStatus.Active);
        await using var context = CreateContext();
        var service = CreateService(context, NewUuidGenerator(Now), Now);
        var sharedKey = Guid.CreateVersion7();
        var create = CreateCommand(actorUserId, personId, "idempotent.person", sharedKey);

        var first = await service.CreateAsync(create);
        var replay = await service.CreateAsync(create);
        await Assert.ThrowsAsync<AccountIdempotencyConflictException>(() =>
            service.CreateAsync(CreateCommand(actorUserId, personId, "changed.person", sharedKey)));
        var deactivated = await service.DeactivateAsync(StatusCommand(
            actorUserId,
            first.Account.Id,
            "Baja idempotente",
            key: sharedKey));
        var statusReplay = await service.DeactivateAsync(StatusCommand(
            actorUserId,
            first.Account.Id,
            "Baja idempotente",
            key: sharedKey));

        Assert.True(replay.Replayed);
        Assert.True(statusReplay.Replayed);
        Assert.Equal(first.Account.Id, replay.Account.Id);
        Assert.Equal(AccountStatus.Inactive, deactivated.Account.Status);
        Assert.Equal(2, await context.IdempotencyRecords.CountAsync(item => item.ResourceId == first.Account.Id));
        Assert.Empty(context.ChangeTracker.Entries());
    }

    [Fact]
    public async Task ConcurrentCreationIsGovernedByPersistentUniqueConstraint()
    {
        var actorUserId = await ResetAndSeedActorAsync(BootstrapContract.DirectionRoleCode);
        var personId = await SeedPersonAsync("PER-RACE", EmploymentStatus.Active);
        await using var firstContext = CreateContext();
        await using var secondContext = CreateContext();
        var firstService = CreateService(firstContext, NewUuidGenerator(Now), Now);
        var secondService = CreateService(secondContext, NewUuidGenerator(Now.AddMilliseconds(1)), Now.AddMilliseconds(1));

        var outcomes = await Task.WhenAll(
            CaptureAsync(() => firstService.CreateAsync(CreateCommand(actorUserId, personId, "race.one"))),
            CaptureAsync(() => secondService.CreateAsync(CreateCommand(actorUserId, personId, "race.two"))));

        Assert.Single(outcomes, outcome => outcome is AccountMutationResult);
        Assert.Single(outcomes, outcome => outcome is AccountConflictException);
        await using var verification = CreateContext();
        Assert.Equal(1, await verification.AppUsers.CountAsync(item => item.PersonId == personId));
        Assert.Equal(1, await verification.AuditEvents.CountAsync(item => item.Action == "USER_CREATED"));
        Assert.Empty(firstContext.ChangeTracker.Entries());
        Assert.Empty(secondContext.ChangeTracker.Entries());
    }

    [Fact]
    public async Task AuditFailureRollsBackFunctionalCredentialAndIdempotencyWrites()
    {
        var actorUserId = await ResetAndSeedActorAsync(BootstrapContract.DirectionRoleCode);
        var personId = await SeedPersonAsync("PER-AUDIT-ROLLBACK", EmploymentStatus.Active);
        var duplicateAuditId = Guid.CreateVersion7();
        await using (var seedContext = CreateContext())
        {
            seedContext.AuditEvents.Add(new AuditEvent
            {
                Id = duplicateAuditId,
                OccurredAt = Now,
                ActorUserId = actorUserId,
                ActorType = "APP_USER",
                Action = "SYNTHETIC_EXISTING_EVENT",
                ResourceType = "APP_USER",
                BranchId = BranchScope.LorettaId,
                CorrelationId = Guid.CreateVersion7(),
                Outcome = "SUCCESS",
            });
            await seedContext.SaveChangesAsync();
        }

        await using var context = CreateContext();
        var userId = Guid.CreateVersion7();
        var service = CreateService(
            context,
            new SequenceUuidGenerator(userId, duplicateAuditId),
            Now.AddMinutes(1));

        await Assert.ThrowsAsync<DbUpdateException>(() =>
            service.CreateAsync(CreateCommand(actorUserId, personId, "audit.rollback")));

        Assert.False(await context.AppUsers.AsNoTracking().AnyAsync(item => item.Id == userId));
        Assert.False(await context.IdentityCredentials.AsNoTracking().AnyAsync(item => item.UserId == userId));
        Assert.False(await context.IdempotencyRecords.AsNoTracking().AnyAsync(item => item.ResourceId == userId));
        Assert.Empty(context.ChangeTracker.Entries());
    }

    [Fact]
    public async Task FunctionalWriteFailureRollsBackAuditAndLeavesTrackerClean()
    {
        var actorUserId = await ResetAndSeedActorAsync(BootstrapContract.DirectionRoleCode);
        var personId = await SeedPersonAsync("PER-FUNCTIONAL-ROLLBACK", EmploymentStatus.Active);
        await using var context = CreateContext();
        await context.Database.ExecuteSqlRawAsync("""
            CREATE FUNCTION sgol_reject_synthetic_app_user() RETURNS trigger AS $$
            BEGIN
                RAISE EXCEPTION 'synthetic app_user rejection';
            END;
            $$ LANGUAGE plpgsql;
            CREATE TRIGGER synthetic_reject_app_user
            BEFORE INSERT ON app_user
            FOR EACH ROW EXECUTE FUNCTION sgol_reject_synthetic_app_user();
            """);
        var service = CreateService(context, NewUuidGenerator(Now), Now);

        await Assert.ThrowsAsync<DbUpdateException>(() =>
            service.CreateAsync(CreateCommand(actorUserId, personId, "functional.rollback")));

        Assert.False(await context.AppUsers.AsNoTracking().AnyAsync(item => item.PersonId == personId));
        Assert.False(await context.AuditEvents.AsNoTracking().AnyAsync(item => item.Action == "USER_CREATED"));
        Assert.Empty(context.ChangeTracker.Entries());
    }

    private async Task<Guid> ResetAndSeedActorAsync(string roleCode)
    {
        await using var context = CreateContext();
        await context.Database.EnsureDeletedAsync();
        await context.Database.MigrateAsync();
        var personId = Guid.CreateVersion7();
        var actorUserId = Guid.CreateVersion7();
        context.People.Add(new Person
        {
            Id = personId,
            StableCode = $"ACTOR-{actorUserId:N}",
            DisplayName = "Actor sintético",
            CreatedAt = Now,
        });
        context.EmploymentVersions.Add(new EmploymentVersion(
            Guid.CreateVersion7(),
            personId,
            BranchScope.LorettaId,
            EmploymentStatus.Active,
            Now));
        context.AppUsers.Add(new AppUser
        {
            Id = actorUserId,
            PersonId = personId,
            Status = AccountStatus.Active,
            MustChangePassword = false,
            MfaEnrolledAt = Now,
            SecurityStamp = "synthetic-actor-security-stamp",
        });
        context.IdentityCredentials.Add(new IdentityCredential
        {
            UserId = actorUserId,
            UserName = "actor.synthetic",
            NormalizedUserName = "ACTOR.SYNTHETIC",
            PasswordHash = "synthetic-hash-not-a-secret",
        });
        context.RoleAssignmentVersions.Add(new RoleAssignmentVersion
        {
            Id = Guid.CreateVersion7(),
            UserId = actorUserId,
            BranchId = BranchScope.LorettaId,
            RoleCode = roleCode,
            Status = BootstrapContract.ActiveRoleStatus,
            ValidFrom = Now,
        });
        await context.SaveChangesAsync();
        return actorUserId;
    }

    private async Task<Guid> SeedPersonAsync(string stableCode, string employmentStatus)
    {
        await using var context = CreateContext();
        var personId = Guid.CreateVersion7();
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
        await context.SaveChangesAsync();
        return personId;
    }

    private SgolDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<SgolDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;
        return new SgolDbContext(options);
    }

    private static EfAccountAdministrationService CreateService(
        SgolDbContext context,
        IUuidGenerator uuidGenerator,
        DateTimeOffset now) => new(
            context,
            new AuditTransaction(context),
            new FixedClock(now),
            uuidGenerator,
            new PasswordHasher<AppUser>());

    private static CreateAccountCommand CreateCommand(
        Guid actorUserId,
        Guid personId,
        string userName,
        Guid? key = null) => new()
        {
            ActorUserId = actorUserId,
            IdempotencyKey = key ?? Guid.CreateVersion7(),
            CorrelationId = Guid.CreateVersion7(),
            PersonId = personId,
            UserName = userName,
            TemporaryPassword = InitialPassword,
        };

    private static ChangeAccountStatusCommand StatusCommand(
        Guid actorUserId,
        Guid userId,
        string reason,
        string? temporaryPassword = null,
        Guid? key = null) => new()
        {
            ActorUserId = actorUserId,
            IdempotencyKey = key ?? Guid.CreateVersion7(),
            CorrelationId = Guid.CreateVersion7(),
            UserId = userId,
            Reason = reason,
            TemporaryPassword = temporaryPassword,
        };

    private static async Task<object> CaptureAsync(Func<Task<AccountMutationResult>> action)
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

    private static Uuid7Generator NewUuidGenerator(DateTimeOffset now) => new(new FixedClock(now));

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
