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

public sealed class EfActivationPolicyService(
    SgolDbContext dbContext,
    AuditTransaction auditTransaction,
    IClock clock,
    IUuidGenerator uuidGenerator) : IActivationPolicyService
{
    private const string IdempotencyPrimaryKey = "PK_idempotency_record";
    private const string ReleaseTaskIndex = "IX_activation_rule_version_release_task";
    private const string VersionNumberIndex = "IX_activation_rule_version_number";

    public async Task<ActivationRuleVersionDetails> PutAsync(
        PutActivationPolicyCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        await EnsureAuthorizedAsync(command.ActorUserId, command.CorrelationId, cancellationToken);
        var seed = TaskDefinitionCatalog.Require(command.TaskCode);
        using var validatedSchedule = ActivationPolicyCatalog.Validate(
            command.TaskCode,
            command.Mode,
            command.Schedule,
            command.OriginKeySchema);
        var normalizedSchedule = validatedSchedule.RootElement.GetRawText();
        const string operation = "ACTIVATION_POLICY_PUT";
        var resource = $"{command.TaskCode}:{command.ReleaseId:D}";
        var scope = IdempotencyProtocol.Scope(command.ActorUserId.ToString("D"), operation, resource);
        var requestHash = IdempotencyProtocol.HashCanonical(operation, command.ActorUserId.ToString("D"), resource, new
        {
            command.TaskDefinitionVersionId,
            command.ReleaseId,
            command.Mode,
            schedule = validatedSchedule.RootElement,
            command.OriginKeySchema,
        }, command.ExpectedRowVersion);
        var legacyScope = $"ACTIVATION_POLICY_PUT:{command.ActorUserId:D}:{command.TaskCode}:{command.ReleaseId:D}";
        var legacyRequestHash = Hash(
            command.TaskDefinitionVersionId.ToString("D"),
            command.ReleaseId.ToString("D"),
            command.Mode,
            normalizedSchedule,
            command.OriginKeySchema,
            command.ExpectedRowVersion?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty);
        var replay = await FindReplayAsync(scope, legacyScope, command.IdempotencyKey, requestHash, legacyRequestHash,
            command.TaskCode, command.ActorUserId, command.CorrelationId, cancellationToken);
        if (replay is not null)
        {
            return replay;
        }

        ActivationRuleVersion? created = null;
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
                        throw new ActivationPolicyReleaseNotDraftException();
                    }

                    var targetVersion = await dbContext.TaskDefinitionVersions
                        .FromSqlInterpolated(
                            $"SELECT * FROM task_definition_version WHERE id = {command.TaskDefinitionVersionId} AND task_definition_id = {seed.Id} AND ((release_id = {command.ReleaseId} AND status = {VersionStatuses.Draft}) OR status = {VersionStatuses.Current}) FOR UPDATE")
                        .AsTracking()
                        .SingleOrDefaultAsync(token)
                        ?? throw new ActivationPolicyDefinitionPreconditionException();

                    var current = await dbContext.ActivationRuleVersions
                        .FromSqlInterpolated(
                            $"SELECT * FROM activation_rule_version WHERE task_definition_id = {seed.Id} AND status = {VersionStatuses.Current} FOR UPDATE")
                        .AsTracking()
                        .SingleOrDefaultAsync(token);
                    if (current is not null && command.ExpectedRowVersion is null)
                    {
                        throw new ActivationPolicyIfMatchRequiredException();
                    }

                    if (current is not null)
                    {
                        VersioningRules.RequireExpectedRowVersion(current.RowVersion, command.ExpectedRowVersion!.Value);
                    }
                    else if (command.ExpectedRowVersion is not null)
                    {
                        throw new VersionConflictException();
                    }

                    var nextVersion = (await dbContext.ActivationRuleVersions
                        .Where(rule => rule.TaskDefinitionId == seed.Id)
                        .MaxAsync(rule => (int?)rule.VersionNo, token) ?? 0) + 1;
                    created = new ActivationRuleVersion(
                        uuidGenerator.NewUuid(),
                        seed.Id,
                        targetVersion.Id,
                        command.ReleaseId,
                        current?.Id,
                        nextVersion,
                        command.Mode,
                        command.Schedule,
                        command.OriginKeySchema);
                    dbContext.ActivationRuleVersions.Add(created);
                    dbContext.IdempotencyRecords.Add(IdempotencyProtocol.Completed(
                        scope, command.IdempotencyKey, requestHash, "ACTIVATION_RULE_VERSION", created.Id,
                        StatusCodes.Status201Created, ToDetails(command.TaskCode, created), clock.UtcNow,
                        DateTimeOffset.MaxValue,
                        responseLocation: $"/api/v1/task-definitions/{command.TaskCode}/activation-policy"));
                    return NewAuditEvent(
                        command.ActorUserId,
                        command.CorrelationId,
                        created.Id,
                        "ACTIVATION_POLICY_DRAFT_CREATED",
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
                ?? throw new ActivationPolicyIdempotencyConflictException();
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
            throw new VersionConflictException();
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

    private async Task<ActivationRuleVersionDetails?> FindReplayAsync(
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
            throw new ActivationPolicyIdempotencyConflictException();
        }

        if (record.ProtocolVersion == IdempotencyProtocol.CurrentVersion)
        {
            return IdempotencyProtocol.ReadPayload<ActivationRuleVersionDetails>(record);
        }

        var rule = await dbContext.ActivationRuleVersions.AsNoTracking()
            .SingleAsync(item => item.Id == record.ResourceId, cancellationToken);
        return ToDetails(taskCode, rule);
    }

    private Task AuditConflictAsync(Guid actorUserId, Guid correlationId, Guid key, Guid resourceId, CancellationToken token) =>
        IdempotencyProtocol.PersistConflictAsync(
            auditTransaction,
            IdempotencyProtocol.ConflictAudit(
                uuidGenerator.NewUuid(), clock.UtcNow, actorUserId, "ACTIVATION_RULE_VERSION", resourceId,
                BranchScope.LorettaId, correlationId, key, "ACTIVATION_POLICY_PUT"), token);

    private async Task EnsureAuthorizedAsync(Guid actorUserId, Guid correlationId, CancellationToken cancellationToken)
    {
        if (await ConfigurationAuthorizationQuery.IsDirectionAsync(dbContext, actorUserId, cancellationToken))
        {
            return;
        }

        await auditTransaction.ExecuteAsync(
            NewAuditEvent(actorUserId, correlationId, null, "ACTIVATION_POLICY_ACCESS_DENIED", outcome: "DENIED"),
            _ => Task.CompletedTask,
            cancellationToken);
        dbContext.ChangeTracker.Clear();
        throw new ActivationPolicyAccessDeniedException();
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
            ResourceType = "ACTIVATION_RULE_VERSION",
            ResourceId = resourceId,
            BranchId = BranchScope.LorettaId,
            CorrelationId = correlationId,
            BeforeData = before,
            AfterData = after,
            Outcome = outcome,
        };

    internal static JsonDocument Serialize(ActivationRuleVersion rule, string taskCode) =>
        JsonSerializer.SerializeToDocument(new
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
        });

    internal static ActivationRuleVersionDetails ToDetails(string taskCode, ActivationRuleVersion rule) => new(
        rule.Id,
        taskCode,
        rule.TaskDefinitionVersionId,
        rule.ReleaseId,
        rule.VersionNo,
        rule.Mode,
        rule.Schedule.RootElement.Clone(),
        rule.OriginKeySchema,
        rule.Status,
        rule.EffectiveFrom,
        rule.EffectiveTo,
        rule.Reason,
        rule.SupersedesId,
        rule.RowVersion);

    private static string Hash(params string[] values) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\n', values))));
}
