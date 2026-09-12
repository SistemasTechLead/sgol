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

public sealed class EfGenerationRequestService(
    SgolDbContext dbContext,
    AuditTransaction auditTransaction,
    IRoleHierarchyResolver hierarchyResolver,
    IClock clock,
    IUuidGenerator uuidGenerator) : IGenerationRequestService
{
    private const string IdempotencyScopePrefix = "generation-request:create:";
    private const string IdempotencyPrimaryKey = "PK_idempotency_record";

    public async Task<GenerationRequestDetails> CreateAsync(
        CreateGenerationRequestCommand command,
        CancellationToken cancellationToken = default)
    {
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

        if (command.BranchId != BranchScope.LorettaId)
        {
            await AuditDeniedAsync(command, cancellationToken);
            throw new GenerationRequestAccessDeniedException();
        }

        var request = new GenerationRequest(
            uuidGenerator.NewUuid(),
            command.IdempotencyKey,
            requestHash,
            command.RuleVersionId,
            command.BranchId,
            command.PeriodId,
            originType,
            originReference,
            command.ActorUserId,
            clock.UtcNow);
        var selectedRequest = request;
        var semanticResult = GenerationRequestResults.Accepted;

        try
        {
            await auditTransaction.ExecuteAsync(
                async token =>
                {
                    await ValidateFreshRequestAsync(
                        command,
                        actorRole,
                        originType,
                        originReference,
                        token);
                    var existingRequest = await FindFunctionalRequestAsync(
                        command.RuleVersionId,
                        command.BranchId,
                        command.PeriodId,
                        originType,
                        originReference,
                        token);
                    if (existingRequest is null)
                    {
                        dbContext.GenerationRequests.Add(request);
                    }
                    else
                    {
                        selectedRequest = existingRequest;
                        semanticResult = GenerationRequestResults.Recovered;
                    }

                    var responseCode = semanticResult == GenerationRequestResults.Accepted
                        ? StatusCodes.Status201Created
                        : StatusCodes.Status200OK;
                    dbContext.IdempotencyRecords.Add(NewIdempotencyRecord(
                        scope,
                        command.IdempotencyKey,
                        requestHash,
                        selectedRequest.Id,
                        request.RequestedAt,
                        ToDetails(selectedRequest, semanticResult, responseCode)));
                    return NewAuditEvent(
                        command.ActorUserId,
                        command.CorrelationId,
                        selectedRequest.Id,
                        semanticResult == GenerationRequestResults.Accepted
                            ? "GENERATION_REQUEST_ACCEPTED"
                            : "GENERATION_REQUEST_RECOVERED",
                        Serialize(selectedRequest),
                        "SUCCESS");
                },
                cancellationToken);
        }
        catch (GenerationRequestAccessDeniedException)
        {
            dbContext.ChangeTracker.Clear();
            await AuditDeniedAsync(command, cancellationToken);
            throw;
        }
        catch (DbUpdateException exception) when (IsUniquenessConflict(exception))
        {
            dbContext.ChangeTracker.Clear();
            var concurrentReplay = await FindReplayAsync(
                scope,
                command.ActorUserId,
                command.CorrelationId,
                command.IdempotencyKey,
                requestHash,
                legacyScope,
                legacyRequestHash,
                cancellationToken);
            if (concurrentReplay is not null)
            {
                return concurrentReplay;
            }

            var keyOwner = await dbContext.GenerationRequests.AsNoTracking()
                .SingleOrDefaultAsync(
                    item => item.RequestedBy == command.ActorUserId
                        && item.IdempotencyKey == command.IdempotencyKey,
                    cancellationToken);
            if (keyOwner is not null)
            {
                await AuditConflictAsync(
                    command.ActorUserId,
                    command.CorrelationId,
                    keyOwner.Id,
                    command.IdempotencyKey,
                    cancellationToken);
                throw new GenerationRequestIdempotencyConflictException();
            }

            var concurrentFunctional = await FindFunctionalRequestAsync(
                command.RuleVersionId,
                command.BranchId,
                command.PeriodId,
                originType,
                originReference,
                cancellationToken);
            if (concurrentFunctional is not null)
            {
                return await BindFunctionalReplayAsync(
                    concurrentFunctional,
                    scope,
                    command,
                    requestHash,
                    cancellationToken);
            }

            throw;
        }
        catch
        {
            dbContext.ChangeTracker.Clear();
            throw;
        }

        var result = ToDetails(
            selectedRequest,
            semanticResult,
            semanticResult == GenerationRequestResults.Accepted
                ? StatusCodes.Status201Created
                : StatusCodes.Status200OK);
        dbContext.ChangeTracker.Clear();
        return result;
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
        if (rule.Status != VersionStatuses.Current)
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
            taskVersion.Status != VersionStatuses.Current)
        {
            throw new GenerationRequestTaskInactiveException();
        }

        var eligibility = await dbContext.EligibilityPolicyVersions
            .FromSqlInterpolated($"SELECT * FROM eligibility_policy_version WHERE task_definition_version_id = {rule.TaskDefinitionVersionId} AND task_definition_id = {rule.TaskDefinitionId} AND status = 'VIGENTE' FOR SHARE")
            .AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken);
        if (eligibility is null || !RoleHierarchy.CanAccessLevel(actorRole, eligibility.RequiredRole))
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

        return ToDetails(request, request.Result);
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

    private async Task<GenerationRequestDetails> BindFunctionalReplayAsync(
        GenerationRequest request,
        string scope,
        CreateGenerationRequestCommand command,
        string requestHash,
        CancellationToken cancellationToken)
    {
        try
        {
            await auditTransaction.ExecuteAsync(
                token =>
                {
                    dbContext.IdempotencyRecords.Add(NewIdempotencyRecord(
                        scope,
                        command.IdempotencyKey,
                        requestHash,
                        request.Id,
                        clock.UtcNow,
                        ToDetails(request, GenerationRequestResults.Recovered, StatusCodes.Status200OK)));
                    return Task.FromResult(NewAuditEvent(
                        command.ActorUserId,
                        command.CorrelationId,
                        request.Id,
                        "GENERATION_REQUEST_RECOVERED",
                        Serialize(request),
                        "SUCCESS"));
                },
                cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniquenessConflict(exception))
        {
            dbContext.ChangeTracker.Clear();
            var replay = await FindReplayAsync(
                scope,
                command.ActorUserId,
                command.CorrelationId,
                command.IdempotencyKey,
                requestHash,
                IdempotencyScopePrefix + command.ActorUserId.ToString("N"),
                Hash(
                    command.RuleVersionId.ToString("N"),
                    command.BranchId.ToString("N"),
                    command.PeriodId.ToString("N"),
                    command.OriginType.Trim(),
                    command.OriginReference.Trim()),
                cancellationToken);
            if (replay is not null)
            {
                return replay;
            }

            throw;
        }

        dbContext.ChangeTracker.Clear();
        return ToDetails(request, GenerationRequestResults.Recovered);
    }

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
            response,
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

    private static JsonDocument Serialize(GenerationRequest request) =>
        JsonSerializer.SerializeToDocument(new
        {
            schemaVersion = 1,
            generationRequestId = request.Id,
            request.RuleVersionId,
            request.BranchId,
            request.PeriodId,
            request.OriginType,
            request.OriginReference,
            request.Result,
            request.RequestedBy,
            request.RequestedAt,
            request.ObligationId,
            request.ErrorCode,
        });

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

    private static bool IsUniquenessConflict(DbUpdateException exception) =>
        (exception.InnerException as PostgresException)?.ConstraintName is
            GenerationRequestConfiguration.IdempotencyIndex or
            GenerationRequestConfiguration.FunctionalKeyIndex or
            IdempotencyPrimaryKey;

    private static string Hash(params string[] values) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\n', values))));
}
