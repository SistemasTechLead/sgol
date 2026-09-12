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
using Sgol.Web.Infrastructure.Persistence.Idempotency;
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
    private const string TaskCurrentIndex = "IX_task_definition_version_one_live";
    private const string TaskSuccessorIndex = "IX_task_definition_version_supersedes_id";
    private const string TaskVersionIndex = "IX_task_definition_version_number";
    private const string TaskValidityConstraint = "EX_task_definition_version_validity";
    private const string EligibilityCurrentIndex = "IX_eligibility_policy_version_one_current";
    private const string EligibilitySuccessorIndex = "IX_eligibility_policy_version_supersedes_id";
    private const string EligibilityVersionIndex = "IX_eligibility_policy_version_number";
    private const string EligibilityValidityConstraint = "EX_eligibility_policy_version_validity";
    private const string ActivationCurrentIndex = "IX_activation_rule_version_one_current";
    private const string ActivationSuccessorIndex = "IX_activation_rule_version_supersedes_id";
    private const string ActivationVersionIndex = "IX_activation_rule_version_number";
    private const string ActivationValidityConstraint = "EX_activation_rule_version_validity";
    private const string EvidenceCurrentIndex = "IX_evidence_policy_version_one_current";
    private const string EvidenceSuccessorIndex = "IX_evidence_policy_version_supersedes_id";
    private const string EvidenceVersionIndex = "IX_evidence_policy_version_number";
    private const string EvidenceValidityConstraint = "EX_evidence_policy_version_validity";
    private const string ValidationCurrentIndex = "IX_validation_policy_version_one_current";
    private const string ValidationSuccessorIndex = "IX_validation_policy_version_supersedes_id";
    private const string ValidationVersionIndex = "IX_validation_policy_version_number";
    private const string ValidationValidityConstraint = "EX_validation_policy_version_validity";

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

        const string operation = "CONFIGURATION_RELEASE_CREATE";
        const string resource = "new:LOR-001";
        var scope = IdempotencyProtocol.Scope(command.ActorUserId.ToString("D"), operation, resource);
        var requestHash = IdempotencyProtocol.HashCanonical(operation, command.ActorUserId.ToString("D"), resource, new { });
        var legacyScope = CreateIdempotencyScope(command.ActorUserId);
        var legacyRequestHash = ComputeHash(legacyScope);
        var replay = await FindReplayAsync(scope, legacyScope, command.IdempotencyKey, requestHash, legacyRequestHash,
            command.ActorUserId, command.CorrelationId, operation, cancellationToken);
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
                    dbContext.IdempotencyRecords.Add(IdempotencyProtocol.Completed(
                        scope, command.IdempotencyKey, requestHash, "CONFIGURATION_RELEASE", release.Id,
                        StatusCodes.Status201Created, ToDetails(release), now, DateTimeOffset.MaxValue,
                        responseLocation: $"/api/v1/configuration/releases/{release.Id:D}"));
                    return Task.CompletedTask;
                },
                cancellationToken);
        }
        catch (DbUpdateException exception) when (GetConstraintName(exception) == IdempotencyPrimaryKey)
        {
            dbContext.ChangeTracker.Clear();
            return await FindReplayAsync(scope, legacyScope, command.IdempotencyKey, requestHash, legacyRequestHash,
                command.ActorUserId, command.CorrelationId, operation, cancellationToken)
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
        var operation = command.TaskDirective switch
        {
            null => "CONFIGURATION_RELEASE_PUBLISH",
            { VersionId: not null } => "TASK_DEFINITION_VERSION_PUBLISH",
            _ => "TASK_DEFINITION_DEACTIVATE_NEW",
        };
        var resource = command.TaskDirective switch
        {
            null => command.ReleaseId.ToString("D"),
            { VersionId: Guid versionId } directive => $"{directive.TaskCode}:{versionId:D}",
            { } directive => $"{directive.TaskCode}:{command.ReleaseId:D}",
        };
        var scope = IdempotencyProtocol.Scope(command.ActorUserId.ToString("D"), operation, resource);
        var canonicalIfMatch = command.TaskDirective?.ExpectedRowVersion ?? command.ExpectedRowVersion;
        var canonicalBody = command.TaskDirective is null
            ? (object)new { command.ReleaseId, command.EffectiveFrom, reason = normalizedReason }
            : new
            {
                command.TaskDirective.TaskCode,
                command.TaskDirective.VersionId,
                command.ReleaseId,
                command.EffectiveFrom,
                reason = normalizedReason,
                command.TaskDirective.ActiveForNew,
                command.TaskDirective.CreateDraft,
            };
        var requestHash = IdempotencyProtocol.HashCanonical(
            operation, command.ActorUserId.ToString("D"), resource, canonicalBody, canonicalIfMatch);
        var legacyScope = CreatePublicationIdempotencyScope(command.ActorUserId, command.ReleaseId);
        var legacyRequestHash = ComputeHash(
            command.ReleaseId.ToString("D"),
            command.ExpectedRowVersion.ToString(System.Globalization.CultureInfo.InvariantCulture),
            command.EffectiveFrom.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
            normalizedReason,
            command.TaskDirective?.TaskCode ?? string.Empty,
            command.TaskDirective?.VersionId?.ToString("D") ?? string.Empty,
            command.TaskDirective?.ExpectedRowVersion.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
            command.TaskDirective?.ActiveForNew.ToString() ?? string.Empty,
            command.TaskDirective?.CreateDraft.ToString() ?? string.Empty);
        var replay = await FindReplayAsync(scope, legacyScope, command.IdempotencyKey, requestHash, legacyRequestHash,
            command.ActorUserId, command.CorrelationId, operation, cancellationToken);
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
                    var taskPlans = await PlanTaskPublicationAsync(
                        draft.Id,
                        command.EffectiveFrom,
                        normalizedReason,
                        command.TaskDirective,
                        token);
                    var eligibilityPlans = await PlanEligibilityPublicationAsync(
                        draft.Id,
                        command.EffectiveFrom,
                        normalizedReason,
                        taskPlans,
                        token);
                    var activationPlans = await PlanActivationPublicationAsync(
                        draft.Id,
                        command.EffectiveFrom,
                        normalizedReason,
                        taskPlans,
                        token);
                    var evidencePlans = await PlanEvidencePublicationAsync(
                        draft.Id,
                        command.EffectiveFrom,
                        normalizedReason,
                        taskPlans,
                        token);
                    var validationPlans = await PlanValidationPublicationAsync(
                        draft.Id,
                        command.EffectiveFrom,
                        normalizedReason,
                        taskPlans,
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

                    foreach (var taskPlan in taskPlans.Where(item => item.Current is not null))
                    {
                        taskPlan.Current!.ApplySuperseded(taskPlan.Plan.Superseded!);
                    }

                    foreach (var eligibilityPlan in eligibilityPlans.Where(item => item.Current is not null))
                    {
                        eligibilityPlan.Current!.ApplySuperseded(eligibilityPlan.Plan.Superseded!);
                    }

                    foreach (var activationPlan in activationPlans.Where(item => item.Current is not null))
                    {
                        activationPlan.Current!.ApplySuperseded(activationPlan.Plan.Superseded!);
                    }

                    foreach (var evidencePlan in evidencePlans.Where(item => item.Current is not null))
                    {
                        evidencePlan.Current!.ApplySuperseded(evidencePlan.Plan.Superseded!);
                    }

                    foreach (var validationPlan in validationPlans.Where(item => item.Current is not null))
                    {
                        validationPlan.Current!.ApplySuperseded(validationPlan.Plan.Superseded!);
                    }

                    if (current is not null ||
                        calendarPlans.Any(item => item.Current is not null) ||
                        taskPlans.Any(item => item.Current is not null) ||
                        eligibilityPlans.Any(item => item.Current is not null) ||
                        activationPlans.Any(item => item.Current is not null) ||
                        evidencePlans.Any(item => item.Current is not null) ||
                        validationPlans.Any(item => item.Current is not null))
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
                    foreach (var taskPlan in taskPlans)
                    {
                        taskPlan.Draft.ApplyPublished(taskPlan.Plan.Published, taskPlan.ActiveForNew);
                        dbContext.AuditEvents.Add(NewTaskPublicationAuditEvent(
                            command.ActorUserId,
                            command.CorrelationId,
                            taskPlan));
                    }
                    foreach (var eligibilityPlan in eligibilityPlans)
                    {
                        eligibilityPlan.Draft.ApplyPublished(eligibilityPlan.Plan.Published);
                        dbContext.AuditEvents.Add(NewEligibilityPublicationAuditEvent(
                            command.ActorUserId,
                            command.CorrelationId,
                            eligibilityPlan));
                    }
                    foreach (var activationPlan in activationPlans)
                    {
                        activationPlan.Draft.ApplyPublished(activationPlan.Plan.Published);
                        dbContext.AuditEvents.Add(NewActivationPublicationAuditEvent(
                            command.ActorUserId,
                            command.CorrelationId,
                            activationPlan));
                    }
                    foreach (var evidencePlan in evidencePlans)
                    {
                        evidencePlan.Draft.ApplyPublished(evidencePlan.Plan.Published);
                        dbContext.AuditEvents.Add(NewEvidencePublicationAuditEvent(
                            command.ActorUserId,
                            command.CorrelationId,
                            evidencePlan));
                    }
                    foreach (var validationPlan in validationPlans)
                    {
                        validationPlan.Draft.ApplyPublished(validationPlan.Plan.Published);
                        dbContext.AuditEvents.Add(NewValidationPublicationAuditEvent(
                            command.ActorUserId,
                            command.CorrelationId,
                            validationPlan));
                    }
                    var response = ToDetails(draft);
                    var taskResponse = command.TaskDirective is null
                        ? null
                        : EfTaskDefinitionService.ToVersionDetails(taskPlans
                            .Single(item => item.Draft.TaskDefinitionId == TaskDefinitionCatalog.Require(command.TaskDirective.TaskCode).Id)
                            .Draft);
                    var idempotencyResponse = (object?)taskResponse ?? response;
                    var responseEtag = taskResponse is null
                        ? $"\"{response.RowVersion}\""
                        : $"\"{taskResponse.RowVersion}\"";
                    dbContext.IdempotencyRecords.Add(IdempotencyProtocol.Completed(
                        scope, command.IdempotencyKey, requestHash, "CONFIGURATION_RELEASE", draft.Id,
                        StatusCodes.Status200OK, idempotencyResponse, publishedAt, DateTimeOffset.MaxValue,
                        responseEtag: responseEtag));
                    var afterData = JsonSerializer.SerializeToDocument(new
                    {
                        schemaVersion = 1,
                        published = AuditValue(draft),
                        superseded = current is null ? null : AuditValue(current),
                    });
                    return (
                        response,
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
            return await FindReplayAsync(scope, legacyScope, command.IdempotencyKey, requestHash, legacyRequestHash,
                command.ActorUserId, command.CorrelationId, operation, cancellationToken)
                ?? throw new ConfigurationIdempotencyConflictException();
        }
        catch (DbUpdateConcurrencyException)
        {
            dbContext.ChangeTracker.Clear();
            throw new VersionConflictException();
        }
        catch (DbUpdateException exception) when (
            GetConstraintName(exception) is OneCurrentIndex or SuccessorIndex or
                CalendarCurrentIndex or CalendarSuccessorIndex or
                TaskCurrentIndex or TaskSuccessorIndex or TaskVersionIndex or
                EligibilityCurrentIndex or EligibilitySuccessorIndex or EligibilityVersionIndex or
                ActivationCurrentIndex or ActivationSuccessorIndex or ActivationVersionIndex or
                EvidenceCurrentIndex or EvidenceSuccessorIndex or EvidenceVersionIndex or
                ValidationCurrentIndex or ValidationSuccessorIndex or ValidationVersionIndex)
        {
            dbContext.ChangeTracker.Clear();
            throw new VersionConflictException();
        }
        catch (DbUpdateException exception) when (
            GetConstraintName(exception) is ValidityConstraint or CalendarValidityConstraint or TaskValidityConstraint or EligibilityValidityConstraint or ActivationValidityConstraint or EvidenceValidityConstraint or ValidationValidityConstraint)
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
                legacyScope,
                command.IdempotencyKey,
                requestHash,
                legacyRequestHash,
                command.ActorUserId,
                command.CorrelationId,
                operation,
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

    private async Task<IReadOnlyList<TaskPublicationPlan>> PlanTaskPublicationAsync(
        Guid releaseId,
        DateTimeOffset effectiveFrom,
        string reason,
        TaskPublicationDirective? directive,
        CancellationToken cancellationToken)
    {
        var drafts = await dbContext.TaskDefinitionVersions
            .FromSqlInterpolated(
                $"""
                SELECT *
                FROM task_definition_version
                WHERE release_id = {releaseId}
                  AND status = {VersionStatuses.Draft}
                ORDER BY task_definition_id, version_no
                FOR UPDATE
                """)
            .AsTracking()
            .ToListAsync(cancellationToken);

        if (directive?.CreateDraft == true)
        {
            var seed = TaskDefinitionCatalog.Require(directive.TaskCode);
            if (drafts.Any(item => item.TaskDefinitionId == seed.Id))
            {
                throw new VersionConflictException();
            }

            var current = await LockCurrentTaskVersionAsync(seed.Id, cancellationToken)
                ?? throw new TaskDefinitionNotFoundException();
            VersioningRules.RequireExpectedRowVersion(current.RowVersion, directive.ExpectedRowVersion);
            var nextVersionNo = (await dbContext.TaskDefinitionVersions
                .Where(item => item.TaskDefinitionId == seed.Id)
                .MaxAsync(item => (int?)item.VersionNo, cancellationToken) ?? 0) + 1;
            using var emptyPayload = JsonDocument.Parse("{}");
            var deactivation = new TaskDefinitionVersion(
                uuidGenerator.NewUuid(),
                seed.Id,
                nextVersionNo,
                TaskDefinitionCatalog.SchemaVersion,
                emptyPayload,
                releaseId);
            dbContext.TaskDefinitionVersions.Add(deactivation);
            drafts.Add(deactivation);
        }

        var definitionCodes = await dbContext.TaskDefinitions
            .AsNoTracking()
            .ToDictionaryAsync(item => item.Id, item => item.TaskCode, cancellationToken);
        var plans = new List<TaskPublicationPlan>(drafts.Count);
        var directiveMatched = directive is null;
        foreach (var taskDraft in drafts)
        {
            var taskCode = definitionCodes[taskDraft.TaskDefinitionId];
            var isTarget = directive is not null &&
                string.Equals(taskCode, directive.TaskCode, StringComparison.Ordinal) &&
                (directive.CreateDraft || directive.VersionId == taskDraft.Id);
            if (directive is not null &&
                string.Equals(taskCode, directive.TaskCode, StringComparison.Ordinal) && !isTarget)
            {
                throw new TaskDefinitionNotFoundException();
            }

            if (isTarget)
            {
                VersioningRules.RequireExpectedRowVersion(taskDraft.RowVersion, directive!.CreateDraft ? 1 : directive.ExpectedRowVersion);
                directiveMatched = true;
            }

            var current = await LockCurrentTaskVersionAsync(taskDraft.TaskDefinitionId, cancellationToken);
            var history = await dbContext.TaskDefinitionVersions
                .AsNoTracking()
                .Where(item =>
                    item.TaskDefinitionId == taskDraft.TaskDefinitionId &&
                    item.Status == VersionStatuses.Superseded)
                .ToListAsync(cancellationToken);
            var normalizedCurrent = current is null
                ? null
                : current.ToVersionRecord() with { Status = VersionStatuses.Current };
            var plan = VersioningRules.PlanPublication(
                taskDraft.ToVersionRecord(),
                normalizedCurrent,
                history.Select(item => item.ToVersionRecord()),
                taskDraft.RowVersion,
                effectiveFrom,
                reason);
            plans.Add(new TaskPublicationPlan(
                taskCode,
                taskDraft,
                current,
                plan,
                !isTarget || directive!.ActiveForNew,
                current is null ? null : TaskAuditValue(taskCode, current),
                TaskAuditValue(taskCode, taskDraft)));
        }

        if (!directiveMatched)
        {
            throw new TaskDefinitionNotFoundException();
        }

        return plans;
    }

    private async Task<TaskDefinitionVersion?> LockCurrentTaskVersionAsync(
        Guid taskDefinitionId,
        CancellationToken cancellationToken) =>
        await dbContext.TaskDefinitionVersions
            .FromSqlInterpolated(
                $"""
                SELECT *
                FROM task_definition_version
                WHERE task_definition_id = {taskDefinitionId}
                  AND status IN ({VersionStatuses.Current}, {TaskDefinitionStatuses.InactiveForNew})
                FOR UPDATE
                """)
            .AsTracking()
            .SingleOrDefaultAsync(cancellationToken);

    private async Task<IReadOnlyList<EligibilityPublicationPlan>> PlanEligibilityPublicationAsync(
        Guid releaseId,
        DateTimeOffset effectiveFrom,
        string reason,
        IReadOnlyList<TaskPublicationPlan> taskPlans,
        CancellationToken cancellationToken)
    {
        var drafts = await dbContext.EligibilityPolicyVersions
            .FromSqlInterpolated(
                $"SELECT * FROM eligibility_policy_version WHERE release_id = {releaseId} AND status = {VersionStatuses.Draft} ORDER BY task_definition_id, version_no FOR UPDATE")
            .AsTracking()
            .ToListAsync(cancellationToken);
        var currents = await dbContext.EligibilityPolicyVersions
            .FromSqlInterpolated(
                $"SELECT * FROM eligibility_policy_version WHERE status = {VersionStatuses.Current} ORDER BY task_definition_id FOR UPDATE")
            .AsTracking()
            .ToListAsync(cancellationToken);
        var currentByTask = currents.ToDictionary(item => item.TaskDefinitionId);

        // Releases predating HU-017 remain publishable until the first complete policy catalog is installed.
        if (drafts.Count == 0 && currents.Count == 0)
        {
            return [];
        }

        var resultingPolicies = new Dictionary<Guid, EligibilityPolicyVersion>(currentByTask);
        foreach (var policy in drafts)
        {
            resultingPolicies[policy.TaskDefinitionId] = policy;
        }

        if (resultingPolicies.Count != EligibilityPolicyCatalog.All.Count ||
            EligibilityPolicyCatalog.All.Keys.Any(code =>
                !resultingPolicies.ContainsKey(TaskDefinitionCatalog.Require(code).Id)))
        {
            throw new EligibilityPolicyCoverageException();
        }

        var applicableVersionIds = await dbContext.TaskDefinitionVersions.AsNoTracking()
            .Where(version => version.Status == VersionStatuses.Current || version.Status == TaskDefinitionStatuses.InactiveForNew)
            .Select(version => version.Id)
            .ToListAsync(cancellationToken);
        foreach (var policy in drafts)
        {
            var taskPlan = taskPlans.SingleOrDefault(item => item.Draft.TaskDefinitionId == policy.TaskDefinitionId);
            if (taskPlan is not null)
            {
                if (policy.TaskDefinitionVersionId != taskPlan.Draft.Id)
                {
                    throw new EligibilityPolicyDefinitionPreconditionException();
                }
            }
            else if (!applicableVersionIds.Contains(policy.TaskDefinitionVersionId))
            {
                throw new EligibilityPolicyDefinitionPreconditionException();
            }
        }

        foreach (var taskPlan in taskPlans)
        {
            if (!drafts.Any(policy =>
                    policy.TaskDefinitionId == taskPlan.Draft.TaskDefinitionId &&
                    policy.TaskDefinitionVersionId == taskPlan.Draft.Id))
            {
                throw new EligibilityPolicyDefinitionPreconditionException();
            }
        }

        var codes = TaskDefinitionCatalog.All.ToDictionary(item => item.Id, item => item.TaskCode);
        var plans = new List<EligibilityPublicationPlan>(drafts.Count);
        foreach (var policy in drafts)
        {
            currentByTask.TryGetValue(policy.TaskDefinitionId, out var current);
            if (policy.BasedOnId != current?.Id)
            {
                throw new VersionConflictException();
            }
            var history = await dbContext.EligibilityPolicyVersions.AsNoTracking()
                .Where(item => item.TaskDefinitionId == policy.TaskDefinitionId && item.Status == VersionStatuses.Superseded)
                .ToListAsync(cancellationToken);
            var plan = VersioningRules.PlanPublication(
                policy.ToVersionRecord(),
                current?.ToVersionRecord(),
                history.Select(item => item.ToVersionRecord()),
                policy.RowVersion,
                effectiveFrom,
                reason);
            plans.Add(new EligibilityPublicationPlan(
                codes[policy.TaskDefinitionId],
                policy,
                current,
                plan,
                current is null ? null : EligibilityAuditValue(codes[policy.TaskDefinitionId], current),
                EligibilityAuditValue(codes[policy.TaskDefinitionId], policy)));
        }

        return plans;
    }

    private AuditEvent NewEligibilityPublicationAuditEvent(
        Guid actorUserId,
        Guid correlationId,
        EligibilityPublicationPlan policyPlan) => new()
        {
            Id = uuidGenerator.NewUuid(),
            OccurredAt = clock.UtcNow,
            ActorUserId = actorUserId,
            ActorType = "APP_USER",
            Action = "ELIGIBILITY_POLICY_PUBLISHED",
            ResourceType = "ELIGIBILITY_POLICY_VERSION",
            ResourceId = policyPlan.Draft.Id,
            BranchId = BranchScope.LorettaId,
            CorrelationId = correlationId,
            BeforeData = JsonSerializer.SerializeToDocument(new
            {
                schemaVersion = 1,
                draft = policyPlan.DraftBefore,
                current = policyPlan.CurrentBefore,
            }),
            AfterData = JsonSerializer.SerializeToDocument(new
            {
                schemaVersion = 1,
                published = EligibilityAuditValue(policyPlan.TaskCode, policyPlan.Draft),
                superseded = policyPlan.Current is null
                    ? null
                    : EligibilityAuditValue(policyPlan.TaskCode, policyPlan.Current),
            }),
            Reason = policyPlan.Draft.Reason,
            Outcome = "SUCCESS",
        };

    private static object EligibilityAuditValue(string taskCode, EligibilityPolicyVersion policy) => new
    {
        schemaVersion = 1,
        taskCode,
        taskDefinitionVersionId = policy.TaskDefinitionVersionId,
        basedOnId = policy.BasedOnId,
        policy.VersionNo,
        policy.RequiredRole,
        policy.RequiresAvailability,
        policy.RequiredShift,
        policy.Status,
        policy.EffectiveFrom,
        policy.EffectiveTo,
        policy.SupersedesId,
        policy.RowVersion,
    };

    private async Task<IReadOnlyList<ValidationPublicationPlan>> PlanValidationPublicationAsync(
        Guid releaseId,
        DateTimeOffset effectiveFrom,
        string reason,
        IReadOnlyList<TaskPublicationPlan> taskPlans,
        CancellationToken cancellationToken)
    {
        var drafts = await dbContext.ValidationPolicyVersions
            .FromSqlInterpolated(
                $"SELECT * FROM validation_policy_version WHERE release_id = {releaseId} AND status = {VersionStatuses.Draft} ORDER BY task_definition_id, version_no FOR UPDATE")
            .AsTracking()
            .ToListAsync(cancellationToken);
        var currents = await dbContext.ValidationPolicyVersions
            .FromSqlInterpolated(
                $"SELECT * FROM validation_policy_version WHERE status = {VersionStatuses.Current} ORDER BY task_definition_id FOR UPDATE")
            .AsTracking()
            .ToListAsync(cancellationToken);
        var currentByTask = currents.ToDictionary(item => item.TaskDefinitionId);

        if (drafts.Count == 0 && currents.Count == 0)
        {
            return [];
        }

        var resultingPolicies = new Dictionary<Guid, ValidationPolicyVersion>(currentByTask);
        foreach (var policy in drafts)
        {
            resultingPolicies[policy.TaskDefinitionId] = policy;
        }

        if (resultingPolicies.Count != ValidationPolicyCatalog.All.Count ||
            ValidationPolicyCatalog.All.Keys.Any(code =>
                !resultingPolicies.ContainsKey(TaskDefinitionCatalog.Require(code).Id)))
        {
            throw new ValidationPolicyCoverageException();
        }

        var applicableVersionIds = await dbContext.TaskDefinitionVersions.AsNoTracking()
            .Where(version => version.Status == VersionStatuses.Current || version.Status == TaskDefinitionStatuses.InactiveForNew)
            .Select(version => version.Id)
            .ToListAsync(cancellationToken);
        foreach (var policy in drafts)
        {
            var taskPlan = taskPlans.SingleOrDefault(item => item.Draft.TaskDefinitionId == policy.TaskDefinitionId);
            if (taskPlan is not null)
            {
                if (policy.TaskDefinitionVersionId != taskPlan.Draft.Id)
                {
                    throw new ValidationPolicyDefinitionPreconditionException();
                }
            }
            else if (!applicableVersionIds.Contains(policy.TaskDefinitionVersionId))
            {
                throw new ValidationPolicyDefinitionPreconditionException();
            }
        }

        foreach (var taskPlan in taskPlans)
        {
            if (!drafts.Any(policy =>
                    policy.TaskDefinitionId == taskPlan.Draft.TaskDefinitionId &&
                    policy.TaskDefinitionVersionId == taskPlan.Draft.Id))
            {
                throw new ValidationPolicyDefinitionPreconditionException();
            }
        }

        var codes = TaskDefinitionCatalog.All.ToDictionary(item => item.Id, item => item.TaskCode);
        var plans = new List<ValidationPublicationPlan>(drafts.Count);
        foreach (var policy in drafts)
        {
            var taskCode = codes[policy.TaskDefinitionId];
            ValidationPolicyCatalog.Validate(
                taskCode,
                policy.IsRequired,
                policy.ExecutorRole,
                policy.ValidatorRelation,
                policy.ValidatorRole,
                EfValidationPolicyService.ReadAllowedResults(policy));
            currentByTask.TryGetValue(policy.TaskDefinitionId, out var current);
            if (policy.BasedOnId != current?.Id)
            {
                throw new VersionConflictException();
            }

            var history = await dbContext.ValidationPolicyVersions.AsNoTracking()
                .Where(item => item.TaskDefinitionId == policy.TaskDefinitionId && item.Status == VersionStatuses.Superseded)
                .ToListAsync(cancellationToken);
            var plan = VersioningRules.PlanPublication(
                policy.ToVersionRecord(),
                current?.ToVersionRecord(),
                history.Select(item => item.ToVersionRecord()),
                policy.RowVersion,
                effectiveFrom,
                reason);
            plans.Add(new ValidationPublicationPlan(taskCode, policy, current, plan));
        }

        return plans;
    }

    private AuditEvent NewValidationPublicationAuditEvent(
        Guid actorUserId,
        Guid correlationId,
        ValidationPublicationPlan policyPlan) => new()
        {
            Id = uuidGenerator.NewUuid(),
            OccurredAt = clock.UtcNow,
            ActorUserId = actorUserId,
            ActorType = "APP_USER",
            Action = "VALIDATION_POLICY_PUBLISHED",
            ResourceType = "VALIDATION_POLICY_VERSION",
            ResourceId = policyPlan.Draft.Id,
            BranchId = BranchScope.LorettaId,
            CorrelationId = correlationId,
            BeforeData = policyPlan.Current is null
                ? null
                : EfValidationPolicyService.Serialize(policyPlan.Current, policyPlan.TaskCode),
            AfterData = EfValidationPolicyService.Serialize(policyPlan.Draft, policyPlan.TaskCode),
            Reason = policyPlan.Draft.Reason,
            Outcome = "SUCCESS",
        };

    private async Task<IReadOnlyList<EvidencePublicationPlan>> PlanEvidencePublicationAsync(
        Guid releaseId,
        DateTimeOffset effectiveFrom,
        string reason,
        IReadOnlyList<TaskPublicationPlan> taskPlans,
        CancellationToken cancellationToken)
    {
        var drafts = await dbContext.EvidencePolicyVersions
            .FromSqlInterpolated(
                $"SELECT * FROM evidence_policy_version WHERE release_id = {releaseId} AND status = {VersionStatuses.Draft} ORDER BY task_definition_id, version_no FOR UPDATE")
            .AsTracking()
            .ToListAsync(cancellationToken);
        var currents = await dbContext.EvidencePolicyVersions
            .FromSqlInterpolated(
                $"SELECT * FROM evidence_policy_version WHERE status = {VersionStatuses.Current} ORDER BY task_definition_id FOR UPDATE")
            .AsTracking()
            .ToListAsync(cancellationToken);
        var currentByTask = currents.ToDictionary(item => item.TaskDefinitionId);

        if (drafts.Count == 0 && currents.Count == 0)
        {
            return [];
        }

        var resultingPolicies = new Dictionary<Guid, EvidencePolicyVersion>(currentByTask);
        foreach (var policy in drafts)
        {
            resultingPolicies[policy.TaskDefinitionId] = policy;
        }

        if (resultingPolicies.Count != EvidencePolicyCatalog.All.Count ||
            EvidencePolicyCatalog.All.Keys.Any(code =>
                !resultingPolicies.ContainsKey(TaskDefinitionCatalog.Require(code).Id)))
        {
            throw new EvidencePolicyCoverageException();
        }

        var applicableVersionIds = await dbContext.TaskDefinitionVersions.AsNoTracking()
            .Where(version => version.Status == VersionStatuses.Current || version.Status == TaskDefinitionStatuses.InactiveForNew)
            .Select(version => version.Id)
            .ToListAsync(cancellationToken);
        foreach (var policy in drafts)
        {
            var taskPlan = taskPlans.SingleOrDefault(item => item.Draft.TaskDefinitionId == policy.TaskDefinitionId);
            if (taskPlan is not null)
            {
                if (policy.TaskDefinitionVersionId != taskPlan.Draft.Id)
                {
                    throw new EvidencePolicyDefinitionPreconditionException();
                }
            }
            else if (!applicableVersionIds.Contains(policy.TaskDefinitionVersionId))
            {
                throw new EvidencePolicyDefinitionPreconditionException();
            }
        }

        foreach (var taskPlan in taskPlans)
        {
            if (!drafts.Any(policy =>
                    policy.TaskDefinitionId == taskPlan.Draft.TaskDefinitionId &&
                    policy.TaskDefinitionVersionId == taskPlan.Draft.Id))
            {
                throw new EvidencePolicyDefinitionPreconditionException();
            }
        }

        var policyIds = drafts.Select(policy => policy.Id).Concat(currents.Select(policy => policy.Id)).ToArray();
        var requirementRows = await dbContext.EvidenceRequirementVersions.AsNoTracking()
            .Where(item => policyIds.Contains(item.PolicyVersionId))
            .OrderBy(item => item.Ordinal)
            .ToListAsync(cancellationToken);
        var codes = TaskDefinitionCatalog.All.ToDictionary(item => item.Id, item => item.TaskCode);
        var plans = new List<EvidencePublicationPlan>(drafts.Count);
        foreach (var policy in drafts)
        {
            var definitions = requirementRows
                .Where(item => item.PolicyVersionId == policy.Id)
                .Select(item => new EvidenceRequirementDefinition(
                    item.RequirementCode,
                    item.Kind,
                    item.ConditionCode,
                    item.Ordinal))
                .ToArray();
            _ = EvidencePolicyCatalog.Validate(
                codes[policy.TaskDefinitionId],
                definitions.Select(item => new EvidenceRequirementInput(item.Code, item.Kind, item.ConditionCode)).ToArray());
            currentByTask.TryGetValue(policy.TaskDefinitionId, out var current);
            if (policy.BasedOnId != current?.Id)
            {
                throw new VersionConflictException();
            }

            var history = await dbContext.EvidencePolicyVersions.AsNoTracking()
                .Where(item => item.TaskDefinitionId == policy.TaskDefinitionId && item.Status == VersionStatuses.Superseded)
                .ToListAsync(cancellationToken);
            var plan = VersioningRules.PlanPublication(
                policy.ToVersionRecord(),
                current?.ToVersionRecord(),
                history.Select(item => item.ToVersionRecord()),
                policy.RowVersion,
                effectiveFrom,
                reason);
            plans.Add(new EvidencePublicationPlan(
                codes[policy.TaskDefinitionId],
                policy,
                current,
                plan,
                definitions,
                current is null
                    ? []
                    : requirementRows
                        .Where(item => item.PolicyVersionId == current.Id)
                        .Select(item => new EvidenceRequirementDefinition(
                            item.RequirementCode,
                            item.Kind,
                            item.ConditionCode,
                            item.Ordinal))
                        .ToArray()));
        }

        return plans;
    }

    private AuditEvent NewEvidencePublicationAuditEvent(
        Guid actorUserId,
        Guid correlationId,
        EvidencePublicationPlan policyPlan) => new()
        {
            Id = uuidGenerator.NewUuid(),
            OccurredAt = clock.UtcNow,
            ActorUserId = actorUserId,
            ActorType = "APP_USER",
            Action = "EVIDENCE_POLICY_PUBLISHED",
            ResourceType = "EVIDENCE_POLICY_VERSION",
            ResourceId = policyPlan.Draft.Id,
            BranchId = BranchScope.LorettaId,
            CorrelationId = correlationId,
            BeforeData = policyPlan.Current is null
                ? null
                : EfEvidencePolicyService.Serialize(
                    policyPlan.Current,
                    policyPlan.TaskCode,
                    policyPlan.CurrentRequirements),
            AfterData = EfEvidencePolicyService.Serialize(
                policyPlan.Draft,
                policyPlan.TaskCode,
                policyPlan.Requirements),
            Reason = policyPlan.Draft.Reason,
            Outcome = "SUCCESS",
        };

    private async Task<IReadOnlyList<ActivationPublicationPlan>> PlanActivationPublicationAsync(
        Guid releaseId,
        DateTimeOffset effectiveFrom,
        string reason,
        IReadOnlyList<TaskPublicationPlan> taskPlans,
        CancellationToken cancellationToken)
    {
        var drafts = await dbContext.ActivationRuleVersions
            .FromSqlInterpolated(
                $"SELECT * FROM activation_rule_version WHERE release_id = {releaseId} AND status = {VersionStatuses.Draft} ORDER BY task_definition_id, version_no FOR UPDATE")
            .AsTracking()
            .ToListAsync(cancellationToken);
        var currents = await dbContext.ActivationRuleVersions
            .FromSqlInterpolated(
                $"SELECT * FROM activation_rule_version WHERE status = {VersionStatuses.Current} ORDER BY task_definition_id FOR UPDATE")
            .AsTracking()
            .ToListAsync(cancellationToken);
        var currentByTask = currents.ToDictionary(item => item.TaskDefinitionId);

        // Releases predating HU-012 remain publishable until the first complete activation catalog is installed.
        if (drafts.Count == 0 && currents.Count == 0)
        {
            return [];
        }

        var resultingPolicies = new Dictionary<Guid, ActivationRuleVersion>(currentByTask);
        foreach (var policy in drafts)
        {
            resultingPolicies[policy.TaskDefinitionId] = policy;
        }

        if (resultingPolicies.Count != ActivationPolicyCatalog.All.Count ||
            ActivationPolicyCatalog.All.Keys.Any(code =>
                !resultingPolicies.ContainsKey(TaskDefinitionCatalog.Require(code).Id)))
        {
            throw new ActivationPolicyCoverageException();
        }

        var activeVersionIds = await dbContext.TaskDefinitionVersions.AsNoTracking()
            .Where(version => version.Status == VersionStatuses.Current)
            .Select(version => version.Id)
            .ToListAsync(cancellationToken);
        foreach (var policy in drafts)
        {
            var taskPlan = taskPlans.SingleOrDefault(item => item.Draft.TaskDefinitionId == policy.TaskDefinitionId);
            if (taskPlan is not null)
            {
                if (!taskPlan.ActiveForNew || policy.TaskDefinitionVersionId != taskPlan.Draft.Id)
                {
                    throw new ActivationPolicyDefinitionPreconditionException();
                }
            }
            else if (!activeVersionIds.Contains(policy.TaskDefinitionVersionId))
            {
                throw new ActivationPolicyDefinitionPreconditionException();
            }
        }

        foreach (var taskPlan in taskPlans.Where(item => item.ActiveForNew))
        {
            if (!drafts.Any(policy =>
                    policy.TaskDefinitionId == taskPlan.Draft.TaskDefinitionId &&
                    policy.TaskDefinitionVersionId == taskPlan.Draft.Id))
            {
                throw new ActivationPolicyDefinitionPreconditionException();
            }
        }

        var codes = TaskDefinitionCatalog.All.ToDictionary(item => item.Id, item => item.TaskCode);
        var plans = new List<ActivationPublicationPlan>(drafts.Count);
        foreach (var policy in drafts)
        {
            currentByTask.TryGetValue(policy.TaskDefinitionId, out var current);
            if (policy.BasedOnId != current?.Id)
            {
                throw new VersionConflictException();
            }

            var history = await dbContext.ActivationRuleVersions.AsNoTracking()
                .Where(item => item.TaskDefinitionId == policy.TaskDefinitionId && item.Status == VersionStatuses.Superseded)
                .ToListAsync(cancellationToken);
            var plan = VersioningRules.PlanPublication(
                policy.ToVersionRecord(),
                current?.ToVersionRecord(),
                history.Select(item => item.ToVersionRecord()),
                policy.RowVersion,
                effectiveFrom,
                reason);
            plans.Add(new ActivationPublicationPlan(
                codes[policy.TaskDefinitionId],
                policy,
                current,
                plan,
                current is null ? null : ActivationAuditValue(codes[policy.TaskDefinitionId], current),
                ActivationAuditValue(codes[policy.TaskDefinitionId], policy)));
        }

        return plans;
    }

    private AuditEvent NewActivationPublicationAuditEvent(
        Guid actorUserId,
        Guid correlationId,
        ActivationPublicationPlan policyPlan) => new()
        {
            Id = uuidGenerator.NewUuid(),
            OccurredAt = clock.UtcNow,
            ActorUserId = actorUserId,
            ActorType = "APP_USER",
            Action = "ACTIVATION_POLICY_PUBLISHED",
            ResourceType = "ACTIVATION_RULE_VERSION",
            ResourceId = policyPlan.Draft.Id,
            BranchId = BranchScope.LorettaId,
            CorrelationId = correlationId,
            BeforeData = JsonSerializer.SerializeToDocument(new
            {
                schemaVersion = 1,
                draft = policyPlan.DraftBefore,
                current = policyPlan.CurrentBefore,
            }),
            AfterData = JsonSerializer.SerializeToDocument(new
            {
                schemaVersion = 1,
                published = ActivationAuditValue(policyPlan.TaskCode, policyPlan.Draft),
                superseded = policyPlan.Current is null
                    ? null
                    : ActivationAuditValue(policyPlan.TaskCode, policyPlan.Current),
            }),
            Reason = policyPlan.Draft.Reason,
            Outcome = "SUCCESS",
        };

    private static object ActivationAuditValue(string taskCode, ActivationRuleVersion rule) => new
    {
        schemaVersion = 1,
        taskCode,
        taskDefinitionVersionId = rule.TaskDefinitionVersionId,
        basedOnId = rule.BasedOnId,
        rule.VersionNo,
        rule.Mode,
        schedule = rule.Schedule.RootElement,
        rule.OriginKeySchema,
        rule.Status,
        rule.EffectiveFrom,
        rule.EffectiveTo,
        rule.SupersedesId,
        rule.RowVersion,
    };

    private AuditEvent NewTaskPublicationAuditEvent(
        Guid actorUserId,
        Guid correlationId,
        TaskPublicationPlan taskPlan) => new()
        {
            Id = uuidGenerator.NewUuid(),
            OccurredAt = clock.UtcNow,
            ActorUserId = actorUserId,
            ActorType = "APP_USER",
            Action = taskPlan.ActiveForNew ? "TASK_DEFINITION_VERSION_PUBLISHED" : "TASK_DEFINITION_DEACTIVATED_NEW",
            ResourceType = "TASK_DEFINITION_VERSION",
            ResourceId = taskPlan.Draft.Id,
            BranchId = BranchScope.LorettaId,
            CorrelationId = correlationId,
            BeforeData = JsonSerializer.SerializeToDocument(new
            {
                schemaVersion = 1,
                draft = taskPlan.DraftBefore,
                current = taskPlan.CurrentBefore,
            }),
            AfterData = JsonSerializer.SerializeToDocument(new
            {
                schemaVersion = 1,
                published = TaskAuditValue(taskPlan.TaskCode, taskPlan.Draft),
                superseded = taskPlan.Current is null ? null : TaskAuditValue(taskPlan.TaskCode, taskPlan.Current),
            }),
            Reason = taskPlan.Draft.Reason,
            Outcome = "SUCCESS",
        };

    internal static object TaskAuditValue(string taskCode, TaskDefinitionVersion version) => new
    {
        schemaVersion = 1,
        taskCode,
        taskDefinitionVersionId = version.Id,
        versionNo = version.VersionNo,
        status = version.Status,
        effectiveFrom = version.EffectiveFrom,
        effectiveTo = version.EffectiveTo,
        payloadSchemaVersion = version.SchemaVersion,
        taskPayload = version.TaskPayload.RootElement,
        releaseId = version.ReleaseId,
        reason = version.Reason,
        supersedesId = version.SupersedesId,
        rowVersion = version.RowVersion,
    };

    private async Task<ConfigurationReleaseDetails?> FindReplayAsync(
        string scope,
        string legacyScope,
        Guid key,
        string requestHash,
        string legacyRequestHash,
        Guid actorUserId,
        Guid correlationId,
        string operation,
        CancellationToken cancellationToken)
    {
        var record = await dbContext.IdempotencyRecords
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Scope == scope && item.Key == key, cancellationToken)
            ?? await dbContext.IdempotencyRecords.AsNoTracking()
                .SingleOrDefaultAsync(item => item.Scope == legacyScope && item.Key == key, cancellationToken);
        if (record is null)
        {
            return null;
        }

        var expectedHash = record.ProtocolVersion == IdempotencyProtocol.CurrentVersion
            ? requestHash
            : legacyRequestHash;
        if (!string.Equals(record.RequestHash, expectedHash, StringComparison.Ordinal))
        {
            await AuditConflictAsync(actorUserId, correlationId, key, operation, record.ResourceId, cancellationToken);
            throw new ConfigurationIdempotencyConflictException();
        }

        if (record.ProtocolVersion == IdempotencyProtocol.CurrentVersion &&
            !operation.StartsWith("TASK_DEFINITION_", StringComparison.Ordinal))
        {
            return IdempotencyProtocol.ReadPayload<ConfigurationReleaseDetails>(record);
        }

        var release = await dbContext.ConfigurationReleases
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == record.ResourceId, cancellationToken)
            ?? throw new ConfigurationReleaseNotFoundException();
        dbContext.ChangeTracker.Clear();
        return ToDetails(release);
    }

    private Task AuditConflictAsync(
        Guid actorUserId,
        Guid correlationId,
        Guid key,
        string operation,
        Guid resourceId,
        CancellationToken token) => IdempotencyProtocol.PersistConflictAsync(
            auditTransaction,
            IdempotencyProtocol.ConflictAudit(
                uuidGenerator.NewUuid(), clock.UtcNow, actorUserId, "CONFIGURATION_RELEASE", resourceId,
                BranchScope.LorettaId, correlationId, key, operation), token);

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

    private sealed record TaskPublicationPlan(
        string TaskCode,
        TaskDefinitionVersion Draft,
        TaskDefinitionVersion? Current,
        VersionPublicationPlan Plan,
        bool ActiveForNew,
        object? CurrentBefore,
        object DraftBefore);

    private sealed record EligibilityPublicationPlan(
        string TaskCode,
        EligibilityPolicyVersion Draft,
        EligibilityPolicyVersion? Current,
        VersionPublicationPlan Plan,
        object? CurrentBefore,
        object DraftBefore);

    private sealed record ActivationPublicationPlan(
        string TaskCode,
        ActivationRuleVersion Draft,
        ActivationRuleVersion? Current,
        VersionPublicationPlan Plan,
        object? CurrentBefore,
        object DraftBefore);

    private sealed record EvidencePublicationPlan(
        string TaskCode,
        EvidencePolicyVersion Draft,
        EvidencePolicyVersion? Current,
        VersionPublicationPlan Plan,
        IReadOnlyList<EvidenceRequirementDefinition> Requirements,
        IReadOnlyList<EvidenceRequirementDefinition> CurrentRequirements);

    private sealed record ValidationPublicationPlan(
        string TaskCode,
        ValidationPolicyVersion Draft,
        ValidationPolicyVersion? Current,
        VersionPublicationPlan Plan);
}
