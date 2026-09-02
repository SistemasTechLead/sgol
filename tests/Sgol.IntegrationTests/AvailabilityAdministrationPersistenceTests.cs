using Microsoft.EntityFrameworkCore;
using Sgol.BuildingBlocks.Identifiers;
using Sgol.BuildingBlocks.Time;
using Sgol.Identity.Contracts;
using Sgol.Organization.Contracts;
using Sgol.Web.Infrastructure.Persistence;
using Sgol.Web.Infrastructure.Persistence.Auditing;
using Sgol.Web.Infrastructure.Persistence.Bootstrap;
using Sgol.Web.Infrastructure.Persistence.Organization;
using Testcontainers.PostgreSql;
using Xunit;

namespace Sgol.IntegrationTests;

public sealed class AvailabilityAdministrationPersistenceTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 9, 2, 0, 30, 0, TimeSpan.Zero);
    private readonly PostgreSqlContainer _postgres = PostgreSqlPersistenceTests.CreateContainerForTests();

    public Task InitializeAsync() => _postgres.StartAsync();

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    [Fact]
    public async Task DirectionRegistersTrueAndFalseWhileAbsenceRemainsMissingAndDateDoesNotShift()
    {
        var seed = await ResetAndSeedAsync("AVAIL-BINARY");
        await using var context = CreateContext();
        var service = CreateService(context, NewUuidGenerator());
        var availableDate = new DateOnly(2026, 9, 1);
        var unavailableDate = new DateOnly(2026, 9, 2);

        var available = await service.PutAsync(Command(seed.ActorUserId, seed.TargetPersonId, availableDate, true));
        var unavailable = await service.PutAsync(Command(seed.ActorUserId, seed.TargetPersonId, unavailableDate, false));
        var queried = await service.GetAsync(
            seed.ActorUserId,
            Guid.CreateVersion7(),
            seed.TargetPersonId,
            new DateOnly(2026, 8, 31),
            new DateOnly(2026, 9, 3));

        Assert.True(available.IsAvailable);
        Assert.False(unavailable.IsAvailable);
        Assert.Equal([availableDate, unavailableDate], queried.Select(item => item.LocalDate));
        Assert.DoesNotContain(queried, item => item.LocalDate == new DateOnly(2026, 8, 31));
        Assert.Equal(2, await context.AvailabilityDayVersions.CountAsync());
        Assert.All(
            await context.AvailabilityDayVersions.AsNoTracking().ToListAsync(),
            item => Assert.Equal(AvailabilityVersionStatus.Current, item.Status));
        Assert.Empty(context.ChangeTracker.Entries());
    }

    [Fact]
    public async Task CorrectionPreservesHistoryRequiresCurrentETagAndLeavesExactlyOneCurrentVersion()
    {
        var seed = await ResetAndSeedAsync("AVAIL-HISTORY");
        await using var context = CreateContext();
        var service = CreateService(context, NewUuidGenerator());
        var localDate = new DateOnly(2026, 9, 2);
        var first = await service.PutAsync(Command(seed.ActorUserId, seed.TargetPersonId, localDate, true));

        await Assert.ThrowsAsync<AvailabilityIfMatchRequiredException>(() =>
            service.PutAsync(Command(seed.ActorUserId, seed.TargetPersonId, localDate, false)));
        await Assert.ThrowsAsync<AvailabilityVersionConflictException>(() =>
            service.PutAsync(Command(seed.ActorUserId, seed.TargetPersonId, localDate, false, expectedRowVersion: 9)));
        var corrected = await service.PutAsync(
            Command(seed.ActorUserId, seed.TargetPersonId, localDate, false, first.RowVersion));

        var history = await context.AvailabilityDayVersions.AsNoTracking()
            .Where(item => item.PersonId == seed.TargetPersonId && item.LocalDate == localDate)
            .OrderBy(item => item.RowVersion)
            .ThenBy(item => item.Status)
            .ToListAsync();
        Assert.Equal(2, history.Count);
        Assert.Single(history, item => item.Status == AvailabilityVersionStatus.Current);
        Assert.Single(history, item => item.Status == AvailabilityVersionStatus.Historical);
        Assert.True(history[0].IsAvailable);
        Assert.False(corrected.IsAvailable);
        Assert.Equal(2, corrected.RowVersion);
        Assert.Equal(first.Id, history.Single(item => item.Status == AvailabilityVersionStatus.Current).SupersedesId);
        Assert.Empty(context.ChangeTracker.Entries());
    }

    [Fact]
    public async Task AuthorizationAndTargetStateAreDeniedWithoutFunctionalEffect()
    {
        var seed = await ResetAndSeedAsync("AVAIL-DENIED", actorRole: CanonicalRole.Administration);
        var inactivePersonId = await SeedTargetAsync("AVAIL-INACTIVE", EmploymentStatus.Inactive);
        await using var context = CreateContext();
        var service = CreateService(context, NewUuidGenerator());
        var date = new DateOnly(2026, 9, 2);

        await Assert.ThrowsAsync<AvailabilityAccessDeniedException>(() =>
            service.PutAsync(Command(seed.ActorUserId, seed.TargetPersonId, date, true)));

        var direction = await SeedActorAsync("AVAIL-DIRECTION", CanonicalRole.Direction);
        await Assert.ThrowsAsync<AvailabilityPersonNotFoundException>(() =>
            service.PutAsync(Command(direction, Guid.CreateVersion7(), date, true)));
        await Assert.ThrowsAsync<AvailabilityPersonInactiveException>(() =>
            service.PutAsync(Command(direction, inactivePersonId, date, true)));

        Assert.False(await context.AvailabilityDayVersions.AnyAsync());
        Assert.Contains(
            await context.AuditEvents.AsNoTracking().ToListAsync(),
            item => item.Action == "AVAILABILITY_ACCESS_DENIED" && item.Outcome == "DENIED");
        Assert.Empty(context.ChangeTracker.Entries());
    }

    [Fact]
    public async Task RangeIsInclusiveBoundedAndReturnsOnlyCurrentValues()
    {
        var seed = await ResetAndSeedAsync("AVAIL-RANGE");
        await using var context = CreateContext();
        var service = CreateService(context, NewUuidGenerator());
        var start = new DateOnly(2026, 1, 1);
        var end = start.AddDays(EfAvailabilityAdministrationService.MaximumQueryDays - 1);

        await service.PutAsync(Command(seed.ActorUserId, seed.TargetPersonId, start, true));
        await service.PutAsync(Command(seed.ActorUserId, seed.TargetPersonId, end, false));
        var values = await service.GetAsync(
            seed.ActorUserId,
            Guid.CreateVersion7(),
            seed.TargetPersonId,
            start,
            end);

        Assert.Equal(2, values.Count);
        await Assert.ThrowsAsync<AvailabilityValidationException>(() => service.GetAsync(
            seed.ActorUserId,
            Guid.CreateVersion7(),
            seed.TargetPersonId,
            start,
            end.AddDays(1)));
        await Assert.ThrowsAsync<AvailabilityValidationException>(() => service.GetAsync(
            seed.ActorUserId,
            Guid.CreateVersion7(),
            seed.TargetPersonId,
            end,
            start));
    }

    [Fact]
    public async Task ConcurrentCorrectionsLeaveOneCurrentVersionUnderPostgreSqlConstraint()
    {
        var seed = await ResetAndSeedAsync("AVAIL-CONCURRENT");
        var localDate = new DateOnly(2026, 9, 2);
        await using (var initialContext = CreateContext())
        {
            await CreateService(initialContext, NewUuidGenerator()).PutAsync(
                Command(seed.ActorUserId, seed.TargetPersonId, localDate, true));
        }

        await using var firstContext = CreateContext();
        await using var secondContext = CreateContext();
        var firstService = CreateService(firstContext, NewUuidGenerator());
        var secondService = CreateService(secondContext, NewUuidGenerator());
        var outcomes = await Task.WhenAll(
            CaptureAsync(() => firstService.PutAsync(
                Command(seed.ActorUserId, seed.TargetPersonId, localDate, false, expectedRowVersion: 1))),
            CaptureAsync(() => secondService.PutAsync(
                Command(seed.ActorUserId, seed.TargetPersonId, localDate, true, expectedRowVersion: 1))));

        Assert.Single(outcomes, result => result is AvailabilityDaySnapshot);
        Assert.Single(outcomes, result => result is AvailabilityVersionConflictException);
        await using var verification = CreateContext();
        Assert.Equal(
            1,
            await verification.AvailabilityDayVersions.CountAsync(item =>
                item.PersonId == seed.TargetPersonId &&
                item.LocalDate == localDate &&
                item.Status == AvailabilityVersionStatus.Current));
        Assert.Equal(2, await verification.AvailabilityDayVersions.CountAsync(item =>
            item.PersonId == seed.TargetPersonId && item.LocalDate == localDate));
        Assert.Empty(firstContext.ChangeTracker.Entries());
        Assert.Empty(secondContext.ChangeTracker.Entries());
    }

    [Fact]
    public async Task PostgreSqlRejectsSecondCurrentVersionAndNonLorettaBranch()
    {
        var seed = await ResetAndSeedAsync("AVAIL-CONSTRAINT");
        await using var context = CreateContext();
        var localDate = new DateOnly(2026, 9, 2);
        context.AvailabilityDayVersions.AddRange(
            new AvailabilityDayVersion(
                Guid.CreateVersion7(), seed.TargetPersonId, BranchScope.LorettaId, localDate, true, seed.ActorUserId),
            new AvailabilityDayVersion(
                Guid.CreateVersion7(), seed.TargetPersonId, BranchScope.LorettaId, localDate, false, seed.ActorUserId));
        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        context.ChangeTracker.Clear();

        context.Branches.Add(new Branch
        {
            Id = Guid.CreateVersion7(),
            Code = "OTHER-001",
            Name = "Sucursal no autorizada",
            Status = BranchScope.ActiveStatus,
            TimeZone = BranchScope.TimeZone,
        });
        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        context.ChangeTracker.Clear();
        Assert.False(await context.AvailabilityDayVersions.AnyAsync());
        Assert.Single(await context.Branches.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task AuditAndFunctionalFailuresRollbackAndSuccessfulWriteChangesNoAuthorityOrCredentials()
    {
        var seed = await ResetAndSeedAsync("AVAIL-ROLLBACK");
        await using var context = CreateContext();
        var accountBefore = await context.AppUsers.AsNoTracking().SingleAsync(item => item.Id == seed.TargetUserId);
        var credentialBefore = await context.IdentityCredentials.AsNoTracking().SingleAsync(item => item.UserId == seed.TargetUserId);
        var employmentBefore = await context.EmploymentVersions.AsNoTracking()
            .SingleAsync(item => item.PersonId == seed.TargetPersonId && item.ValidTo == null);
        var roleBefore = await context.RoleAssignmentVersions.AsNoTracking()
            .SingleAsync(item => item.UserId == seed.TargetUserId && item.Status == RoleAssignmentStatus.Active);
        var service = CreateService(context, NewUuidGenerator());
        var successful = await service.PutAsync(
            Command(seed.ActorUserId, seed.TargetPersonId, new DateOnly(2026, 9, 1), true));
        var audit = await context.AuditEvents.AsNoTracking()
            .SingleAsync(item => item.ResourceId == successful.Id);

        Assert.Null(audit.BeforeData);
        Assert.True(audit.AfterData!.RootElement.GetProperty("isAvailable").GetBoolean());
        AssertSafeAudit(audit);
        Assert.Equal(accountBefore.SecurityStamp, (await context.AppUsers.AsNoTracking().SingleAsync(item => item.Id == seed.TargetUserId)).SecurityStamp);
        Assert.Equal(credentialBefore.PasswordHash, (await context.IdentityCredentials.AsNoTracking().SingleAsync(item => item.UserId == seed.TargetUserId)).PasswordHash);
        Assert.Equal(employmentBefore.Id, (await context.EmploymentVersions.AsNoTracking().SingleAsync(item => item.PersonId == seed.TargetPersonId && item.ValidTo == null)).Id);
        Assert.Equal(roleBefore.Id, (await context.RoleAssignmentVersions.AsNoTracking().SingleAsync(item => item.UserId == seed.TargetUserId && item.Status == RoleAssignmentStatus.Active)).Id);

        var duplicateAuditId = Guid.CreateVersion7();
        context.AuditEvents.Add(NewAudit(duplicateAuditId, seed.ActorUserId));
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        var auditFailureService = CreateService(
            context,
            new SequenceUuidGenerator(Guid.CreateVersion7(), duplicateAuditId));
        await Assert.ThrowsAsync<DbUpdateException>(() => auditFailureService.PutAsync(
            Command(seed.ActorUserId, seed.TargetPersonId, new DateOnly(2026, 9, 2), false)));
        Assert.False(await context.AvailabilityDayVersions.AsNoTracking()
            .AnyAsync(item => item.LocalDate == new DateOnly(2026, 9, 2)));

        var duplicateVersionId = successful.Id;
        var functionalFailureAuditId = Guid.CreateVersion7();
        var functionalFailureService = CreateService(
            context,
            new SequenceUuidGenerator(duplicateVersionId, functionalFailureAuditId));
        await Assert.ThrowsAsync<DbUpdateException>(() => functionalFailureService.PutAsync(
            Command(seed.ActorUserId, seed.TargetPersonId, new DateOnly(2026, 9, 3), true)));
        Assert.False(await context.AuditEvents.AsNoTracking().AnyAsync(item => item.Id == functionalFailureAuditId));
        Assert.False(await context.AvailabilityDayVersions.AsNoTracking()
            .AnyAsync(item => item.LocalDate == new DateOnly(2026, 9, 3)));
        Assert.Empty(context.ChangeTracker.Entries());
    }

    private async Task<Seed> ResetAndSeedAsync(string prefix, string actorRole = CanonicalRole.Direction)
    {
        await using var context = CreateContext();
        await context.Database.EnsureDeletedAsync();
        await context.Database.MigrateAsync();
        var actorUserId = await SeedActorAsync($"{prefix}-ACTOR", actorRole);
        var target = await SeedTargetWithAccountAsync($"{prefix}-TARGET", EmploymentStatus.Active);
        return new Seed(actorUserId, target.PersonId, target.UserId);
    }

    private async Task<Guid> SeedActorAsync(string stableCode, string roleCode)
    {
        var actor = await SeedTargetWithAccountAsync(stableCode, EmploymentStatus.Active, roleCode);
        return actor.UserId;
    }

    private async Task<Guid> SeedTargetAsync(string stableCode, string employmentStatus)
    {
        var target = await SeedTargetWithAccountAsync(stableCode, employmentStatus, roleCode: null);
        return target.PersonId;
    }

    private async Task<(Guid PersonId, Guid UserId)> SeedTargetWithAccountAsync(
        string stableCode,
        string employmentStatus,
        string? roleCode = CanonicalRole.SalesFloor)
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
            Now,
            positionText: "Director",
            shiftText: "Turno sintético"));
        context.AppUsers.Add(new AppUser
        {
            Id = userId,
            PersonId = personId,
            Status = AccountStatus.Active,
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
        return (personId, userId);
    }

    private SgolDbContext CreateContext() => new(
        new DbContextOptionsBuilder<SgolDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options);

    private static EfAvailabilityAdministrationService CreateService(
        SgolDbContext context,
        IUuidGenerator uuidGenerator) => new(
            context,
            new AuditTransaction(context),
            new FixedClock(Now),
            uuidGenerator);

    private static PutAvailabilityCommand Command(
        Guid actorUserId,
        Guid personId,
        DateOnly localDate,
        bool isAvailable,
        long? expectedRowVersion = null) => new(
            actorUserId,
            Guid.CreateVersion7(),
            personId,
            localDate,
            isAvailable,
            expectedRowVersion);

    private static AuditEvent NewAudit(Guid id, Guid actorUserId) => new()
    {
        Id = id,
        OccurredAt = Now,
        ActorUserId = actorUserId,
        ActorType = "APP_USER",
        Action = "SYNTHETIC_EXISTING_EVENT",
        ResourceType = "AVAILABILITY_DAY",
        BranchId = BranchScope.LorettaId,
        CorrelationId = Guid.CreateVersion7(),
        Outcome = "SUCCESS",
    };

    private static void AssertSafeAudit(AuditEvent audit)
    {
        var text = (audit.BeforeData?.RootElement.GetRawText() ?? string.Empty) +
            (audit.AfterData?.RootElement.GetRawText() ?? string.Empty);
        Assert.DoesNotContain("securityStamp", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("password", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("totp", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("recovery", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("position", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("shift", text, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<object> CaptureAsync(Func<Task<AvailabilityDaySnapshot>> action)
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

    private sealed record Seed(Guid ActorUserId, Guid TargetPersonId, Guid TargetUserId);

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
