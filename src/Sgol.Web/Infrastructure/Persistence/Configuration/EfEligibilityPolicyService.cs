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

public sealed class EfEligibilityPolicyService(
    SgolDbContext dbContext,
    AuditTransaction auditTransaction,
    IClock clock,
    IUuidGenerator uuidGenerator) : IEligibilityPolicyService
{
    private const string IdempotencyPrimaryKey = "PK_idempotency_record";
    private const string ReleaseTaskIndex = "IX_eligibility_policy_version_release_task";
    private const string VersionNumberIndex = "IX_eligibility_policy_version_number";

    public async Task<IReadOnlyList<EligibilityPolicyVersionDetails>> ListAsync(
        Guid actorUserId,
        Guid correlationId,
        CancellationToken cancellationToken = default)
    {
        if (!await ConfigurationAuthorizationQuery.IsActiveLorettaUserAsync(dbContext, actorUserId, cancellationToken))
        {
            await AuditDeniedAsync(actorUserId, correlationId, cancellationToken);
            throw new EligibilityPolicyAccessDeniedException();
        }

        var policies = await dbContext.EligibilityPolicyVersions.AsNoTracking()
            .Where(policy => policy.Status == VersionStatuses.Current)
            .OrderBy(policy => policy.TaskDefinitionId)
            .ToListAsync(cancellationToken);
        var codes = TaskDefinitionCatalog.All.ToDictionary(item => item.Id, item => item.TaskCode);
        var result = policies.Select(policy => ToDetails(codes[policy.TaskDefinitionId], policy)).ToArray();
        dbContext.ChangeTracker.Clear();
        return result;
    }

    public async Task<EligibilityPolicyVersionDetails> PutAsync(
        PutEligibilityPolicyCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        await EnsureAuthorizedAsync(command.ActorUserId, command.CorrelationId, cancellationToken);
        var seed = TaskDefinitionCatalog.Require(command.TaskCode);
        EligibilityPolicyCatalog.Validate(command.TaskCode, command.RequiredRole, command.RequiresAvailability, command.RequiredShift);
        const string operation = "ELIGIBILITY_POLICY_PUT";
        var resource = $"{command.TaskCode}:{command.ReleaseId:D}";
        var scope = IdempotencyProtocol.Scope(command.ActorUserId.ToString("D"), operation, resource);
        var requestHash = IdempotencyProtocol.HashCanonical(operation, command.ActorUserId.ToString("D"), resource, new
        {
            command.ReleaseId,
            command.RequiredRole,
            command.RequiresAvailability,
            command.RequiredShift,
        }, command.ExpectedRowVersion);
        var legacyScope = $"ELIGIBILITY_POLICY_PUT:{command.ActorUserId:D}:{command.TaskCode}:{command.ReleaseId:D}";
        var legacyRequestHash = Hash(
            command.ReleaseId.ToString("D"),
            command.RequiredRole,
            command.RequiresAvailability.ToString(),
            command.RequiredShift ?? string.Empty,
            command.ExpectedRowVersion?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty);
        var replay = await FindReplayAsync(scope, legacyScope, command.IdempotencyKey, requestHash, legacyRequestHash,
            command.TaskCode, command.ActorUserId, command.CorrelationId, cancellationToken);
        if (replay is not null)
        {
            return replay;
        }

        EligibilityPolicyVersion? created = null;
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
                        throw new EligibilityPolicyReleaseNotDraftException();
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
                        ?? throw new EligibilityPolicyDefinitionPreconditionException();

                    var current = await dbContext.EligibilityPolicyVersions
                        .FromSqlInterpolated(
                            $"SELECT * FROM eligibility_policy_version WHERE task_definition_id = {seed.Id} AND status = {VersionStatuses.Current} FOR UPDATE")
                        .AsTracking()
                        .SingleOrDefaultAsync(token);
                    if (current is not null && command.ExpectedRowVersion is null)
                    {
                        throw new EligibilityPolicyIfMatchRequiredException();
                    }

                    if (current is not null)
                    {
                        VersioningRules.RequireExpectedRowVersion(current.RowVersion, command.ExpectedRowVersion!.Value);
                    }
                    else if (command.ExpectedRowVersion is not null)
                    {
                        throw new VersionConflictException();
                    }

                    var nextVersion = (await dbContext.EligibilityPolicyVersions
                        .Where(policy => policy.TaskDefinitionId == seed.Id)
                        .MaxAsync(policy => (int?)policy.VersionNo, token) ?? 0) + 1;
                    created = new EligibilityPolicyVersion(
                        uuidGenerator.NewUuid(),
                        seed.Id,
                        targetVersion.Id,
                        command.ReleaseId,
                        current?.Id,
                        nextVersion,
                        command.RequiredRole,
                        command.RequiresAvailability,
                        command.RequiredShift);
                    dbContext.EligibilityPolicyVersions.Add(created);
                    dbContext.IdempotencyRecords.Add(IdempotencyProtocol.Completed(
                        scope, command.IdempotencyKey, requestHash, "ELIGIBILITY_POLICY_VERSION", created.Id,
                        StatusCodes.Status201Created, ToDetails(command.TaskCode, created), clock.UtcNow,
                        DateTimeOffset.MaxValue,
                        responseLocation: $"/api/v1/task-definitions/{command.TaskCode}/eligibility-policy"));
                    return NewAuditEvent(
                        command.ActorUserId,
                        command.CorrelationId,
                        created.Id,
                        "ELIGIBILITY_POLICY_DRAFT_CREATED",
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
                ?? throw new EligibilityPolicyIdempotencyConflictException();
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

    private async Task<EligibilityPolicyVersionDetails?> FindReplayAsync(
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
            throw new EligibilityPolicyIdempotencyConflictException();
        }

        if (record.ProtocolVersion == IdempotencyProtocol.CurrentVersion)
        {
            return IdempotencyProtocol.ReadPayload<EligibilityPolicyVersionDetails>(record);
        }

        var policy = await dbContext.EligibilityPolicyVersions.AsNoTracking()
            .SingleAsync(item => item.Id == record.ResourceId, cancellationToken);
        return ToDetails(taskCode, policy);
    }

    private Task AuditConflictAsync(Guid actorUserId, Guid correlationId, Guid key, Guid resourceId, CancellationToken token) =>
        IdempotencyProtocol.PersistConflictAsync(
            auditTransaction,
            IdempotencyProtocol.ConflictAudit(
                uuidGenerator.NewUuid(), clock.UtcNow, actorUserId, "ELIGIBILITY_POLICY_VERSION", resourceId,
                BranchScope.LorettaId, correlationId, key, "ELIGIBILITY_POLICY_PUT"), token);

    private async Task EnsureAuthorizedAsync(Guid actorUserId, Guid correlationId, CancellationToken cancellationToken)
    {
        if (await ConfigurationAuthorizationQuery.IsDirectionAsync(dbContext, actorUserId, cancellationToken))
        {
            return;
        }

        await AuditDeniedAsync(actorUserId, correlationId, cancellationToken);
        throw new EligibilityPolicyAccessDeniedException();
    }

    private Task AuditDeniedAsync(Guid actorUserId, Guid correlationId, CancellationToken cancellationToken) =>
        auditTransaction.ExecuteAsync(
            NewAuditEvent(actorUserId, correlationId, null, "ELIGIBILITY_POLICY_ACCESS_DENIED", outcome: "DENIED"),
            _ => Task.CompletedTask,
            cancellationToken);

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
            ResourceType = "ELIGIBILITY_POLICY_VERSION",
            ResourceId = resourceId,
            BranchId = BranchScope.LorettaId,
            CorrelationId = correlationId,
            BeforeData = before,
            AfterData = after,
            Outcome = outcome,
        };

    internal static JsonDocument Serialize(EligibilityPolicyVersion policy, string taskCode) =>
        JsonSerializer.SerializeToDocument(new
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
        });

    internal static EligibilityPolicyVersionDetails ToDetails(string taskCode, EligibilityPolicyVersion policy) => new(
        policy.Id,
        taskCode,
        policy.TaskDefinitionVersionId,
        policy.ReleaseId,
        policy.VersionNo,
        policy.RequiredRole,
        policy.RequiresAvailability,
        policy.RequiredShift,
        policy.Status,
        policy.EffectiveFrom,
        policy.EffectiveTo,
        policy.Reason,
        policy.SupersedesId,
        policy.RowVersion);

    private static string Hash(params string[] values) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\n', values))));
}
