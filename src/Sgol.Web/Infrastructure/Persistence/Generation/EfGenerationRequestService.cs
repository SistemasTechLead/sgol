using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Sgol.BuildingBlocks.Identifiers;
using Sgol.BuildingBlocks.Time;
using Sgol.BuildingBlocks.Versioning;
using Sgol.Configuration.Contracts;
using Sgol.Generation.Contracts;
using Sgol.Identity.Contracts;
using Sgol.Organization.Contracts;
using Sgol.Web.Infrastructure.Persistence.Auditing;
using Sgol.Web.Infrastructure.Persistence.Bootstrap;
using Sgol.Web.Infrastructure.Persistence.Idempotency;

namespace Sgol.Web.Infrastructure.Persistence.Generation;

public sealed partial class EfGenerationRequestService(
    SgolDbContext dbContext,
    AuditTransaction auditTransaction,
    IRoleHierarchyResolver hierarchyResolver,
    IClock clock,
    IUuidGenerator uuidGenerator, Microsoft.AspNetCore.DataProtection.IDataProtectionProvider? protectionProvider = null) : IGenerationRequestService, IManualGenerationReader
{
    private const string IdempotencyScopePrefix = "generation-request:create:";


    public async Task<GenerationRequestDetails> CreateAsync(
        CreateGenerationRequestCommand command,
        CancellationToken cancellationToken = default)
    {
        if (command.InputPayload is not null)
            return await CreateManualAsync(command, cancellationToken);
        var originType = command.OriginType?.Trim() ?? string.Empty;
        var originReference = command.OriginReference?.Trim() ?? string.Empty;
        var legacyRequestHash = Hash(
            command.RuleVersionId.ToString("N"),
            command.BranchId.ToString("N"),
            command.PeriodId.ToString("N"),
            originType,
            originReference);
        var legacyScope = IdempotencyScopePrefix + command.ActorUserId.ToString("N");
        var actor = command.ActorUserId.ToString("D");
        const string operation = "GENERATION_REQUEST_CREATE";
        const string resource = "new:LOR-001";
        var scope = IdempotencyProtocol.Scope(actor, operation, resource);
        var requestHash = IdempotencyProtocol.HashCanonical(
            operation,
            actor,
            resource,
            new
            {
                command.RuleVersionId,
                command.BranchId,
                command.PeriodId,
                originType,
                originReference
            });

        var actorRole = await GenerationAuthorizationQuery.GetCreatorRoleAsync(
            dbContext,
            command.ActorUserId,
            clock.UtcNow,
            cancellationToken);
        if (actorRole is null)
        {
            await AuditDeniedAsync(command, cancellationToken);
            throw new GenerationRequestAccessDeniedException();
        }

        if (string.IsNullOrWhiteSpace(originType) || string.IsNullOrWhiteSpace(originReference))
        {
            throw new GenerationRequestOriginInvalidException();
        }

        var replay = await FindReplayAsync(
            scope,
            command.ActorUserId,
            command.CorrelationId,
            command.IdempotencyKey,
            requestHash,
            legacyScope,
            legacyRequestHash,
            cancellationToken);
        if (replay is not null)
        {
            return replay;
        }

        throw new ManualGenerationException("GENERATION_REQUEST_SCHEMA_REQUERIDO");
    }
    private async Task ValidateFreshRequestAsync(
        CreateGenerationRequestCommand command,
        string actorRole,
        string originType,
        string originReference,
        CancellationToken cancellationToken)
    {
        var currentActorRole = await GenerationAuthorizationQuery.GetCreatorRoleAsync(
            dbContext,
            command.ActorUserId,
            clock.UtcNow,
            cancellationToken);
        if (!string.Equals(currentActorRole, actorRole, StringComparison.Ordinal))
        {
            throw new GenerationRequestAccessDeniedException();
        }

        var rule = await dbContext.ActivationRuleVersions
            .FromSqlInterpolated($"SELECT * FROM activation_rule_version WHERE id = {command.RuleVersionId} FOR SHARE")
            .AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new GenerationRequestRuleNotFoundException();
        if (rule.Status != VersionStatuses.Current || rule.EffectiveFrom is null || rule.EffectiveFrom > clock.UtcNow || rule.EffectiveTo <= clock.UtcNow)
        {
            throw new GenerationRequestRuleNotFoundException();
        }

        if (rule.Mode != ActivationModes.Manual)
        {
            throw new GenerationRequestRuleNotManualException();
        }

        var taskVersion = await dbContext.TaskDefinitionVersions
            .FromSqlInterpolated($"SELECT * FROM task_definition_version WHERE id = {rule.TaskDefinitionVersionId} FOR SHARE")
            .AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken);
        if (taskVersion is null ||
            taskVersion.TaskDefinitionId != rule.TaskDefinitionId ||
            taskVersion.Status != VersionStatuses.Current || taskVersion.EffectiveFrom is null || taskVersion.EffectiveFrom > clock.UtcNow || taskVersion.EffectiveTo <= clock.UtcNow)
        {
            throw new GenerationRequestTaskInactiveException();
        }

        var eligibility = await dbContext.EligibilityPolicyVersions
            .FromSqlInterpolated($"SELECT * FROM eligibility_policy_version WHERE task_definition_version_id = {rule.TaskDefinitionVersionId} AND task_definition_id = {rule.TaskDefinitionId} AND status = 'VIGENTE' FOR SHARE")
            .AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken);
        if (eligibility is null || eligibility.EffectiveFrom is null || eligibility.EffectiveFrom > clock.UtcNow || eligibility.EffectiveTo <= clock.UtcNow || !RoleHierarchy.CanAccessLevel(actorRole, eligibility.RequiredRole))
        {
            throw new GenerationRequestAccessDeniedException();
        }

        var periodExists = await dbContext.WeekPeriods.AsNoTracking()
            .AnyAsync(item => item.Id == command.PeriodId && item.BranchId == command.BranchId, cancellationToken);
        if (!periodExists)
        {
            throw new GenerationRequestPeriodNotFoundException();
        }

        if (!string.Equals(originType, rule.OriginKeySchema, StringComparison.Ordinal) ||
            rule.OriginKeySchema != ActivationOriginSchemas.ManualReference ||
            string.IsNullOrWhiteSpace(originReference))
        {
            throw new GenerationRequestOriginInvalidException();
        }
    }

    public async Task<GenerationRequestDetails> GetAsync(
        Guid actorUserId,
        Guid correlationId,
        Guid generationRequestId,
        CancellationToken cancellationToken = default)
    {
        var request = await dbContext.GenerationRequests.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == generationRequestId, cancellationToken);
        if (request is null)
        {
            throw new GenerationRequestNotFoundException();
        }

        if (request.RequestedBy is not Guid requestedBy ||
            !await hierarchyResolver.CanAccessUserAsync(actorUserId, requestedBy, cancellationToken))
        {
            await auditTransaction.ExecuteAsync(
                NewAuditEvent(
                    actorUserId,
                    correlationId,
                    request.Id,
                    "GENERATION_REQUEST_READ_ACCESS_DENIED",
                    after: null,
                    outcome: "DENIED"),
                _ => Task.CompletedTask,
                cancellationToken);
            dbContext.ChangeTracker.Clear();
            throw new GenerationRequestNotFoundException();
        }

        if (await GenerationAuthorizationQuery.GetCreatorRoleAsync(dbContext, actorUserId, clock.UtcNow, cancellationToken) is null)
            throw new GenerationRequestNotFoundException();
        await RequireVisibleAsync(actorUserId, generationRequestId, cancellationToken);
        var obligation = await dbContext.WorkObligations.AsNoTracking()
            .SingleOrDefaultAsync(item => item.GenerationRequestId == request.Id, cancellationToken);
        return obligation?.ManualTaskCode is not null
            ? ManualDetails(request, obligation, request.Result, 200, includeInput: true)
            : ToDetails(request, request.Result);
    }

    private async Task<GenerationRequestDetails?> FindReplayAsync(
        string scope,
        Guid actorUserId,
        Guid correlationId,
        Guid key,
        string requestHash,
        string legacyScope,
        string legacyRequestHash,
        CancellationToken cancellationToken)
    {
        var record = await dbContext.IdempotencyRecords.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Scope == scope && item.Key == key, cancellationToken);
        if (record is null)
        {
            record = await dbContext.IdempotencyRecords.AsNoTracking()
                .SingleOrDefaultAsync(item => item.Scope == legacyScope && item.Key == key, cancellationToken);
        }
        if (record is null)
        {
            return null;
        }

        await GetAsync(actorUserId, correlationId, record.ResourceId, cancellationToken);
        var expectedHash = record.ProtocolVersion == IdempotencyProtocol.CurrentVersion
            ? requestHash
            : legacyRequestHash;
        if (!string.Equals(record.RequestHash, expectedHash, StringComparison.Ordinal))
        {
            await AuditConflictAsync(actorUserId, correlationId, record.ResourceId, key, cancellationToken);
            throw new GenerationRequestIdempotencyConflictException();
        }

        if (record.ProtocolVersion == IdempotencyProtocol.CurrentVersion)
        {
            return IdempotencyProtocol.ReadPayload<GenerationRequestDetails>(record) with
            {
                Result = GenerationRequestResults.Recovered,
                ResponseCode = record.ResponseCode
            };
        }

        var request = await dbContext.GenerationRequests.AsNoTracking()
            .SingleAsync(item => item.Id == record.ResourceId, cancellationToken);
        return ToDetails(request, GenerationRequestResults.Recovered, StatusCodes.Status200OK);
    }

    private Task<GenerationRequest?> FindFunctionalRequestAsync(
        Guid ruleVersionId,
        Guid branchId,
        Guid periodId,
        string originType,
        string originReference,
        CancellationToken cancellationToken) =>
        dbContext.GenerationRequests.AsNoTracking().SingleOrDefaultAsync(
            item => item.RuleVersionId == ruleVersionId &&
                item.BranchId == branchId &&
                item.PeriodId == periodId &&
                item.OriginType == originType &&
                item.OriginReference == originReference,
            cancellationToken);

    private async Task AuditDeniedAsync(
        CreateGenerationRequestCommand command,
        CancellationToken cancellationToken)
    {
        await auditTransaction.ExecuteAsync(
            NewAuditEvent(
                command.ActorUserId,
                command.CorrelationId,
                resourceId: null,
                "GENERATION_REQUEST_ACCESS_DENIED",
                after: null,
                outcome: "DENIED"),
            _ => Task.CompletedTask,
            cancellationToken);
        dbContext.ChangeTracker.Clear();
    }

    private async Task AuditConflictAsync(
        Guid actorUserId,
        Guid correlationId,
        Guid resourceId,
        Guid idempotencyKey,
        CancellationToken cancellationToken)
    {
        try
        {
            await auditTransaction.ExecuteAsync(
                new AuditEvent
                {
                    Id = uuidGenerator.NewUuid(),
                    OccurredAt = clock.UtcNow,
                    ActorUserId = actorUserId,
                    ActorType = "USER",
                    Action = "IDEMPOTENCY_CONFLICT_REJECTED",
                    ResourceType = "GENERATION_REQUEST",
                    ResourceId = resourceId,
                    BranchId = BranchScope.LorettaId,
                    CorrelationId = correlationId,
                    RequestId = idempotencyKey.ToString("D"),
                    AfterData = JsonSerializer.SerializeToDocument(new
                    {
                        operation = "GENERATION_REQUEST_CREATE",
                        protocolVersion = IdempotencyProtocol.CurrentVersion,
                        reasonCode = "IDEMPOTENCY_KEY_REUSED_WITH_DIFFERENT_CONTENT"
                    }),
                    Outcome = "REJECTED"
                },
                _ => Task.CompletedTask,
                cancellationToken);
            dbContext.ChangeTracker.Clear();
        }
        catch (Exception exception)
        {
            dbContext.ChangeTracker.Clear();
            throw new GenerationRequestConflictAuditException(exception);
        }
    }

    private static IdempotencyRecord NewIdempotencyRecord(
        string scope,
        Guid key,
        string requestHash,
        Guid resourceId,
        DateTimeOffset createdAt,
        GenerationRequestDetails response) => IdempotencyProtocol.Completed(
            scope,
            key,
            requestHash,
            "GENERATION_REQUEST",
            resourceId,
            response.ResponseCode,
            GenerationRequestSerialization.Snapshot(response),
            createdAt,
            DateTimeOffset.MaxValue,
            responseLocation: $"/api/v1/generation-requests/{resourceId:D}");

    private AuditEvent NewAuditEvent(
        Guid actorUserId,
        Guid correlationId,
        Guid? resourceId,
        string action,
        JsonDocument? after,
        string outcome) => new()
        {
            Id = uuidGenerator.NewUuid(),
            OccurredAt = clock.UtcNow,
            ActorUserId = actorUserId,
            ActorType = "APP_USER",
            Action = action,
            ResourceType = "GENERATION_REQUEST",
            ResourceId = resourceId,
            BranchId = BranchScope.LorettaId,
            CorrelationId = correlationId,
            AfterData = after,
            Outcome = outcome,
        };

    private static GenerationRequestDetails ToDetails(
        GenerationRequest request,
        string result,
        int responseCode = StatusCodes.Status200OK) => new(
        request.Id,
        request.RuleVersionId,
        request.BranchId,
        request.PeriodId,
        request.OriginType,
        request.OriginReference,
        result,
        request.RequestedBy,
        request.RequestedAt,
        request.ObligationId,
        request.ErrorCode,
        responseCode);

    private static string Hash(params string[] values) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\n', values))));
}
