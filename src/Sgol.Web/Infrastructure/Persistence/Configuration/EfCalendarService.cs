using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Sgol.BuildingBlocks.Identifiers;
using Sgol.BuildingBlocks.Time;
using Sgol.BuildingBlocks.Versioning;
using Sgol.Configuration.Contracts;
using Sgol.Organization.Contracts;
using Sgol.Web.Infrastructure.Persistence.Auditing;

namespace Sgol.Web.Infrastructure.Persistence.Configuration;

public sealed class EfCalendarService(
    SgolDbContext dbContext,
    AuditTransaction auditTransaction,
    IClock clock,
    IUuidGenerator uuidGenerator) : ICalendarService
{
    private const string DraftIndex = "IX_calendar_day_version_release_id_local_date";

    public async Task<IReadOnlyList<CalendarDayDetails>> GetAsync(
        Guid actorUserId,
        Guid correlationId,
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken cancellationToken = default)
    {
        ValidateRange(fromDate, toDate);
        await EnsureReadableAsync(actorUserId, correlationId, cancellationToken);

        var now = clock.UtcNow;
        var days = await dbContext.CalendarDayVersions
            .AsNoTracking()
            .Where(day =>
                day.BranchId == BranchScope.LorettaId &&
                day.Status != VersionStatuses.Draft &&
                day.EffectiveFrom <= now &&
                (day.EffectiveTo == null || now < day.EffectiveTo) &&
                day.LocalDate >= fromDate &&
                day.LocalDate <= toDate)
            .OrderBy(day => day.LocalDate)
            .Select(day => new CalendarDayDetails(
                day.Id,
                day.LocalDate,
                day.DayType,
                day.IsWorkingDay,
                CalendarContract.TimeZone,
                day.ReleaseId,
                day.Status,
                day.EffectiveFrom,
                day.EffectiveTo,
                day.Reason,
                day.SupersedesId,
                day.RowVersion))
            .ToListAsync(cancellationToken);
        dbContext.ChangeTracker.Clear();
        return days;
    }

    public async Task<CalendarDayDetails> PutAsync(
        PutCalendarDayCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        await EnsureAuthorizedAsync(command.ActorUserId, command.CorrelationId, cancellationToken);
        CalendarContract.ValidateDay(command.DayType, command.IsWorkingDay);
        var reason = VersioningRules.NormalizeRequiredReason(command.Reason);

        CalendarDayVersion? saved = null;
        try
        {
            await auditTransaction.ExecuteAsync(
                async token =>
                {
                    var release = await dbContext.ConfigurationReleases
                        .FromSqlInterpolated(
                            $"SELECT * FROM configuration_release WHERE id = {command.ReleaseId} FOR UPDATE")
                        .AsTracking()
                        .SingleOrDefaultAsync(token);
                    if (release is null ||
                        release.BranchId != BranchScope.LorettaId ||
                        release.Status != VersionStatuses.Draft)
                    {
                        throw new CalendarReleaseNotFoundException();
                    }

                    var draft = await dbContext.CalendarDayVersions
                        .FromSqlInterpolated(
                            $"""
                            SELECT *
                            FROM calendar_day_version
                            WHERE release_id = {command.ReleaseId}
                              AND local_date = {command.LocalDate}
                              AND status = {VersionStatuses.Draft}
                            FOR UPDATE
                            """)
                        .AsTracking()
                        .SingleOrDefaultAsync(token);

                    JsonDocument? beforeData = null;
                    if (draft is null)
                    {
                        if (command.ExpectedRowVersion is not null)
                        {
                            throw new CalendarVersionConflictException();
                        }

                        saved = new CalendarDayVersion(
                            uuidGenerator.NewUuid(),
                            BranchScope.LorettaId,
                            command.LocalDate,
                            command.DayType,
                            command.IsWorkingDay,
                            command.ReleaseId,
                            reason);
                        dbContext.CalendarDayVersions.Add(saved);
                    }
                    else
                    {
                        if (command.ExpectedRowVersion is null)
                        {
                            throw new CalendarIfMatchRequiredException();
                        }

                        beforeData = SerializeAuditValue(draft);
                        draft.CorrectDraft(
                            command.DayType,
                            command.IsWorkingDay,
                            reason,
                            command.ExpectedRowVersion.Value);
                        saved = draft;
                    }

                    return NewAuditEvent(
                        command.ActorUserId,
                        command.CorrelationId,
                        saved.Id,
                        draft is null ? "CALENDAR_DAY_DRAFT_CREATED" : "CALENDAR_DAY_DRAFT_CORRECTED",
                        beforeData,
                        SerializeAuditValue(saved),
                        reason);
                },
                cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            dbContext.ChangeTracker.Clear();
            throw new CalendarVersionConflictException(exception);
        }
        catch (DbUpdateException exception) when (GetConstraintName(exception) == DraftIndex)
        {
            dbContext.ChangeTracker.Clear();
            throw new CalendarVersionConflictException(exception);
        }
        catch (VersionConflictException exception)
        {
            dbContext.ChangeTracker.Clear();
            throw new CalendarVersionConflictException(exception);
        }
        catch
        {
            dbContext.ChangeTracker.Clear();
            throw;
        }

        var result = ToDetails(saved!);
        dbContext.ChangeTracker.Clear();
        return result;
    }

    private async Task EnsureAuthorizedAsync(
        Guid actorUserId,
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        if (await ConfigurationAuthorizationQuery.IsDirectionAsync(
                dbContext,
                actorUserId,
                cancellationToken))
        {
            return;
        }

        await auditTransaction.ExecuteAsync(
            NewAuditEvent(
                actorUserId,
                correlationId,
                resourceId: null,
                action: "CALENDAR_ACCESS_DENIED",
                outcome: "DENIED"),
            _ => Task.CompletedTask,
            cancellationToken);
        dbContext.ChangeTracker.Clear();
        throw new CalendarAccessDeniedException();
    }

    private async Task EnsureReadableAsync(
        Guid actorUserId,
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        if (await ConfigurationAuthorizationQuery.IsActiveLorettaUserAsync(
                dbContext,
                actorUserId,
                cancellationToken))
        {
            return;
        }

        await auditTransaction.ExecuteAsync(
            NewAuditEvent(
                actorUserId,
                correlationId,
                resourceId: null,
                action: "CALENDAR_ACCESS_DENIED",
                outcome: "DENIED"),
            _ => Task.CompletedTask,
            cancellationToken);
        dbContext.ChangeTracker.Clear();
        throw new CalendarAccessDeniedException();
    }

    private AuditEvent NewAuditEvent(
        Guid actorUserId,
        Guid correlationId,
        Guid? resourceId,
        string action,
        JsonDocument? beforeData = null,
        JsonDocument? afterData = null,
        string? reason = null,
        string outcome = "SUCCESS") => new()
        {
            Id = uuidGenerator.NewUuid(),
            OccurredAt = clock.UtcNow,
            ActorUserId = actorUserId,
            ActorType = "APP_USER",
            Action = action,
            ResourceType = "CALENDAR_DAY",
            ResourceId = resourceId,
            BranchId = BranchScope.LorettaId,
            CorrelationId = correlationId,
            BeforeData = beforeData,
            AfterData = afterData,
            Reason = reason,
            Outcome = outcome,
        };

    internal static JsonDocument SerializeAuditValue(CalendarDayVersion day) =>
        JsonSerializer.SerializeToDocument(AuditValue(day));

    internal static object AuditValue(CalendarDayVersion day) => new
    {
        schemaVersion = 1,
        calendarDayVersionId = day.Id,
        branchId = day.BranchId,
        localDate = day.LocalDate,
        dayType = day.DayType,
        isWorkingDay = day.IsWorkingDay,
        timeZone = CalendarContract.TimeZone,
        releaseId = day.ReleaseId,
        status = day.Status,
        effectiveFrom = day.EffectiveFrom,
        effectiveTo = day.EffectiveTo,
        reason = day.Reason,
        supersedesId = day.SupersedesId,
        rowVersion = day.RowVersion,
    };

    internal static CalendarDayDetails ToDetails(CalendarDayVersion day) => new(
        day.Id,
        day.LocalDate,
        day.DayType,
        day.IsWorkingDay,
        CalendarContract.TimeZone,
        day.ReleaseId,
        day.Status,
        day.EffectiveFrom,
        day.EffectiveTo,
        day.Reason,
        day.SupersedesId,
        day.RowVersion);

    private static void ValidateRange(DateOnly fromDate, DateOnly toDate)
    {
        if (toDate < fromDate)
        {
            throw new CalendarValidationException("to must be on or after from.");
        }
    }

    private static string? GetConstraintName(DbUpdateException exception) =>
        (exception.InnerException as PostgresException)?.ConstraintName;
}
