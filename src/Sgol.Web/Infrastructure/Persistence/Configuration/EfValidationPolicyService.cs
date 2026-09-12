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

public sealed class EfValidationPolicyService(
    SgolDbContext dbContext,
    AuditTransaction auditTransaction,
    IClock clock,
    IUuidGenerator uuidGenerator) : IValidationPolicyService
{
    private const string IdempotencyPrimaryKey = "PK_idempotency_record";
    private const string ReleaseTaskIndex = "IX_validation_policy_version_release_task";
    private const string VersionNumberIndex = "IX_validation_policy_version_number";

    public async Task<ValidationPolicyVersionDetails> PutAsync(
        PutValidationPolicyCommand command,
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
            NewAuditEvent(actorUserId, correlationId, null, "VALIDATION_POLICY_REJECTED", outcome: "REJECTED"),
            _ => Task.CompletedTask,
            cancellationToken);
        dbContext.ChangeTracker.Clear();
    }

    private async Task<ValidationPolicyVersionDetails> PutAuthorizedAsync(
        PutValidationPolicyCommand command,
        CancellationToken cancellationToken)
    {
        var seed = TaskDefinitionCatalog.Require(command.TaskCode);
        ValidationPolicyCatalog.Validate(
            command.TaskCode,
            command.IsRequired,
            command.ExecutorRole,
            command.ValidatorRelation,
            command.ValidatorRole,
            command.AllowedResults);
        const string operation = "VALIDATION_POLICY_PUT";
        var resource = $"{command.TaskCode}:{command.ReleaseId:D}";
        var scope = IdempotencyProtocol.Scope(command.ActorUserId.ToString("D"), operation, resource);
        var requestHash = IdempotencyProtocol.HashCanonical(operation, command.ActorUserId.ToString("D"), resource, new
        {
            command.ReleaseId,
            command.IsRequired,
            command.ExecutorRole,
            command.ValidatorRelation,
            command.ValidatorRole,
            allowedResults = ValidationPolicyValues.AllowedResults,
        }, command.ExpectedRowVersion);
        var legacyScope = $"VALIDATION_POLICY_PUT:{command.ActorUserId:D}";
        var legacyRequestHash = Hash(
            command.ActorUserId.ToString("D"),
            "VALIDATION_POLICY_PUT",
            command.TaskCode,
            command.ReleaseId.ToString("D"),
            command.IsRequired.ToString(System.Globalization.CultureInfo.InvariantCulture),
            command.ExecutorRole,
            command.ValidatorRelation,
            command.ValidatorRole,
            string.Join('|', ValidationPolicyValues.AllowedResults),
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
                    "VALIDATION_POLICY_DRAFT_RECOVERED"),
                _ => Task.CompletedTask,
                cancellationToken);
            return replay;
        }

        ValidationPolicyVersion? created = null;
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
                        throw new ValidationPolicyReleaseNotDraftException();
                    }

                    _ = await dbContext.TaskDefinitions
                        .FromSqlInterpolated($"SELECT * FROM task_definition WHERE id = {seed.Id} FOR UPDATE")
                        .AsTracking()
                        .SingleAsync(token);

                    var pending = await dbContext.ValidationPolicyVersions.AsNoTracking()
                        .SingleOrDefaultAsync(
                            policy => policy.TaskDefinitionId == seed.Id && policy.Status == VersionStatuses.Draft,
                            token);
                    if (pending is not null)
                    {
                        if (pending.ReleaseId == command.ReleaseId)
                        {
                            throw new ValidationPolicyOverlapException();
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
                        ?? throw new ValidationPolicyDefinitionPreconditionException();

                    var current = await dbContext.ValidationPolicyVersions
                        .FromSqlInterpolated(
                            $"SELECT * FROM validation_policy_version WHERE task_definition_id = {seed.Id} AND status = {VersionStatuses.Current} FOR UPDATE")
                        .AsTracking()
                        .SingleOrDefaultAsync(token);
                    if (current is not null && command.ExpectedRowVersion is null)
                    {
                        throw new ValidationPolicyIfMatchRequiredException();
                    }

                    if (current is not null)
                    {
                        VersioningRules.RequireExpectedRowVersion(current.RowVersion, command.ExpectedRowVersion!.Value);
                    }
                    else if (command.ExpectedRowVersion is not null)
                    {
                        throw new VersionConflictException();
                    }

                    var nextVersion = (await dbContext.ValidationPolicyVersions
                        .Where(policy => policy.TaskDefinitionId == seed.Id)
                        .MaxAsync(policy => (int?)policy.VersionNo, token) ?? 0) + 1;
                    created = new ValidationPolicyVersion(
                        uuidGenerator.NewUuid(),
                        seed.Id,
                        targetVersion.Id,
                        command.ReleaseId,
                        current?.Id,
                        nextVersion,
                        command.IsRequired,
                        command.ExecutorRole,
                        command.ValidatorRelation,
                        command.ValidatorRole,
                        command.AllowedResults);
                    dbContext.ValidationPolicyVersions.Add(created);
                    dbContext.IdempotencyRecords.Add(IdempotencyProtocol.Completed(
                        scope, command.IdempotencyKey, requestHash, "VALIDATION_POLICY_VERSION", created.Id,
                        StatusCodes.Status201Created, ToDetails(command.TaskCode, created), clock.UtcNow,
                        DateTimeOffset.MaxValue,
                        responseLocation: $"/api/v1/task-definitions/{command.TaskCode}/validation-policy"));
                    return NewAuditEvent(
                        command.ActorUserId,
                        command.CorrelationId,
                        created.Id,
                        "VALIDATION_POLICY_DRAFT_CREATED",
                        current is null ? null : Serialize(current, command.TaskCode),
                        Serialize(created, command.TaskCode));
                },
                cancellationToken);
        }
        catch (DbUpdateException exception) when ((exception.InnerException as PostgresException)?.ConstraintName == IdempotencyPrimaryKey)
        {
            dbContext.ChangeTracker.Clear();
            return await FindReplayAsync(scope, legacyScope, command.IdempotencyKey, requestHash, legacyRequestHash,
                command.TaskCode, command.ActorUserId, command.CorrelationId, cancellationToken)
                ?? throw new ValidationPolicyIdempotencyConflictException();
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
            throw new ValidationPolicyOverlapException();
        }
        catch
        {
            dbContext.ChangeTracker.Clear();
            throw;
        }

        var result = ToDetails(command.TaskCode, created!);
        dbContext.ChangeTracker.Clear();
        return result;
    }

    private static bool IsAuditableRejection(Exception exception) => exception is
        TaskDefinitionNotMvpException or
        ValidationPolicyReleaseNotDraftException or
        ValidationPolicyDefinitionPreconditionException or
        ValidationPolicyIfMatchRequiredException or
        ValidationPolicyOverlapException or
        ValidationPolicyValidationException or
        VersionConflictException or
        VersioningValidationException or
        VersioningStateException;

    private async Task<ValidationPolicyVersionDetails?> FindReplayAsync(
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
            throw new ValidationPolicyIdempotencyConflictException();
        }

        if (record.ProtocolVersion == IdempotencyProtocol.CurrentVersion)
        {
            return IdempotencyProtocol.ReadPayload<ValidationPolicyVersionDetails>(record);
        }

        var policy = await dbContext.ValidationPolicyVersions.AsNoTracking()
            .SingleAsync(item => item.Id == record.ResourceId, cancellationToken);
        return ToDetails(taskCode, policy);
    }

    private Task AuditConflictAsync(Guid actorUserId, Guid correlationId, Guid key, Guid resourceId, CancellationToken token) =>
        IdempotencyProtocol.PersistConflictAsync(
            auditTransaction,
            IdempotencyProtocol.ConflictAudit(
                uuidGenerator.NewUuid(), clock.UtcNow, actorUserId, "VALIDATION_POLICY_VERSION", resourceId,
                BranchScope.LorettaId, correlationId, key, "VALIDATION_POLICY_PUT"), token);

    private async Task EnsureAuthorizedAsync(Guid actorUserId, Guid correlationId, CancellationToken cancellationToken)
    {
        if (await ConfigurationAuthorizationQuery.IsDirectionAsync(dbContext, actorUserId, cancellationToken))
        {
            return;
        }

        await auditTransaction.ExecuteAsync(
            NewAuditEvent(actorUserId, correlationId, null, "VALIDATION_POLICY_ACCESS_DENIED", outcome: "DENIED"),
            _ => Task.CompletedTask,
            cancellationToken);
        throw new ValidationPolicyAccessDeniedException();
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
            ResourceType = "VALIDATION_POLICY_VERSION",
            ResourceId = resourceId,
            BranchId = BranchScope.LorettaId,
            CorrelationId = correlationId,
            BeforeData = before,
            AfterData = after,
            Outcome = outcome,
        };

    internal static JsonDocument Serialize(ValidationPolicyVersion policy, string taskCode) =>
        JsonSerializer.SerializeToDocument(new
        {
            schemaVersion = 1,
            taskCode,
            taskDefinitionVersionId = policy.TaskDefinitionVersionId,
            basedOnPolicyVersionId = policy.BasedOnId,
            policy.VersionNo,
            policy.IsRequired,
            policy.ExecutorRole,
            policy.ValidatorRelation,
            policy.ValidatorRole,
            allowedResults = ReadAllowedResults(policy),
            policy.Status,
            policy.EffectiveFrom,
            policy.EffectiveTo,
            supersedesPolicyVersionId = policy.SupersedesId,
            policy.RowVersion,
        });

    internal static ValidationPolicyVersionDetails ToDetails(string taskCode, ValidationPolicyVersion policy) => new(
        policy.Id,
        taskCode,
        policy.TaskDefinitionVersionId,
        policy.ReleaseId,
        policy.VersionNo,
        policy.IsRequired,
        policy.ExecutorRole,
        policy.ValidatorRelation,
        policy.ValidatorRole,
        ReadAllowedResults(policy),
        policy.Status,
        policy.EffectiveFrom,
        policy.EffectiveTo,
        policy.Reason,
        policy.BasedOnId,
        policy.SupersedesId,
        policy.RowVersion);

    internal static IReadOnlyList<string> ReadAllowedResults(ValidationPolicyVersion policy) =>
        policy.AllowedResults.RootElement.EnumerateArray().Select(item => item.GetString()!).ToArray();

    private static string Hash(params string[] values) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\n', values))));
}
