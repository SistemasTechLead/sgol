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

public sealed class EfEvidencePolicyService(
    SgolDbContext dbContext,
    AuditTransaction auditTransaction,
    IClock clock,
    IUuidGenerator uuidGenerator) : IEvidencePolicyService
{
    private const string IdempotencyPrimaryKey = "PK_idempotency_record";
    private const string ReleaseTaskIndex = "IX_evidence_policy_version_release_task";
    private const string VersionNumberIndex = "IX_evidence_policy_version_number";

    public async Task<EvidencePolicyVersionDetails> PutAsync(
        PutEvidencePolicyCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        await EnsureAuthorizedAsync(command.ActorUserId, command.CorrelationId, cancellationToken);
        try
        {
            return await PutAuthorizedAsync(command, cancellationToken);
        }
        catch (Exception exception) when (IsAuditableRejection(exception))
        {
            dbContext.ChangeTracker.Clear();
            await RecordRejectionAsync(command.ActorUserId, command.CorrelationId, cancellationToken);
            throw;
        }
    }

    public async Task RecordRejectionAsync(
        Guid actorUserId,
        Guid correlationId,
        CancellationToken cancellationToken = default)
    {
        await EnsureAuthorizedAsync(actorUserId, correlationId, cancellationToken);
        dbContext.ChangeTracker.Clear();
        await auditTransaction.ExecuteAsync(
            NewAuditEvent(actorUserId, correlationId, null, "EVIDENCE_POLICY_REJECTED", outcome: "REJECTED"),
            _ => Task.CompletedTask,
            cancellationToken);
        dbContext.ChangeTracker.Clear();
    }

    private async Task<EvidencePolicyVersionDetails> PutAuthorizedAsync(
        PutEvidencePolicyCommand command,
        CancellationToken cancellationToken)
    {
        var seed = TaskDefinitionCatalog.Require(command.TaskCode);
        var canonical = EvidencePolicyCatalog.Validate(command.TaskCode, command.Requirements);
        const string operation = "EVIDENCE_POLICY_PUT";
        var resource = $"{command.TaskCode}:{command.ReleaseId:D}";
        var scope = IdempotencyProtocol.Scope(command.ActorUserId.ToString("D"), operation, resource);
        var requestHash = IdempotencyProtocol.HashCanonical(operation, command.ActorUserId.ToString("D"), resource,
            new { command.ReleaseId, requirements = canonical }, command.ExpectedRowVersion);
        var legacyScope = $"EVIDENCE_POLICY_PUT:{command.ActorUserId:D}:{command.TaskCode}:{command.ReleaseId:D}";
        var legacyRequestHash = Hash(
            command.ReleaseId.ToString("D"),
            string.Join('|', canonical.Select(item => $"{item.Code}:{item.Kind}:{item.ConditionCode}")),
            command.ExpectedRowVersion?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty);
        var replay = await FindReplayAsync(scope, legacyScope, command.IdempotencyKey, requestHash, legacyRequestHash,
            command.TaskCode, command.ActorUserId, command.CorrelationId, cancellationToken);
        if (replay is not null)
        {
            await auditTransaction.ExecuteAsync(
                NewAuditEvent(
                    command.ActorUserId,
                    command.CorrelationId,
                    replay.PolicyVersionId,
                    "EVIDENCE_POLICY_DRAFT_RECOVERED",
                    outcome: "SUCCESS"),
                _ => Task.CompletedTask,
                cancellationToken);
            return replay;
        }

        EvidencePolicyVersion? created = null;
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
                        throw new EvidencePolicyReleaseNotDraftException();
                    }

                    _ = await dbContext.TaskDefinitions
                        .FromSqlInterpolated($"SELECT * FROM task_definition WHERE id = {seed.Id} FOR UPDATE")
                        .AsTracking()
                        .SingleAsync(token);

                    var pending = await dbContext.EvidencePolicyVersions.AsNoTracking()
                        .SingleOrDefaultAsync(
                            policy => policy.TaskDefinitionId == seed.Id && policy.Status == VersionStatuses.Draft,
                            token);
                    if (pending is not null)
                    {
                        if (pending.ReleaseId == command.ReleaseId)
                        {
                            throw new EvidencePolicyOverlapException();
                        }

                        throw new VersionConflictException();
                    }

                    var targetVersion = await dbContext.TaskDefinitionVersions
                        .FromSqlInterpolated(
                            $"SELECT * FROM task_definition_version WHERE task_definition_id = {seed.Id} AND release_id = {command.ReleaseId} AND status = {VersionStatuses.Draft} FOR UPDATE")
                        .AsTracking()
                        .SingleOrDefaultAsync(token)
                        ?? await dbContext.TaskDefinitionVersions
                            .FromSqlInterpolated(
                                $"SELECT * FROM task_definition_version WHERE task_definition_id = {seed.Id} AND status IN ({VersionStatuses.Current}, {TaskDefinitionStatuses.InactiveForNew}) FOR UPDATE")
                            .AsTracking()
                            .SingleOrDefaultAsync(token)
                        ?? throw new EvidencePolicyDefinitionPreconditionException();

                    var current = await dbContext.EvidencePolicyVersions
                        .FromSqlInterpolated(
                            $"SELECT * FROM evidence_policy_version WHERE task_definition_id = {seed.Id} AND status = {VersionStatuses.Current} FOR UPDATE")
                        .AsTracking()
                        .SingleOrDefaultAsync(token);
                    if (current is not null && command.ExpectedRowVersion is null)
                    {
                        throw new EvidencePolicyIfMatchRequiredException();
                    }

                    if (current is not null)
                    {
                        VersioningRules.RequireExpectedRowVersion(current.RowVersion, command.ExpectedRowVersion!.Value);
                    }
                    else if (command.ExpectedRowVersion is not null)
                    {
                        throw new VersionConflictException();
                    }

                    var nextVersion = (await dbContext.EvidencePolicyVersions
                        .Where(policy => policy.TaskDefinitionId == seed.Id)
                        .MaxAsync(policy => (int?)policy.VersionNo, token) ?? 0) + 1;
                    created = new EvidencePolicyVersion(
                        uuidGenerator.NewUuid(),
                        seed.Id,
                        targetVersion.Id,
                        command.ReleaseId,
                        current?.Id,
                        nextVersion);
                    dbContext.EvidencePolicyVersions.Add(created);
                    foreach (var definition in canonical)
                    {
                        dbContext.EvidenceRequirementVersions.Add(new EvidenceRequirementVersion(
                            uuidGenerator.NewUuid(),
                            created.Id,
                            seed.Id,
                            definition));
                    }

                    var response = ToDetails(command.TaskCode, created, canonical.Select(ToDetails).ToArray());
                    dbContext.IdempotencyRecords.Add(IdempotencyProtocol.Completed(
                        scope, command.IdempotencyKey, requestHash, "EVIDENCE_POLICY_VERSION", created.Id,
                        StatusCodes.Status201Created, response, clock.UtcNow, DateTimeOffset.MaxValue,
                        responseLocation: $"/api/v1/task-definitions/{command.TaskCode}/evidence-policy"));
                    return NewAuditEvent(
                        command.ActorUserId,
                        command.CorrelationId,
                        created.Id,
                        "EVIDENCE_POLICY_DRAFT_CREATED",
                        current is null ? null : Serialize(current, command.TaskCode, []),
                        Serialize(created, command.TaskCode, canonical));
                },
                cancellationToken);
        }
        catch (DbUpdateException exception) when ((exception.InnerException as PostgresException)?.ConstraintName == IdempotencyPrimaryKey)
        {
            dbContext.ChangeTracker.Clear();
            return await FindReplayAsync(scope, legacyScope, command.IdempotencyKey, requestHash, legacyRequestHash,
                command.TaskCode, command.ActorUserId, command.CorrelationId, cancellationToken)
                ?? throw new EvidencePolicyIdempotencyConflictException();
        }
        catch (DbUpdateConcurrencyException)
        {
            dbContext.ChangeTracker.Clear();
            throw new VersionConflictException();
        }
        catch (DbUpdateException exception) when (
            (exception.InnerException as PostgresException)?.ConstraintName is ReleaseTaskIndex or VersionNumberIndex)
        {
            dbContext.ChangeTracker.Clear();
            throw new EvidencePolicyOverlapException();
        }
        catch
        {
            dbContext.ChangeTracker.Clear();
            throw;
        }

        var requirements = canonical.Select(ToDetails).ToArray();
        var result = ToDetails(command.TaskCode, created!, requirements);
        dbContext.ChangeTracker.Clear();
        return result;
    }

    private static bool IsAuditableRejection(Exception exception) => exception is
        TaskDefinitionNotMvpException or
        EvidencePolicyReleaseNotDraftException or
        EvidencePolicyDefinitionPreconditionException or
        EvidencePolicyIfMatchRequiredException or
        EvidencePolicyOverlapException or
        EvidencePolicyValidationException or
        VersionConflictException or
        VersioningValidationException or
        VersioningStateException;

    private async Task<EvidencePolicyVersionDetails?> FindReplayAsync(
        string scope,
        string legacyScope,
        Guid key,
        string hash,
        string legacyHash,
        string taskCode,
        Guid actorUserId,
        Guid correlationId,
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

        var expectedHash = record.ProtocolVersion == IdempotencyProtocol.CurrentVersion ? hash : legacyHash;
        if (!string.Equals(record.RequestHash, expectedHash, StringComparison.Ordinal))
        {
            await AuditConflictAsync(actorUserId, correlationId, key, record.ResourceId, cancellationToken);
            throw new EvidencePolicyIdempotencyConflictException();
        }

        if (record.ProtocolVersion == IdempotencyProtocol.CurrentVersion)
        {
            return IdempotencyProtocol.ReadPayload<EvidencePolicyVersionDetails>(record);
        }

        var policy = await dbContext.EvidencePolicyVersions.AsNoTracking()
            .SingleAsync(item => item.Id == record.ResourceId, cancellationToken);
        var requirements = await dbContext.EvidenceRequirementVersions.AsNoTracking()
            .Where(item => item.PolicyVersionId == policy.Id)
            .OrderBy(item => item.Ordinal)
            .Select(item => new EvidenceRequirementVersionDetails(
                item.RequirementCode,
                item.Kind,
                item.ConditionCode == EvidenceConditionCodes.Always
                    ? null
                    : new EvidenceConditionDetails(item.ConditionCode),
                item.IsRequired,
                item.Ordinal))
            .ToListAsync(cancellationToken);
        return ToDetails(taskCode, policy, requirements);
    }

    private Task AuditConflictAsync(Guid actorUserId, Guid correlationId, Guid key, Guid resourceId, CancellationToken token) =>
        IdempotencyProtocol.PersistConflictAsync(
            auditTransaction,
            IdempotencyProtocol.ConflictAudit(
                uuidGenerator.NewUuid(), clock.UtcNow, actorUserId, "EVIDENCE_POLICY_VERSION", resourceId,
                BranchScope.LorettaId, correlationId, key, "EVIDENCE_POLICY_PUT"), token);

    private async Task EnsureAuthorizedAsync(Guid actorUserId, Guid correlationId, CancellationToken cancellationToken)
    {
        if (await ConfigurationAuthorizationQuery.IsDirectionAsync(dbContext, actorUserId, cancellationToken))
        {
            return;
        }

        await auditTransaction.ExecuteAsync(
            NewAuditEvent(actorUserId, correlationId, null, "EVIDENCE_POLICY_ACCESS_DENIED", outcome: "DENIED"),
            _ => Task.CompletedTask,
            cancellationToken);
        throw new EvidencePolicyAccessDeniedException();
    }

    private AuditEvent NewAuditEvent(
        Guid actorUserId,
        Guid correlationId,
        Guid? resourceId,
        string action,
        JsonDocument? before = null,
        JsonDocument? after = null,
        string outcome = "SUCCESS") => new()
        {
            Id = uuidGenerator.NewUuid(),
            OccurredAt = clock.UtcNow,
            ActorUserId = actorUserId,
            ActorType = "APP_USER",
            Action = action,
            ResourceType = "EVIDENCE_POLICY_VERSION",
            ResourceId = resourceId,
            BranchId = BranchScope.LorettaId,
            CorrelationId = correlationId,
            BeforeData = before,
            AfterData = after,
            Outcome = outcome,
        };

    internal static JsonDocument Serialize(
        EvidencePolicyVersion policy,
        string taskCode,
        IEnumerable<EvidenceRequirementDefinition> requirements) =>
        JsonSerializer.SerializeToDocument(new
        {
            schemaVersion = 1,
            taskCode,
            taskDefinitionVersionId = policy.TaskDefinitionVersionId,
            basedOnPolicyVersionId = policy.BasedOnId,
            policy.VersionNo,
            policy.Status,
            policy.EffectiveFrom,
            policy.EffectiveTo,
            supersedesPolicyVersionId = policy.SupersedesId,
            policy.RowVersion,
            requirements = requirements.Select(item => new
            {
                item.Code,
                item.Kind,
                item.ConditionCode,
                isRequired = true,
                item.Ordinal,
            }),
        });

    internal static EvidencePolicyVersionDetails ToDetails(
        string taskCode,
        EvidencePolicyVersion policy,
        IReadOnlyList<EvidenceRequirementVersionDetails> requirements) => new(
        policy.Id,
        taskCode,
        policy.TaskDefinitionVersionId,
        policy.ReleaseId,
        policy.VersionNo,
        policy.Status,
        policy.EffectiveFrom,
        policy.EffectiveTo,
        policy.Reason,
        policy.BasedOnId,
        policy.SupersedesId,
        requirements,
        policy.RowVersion);

    private static EvidenceRequirementVersionDetails ToDetails(EvidenceRequirementDefinition item) =>
        new(
            item.Code,
            item.Kind,
            item.ConditionCode == EvidenceConditionCodes.Always
                ? null
                : new EvidenceConditionDetails(item.ConditionCode),
            true,
            item.Ordinal);

    private static string Hash(params string[] values) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\n', values))));
}
