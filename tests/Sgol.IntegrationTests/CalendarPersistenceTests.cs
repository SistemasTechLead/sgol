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

public sealed class CalendarPersistenceTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 9, 3, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Monday = new(2026, 9, 14);
    private static readonly DateOnly Holiday = new(2026, 9, 16);
    private readonly PostgreSqlContainer _postgres = PostgreSqlPersistenceTests.CreateContainerForTests();

    public Task InitializeAsync() => _postgres.StartAsync();

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    [Fact]
    public async Task DraftReleasePublishesMondayHolidayAndSuccessorWithHistoryAndServerZone()
    {
        var actorUserId = await ResetAndSeedActorAsync("CALENDAR-CYCLE", CanonicalRole.Direction);
        await using var context = CreateContext();
        var releases = CreateReleaseService(context, NewUuidGenerator());
        var calendar = CreateCalendarService(context, NewUuidGenerator());
        var personCount = await context.People.CountAsync();
        var roleCount = await context.RoleAssignmentVersions.CountAsync();

        var firstRelease = await releases.CreateDraftAsync(CreateReleaseCommand(actorUserId));
        await calendar.PutAsync(PutCommand(
            actorUserId,
            firstRelease.Id,
            Monday,
            CalendarContract.WorkingDay,
            true,
            "Semana ISO inicia en lunes"));
        await calendar.PutAsync(PutCommand(
            actorUserId,
            firstRelease.Id,
            Holiday,
            CalendarContract.Holiday,
            false,
            "Festivo configurado"));
        await releases.PublishAsync(PublishCommand(
            actorUserId,
            firstRelease,
            Now,
            "Calendario inicial"));

        var initial = await calendar.GetAsync(actorUserId, Guid.CreateVersion7(), Monday, Holiday);
        Assert.Equal(DayOfWeek.Monday, initial[0].LocalDate.DayOfWeek);
        Assert.Equal(CalendarContract.WorkingDay, initial[0].DayType);
        Assert.True(initial[0].IsWorkingDay);
        Assert.Equal(CalendarContract.Holiday, initial[1].DayType);
        Assert.False(initial[1].IsWorkingDay);
        Assert.All(initial, day => Assert.Equal(CalendarContract.TimeZone, day.TimeZone));

        var secondRelease = await releases.CreateDraftAsync(CreateReleaseCommand(actorUserId));
        await calendar.PutAsync(PutCommand(
            actorUserId,
            secondRelease.Id,
            Holiday,
            CalendarContract.ExtraordinaryClosure,
            false,
            "Cierre extraordinario"));
        await releases.PublishAsync(PublishCommand(
            actorUserId,
            secondRelease,
            Now.AddDays(7),
            "Calendario sucesor"));

        var holidayHistory = await context.CalendarDayVersions.AsNoTracking()
            .Where(day => day.LocalDate == Holiday)
            .OrderBy(day => day.EffectiveFrom)
            .ToListAsync();
        Assert.Equal(2, holidayHistory.Count);
        Assert.Equal(VersionStatuses.Superseded, holidayHistory[0].Status);
        Assert.Equal(Now.AddDays(7), holidayHistory[0].EffectiveTo);
        Assert.Equal(VersionStatuses.Current, holidayHistory[1].Status);
        Assert.Equal(holidayHistory[0].Id, holidayHistory[1].SupersedesId);
        Assert.Equal(CalendarContract.ExtraordinaryClosure, holidayHistory[1].DayType);
        var beforeFutureEffect = await calendar.GetAsync(
            actorUserId,
            Guid.CreateVersion7(),
            Holiday,
            Holiday);
        Assert.Equal(CalendarContract.Holiday, Assert.Single(beforeFutureEffect).DayType);
        var afterFutureEffect = await CreateCalendarService(
                context,
                NewUuidGenerator(),
                Now.AddDays(8))
            .GetAsync(actorUserId, Guid.CreateVersion7(), Holiday, Holiday);
        Assert.Equal(CalendarContract.ExtraordinaryClosure, Assert.Single(afterFutureEffect).DayType);
        Assert.Equal(personCount, await context.People.CountAsync());
        Assert.Equal(roleCount, await context.RoleAssignmentVersions.CountAsync());
        Assert.Equal(3, await context.AuditEvents.AsNoTracking()
            .CountAsync(audit => audit.Action == "CALENDAR_DAY_PUBLISHED"));
        Assert.Empty(context.ChangeTracker.Entries());
    }

    [Fact]
    public async Task PermissionValuesAndReleaseStateFailuresHaveNoCalendarEffect()
    {
        var deniedActor = await ResetAndSeedActorAsync("CALENDAR-DENIED", CanonicalRole.Administration);
        var direction = await SeedActorAsync("CALENDAR-DIRECTION", CanonicalRole.Direction);
        await using var context = CreateContext();
        var releases = CreateReleaseService(context, NewUuidGenerator());
        var calendar = CreateCalendarService(context, NewUuidGenerator());
        var draft = await releases.CreateDraftAsync(CreateReleaseCommand(direction));

        await Assert.ThrowsAsync<CalendarAccessDeniedException>(() => calendar.PutAsync(PutCommand(
            deniedActor,
            draft.Id,
            Holiday,
            CalendarContract.Holiday,
            false,
            "Denegado")));
        await Assert.ThrowsAsync<CalendarValidationException>(() => calendar.PutAsync(PutCommand(
            direction,
            draft.Id,
            Holiday,
            "INHABIL",
            false,
            "Tipo no aprobado")));
        await Assert.ThrowsAsync<CalendarValidationException>(() => calendar.PutAsync(PutCommand(
            direction,
            draft.Id,
            Holiday,
            CalendarContract.Holiday,
            true,
            "Inconsistente")));

        await releases.PublishAsync(PublishCommand(direction, draft, Now, "Publicación sin días"));
        await Assert.ThrowsAsync<CalendarReleaseNotFoundException>(() => calendar.PutAsync(PutCommand(
            direction,
            draft.Id,
            Holiday,
            CalendarContract.Holiday,
            false,
            "Release publicada")));

        Assert.False(await context.CalendarDayVersions.AnyAsync());
        Assert.Contains(
            await context.AuditEvents.AsNoTracking().ToListAsync(),
            audit => audit.Action == "CALENDAR_ACCESS_DENIED" && audit.Outcome == "DENIED");
        Assert.Empty(context.ChangeTracker.Entries());
    }

    [Fact]
    public async Task SameDraftEtagAllowsOneCorrectionAndLeavesTrackerClean()
    {
        var actorUserId = await ResetAndSeedActorAsync("CALENDAR-RACE", CanonicalRole.Direction);
        CalendarDayDetails draft;
        ConfigurationReleaseDetails release;
        await using (var setupContext = CreateContext())
        {
            release = await CreateReleaseService(setupContext, NewUuidGenerator())
                .CreateDraftAsync(CreateReleaseCommand(actorUserId));
            draft = await CreateCalendarService(setupContext, NewUuidGenerator()).PutAsync(PutCommand(
                actorUserId,
                release.Id,
                Holiday,
                CalendarContract.Holiday,
                false,
                "Inicial"));
        }

        await using var firstContext = CreateContext();
        await using var secondContext = CreateContext();
        var results = await Task.WhenAll(
            CaptureAsync(() => CreateCalendarService(firstContext, NewUuidGenerator()).PutAsync(PutCommand(
                actorUserId,
                release.Id,
                Holiday,
                CalendarContract.ExtraordinaryClosure,
                false,
                "Corrección uno",
                draft.RowVersion))),
            CaptureAsync(() => CreateCalendarService(secondContext, NewUuidGenerator()).PutAsync(PutCommand(
                actorUserId,
                release.Id,
                Holiday,
                CalendarContract.Holiday,
                false,
                "Corrección dos",
                draft.RowVersion))));

        Assert.Single(results, result => result is CalendarDayDetails);
        Assert.Single(results, result => result is CalendarVersionConflictException);
        await using var verification = CreateContext();
        var persisted = await verification.CalendarDayVersions.AsNoTracking().SingleAsync();
        Assert.Equal(2, persisted.RowVersion);
        Assert.Equal(VersionStatuses.Draft, persisted.Status);
        Assert.Empty(firstContext.ChangeTracker.Entries());
        Assert.Empty(secondContext.ChangeTracker.Entries());
    }

    [Fact]
    public async Task CalendarAuditFailureRollsBackReleaseAndDayPublication()
    {
        var actorUserId = await ResetAndSeedActorAsync("CALENDAR-ROLLBACK", CanonicalRole.Direction);
        await using var context = CreateContext();
        var releaseService = CreateReleaseService(context, NewUuidGenerator());
        var release = await releaseService.CreateDraftAsync(CreateReleaseCommand(actorUserId));
        await CreateCalendarService(context, NewUuidGenerator()).PutAsync(PutCommand(
            actorUserId,
            release.Id,
            Holiday,
            CalendarContract.Holiday,
            false,
            "Debe revertirse"));
        var duplicateAuditId = Guid.CreateVersion7();
        context.AuditEvents.Add(NewExistingAudit(duplicateAuditId, actorUserId));
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var failingService = CreateReleaseService(
            context,
            new SequenceUuidGenerator(duplicateAuditId, Guid.CreateVersion7()));
        await Assert.ThrowsAsync<DbUpdateException>(() => failingService.PublishAsync(PublishCommand(
            actorUserId,
            release,
            Now,
            "Publicación revertida")));

        var persistedRelease = await context.ConfigurationReleases.AsNoTracking().SingleAsync();
        var persistedDay = await context.CalendarDayVersions.AsNoTracking().SingleAsync();
        Assert.Equal(VersionStatuses.Draft, persistedRelease.Status);
        Assert.Equal(VersionStatuses.Draft, persistedDay.Status);
        Assert.Equal("Debe revertirse", persistedDay.PendingReason);
        Assert.Single(await context.AuditEvents.AsNoTracking()
            .Where(audit => audit.Id == duplicateAuditId)
            .ToListAsync());
        Assert.Empty(context.ChangeTracker.Entries());
    }

    [Fact]
    public async Task PostgreSqlRejectsSecondCurrentVersionAndInconsistentDayType()
    {
        var actorUserId = await ResetAndSeedActorAsync("CALENDAR-DB", CanonicalRole.Direction);
        await using var context = CreateContext();
        var releases = CreateReleaseService(context, NewUuidGenerator());
        var calendar = CreateCalendarService(context, NewUuidGenerator());
        var release = await releases.CreateDraftAsync(CreateReleaseCommand(actorUserId));
        await calendar.PutAsync(PutCommand(
            actorUserId,
            release.Id,
            Holiday,
            CalendarContract.Holiday,
            false,
            "Festivo"));
        await releases.PublishAsync(PublishCommand(actorUserId, release, Now, "Publicación"));

        var duplicateCurrent = await Assert.ThrowsAsync<PostgresException>(() =>
            context.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO calendar_day_version
                    (id, branch_id, local_date, day_type, is_working_day, release_id,
                     status, effective_from, reason, row_version)
                VALUES
                    ({Guid.CreateVersion7()}, {BranchScope.LorettaId}, {Holiday},
                     {CalendarContract.Holiday}, {false}, {release.Id},
                     {VersionStatuses.Current}, {Now.AddDays(1)}, {"Duplicada"}, {1L})
                """));
        Assert.Equal(PostgresErrorCodes.UniqueViolation, duplicateCurrent.SqlState);

        var inconsistent = await Assert.ThrowsAsync<PostgresException>(() =>
            context.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO calendar_day_version
                    (id, branch_id, local_date, day_type, is_working_day, release_id,
                     pending_reason, status, row_version)
                VALUES
                    ({Guid.CreateVersion7()}, {BranchScope.LorettaId}, {Holiday.AddDays(1)},
                     {CalendarContract.Holiday}, {true}, {release.Id},
                     {"Inconsistente"}, {VersionStatuses.Draft}, {1L})
                """));
        Assert.Equal(PostgresErrorCodes.CheckViolation, inconsistent.SqlState);
        context.ChangeTracker.Clear();
        Assert.Empty(context.ChangeTracker.Entries());
    }

    private async Task<Guid> ResetAndSeedActorAsync(string stableCode, string roleCode)
    {
        await using var context = CreateContext();
        await context.Database.EnsureDeletedAsync();
        await context.Database.MigrateAsync();
        return await SeedActorAsync(stableCode, roleCode);
    }

    private async Task<Guid> SeedActorAsync(string stableCode, string roleCode)
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
            EmploymentStatus.Active,
            Now));
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
        context.RoleAssignmentVersions.Add(new RoleAssignmentVersion
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            BranchId = BranchScope.LorettaId,
            RoleCode = roleCode,
            Status = RoleAssignmentStatus.Active,
            ValidFrom = Now,
        });
        await context.SaveChangesAsync();
        return userId;
    }

    private SgolDbContext CreateContext() => new(
        new DbContextOptionsBuilder<SgolDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options);

    private static EfCalendarService CreateCalendarService(
        SgolDbContext context,
        IUuidGenerator uuidGenerator,
        DateTimeOffset? now = null) => new(
            context,
            new AuditTransaction(context),
            new FixedClock(now ?? Now),
            uuidGenerator);

    private static EfConfigurationReleaseService CreateReleaseService(
        SgolDbContext context,
        IUuidGenerator uuidGenerator) => new(
            context,
            new AuditTransaction(context),
            new VersioningTransaction(context, new AuditTransaction(context)),
            new FixedClock(Now),
            uuidGenerator);

    private static CreateConfigurationReleaseCommand CreateReleaseCommand(Guid actorUserId) => new(
        actorUserId,
        Guid.CreateVersion7(),
        Guid.CreateVersion7());

    private static PublishConfigurationReleaseCommand PublishCommand(
        Guid actorUserId,
        ConfigurationReleaseDetails release,
        DateTimeOffset effectiveFrom,
        string reason) => new(
            actorUserId,
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            release.Id,
            release.RowVersion,
            effectiveFrom,
            reason);

    private static PutCalendarDayCommand PutCommand(
        Guid actorUserId,
        Guid releaseId,
        DateOnly localDate,
        string dayType,
        bool isWorkingDay,
        string reason,
        long? expectedRowVersion = null) => new(
            actorUserId,
            Guid.CreateVersion7(),
            localDate,
            releaseId,
            dayType,
            isWorkingDay,
            reason,
            expectedRowVersion);

    private static AuditEvent NewExistingAudit(Guid id, Guid actorUserId) => new()
    {
        Id = id,
        OccurredAt = Now,
        ActorUserId = actorUserId,
        ActorType = "APP_USER",
        Action = "SYNTHETIC_EXISTING_EVENT",
        ResourceType = "CALENDAR_DAY",
        BranchId = BranchScope.LorettaId,
        CorrelationId = Guid.CreateVersion7(),
        Outcome = "SUCCESS",
    };

    private static async Task<object> CaptureAsync(Func<Task<CalendarDayDetails>> action)
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
