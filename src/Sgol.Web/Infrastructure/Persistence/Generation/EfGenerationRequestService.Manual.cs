using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Sgol.BuildingBlocks.Versioning;
using Sgol.Configuration.Contracts;
using Sgol.Generation.Contracts;
using Sgol.Identity.Contracts;
using Sgol.Organization.Contracts;
using Sgol.Web.Infrastructure.Persistence.Auditing;
using Sgol.Web.Infrastructure.Persistence.Idempotency;

namespace Sgol.Web.Infrastructure.Persistence.Generation;

public sealed partial class EfGenerationRequestService
{
    private async Task<GenerationRequestDetails> CreateManualAsync(CreateGenerationRequestCommand command, CancellationToken token)
    {
        if (await GenerationAuthorizationQuery.GetCreatorRoleAsync(dbContext, command.ActorUserId, clock.UtcNow, token) is null ||
            command.BranchId != BranchScope.LorettaId)
        {
            await AuditDeniedAsync(command, token);
            throw new GenerationRequestAccessDeniedException();
        }
        var input = ManualGenerationInput.Normalize(command.InputPayload!.Value);
        var taskCode = ManualGenerationInput.TaskCode(input);
        var actor = command.ActorUserId.ToString("D");
        const string operation = "GENERATION_REQUEST_CREATE";
        const string resource = "new:LOR-001";
        var scope = IdempotencyProtocol.Scope(actor, operation, resource);
        var hash = IdempotencyProtocol.HashCanonical(operation, actor, resource, new
        {
            schemaVersion = 2,
            command.RuleVersionId,
            command.BranchId,
            command.PeriodId,
            command.OriginType,
            inputPayload = input
        });
        try
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(token);
            // Order is always intention then branch/TAR. Different HTTP keys still share the CAT guard.
            await LockAsync(scope + ":" + command.IdempotencyKey.ToString("D"), token);
            var replay = await dbContext.IdempotencyRecords.AsNoTracking().SingleOrDefaultAsync(
                row => (row.Scope == scope || row.Scope == IdempotencyScopePrefix + command.ActorUserId.ToString("N")) &&
                    row.Key == command.IdempotencyKey, token);
            if (replay is not null)
            {
                await RequireVisibleAsync(command.ActorUserId, replay.ResourceId, token);
                if (replay.RequestHash != hash) throw new GenerationRequestIdempotencyConflictException();
                return IdempotencyProtocol.ReadPayload<GenerationRequestDetails>(replay) with
                { Result = GenerationRequestResults.Recovered, ResponseCode = replay.ResponseCode };
            }
            await LockAsync("manual:" + command.BranchId.ToString("D") + ":" + taskCode, token);
            var role = await GenerationAuthorizationQuery.GetCreatorRoleAsync(dbContext, command.ActorUserId, clock.UtcNow, token)
                ?? throw new GenerationRequestAccessDeniedException();
            await ValidateFreshRequestAsync(command, role, command.OriginType, "CAT2", token);
            var rule = await dbContext.ActivationRuleVersions.AsNoTracking().SingleAsync(r => r.Id == command.RuleVersionId, token);
            var task = await dbContext.TaskDefinitions.AsNoTracking().SingleAsync(t => t.Id == rule.TaskDefinitionId, token);
            if (task.TaskCode != taskCode) throw ManualGenerationInput.Invalid("taskCode", "MISMATCH");
            if (await HasLegacyCompetitorAsync(task.Id, taskCode, token))
                throw new ManualGenerationException("GENERACION_LEGACY_REQUIERE_REVISION", 409);
            ManualGenerationInput.ValidateTimes(input, clock.UtcNow);
            var parentId = await ValidateParentAsync(command, input, token);
            var identity = ManualGenerationInput.Identity(input);
            var canonicalIdentity = IdempotencyProtocol.Canonicalize(identity);
            var originKey = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalIdentity)));
            var originReference = "CAT2:" + originKey;
            var activeOnly = taskCode is "TAR-0008" or "TAR-0092";
            var existing = await dbContext.WorkObligations.AsNoTracking().Where(o => o.BranchId == command.BranchId &&
                o.ManualTaskCode == taskCode && o.ManualOriginKey == originKey &&
                (!activeOnly || o.ExecutionStatus == WorkObligationStatuses.Pending)).SingleOrDefaultAsync(token);
            // RN-010 retains the historical same rule/period/origin key even after active uniqueness is freed.
            var functional = await FindFunctionalRequestAsync(command.RuleVersionId, command.BranchId, command.PeriodId,
                command.OriginType, originReference, token);
            if (existing is null && functional?.ObligationId is Guid oldId)
                existing = await dbContext.WorkObligations.AsNoTracking().SingleAsync(o => o.Id == oldId, token);
            GenerationRequest request;
            WorkObligation obligation;
            var result = GenerationRequestResults.Accepted;
            var status = 201;
            if (existing is not null)
            {
                request = await RequireVisibleAsync(command.ActorUserId, existing.GenerationRequestId, token);
                var snapshot = existing.InputPayload!.RootElement;
                if (IdempotencyProtocol.Canonicalize(JsonNode.Parse(snapshot.GetProperty("originIdentity").GetRawText())) != canonicalIdentity)
                    throw new ManualGenerationException("ORIGEN_IDENTIDAD_INCONSISTENTE", 409);
                if (request.RuleVersionId != command.RuleVersionId || request.PeriodId != command.PeriodId ||
                    IdempotencyProtocol.Canonicalize(JsonNode.Parse(snapshot.GetProperty("input").GetRawText())) !=
                    IdempotencyProtocol.Canonicalize(JsonNode.Parse(input.GetRawText())))
                    throw new ManualGenerationException("ORIGEN_YA_REGISTRADO", 409);
                obligation = existing;
                result = GenerationRequestResults.Recovered;
                status = 200;
            }
            else
            {
                var capturedAt = clock.UtcNow;
                var evidence = await dbContext.EvidencePolicyVersions
                    .FromSqlInterpolated($"SELECT * FROM evidence_policy_version WHERE task_definition_version_id = {rule.TaskDefinitionVersionId} AND status = 'VIGENTE' FOR SHARE")
                    .AsNoTracking().SingleOrDefaultAsync(token);
                var validation = await dbContext.ValidationPolicyVersions
                    .FromSqlInterpolated($"SELECT * FROM validation_policy_version WHERE task_definition_version_id = {rule.TaskDefinitionVersionId} AND status = 'VIGENTE' FOR SHARE")
                    .AsNoTracking().SingleOrDefaultAsync(token);
                if (evidence is null || validation is null || evidence.EffectiveFrom > capturedAt || validation.EffectiveFrom > capturedAt ||
                    evidence.EffectiveFrom is null || validation.EffectiveFrom is null || evidence.EffectiveTo <= capturedAt || validation.EffectiveTo <= capturedAt)
                    throw new ManualGenerationException("CONFIGURACION_GENERACION_INCOMPLETA", 409);
                var (dueAt, calendarIds) = await DeadlineAsync(input, capturedAt, token);
                var snapshot = JsonSerializer.SerializeToDocument(new { schemaVersion = 2, input, originIdentity = identity, calendarDayVersionIds = calendarIds });
                request = new GenerationRequest(uuidGenerator.NewUuid(), command.IdempotencyKey, hash, command.RuleVersionId,
                    command.BranchId, command.PeriodId, command.OriginType, originReference, command.ActorUserId, capturedAt);
                dbContext.GenerationRequests.Add(request);
                await dbContext.SaveChangesAsync(token);
                obligation = await EfWorkObligationMaterializer.MaterializeManualInTransactionAsync(dbContext, request,
                    uuidGenerator.NewUuid(), rule.TaskDefinitionVersionId, evidence.Id, validation.Id,
                    new(taskCode, originKey, parentId, snapshot, dueAt), token);
            }
            var details = ManualDetails(request, obligation, result, status);
            dbContext.AuditEvents.Add(ManualAudit(command, request.Id, "GENERATION_REQUEST_" + (status == 201 ? "ACCEPTED" : "RECOVERED"), taskCode));
            dbContext.AuditEvents.Add(ManualAudit(command, obligation.Id, "WORK_OBLIGATION_" + (status == 201 ? "CREATED" : "RECOVERED"), taskCode, "WORK_OBLIGATION"));
            dbContext.IdempotencyRecords.Add(NewIdempotencyRecord(scope, command.IdempotencyKey, hash, request.Id, clock.UtcNow, details));
            await dbContext.SaveChangesAsync(token);
            await transaction.CommitAsync(token);
            return details;
        }
        catch (GenerationRequestIdempotencyConflictException)
        {
            dbContext.ChangeTracker.Clear();
            await AuditConflictAsync(command.ActorUserId, command.CorrelationId, Guid.Empty, command.IdempotencyKey, token);
            throw;
        }
        catch (GenerationRequestAccessDeniedException)
        {
            dbContext.ChangeTracker.Clear();
            await AuditDeniedAsync(command, token);
            throw;
        }
        finally { dbContext.ChangeTracker.Clear(); }
    }

    private Task<int> LockAsync(string key, CancellationToken token) => dbContext.Database.ExecuteSqlInterpolatedAsync(
        $"SELECT pg_advisory_xact_lock(hashtextextended({key}, 0))", token);

    private async Task<GenerationRequest> RequireVisibleAsync(Guid actor, Guid requestId, CancellationToken token)
    {
        var request = await dbContext.GenerationRequests.AsNoTracking().SingleOrDefaultAsync(r => r.Id == requestId, token);
        if (request?.RequestedBy is not Guid owner || request.BranchId != BranchScope.LorettaId ||
            !await hierarchyResolver.CanAccessUserAsync(actor, owner, token)) throw new GenerationRequestNotFoundException();
        var role = await GenerationAuthorizationQuery.GetCreatorRoleAsync(dbContext, actor, clock.UtcNow, token);
        var code = await (from rule in dbContext.ActivationRuleVersions
                          join task in dbContext.TaskDefinitions on rule.TaskDefinitionId equals task.Id
                          where rule.Id == request.RuleVersionId
                          select task.TaskCode).SingleAsync(token);
        if (role is null || !RoleHierarchy.CanAccessLevel(role, EligibilityPolicyCatalog.RequireRole(code)))
            throw new GenerationRequestNotFoundException();
        return request;
    }

    private Task<bool> HasLegacyCompetitorAsync(Guid taskId, string taskCode, CancellationToken token) =>
        (from request in dbContext.GenerationRequests
         join rule in dbContext.ActivationRuleVersions on request.RuleVersionId equals rule.Id
         join obligation in dbContext.WorkObligations on request.ObligationId equals obligation.Id into obligations
         from obligation in obligations.DefaultIfEmpty()
         where request.BranchId == BranchScope.LorettaId && request.RequestedBy != null && rule.TaskDefinitionId == taskId &&
             (obligation == null || obligation.ManualTaskCode == null) &&
             ((taskCode != "TAR-0008" && taskCode != "TAR-0092") || obligation == null || obligation.ExecutionStatus == WorkObligationStatuses.Pending)
         select request.Id).AnyAsync(token);

    private AuditEvent ManualAudit(CreateGenerationRequestCommand command, Guid resourceId, string action, string code,
        string resourceType = "GENERATION_REQUEST") => new()
        {
            Id = uuidGenerator.NewUuid(),
            OccurredAt = clock.UtcNow,
            ActorUserId = command.ActorUserId,
            ActorType = "USER",
            Action = action,
            ResourceType = resourceType,
            ResourceId = resourceId,
            BranchId = command.BranchId,
            CorrelationId = command.CorrelationId,
            Outcome = "SUCCESS",
            AfterData = JsonSerializer.SerializeToDocument(new { schemaVersion = 2, taskCode = code, resourceId })
        };

    private static GenerationRequestDetails ManualDetails(GenerationRequest request, WorkObligation obligation,
        string result, int status, bool includeInput = false) => new(request.Id, request.RuleVersionId, request.BranchId,
        request.PeriodId, request.OriginType, request.OriginReference, result, request.RequestedBy, request.RequestedAt,
        obligation.Id, null, status, 2, obligation.ManualTaskCode, obligation.TaskDefinitionVersionId,
        obligation.EvidencePolicyVersionId, obligation.ValidationPolicyVersionId, obligation.DueAt,
        includeInput ? obligation.InputPayload!.RootElement.GetProperty("input").Clone() : null);
}
