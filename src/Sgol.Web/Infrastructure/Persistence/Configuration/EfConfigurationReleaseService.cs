using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Sgol.BuildingBlocks.Identifiers;
using Sgol.BuildingBlocks.Time;
using Sgol.BuildingBlocks.Versioning;
using Sgol.Configuration.Contracts;
using Sgol.Organization.Contracts;
using Sgol.Web.Infrastructure.Persistence.Auditing;
using Sgol.Web.Infrastructure.Persistence.Bootstrap;
using Sgol.Web.Infrastructure.Persistence.Versioning;

namespace Sgol.Web.Infrastructure.Persistence.Configuration;

public sealed class EfConfigurationReleaseService(
    SgolDbContext dbContext,
    AuditTransaction auditTransaction,
    VersioningTransaction versioningTransaction,
    IClock clock,
    IUuidGenerator uuidGenerator) : IConfigurationReleaseService
{
    private const string IdempotencyPrimaryKey = "PK_idempotency_record";
    private const string OneCurrentIndex = "IX_configuration_release_one_current";
    private const string SuccessorIndex = "IX_configuration_release_supersedes_id";
    private const string ValidityConstraint = "EX_configuration_release_validity";
    private const string CalendarCurrentIndex = "IX_calendar_day_version_one_current";
    private const string CalendarSuccessorIndex = "IX_calendar_day_version_supersedes_id";
    private const string CalendarValidityConstraint = "EX_calendar_day_version_validity";

    public async Task<IReadOnlyList<ConfigurationReleaseDetails>> ListAsync(
        Guid actorUserId,
        Guid correlationId,
        CancellationToken cancellationToken = default)
    {
        await EnsureAuthorizedAsync(actorUserId, correlationId, cancellationToken);
        var releases = await dbContext.ConfigurationReleases
            .AsNoTracking()
            .Where(release => release.BranchId == BranchScope.LorettaId)
            .OrderByDescending(release => release.VersionNo)
            .ThenByDescending(release => release.Id)
            .Select(release => new ConfigurationReleaseDetails(
                release.Id,
                release.VersionNo,
                release.Status,
                release.EffectiveFrom,
                release.EffectiveTo,
                release.Reason,
                release.PublishedBy,
                release.PublishedAt,
                release.SupersedesId,
                release.RowVersion))
            .ToListAsync(cancellationToken);
        dbContext.ChangeTracker.Clear();
        return releases;
    }

    public async Task<ConfigurationReleaseDetails> CreateDraftAsync(
        CreateConfigurationReleaseCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        await EnsureAuthorizedAsync(command.ActorUserId, command.CorrelationId, cancellationToken);

        var scope = CreateIdempotencyScope(command.ActorUserId);
        var requestHash = ComputeHash(scope);
        var replay = await FindReplayAsync(scope, command.IdempotencyKey, requestHash, cancellationToken);
        if (replay is not null)
        {
            return replay;
        }

        var release = new ConfigurationRelease(uuidGenerator.NewUuid(), BranchScope.LorettaId);
        var now = clock.UtcNow;
        try
        {
            await auditTransaction.ExecuteAsync(
                NewAuditEvent(
                    command.ActorUserId,
                    command.CorrelationId,
                    release.Id,
                    "CONFIGURATION_RELEASE_DRAFT_CREATED",
                    afterData: SerializeAuditValue(release)),
                _ =>
                {
                    dbContext.ConfigurationReleases.Add(release);
                    dbContext.IdempotencyRecords.Add(new IdempotencyRecord
                    {
                        Scope = scope,
                        Key = command.IdempotencyKey,
                        RequestHash = requestHash,
                        Status = "COMPLETED",
                        ResourceType = "CONFIGURATION_RELEASE",
                        ResourceId = release.Id,
                        ResponseCode = StatusCodes.Status201Created,
                        CreatedAt = now,
                        ExpiresAt = DateTimeOffset.MaxValue,
                    });
                    return Task.CompletedTask;
                },
                cancellationToken);
        }
        catch (DbUpdateException exception) when (GetConstraintName(exception) == IdempotencyPrimaryKey)
        {
            dbContext.ChangeTracker.Clear();
            return await FindReplayAsync(scope, command.IdempotencyKey, requestHash, cancellationToken)
                ?? throw new ConfigurationIdempotencyConflictException();
        }
        catch
        {
            dbContext.ChangeTracker.Clear();
            throw;
        }

        var result = ToDetails(release);
        dbContext.ChangeTracker.Clear();
        return result;
    }

    public async Task<ConfigurationReleaseDetails> PublishAsync(
        PublishConfigurationReleaseCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        await EnsureAuthorizedAsync(command.ActorUserId, command.CorrelationId, cancellationToken);
        var normalizedReason = VersioningRules.NormalizeRequiredReason(command.Reason);
        var scope = CreatePublicationIdempotencyScope(command.ActorUserId, command.ReleaseId);
        var requestHash = ComputeHash(
            command.ReleaseId.ToString("D"),
            command.ExpectedRowVersion.ToString(System.Globalization.CultureInfo.InvariantCulture),
            command.EffectiveFrom.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
            normalizedReason);
        var replay = await FindReplayAsync(scope, command.IdempotencyKey, requestHash, cancellationToken);
        if (replay is not null)
        {
            return replay;
        }

        try
        {
            return await versioningTransaction.ExecuteAsync(
                token => IsAuthorizedAsync(command.ActorUserId, token),
                () => NewAuditEvent(
                    command.ActorUserId,
                    command.CorrelationId,
                    command.ReleaseId,
                    "CONFIGURATION_RELEASE_ACCESS_DENIED",
                    outcome: "DENIED"),
                normalizedReason,
                async token =>
                {
                    await LockLorettaScopeAsync(token);
                    var draft = await dbContext.ConfigurationReleases
                        .FromSqlInterpolated(
                            $"SELECT * FROM configuration_release WHERE id = {command.ReleaseId} FOR UPDATE")
                        .AsTracking()
                        .SingleOrDefaultAsync(token)
                        ?? throw new ConfigurationReleaseNotFoundException();
                    if (draft.BranchId != BranchScope.LorettaId)
                    {
                        throw new ConfigurationAccessDeniedException();
                    }

                    var current = await dbContext.ConfigurationReleases
                        .FromSqlInterpolated(
                            $"""
                            SELECT *
                            FROM configuration_release
                            WHERE branch_id = {BranchScope.LorettaId}
                              AND status = {VersionStatuses.Current}
                            FOR UPDATE
                            """)
                        .AsTracking()
                        .SingleOrDefaultAsync(token);
                    var historicalReleases = await dbContext.ConfigurationReleases
                        .AsNoTracking()
                        .Where(release =>
                            release.BranchId == BranchScope.LorettaId &&
                            release.Status != VersionStatuses.Draft)
                        .ToListAsync(token);
                    var history = historicalReleases.Select(release => release.ToVersionRecord());

                    var plan = VersioningRules.PlanPublication(
                        draft.ToVersionRecord(),
                        current?.ToVersionRecord(),
                        history,
                        command.ExpectedRowVersion,
                        command.EffectiveFrom,
                        command.Reason);
                    var nextVersion = (await dbContext.ConfigurationReleases
                        .Where(release => release.BranchId == BranchScope.LorettaId)
                        .MaxAsync(release => (int?)release.VersionNo, token) ?? 0) + 1;
                    var publishedAt = clock.UtcNow;
                    var calendarPlans = await PlanCalendarPublicationAsync(
                        draft.Id,
                        command.EffectiveFrom,
                        token);

                    var beforeData = JsonSerializer.SerializeToDocument(new
                    {
                        schemaVersion = 1,
                        draft = AuditValue(draft),
                        current = current is null ? null : AuditValue(current),
                    });
                    if (current is not null)
                    {
                        current.ApplySuperseded(plan.Superseded!);
                    }

                    foreach (var calendarPlan in calendarPlans.Where(item => item.Current is not null))
                    {
                        calendarPlan.Current!.ApplySuperseded(calendarPlan.Plan.Superseded!);
                    }

                    if (current is not null || calendarPlans.Any(item => item.Current is not null))
                    {
                        await dbContext.SaveChangesAsync(token);
                    }

                    draft.ApplyPublished(plan.Published, nextVersion, command.ActorUserId, publishedAt);
                    foreach (var calendarPlan in calendarPlans)
                    {
                        calendarPlan.Draft.ApplyPublished(calendarPlan.Plan.Published);
                        dbContext.AuditEvents.Add(NewCalendarPublicationAuditEvent(
                            command.ActorUserId,
                            command.CorrelationId,
                            calendarPlan));
                    }
                    dbContext.IdempotencyRecords.Add(new IdempotencyRecord
                    {
                        Scope = scope,
                        Key = command.IdempotencyKey,
                        RequestHash = requestHash,
                        Status = "COMPLETED",
                        ResourceType = "CONFIGURATION_RELEASE",
                        ResourceId = draft.Id,
                        ResponseCode = StatusCodes.Status200OK,
                        CreatedAt = publishedAt,
                        ExpiresAt = DateTimeOffset.MaxValue,
                    });
                    var afterData = JsonSerializer.SerializeToDocument(new
                    {
                        schemaVersion = 1,
                        published = AuditValue(draft),
                        superseded = current is null ? null : AuditValue(current),
                    });
                    return (
                        ToDetails(draft),
                        NewAuditEvent(
                            command.ActorUserId,
                            command.CorrelationId,
                            draft.Id,
                            "CONFIGURATION_RELEASE_PUBLISHED",
                            beforeData,
                            afterData,
                            normalizedReason));
                },
                cancellationToken);
        }
        catch (DbUpdateException exception) when (GetConstraintName(exception) == IdempotencyPrimaryKey)
        {
            dbContext.ChangeTracker.Clear();
            return await FindReplayAsync(scope, command.IdempotencyKey, requestHash, cancellationToken)
                ?? throw new ConfigurationIdempotencyConflictException();
        }
        catch (DbUpdateConcurrencyException)
        {
            dbContext.ChangeTracker.Clear();
            throw new VersionConflictException();
        }
        catch (DbUpdateException exception) when (
            GetConstraintName(exception) is OneCurrentIndex or SuccessorIndex or
                CalendarCurrentIndex or CalendarSuccessorIndex)
        {
            dbContext.ChangeTracker.Clear();
            throw new VersionConflictException();
        }
        catch (DbUpdateException exception) when (
            GetConstraintName(exception) is ValidityConstraint or CalendarValidityConstraint)
        {
            dbContext.ChangeTracker.Clear();
            throw new VersioningOverlapException();
        }
        catch (Exception exception) when (
            exception is VersioningStateException or VersionConflictException or VersioningOverlapException)
        {
            dbContext.ChangeTracker.Clear();
            var concurrentReplay = await FindReplayAsync(
                scope,
                command.IdempotencyKey,
                requestHash,
                cancellationToken);
            if (concurrentReplay is not null)
            {
                return concurrentReplay;
            }

            throw;
        }
    }

    private async Task EnsureAuthorizedAsync(
        Guid actorUserId,
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        if (await IsAuthorizedAsync(actorUserId, cancellationToken))
        {
            return;
        }

        await auditTransaction.ExecuteAsync(
            NewAuditEvent(
                actorUserId,
                correlationId,
                resourceId: null,
                action: "CONFIGURATION_RELEASE_ACCESS_DENIED",
                outcome: "DENIED"),
            _ => Task.CompletedTask,
            cancellationToken);
        dbContext.ChangeTracker.Clear();
        throw new ConfigurationAccessDeniedException();
    }

    private Task<bool> IsAuthorizedAsync(Guid actorUserId, CancellationToken cancellationToken) =>
        ConfigurationAuthorizationQuery.IsDirectionAsync(
            dbContext,
            actorUserId,
            cancellationToken);

    private async Task LockLorettaScopeAsync(CancellationToken cancellationToken) =>
        _ = await dbContext.Branches
            .FromSqlInterpolated($"SELECT * FROM branch WHERE id = {BranchScope.LorettaId} FOR UPDATE")
            .AsTracking()
            .SingleAsync(cancellationToken);

    private async Task<IReadOnlyList<CalendarPublicationPlan>> PlanCalendarPublicationAsync(
        Guid releaseId,
        DateTimeOffset effectiveFrom,
        CancellationToken cancellationToken)
    {
        var drafts = await dbContext.CalendarDayVersions
            .FromSqlInterpolated(
                $"""
                SELECT *
                FROM calendar_day_version
                WHERE release_id = {releaseId}
                  AND status = {VersionStatuses.Draft}
                ORDER BY local_date, id
                FOR UPDATE
                """)
            .AsTracking()
            .ToListAsync(cancellationToken);
        var plans = new List<CalendarPublicationPlan>(drafts.Count);
        foreach (var calendarDraft in drafts)
        {
            var current = await dbContext.CalendarDayVersions
                .FromSqlInterpolated(
                    $"""
                    SELECT *
                    FROM calendar_day_version
                    WHERE branch_id = {BranchScope.LorettaId}
                      AND local_date = {calendarDraft.LocalDate}
                      AND status = {VersionStatuses.Current}
                    FOR UPDATE
                    """)
                .AsTracking()
                .SingleOrDefaultAsync(cancellationToken);
            var historicalDays = await dbContext.CalendarDayVersions
                .AsNoTracking()
                .Where(day =>
                    day.BranchId == BranchScope.LorettaId &&
                    day.LocalDate == calendarDraft.LocalDate &&
                    day.Status != VersionStatuses.Draft)
                .ToListAsync(cancellationToken);
            var history = historicalDays.Select(day => day.ToVersionRecord());
            var publicationPlan = VersioningRules.PlanPublication(
                calendarDraft.ToVersionRecord(),
                current?.ToVersionRecord(),
                history,
                calendarDraft.RowVersion,
                effectiveFrom,
                calendarDraft.PendingReason!);
            plans.Add(new CalendarPublicationPlan(
                calendarDraft,
                current,
                publicationPlan,
                current is null ? null : EfCalendarService.AuditValue(current),
                EfCalendarService.AuditValue(calendarDraft)));
        }

        return plans;
    }

    private AuditEvent NewCalendarPublicationAuditEvent(
        Guid actorUserId,
        Guid correlationId,
        CalendarPublicationPlan calendarPlan) => new()
        {
            Id = uuidGenerator.NewUuid(),
            OccurredAt = clock.UtcNow,
            ActorUserId = actorUserId,
            ActorType = "APP_USER",
            Action = "CALENDAR_DAY_PUBLISHED",
            ResourceType = "CALENDAR_DAY",
            ResourceId = calendarPlan.Draft.Id,
            BranchId = BranchScope.LorettaId,
            CorrelationId = correlationId,
            BeforeData = JsonSerializer.SerializeToDocument(new
            {
                schemaVersion = 1,
                draft = calendarPlan.DraftBefore,
                current = calendarPlan.CurrentBefore,
            }),
            AfterData = JsonSerializer.SerializeToDocument(new
            {
                schemaVersion = 1,
                published = EfCalendarService.AuditValue(calendarPlan.Draft),
                superseded = calendarPlan.Current is null
                    ? null
                    : EfCalendarService.AuditValue(calendarPlan.Current),
            }),
            Reason = calendarPlan.Draft.Reason,
            Outcome = "SUCCESS",
        };

    private async Task<ConfigurationReleaseDetails?> FindReplayAsync(
        string scope,
        Guid key,
        string requestHash,
        CancellationToken cancellationToken)
    {
        var record = await dbContext.IdempotencyRecords
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Scope == scope && item.Key == key, cancellationToken);
        if (record is null)
        {
            return null;
        }

        if (!string.Equals(record.RequestHash, requestHash, StringComparison.Ordinal))
        {
            throw new ConfigurationIdempotencyConflictException();
        }

        var release = await dbContext.ConfigurationReleases
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == record.ResourceId, cancellationToken)
            ?? throw new ConfigurationReleaseNotFoundException();
        dbContext.ChangeTracker.Clear();
        return ToDetails(release);
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
            ResourceType = "CONFIGURATION_RELEASE",
            ResourceId = resourceId,
            BranchId = BranchScope.LorettaId,
            CorrelationId = correlationId,
            BeforeData = beforeData,
            AfterData = afterData,
            Reason = reason,
            Outcome = outcome,
        };

    private static JsonDocument SerializeAuditValue(ConfigurationRelease release) =>
        JsonSerializer.SerializeToDocument(AuditValue(release));

    private static object AuditValue(ConfigurationRelease release) => new
    {
        releaseId = release.Id,
        branchId = release.BranchId,
        versionNo = release.VersionNo,
        status = release.Status,
        effectiveFrom = release.EffectiveFrom,
        effectiveTo = release.EffectiveTo,
        reason = release.Reason,
        publishedBy = release.PublishedBy,
        publishedAt = release.PublishedAt,
        supersedesId = release.SupersedesId,
        rowVersion = release.RowVersion,
    };

    private static ConfigurationReleaseDetails ToDetails(ConfigurationRelease release) => new(
        release.Id,
        release.VersionNo,
        release.Status,
        release.EffectiveFrom,
        release.EffectiveTo,
        release.Reason,
        release.PublishedBy,
        release.PublishedAt,
        release.SupersedesId,
        release.RowVersion);

    private static string CreateIdempotencyScope(Guid actorUserId) =>
        $"CONFIGURATION_RELEASE_CREATE:{actorUserId:D}:LOR-001";

    private static string CreatePublicationIdempotencyScope(Guid actorUserId, Guid releaseId) =>
        $"CONFIGURATION_RELEASE_PUBLISH:{actorUserId:D}:{releaseId:D}";

    private static string ComputeHash(params string[] values) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\n', values))));

    private static string? GetConstraintName(DbUpdateException exception) =>
        (exception.InnerException as PostgresException)?.ConstraintName;

    private sealed record CalendarPublicationPlan(
        CalendarDayVersion Draft,
        CalendarDayVersion? Current,
        VersionPublicationPlan Plan,
        object? CurrentBefore,
        object DraftBefore);
}
