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

namespace Sgol.Web.Infrastructure.Persistence.Configuration;

public sealed class EfTaskDefinitionService(
    SgolDbContext dbContext,
    AuditTransaction auditTransaction,
    EfConfigurationReleaseService releaseService,
    IClock clock,
    IUuidGenerator uuidGenerator) : ITaskDefinitionService
{
    private const string IdempotencyPrimaryKey = "PK_idempotency_record";
    private const string VersionNumberIndex = "IX_task_definition_version_number";
    private const string ReleaseDefinitionIndex = "IX_task_definition_version_release_definition";

    public async Task<IReadOnlyList<TaskDefinitionDetails>> ListAsync(
        Guid actorUserId,
        Guid correlationId,
        CancellationToken cancellationToken = default)
    {
        await EnsureReadableAsync(actorUserId, correlationId, cancellationToken);
        var definitions = await dbContext.TaskDefinitions.AsNoTracking()
            .OrderBy(item => item.TaskCode)
            .ToListAsync(cancellationToken);
        var versions = await dbContext.TaskDefinitionVersions.AsNoTracking()
            .Where(item => item.Status != VersionStatuses.Draft)
            .OrderByDescending(item => item.VersionNo)
            .ToListAsync(cancellationToken);
        var policies = await dbContext.EligibilityPolicyVersions.AsNoTracking()
            .Where(item => item.Status == VersionStatuses.Current)
            .ToListAsync(cancellationToken);
        var evidencePolicies = await dbContext.EvidencePolicyVersions.AsNoTracking()
            .Where(item => item.Status == VersionStatuses.Current)
            .ToListAsync(cancellationToken);
        var evidencePolicyIds = evidencePolicies.Select(item => item.Id).ToArray();
        var evidenceRequirements = await dbContext.EvidenceRequirementVersions.AsNoTracking()
            .Where(item => evidencePolicyIds.Contains(item.PolicyVersionId))
            .OrderBy(item => item.Ordinal)
            .ToListAsync(cancellationToken);
        var validationPolicies = await dbContext.ValidationPolicyVersions.AsNoTracking()
            .Where(item => item.Status != VersionStatuses.Draft)
            .OrderByDescending(item => item.VersionNo)
            .ToListAsync(cancellationToken);
        var result = definitions.Select(definition => ToDetails(
            definition,
            versions.Where(item => item.TaskDefinitionId == definition.Id),
            policies.SingleOrDefault(item => item.TaskDefinitionId == definition.Id),
            evidencePolicies.SingleOrDefault(item => item.TaskDefinitionId == definition.Id),
            evidenceRequirements,
            validationPolicies.Where(item => item.TaskDefinitionId == definition.Id))).ToArray();
        dbContext.ChangeTracker.Clear();
        return result;
    }

    public async Task<TaskDefinitionDetails> GetAsync(
        Guid actorUserId,
        Guid correlationId,
        string taskCode,
        CancellationToken cancellationToken = default)
    {
        await EnsureReadableAsync(actorUserId, correlationId, cancellationToken);
        var seed = TaskDefinitionCatalog.Require(taskCode);
        var definition = await dbContext.TaskDefinitions.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == seed.Id, cancellationToken)
            ?? throw new TaskDefinitionNotFoundException();
        var versions = await dbContext.TaskDefinitionVersions.AsNoTracking()
            .Where(item => item.TaskDefinitionId == seed.Id && item.Status != VersionStatuses.Draft)
            .OrderByDescending(item => item.VersionNo)
            .ToListAsync(cancellationToken);
        var policy = await dbContext.EligibilityPolicyVersions.AsNoTracking()
            .SingleOrDefaultAsync(item => item.TaskDefinitionId == seed.Id && item.Status == VersionStatuses.Current, cancellationToken);
        var evidencePolicy = await dbContext.EvidencePolicyVersions.AsNoTracking()
            .SingleOrDefaultAsync(item => item.TaskDefinitionId == seed.Id && item.Status == VersionStatuses.Current, cancellationToken);
        var evidenceRequirements = evidencePolicy is null
            ? []
            : await dbContext.EvidenceRequirementVersions.AsNoTracking()
                .Where(item => item.PolicyVersionId == evidencePolicy.Id)
                .OrderBy(item => item.Ordinal)
                .ToListAsync(cancellationToken);
        var validationPolicies = await dbContext.ValidationPolicyVersions.AsNoTracking()
            .Where(item => item.TaskDefinitionId == seed.Id && item.Status != VersionStatuses.Draft)
            .OrderByDescending(item => item.VersionNo)
            .ToListAsync(cancellationToken);
        var result = ToDetails(definition, versions, policy, evidencePolicy, evidenceRequirements, validationPolicies);
        dbContext.ChangeTracker.Clear();
        return result;
    }

    public async Task<TaskDefinitionVersionDetails> CreateVersionAsync(
        CreateTaskDefinitionVersionCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        await EnsureAuthorizedAsync(command.ActorUserId, command.CorrelationId, cancellationToken);
        var seed = TaskDefinitionCatalog.Require(command.TaskCode);
        using var payload = TaskDefinitionCatalog.ValidatePayload(command.SchemaVersion, command.TaskPayload);
        const string operation = "TASK_DEFINITION_VERSION_CREATE";
        var resource = $"{command.TaskCode}:{command.ReleaseId:D}";
        var scope = IdempotencyProtocol.Scope(command.ActorUserId.ToString("D"), operation, resource);
        var requestHash = IdempotencyProtocol.HashCanonical(operation, command.ActorUserId.ToString("D"), resource,
            new { command.ReleaseId, command.SchemaVersion, taskPayload = payload.RootElement });
        var legacyScope = $"TASK_DEFINITION_VERSION_CREATE:{command.ActorUserId:D}:{command.TaskCode}:{command.ReleaseId:D}";
        var legacyRequestHash = Hash(command.ReleaseId.ToString("D"), command.SchemaVersion.ToString(System.Globalization.CultureInfo.InvariantCulture));
        var replay = await FindVersionReplayAsync(scope, legacyScope, command.IdempotencyKey, requestHash, legacyRequestHash,
            command.ActorUserId, command.CorrelationId, operation, cancellationToken);
        if (replay is not null)
        {
            return replay;
        }

        TaskDefinitionVersion? created = null;
        try
        {
            await auditTransaction.ExecuteAsync(
                async token =>
                {
                    var release = await dbContext.ConfigurationReleases
                        .FromSqlInterpolated($"SELECT * FROM configuration_release WHERE id = {command.ReleaseId} FOR UPDATE")
                        .AsTracking()
                        .SingleOrDefaultAsync(token);
                    if (release is null || release.BranchId != BranchScope.LorettaId || release.Status != VersionStatuses.Draft)
                    {
                        throw new TaskDefinitionReleaseNotDraftException();
                    }

                    _ = await dbContext.TaskDefinitions
                        .FromSqlInterpolated($"SELECT * FROM task_definition WHERE id = {seed.Id} FOR UPDATE")
                        .AsTracking()
                        .SingleAsync(token);
                    var nextVersionNo = (await dbContext.TaskDefinitionVersions
                        .Where(item => item.TaskDefinitionId == seed.Id)
                        .MaxAsync(item => (int?)item.VersionNo, token) ?? 0) + 1;
                    created = new TaskDefinitionVersion(
                        uuidGenerator.NewUuid(),
                        seed.Id,
                        nextVersionNo,
                        command.SchemaVersion,
                        payload,
                        command.ReleaseId);
                    dbContext.TaskDefinitionVersions.Add(created);
                    dbContext.IdempotencyRecords.Add(NewIdempotency(
                        scope,
                        command.IdempotencyKey,
                        requestHash,
                        created.Id,
                        StatusCodes.Status201Created,
                        ToVersionDetails(created),
                        $"/api/v1/task-definitions/{command.TaskCode}/versions/{created.Id:D}"));
                    return NewAuditEvent(
                        command.ActorUserId,
                        command.CorrelationId,
                        created.Id,
                        "TASK_DEFINITION_VERSION_DRAFT_CREATED",
                        afterData: JsonSerializer.SerializeToDocument(
                            EfConfigurationReleaseService.TaskAuditValue(command.TaskCode, created)));
                },
                cancellationToken);
        }
        catch (DbUpdateException exception) when (GetConstraintName(exception) == IdempotencyPrimaryKey)
        {
            dbContext.ChangeTracker.Clear();
            return await FindVersionReplayAsync(scope, legacyScope, command.IdempotencyKey, requestHash, legacyRequestHash,
                command.ActorUserId, command.CorrelationId, operation, cancellationToken)
                ?? throw new TaskDefinitionIdempotencyConflictException();
        }
        catch (DbUpdateException exception) when (GetConstraintName(exception) is VersionNumberIndex or ReleaseDefinitionIndex)
        {
            dbContext.ChangeTracker.Clear();
            throw new VersionConflictException();
        }
        catch
        {
            dbContext.ChangeTracker.Clear();
            throw;
        }

        var result = ToVersionDetails(created!);
        dbContext.ChangeTracker.Clear();
        return result;
    }

    public async Task<TaskDefinitionVersionDetails> PublishVersionAsync(
        PublishTaskDefinitionVersionCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        await EnsureAuthorizedAsync(command.ActorUserId, command.CorrelationId, cancellationToken);
        var seed = TaskDefinitionCatalog.Require(command.TaskCode);
        var draft = await dbContext.TaskDefinitionVersions.AsNoTracking()
            .SingleOrDefaultAsync(item =>
                item.Id == command.VersionId &&
                item.TaskDefinitionId == seed.Id,
                cancellationToken)
            ?? throw new TaskDefinitionNotFoundException();
        if (draft.Status != VersionStatuses.Draft)
        {
            return await RequirePublicationReplayAsync(
                command.ActorUserId,
                command.IdempotencyKey,
                command.CorrelationId,
                command.TaskCode,
                draft,
                command.EffectiveFrom,
                command.Reason,
                "TASK_DEFINITION_VERSION_PUBLISH",
                command.ExpectedRowVersion,
                cancellationToken);
        }

        var release = await RequireDraftReleaseAsync(draft.ReleaseId, cancellationToken);
        await releaseService.PublishAsync(
            new PublishConfigurationReleaseCommand(
                command.ActorUserId,
                command.IdempotencyKey,
                command.CorrelationId,
                release.Id,
                release.RowVersion,
                command.EffectiveFrom,
                command.Reason,
                new TaskPublicationDirective(
                    command.TaskCode,
                    command.VersionId,
                    command.ExpectedRowVersion,
                    ActiveForNew: true,
                    CreateDraft: false)),
            cancellationToken);
        return await ReadVersionAsync(seed.Id, command.VersionId, cancellationToken);
    }

    public async Task<TaskDefinitionVersionDetails> DeactivateNewAsync(
        DeactivateTaskDefinitionCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        await EnsureAuthorizedAsync(command.ActorUserId, command.CorrelationId, cancellationToken);
        var seed = TaskDefinitionCatalog.Require(command.TaskCode);
        var existing = await dbContext.TaskDefinitionVersions.AsNoTracking()
            .SingleOrDefaultAsync(item =>
                item.TaskDefinitionId == seed.Id && item.ReleaseId == command.ReleaseId,
                cancellationToken);
        if (existing is not null && existing.Status != VersionStatuses.Draft)
        {
            return await RequirePublicationReplayAsync(
                command.ActorUserId,
                command.IdempotencyKey,
                command.CorrelationId,
                command.TaskCode,
                existing,
                command.EffectiveFrom,
                command.Reason,
                "TASK_DEFINITION_DEACTIVATE_NEW",
                command.ExpectedRowVersion,
                cancellationToken);
        }

        var release = await RequireDraftReleaseAsync(command.ReleaseId, cancellationToken);
        await releaseService.PublishAsync(
            new PublishConfigurationReleaseCommand(
                command.ActorUserId,
                command.IdempotencyKey,
                command.CorrelationId,
                release.Id,
                release.RowVersion,
                command.EffectiveFrom,
                command.Reason,
                new TaskPublicationDirective(
                    command.TaskCode,
                    VersionId: null,
                    command.ExpectedRowVersion,
                    ActiveForNew: false,
                    CreateDraft: true)),
            cancellationToken);
        var saved = await dbContext.TaskDefinitionVersions.AsNoTracking()
            .Where(item => item.TaskDefinitionId == seed.Id && item.ReleaseId == release.Id)
            .OrderByDescending(item => item.VersionNo)
            .SingleAsync(cancellationToken);
        var result = ToVersionDetails(saved);
        dbContext.ChangeTracker.Clear();
        return result;
    }

    private async Task<TaskDefinitionVersionDetails> RequirePublicationReplayAsync(
        Guid actorUserId,
        Guid idempotencyKey,
        Guid correlationId,
        string taskCode,
        TaskDefinitionVersion version,
        DateTimeOffset effectiveFrom,
        string reason,
        string operation,
        long expectedRowVersion,
        CancellationToken cancellationToken)
    {
        var normalizedReason = VersioningRules.NormalizeRequiredReason(reason);
        var resource = operation == "TASK_DEFINITION_VERSION_PUBLISH"
            ? $"{taskCode}:{version.Id:D}"
            : $"{taskCode}:{version.ReleaseId:D}";
        var scope = IdempotencyProtocol.Scope(actorUserId.ToString("D"), operation, resource);
        var legacyScope = $"CONFIGURATION_RELEASE_PUBLISH:{actorUserId:D}:{version.ReleaseId:D}";
        var body = new
        {
            TaskCode = taskCode,
            VersionId = operation == "TASK_DEFINITION_VERSION_PUBLISH" ? version.Id : (Guid?)null,
            ReleaseId = version.ReleaseId,
            EffectiveFrom = effectiveFrom,
            reason = normalizedReason,
            ActiveForNew = operation == "TASK_DEFINITION_VERSION_PUBLISH",
            CreateDraft = operation == "TASK_DEFINITION_DEACTIVATE_NEW",
        };
        var hash = IdempotencyProtocol.HashCanonical(
            operation, actorUserId.ToString("D"), resource, body, expectedRowVersion);
        var replay = await dbContext.IdempotencyRecords.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Scope == scope && item.Key == idempotencyKey, cancellationToken)
            ?? await dbContext.IdempotencyRecords.AsNoTracking()
                .SingleOrDefaultAsync(item => item.Scope == legacyScope && item.Key == idempotencyKey, cancellationToken);
        if (replay is null || replay.ResourceId != version.ReleaseId ||
            version.EffectiveFrom != effectiveFrom ||
            !string.Equals(version.Reason, normalizedReason, StringComparison.Ordinal) ||
            replay.ProtocolVersion == IdempotencyProtocol.CurrentVersion && replay.RequestHash != hash)
        {
            if (replay is not null)
            {
                await AuditConflictAsync(
                    actorUserId, correlationId, idempotencyKey, operation, version.Id, cancellationToken);
            }
            throw new TaskDefinitionIdempotencyConflictException();
        }

        if (replay.ProtocolVersion == IdempotencyProtocol.CurrentVersion)
        {
            return IdempotencyProtocol.ReadPayload<TaskDefinitionVersionDetails>(replay);
        }

        var result = ToVersionDetails(version);
        dbContext.ChangeTracker.Clear();
        return result;
    }

    private async Task<ConfigurationRelease> RequireDraftReleaseAsync(Guid releaseId, CancellationToken cancellationToken)
    {
        var release = await dbContext.ConfigurationReleases.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == releaseId, cancellationToken);
        if (release is null || release.BranchId != BranchScope.LorettaId || release.Status != VersionStatuses.Draft)
        {
            throw new TaskDefinitionReleaseNotDraftException();
        }

        return release;
    }

    private async Task<TaskDefinitionVersionDetails> ReadVersionAsync(
        Guid taskDefinitionId,
        Guid versionId,
        CancellationToken cancellationToken)
    {
        var saved = await dbContext.TaskDefinitionVersions.AsNoTracking()
            .SingleAsync(item => item.TaskDefinitionId == taskDefinitionId && item.Id == versionId, cancellationToken);
        var result = ToVersionDetails(saved);
        dbContext.ChangeTracker.Clear();
        return result;
    }

    private async Task<TaskDefinitionVersionDetails?> FindVersionReplayAsync(
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
        var record = await dbContext.IdempotencyRecords.AsNoTracking()
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
            throw new TaskDefinitionIdempotencyConflictException();
        }

        if (record.ProtocolVersion == IdempotencyProtocol.CurrentVersion)
        {
            return IdempotencyProtocol.ReadPayload<TaskDefinitionVersionDetails>(record);
        }

        var version = await dbContext.TaskDefinitionVersions.AsNoTracking()
            .SingleAsync(item => item.Id == record.ResourceId, cancellationToken);
        return ToVersionDetails(version);
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
                uuidGenerator.NewUuid(), clock.UtcNow, actorUserId, "TASK_DEFINITION_VERSION", resourceId,
                BranchScope.LorettaId, correlationId, key, operation), token);

    private async Task EnsureAuthorizedAsync(Guid actorUserId, Guid correlationId, CancellationToken cancellationToken)
    {
        if (await ConfigurationAuthorizationQuery.IsDirectionAsync(dbContext, actorUserId, cancellationToken))
        {
            return;
        }

        await AuditDeniedAsync(actorUserId, correlationId, cancellationToken);
        throw new TaskDefinitionAccessDeniedException();
    }

    private async Task EnsureReadableAsync(Guid actorUserId, Guid correlationId, CancellationToken cancellationToken)
    {
        if (await ConfigurationAuthorizationQuery.IsActiveLorettaUserAsync(dbContext, actorUserId, cancellationToken))
        {
            return;
        }

        await AuditDeniedAsync(actorUserId, correlationId, cancellationToken);
        throw new TaskDefinitionAccessDeniedException();
    }

    private Task AuditDeniedAsync(Guid actorUserId, Guid correlationId, CancellationToken cancellationToken) =>
        auditTransaction.ExecuteAsync(
            NewAuditEvent(actorUserId, correlationId, null, "TASK_DEFINITION_ACCESS_DENIED", outcome: "DENIED"),
            _ => Task.CompletedTask,
            cancellationToken);

    private IdempotencyRecord NewIdempotency(
        string scope,
        Guid key,
        string requestHash,
        Guid resourceId,
        int responseCode,
        TaskDefinitionVersionDetails response,
        string? responseLocation) => IdempotencyProtocol.Completed(
            scope,
            key,
            requestHash,
            "TASK_DEFINITION_VERSION",
            resourceId,
            responseCode,
            response,
            clock.UtcNow,
            DateTimeOffset.MaxValue,
            responseEtag: $"\"{response.RowVersion}\"",
            responseLocation: responseLocation);

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
            ResourceType = "TASK_DEFINITION_VERSION",
            ResourceId = resourceId,
            BranchId = BranchScope.LorettaId,
            CorrelationId = correlationId,
            BeforeData = beforeData,
            AfterData = afterData,
            Reason = reason,
            Outcome = outcome,
        };

    private static TaskDefinitionDetails ToDetails(
        TaskDefinition definition,
        IEnumerable<TaskDefinitionVersion> versions,
        EligibilityPolicyVersion? policy,
        EvidencePolicyVersion? evidencePolicy,
        IEnumerable<EvidenceRequirementVersion> evidenceRequirements,
        IEnumerable<ValidationPolicyVersion> validationPolicies)
    {
        var ordered = versions.OrderByDescending(item => item.VersionNo).ToArray();
        var current = ordered.FirstOrDefault(item =>
            item.Status is VersionStatuses.Current or TaskDefinitionStatuses.InactiveForNew);
        var validationHistory = validationPolicies.OrderByDescending(item => item.VersionNo).ToArray();
        var currentValidationPolicy = validationHistory.FirstOrDefault(item => item.Status == VersionStatuses.Current);
        return new TaskDefinitionDetails(
            definition.Id,
            definition.TaskCode,
            definition.Name,
            current is null ? null : ToVersionDetails(current),
            policy is null ? null : EfEligibilityPolicyService.ToDetails(definition.TaskCode, policy),
            evidencePolicy is null
                ? null
                : EfEvidencePolicyService.ToDetails(
                    definition.TaskCode,
                    evidencePolicy,
                    evidenceRequirements
                        .Where(item => item.PolicyVersionId == evidencePolicy.Id)
                        .OrderBy(item => item.Ordinal)
                        .Select(item => new EvidenceRequirementVersionDetails(
                            item.RequirementCode,
                            item.Kind,
                            item.ConditionCode == EvidenceConditionCodes.Always
                                ? null
                                : new EvidenceConditionDetails(item.ConditionCode),
                            item.IsRequired,
                            item.Ordinal))
                        .ToArray()),
            currentValidationPolicy is null
                ? null
                : EfValidationPolicyService.ToDetails(definition.TaskCode, currentValidationPolicy),
            validationHistory
                .Select(item => EfValidationPolicyService.ToDetails(definition.TaskCode, item))
                .ToArray(),
            ordered.Select(ToVersionDetails).ToArray());
    }

    internal static TaskDefinitionVersionDetails ToVersionDetails(TaskDefinitionVersion version) => new(
        version.Id,
        version.VersionNo,
        version.Status,
        version.EffectiveFrom,
        version.EffectiveTo,
        version.SchemaVersion,
        version.TaskPayload.RootElement.Clone(),
        version.ReleaseId,
        version.Reason,
        version.SupersedesId,
        version.RowVersion);

    private static string Hash(params string[] values) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\n', values))));

    private static string? GetConstraintName(DbUpdateException exception) =>
        (exception.InnerException as PostgresException)?.ConstraintName;
}
