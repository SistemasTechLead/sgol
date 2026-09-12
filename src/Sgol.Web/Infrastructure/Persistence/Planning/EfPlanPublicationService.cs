using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Sgol.Assignment.Contracts;
using Sgol.BuildingBlocks.Identifiers;
using Sgol.BuildingBlocks.Time;
using Sgol.Configuration.Contracts;
using Sgol.Identity.Contracts;
using Sgol.Organization.Contracts;
using Sgol.Planning.Contracts;
using Sgol.Web.Infrastructure.Persistence.Auditing;
using Sgol.Web.Infrastructure.Persistence.Bootstrap;
using Sgol.Web.Infrastructure.Persistence.Idempotency;

namespace Sgol.Web.Infrastructure.Persistence.Planning;

public sealed class EfPlanPublicationService(
    SgolDbContext dbContext,
    AuditTransaction auditTransaction,
    IClock clock,
    IUuidGenerator uuidGenerator) : IPlanPublicationService
{
    private const string ScopePrefix = "planning:plan-publication";
    private const string PlanResource = "WORK_PLAN";
    private const string PublicationResource = "PLAN_VERSION";
    private const string BranchResource = "BRANCH";
    private const string IdempotencyPrimaryKey = "PK_idempotency_record";
    private const int MaximumAttempts = 3;

    public async Task<PlanPublicationResult> PublishAsync(
        PublishWorkPlanCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.ActorUserId == Guid.Empty || command.IdempotencyKey == Guid.Empty ||
            command.CorrelationId == Guid.Empty || command.PlanId == Guid.Empty || command.ExpectedRowVersion < 1)
        {
            throw new ArgumentException("The plan-publication command is invalid.", nameof(command));
        }

        const string operation = "PLAN_PUBLICATION_CREATE";
        var resource = command.PlanId.ToString("D");
        var scope = IdempotencyProtocol.Scope(command.ActorUserId.ToString("D"), operation, resource);
        var requestHash = IdempotencyProtocol.HashCanonical(
            operation,
            command.ActorUserId.ToString("D"),
            resource,
            new { command.PlanId },
            command.ExpectedRowVersion);
        var legacyScope = LegacyScope(command.ActorUserId, command.PlanId);
        var legacyRequestHash = LegacyHash(command);

        for (var attempt = 1; attempt <= MaximumAttempts; attempt++)
        {
            PlanPublicationResult? result = null;
            PlanPublicationException? rejection = null;
            try
            {
                await auditTransaction.ExecuteAsync(
                    IsolationLevel.ReadCommitted,
                    async token =>
                    {
                        var actor = await LockActorAsync(command.ActorUserId, token);
                        var publishedAt = clock.UtcNow;
                        var actorRole = AuthorizedRole(actor, publishedAt);
                        if (actorRole is null)
                        {
                            rejection = new PlanPublicationAccessDeniedException();
                            return RejectedAudit(command, null, null, rejection.ErrorCode, publishedAt);
                        }

                        var replay = await LockIdempotencyAsync(
                            scope, legacyScope, command.IdempotencyKey, token);

                        if (replay is not null)
                        {
                            var expectedHash = replay.ProtocolVersion == IdempotencyProtocol.CurrentVersion
                                ? requestHash
                                : legacyRequestHash;
                            if (!string.Equals(replay.RequestHash, expectedHash, StringComparison.Ordinal))
                            {
                                rejection = new PlanPublicationIdempotencyConflictException();
                                return IdempotencyConflictAudit(command, actorRole, publishedAt);
                            }

                            if (replay.ResponseCode is >= 200 and < 300 && replay.ResourceType == PublicationResource)
                            {
                                result = replay.ProtocolVersion == IdempotencyProtocol.CurrentVersion
                                    ? IdempotencyProtocol.ReadPayload<PlanPublicationResult>(replay)
                                        with
                                    { Result = PlanPublicationResults.Recovered }
                                    : await LoadReplayAsync(command, replay.ResourceId, actorRole, token);
                                return RecoveredAudit(command, result, publishedAt);
                            }

                            rejection = replay.ProtocolVersion == IdempotencyProtocol.CurrentVersion
                                ? ExceptionFor(
                                    IdempotencyProtocol.ReadPayload<RejectionSnapshot>(replay).ErrorCode,
                                    replay.ResponseCode)
                                : await ReplayRejectionAsync(command, replay, token);
                            return RejectedAudit(command, null, actorRole, rejection.ErrorCode, publishedAt);
                        }

                        var branch = await dbContext.Branches
                            .FromSqlInterpolated($"SELECT * FROM branch WHERE id = {BranchScope.LorettaId} FOR UPDATE")
                            .AsNoTracking()
                            .SingleOrDefaultAsync(token);
                        if (branch is null || branch.Code != BranchScope.LorettaCode || branch.Status != BranchScope.ActiveStatus)
                        {
                            rejection = new PlanPublicationConflictException();
                            return RejectedAudit(command, null, actorRole, rejection.ErrorCode, publishedAt);
                        }

                        var plan = await dbContext.WorkPlans
                            .FromSqlInterpolated($"SELECT * FROM work_plan WHERE id = {command.PlanId} FOR UPDATE")
                            .AsTracking()
                            .SingleOrDefaultAsync(token);
                        if (plan is null || plan.BranchId != BranchScope.LorettaId)
                        {
                            rejection = new PlanPublicationNotFoundException();
                            return RejectedAudit(command, null, actorRole, rejection.ErrorCode, publishedAt);
                        }

                        if (plan.Status is not WorkPlanStatuses.Draft and not WorkPlanStatuses.Published)
                        {
                            rejection = new PlanPublicationStateConflictException();
                            return RejectedAudit(command, plan.Id, actorRole, rejection.ErrorCode, publishedAt);
                        }

                        if (plan.RowVersion != command.ExpectedRowVersion)
                        {
                            rejection = new PlanPublicationVersionConflictException();
                            return RejectedAudit(command, plan.Id, actorRole, rejection.ErrorCode, publishedAt);
                        }

                        var current = await dbContext.PlanVersions
                            .FromSqlInterpolated($"SELECT * FROM plan_version WHERE plan_id = {plan.Id} AND scope_role = {actorRole} AND status = {PlanVersionStatuses.Current} FOR UPDATE")
                            .AsTracking()
                            .SingleOrDefaultAsync(token);
                        var previousItems = current is null
                            ? []
                            : await dbContext.PlanVersionObligations.AsNoTracking()
                                .Where(item => item.PlanVersionId == current.Id)
                                .OrderBy(item => item.ObligationId)
                                .Select(item => new PlanPublicationItem(item.ObligationId, item.AssignmentVersionId))
                                .ToListAsync(token);

                        IReadOnlyList<PlanPublicationItem> added;
                        try
                        {
                            added = await SelectNewItemsAsync(plan, actorRole, previousItems, publishedAt, token);
                        }
                        catch (PlanPublicationException exception)
                        {
                            rejection = exception;
                            return RejectedAudit(command, plan.Id, actorRole, rejection.ErrorCode, publishedAt);
                        }

                        if (added.Count == 0)
                        {
                            rejection = current is null
                                ? new PlanPublicationEmptyException()
                                : new PlanPublicationNoChangesException();
                            return RejectedAudit(command, plan.Id, actorRole, rejection.ErrorCode, publishedAt);
                        }

                        var nextVersion = await dbContext.PlanVersions.AsNoTracking()
                            .Where(version => version.PlanId == plan.Id)
                            .Select(version => (int?)version.VersionNo)
                            .MaxAsync(token) ?? 0;
                        var beforeStatus = plan.Status;
                        var beforeRowVersion = plan.RowVersion;
                        if (current is not null)
                        {
                            current.Supersede();
                            await dbContext.SaveChangesAsync(token);
                        }

                        plan.ApplyPublication();
                        var publication = new PlanVersion(
                            uuidGenerator.NewUuid(),
                            plan.Id,
                            checked(nextVersion + 1),
                            actorRole,
                            command.ActorUserId,
                            publishedAt,
                            current?.Id,
                            plan.RowVersion);
                        var snapshot = previousItems.Concat(added)
                            .OrderBy(item => item.ObligationId)
                            .ToArray();
                        dbContext.PlanVersions.Add(publication);
                        dbContext.PlanVersionObligations.AddRange(snapshot.Select(item =>
                            new PlanVersionObligation(publication.Id, item.ObligationId, item.AssignmentVersionId)));
                        result = Result(
                            current is null ? PlanPublicationResults.Initial : PlanPublicationResults.Incremental,
                            plan,
                            publication,
                            snapshot,
                            added);
                        AddSuccessIdempotency(scope, command, requestHash, publication.Id, result, publishedAt);
                        return PublishedAudit(
                            command,
                            result,
                            current,
                            beforeStatus,
                            beforeRowVersion,
                            publishedAt);
                    },
                    cancellationToken);

                dbContext.ChangeTracker.Clear();
                if (rejection is not null)
                {
                    throw rejection;
                }

                return result ?? throw new InvalidOperationException("The publication transaction produced no result.");
            }
            catch (Exception exception) when (rejection is PlanPublicationIdempotencyConflictException &&
                exception is not PlanPublicationIdempotencyConflictException)
            {
                dbContext.ChangeTracker.Clear();
                throw new IdempotencyConflictAuditException(exception);
            }
            catch (PlanPublicationException)
            {
                dbContext.ChangeTracker.Clear();
                throw;
            }
            catch (Exception exception) when (IsRetryable(exception) && attempt < MaximumAttempts)
            {
                dbContext.ChangeTracker.Clear();
                await IdempotencyProtocol.DelayBeforeRetryAsync(
                    "PLAN_PUBLICATION_CREATE", exception, attempt, cancellationToken);
            }
            catch (Exception exception) when (IsRetryable(exception))
            {
                dbContext.ChangeTracker.Clear();
                throw new PlanPublicationConcurrencyException();
            }
            catch (DbUpdateException exception) when (GetConstraintName(exception) is not null)
            {
                dbContext.ChangeTracker.Clear();
                throw new PlanPublicationConflictException();
            }
            catch
            {
                dbContext.ChangeTracker.Clear();
                throw;
            }
        }

        throw new PlanPublicationConcurrencyException();
    }

    private async Task<IReadOnlyList<PlanPublicationItem>> SelectNewItemsAsync(
        WorkPlan plan,
        string scopeRole,
        IReadOnlyList<PlanPublicationItem> previousItems,
        DateTimeOffset at,
        CancellationToken token)
    {
        var previousIds = previousItems.Select(item => item.ObligationId).ToHashSet();
        var obligations = await dbContext.WorkObligations
            .FromSqlInterpolated($"SELECT * FROM work_obligation WHERE branch_id = {plan.BranchId} AND period_id = {plan.PeriodId} ORDER BY id FOR UPDATE")
            .AsNoTracking()
            .ToListAsync(token);
        var added = new List<PlanPublicationItem>();

        foreach (var obligation in obligations)
        {
            if (previousIds.Contains(obligation.Id) || obligation.ExecutionStatus != "PENDIENTE")
            {
                continue;
            }

            var policies = await dbContext.EligibilityPolicyVersions
                .FromSqlInterpolated($"SELECT * FROM eligibility_policy_version WHERE task_definition_version_id = {obligation.TaskDefinitionVersionId} FOR UPDATE")
                .AsNoTracking()
                .ToListAsync(token);
            if (policies.Count != 1)
            {
                throw new PlanPublicationContentConflictException();
            }

            var policy = policies[0];
            if (!RoleHierarchy.CanAccessLevel(scopeRole, policy.RequiredRole))
            {
                continue;
            }

            var assignments = await dbContext.AssignmentVersions
                .FromSqlInterpolated($"SELECT * FROM assignment_version WHERE obligation_id = {obligation.Id} AND status = {AssignmentVersionStatuses.Current} FOR UPDATE")
                .AsNoTracking()
                .ToListAsync(token);
            if (assignments.Count == 0)
            {
                throw new PlanPublicationUnassignedObligationException();
            }

            if (assignments.Count != 1)
            {
                throw new PlanPublicationContentConflictException();
            }

            var assignment = assignments[0];
            if (!await ReferencesPolicyAsync(assignment, obligation.Id, policy.Id, token))
            {
                throw new PlanPublicationContentConflictException();
            }

            var responsible = await LockResponsibleAsync(assignment.PersonId, token);
            if (!IsPublishableResponsible(responsible, policy.RequiredRole, at))
            {
                throw new PlanPublicationAssignmentInvalidException();
            }

            added.Add(new PlanPublicationItem(obligation.Id, assignment.Id));
        }

        return added.OrderBy(item => item.ObligationId).ToArray();
    }

    private async Task<ActorState> LockActorAsync(Guid actorUserId, CancellationToken token)
    {
        var user = await dbContext.AppUsers
            .FromSqlInterpolated($"SELECT * FROM app_user WHERE id = {actorUserId} FOR UPDATE")
            .AsNoTracking()
            .SingleOrDefaultAsync(token);
        if (user is null)
        {
            return ActorState.Missing;
        }

        var employments = await dbContext.EmploymentVersions
            .FromSqlInterpolated($"SELECT * FROM employment_version WHERE person_id = {user.PersonId} AND valid_to IS NULL FOR UPDATE")
            .AsNoTracking().ToListAsync(token);
        var roles = await dbContext.RoleAssignmentVersions
            .FromSqlInterpolated($"SELECT * FROM role_assignment_version WHERE user_id = {user.Id} AND branch_id = {BranchScope.LorettaId} AND valid_to IS NULL FOR UPDATE")
            .AsNoTracking().ToListAsync(token);
        return new ActorState(user, employments, roles);
    }

    private async Task<IdempotencyRecord?> LockIdempotencyAsync(
        string scope,
        string legacyScope,
        Guid key,
        CancellationToken token)
    {
        var record = await dbContext.IdempotencyRecords
            .FromSqlInterpolated($"SELECT * FROM idempotency_record WHERE scope = {scope} AND key = {key} FOR UPDATE")
            .AsNoTracking()
            .SingleOrDefaultAsync(token);
        return record ?? await dbContext.IdempotencyRecords
            .FromSqlInterpolated($"SELECT * FROM idempotency_record WHERE scope = {legacyScope} AND key = {key} FOR UPDATE")
            .AsNoTracking()
            .SingleOrDefaultAsync(token);
    }

    private async Task<ActorState> LockResponsibleAsync(Guid personId, CancellationToken token)
    {
        var user = await dbContext.AppUsers
            .FromSqlInterpolated($"SELECT * FROM app_user WHERE person_id = {personId} FOR UPDATE")
            .AsNoTracking().SingleOrDefaultAsync(token);
        if (user is null)
        {
            return ActorState.Missing;
        }

        var employments = await dbContext.EmploymentVersions
            .FromSqlInterpolated($"SELECT * FROM employment_version WHERE person_id = {personId} AND valid_to IS NULL FOR UPDATE")
            .AsNoTracking().ToListAsync(token);
        var roles = await dbContext.RoleAssignmentVersions
            .FromSqlInterpolated($"SELECT * FROM role_assignment_version WHERE user_id = {user.Id} AND branch_id = {BranchScope.LorettaId} AND valid_to IS NULL FOR UPDATE")
            .AsNoTracking().ToListAsync(token);
        return new ActorState(user, employments, roles);
    }

    private static string? AuthorizedRole(ActorState actor, DateTimeOffset at)
    {
        if (!IsCurrentIdentity(actor, at) || actor.Roles[0].RoleCode == CanonicalRole.SalesFloor)
        {
            return null;
        }

        return actor.Roles[0].RoleCode is CanonicalRole.Direction or CanonicalRole.Administration or CanonicalRole.Subcoordination
            ? actor.Roles[0].RoleCode
            : null;
    }

    private static bool IsPublishableResponsible(ActorState responsible, string requiredRole, DateTimeOffset at) =>
        IsCurrentIdentity(responsible, at) && responsible.Roles[0].RoleCode == requiredRole;

    private static bool IsCurrentIdentity(ActorState state, DateTimeOffset at) =>
        state.User is { Status: AccountStatus.Active } &&
        state.Employments.Count == 1 &&
        state.Employments[0].BranchId == BranchScope.LorettaId &&
        state.Employments[0].Status == EmploymentStatus.Active &&
        state.Employments[0].ValidFrom <= at &&
        state.Employments[0].ValidTo is null &&
        state.Roles.Count == 1 &&
        state.Roles[0].Status == RoleAssignmentStatus.Active &&
        state.Roles[0].ValidFrom <= at &&
        state.Roles[0].ValidTo is null &&
        CanonicalRole.IsDefined(state.Roles[0].RoleCode);

    private async Task<bool> ReferencesPolicyAsync(
        AssignmentVersion assignment,
        Guid obligationId,
        Guid policyId,
        CancellationToken token)
    {
        var explanation = assignment.Explanation.RootElement;
        var hasEvaluation = TryReadGuid(explanation, "eligibilityEvaluationId", out var evaluationId);

        if (assignment.AssignmentType == AssignmentTypes.Automatic)
        {
            return hasEvaluation &&
                await EvaluationReferencesPolicyAsync(evaluationId, obligationId, policyId, token);
        }

        if (assignment.AssignmentType != AssignmentTypes.Correction ||
            !TryReadGuid(explanation, "eligibilityPolicyVersionId", out var directPolicyId) ||
            directPolicyId != policyId)
        {
            return false;
        }

        return !hasEvaluation ||
            await EvaluationReferencesPolicyAsync(evaluationId, obligationId, policyId, token);
    }

    private async Task<bool> EvaluationReferencesPolicyAsync(
        Guid evaluationId,
        Guid obligationId,
        Guid policyId,
        CancellationToken token)
    {
        var evaluations = await dbContext.EligibilityEvaluations
            .FromSqlInterpolated($"SELECT * FROM eligibility_evaluation WHERE id = {evaluationId} FOR UPDATE")
            .AsNoTracking()
            .ToListAsync(token);
        return evaluations.Count == 1 &&
            evaluations[0].ObligationId == obligationId &&
            evaluations[0].PolicyVersionId == policyId;
    }

    private static bool TryReadGuid(JsonElement explanation, string propertyName, out Guid value)
    {
        value = Guid.Empty;
        return explanation.TryGetProperty(propertyName, out var property) &&
            property.ValueKind == JsonValueKind.String &&
            property.TryGetGuid(out value) &&
            value != Guid.Empty;
    }

    private async Task<PlanPublicationResult> LoadReplayAsync(
        PublishWorkPlanCommand command,
        Guid publicationId,
        string actorRole,
        CancellationToken token)
    {
        var branch = await dbContext.Branches
            .FromSqlInterpolated($"SELECT * FROM branch WHERE id = {BranchScope.LorettaId} FOR UPDATE")
            .AsNoTracking().SingleOrDefaultAsync(token);
        if (branch is null || branch.Code != BranchScope.LorettaCode || branch.Status != BranchScope.ActiveStatus)
        {
            throw new PlanPublicationConflictException();
        }

        var plan = await dbContext.WorkPlans
            .FromSqlInterpolated($"SELECT * FROM work_plan WHERE id = {command.PlanId} FOR UPDATE")
            .AsNoTracking().SingleOrDefaultAsync(token);
        if (plan is null || plan.BranchId != BranchScope.LorettaId)
        {
            throw new PlanPublicationNotFoundException();
        }

        var publication = await dbContext.PlanVersions
            .FromSqlInterpolated($"SELECT * FROM plan_version WHERE id = {publicationId} FOR UPDATE")
            .AsNoTracking().SingleOrDefaultAsync(token)
            ?? throw new PlanPublicationConflictException();
        if (publication.PlanId != command.PlanId || !RoleHierarchy.CanAccessLevel(actorRole, publication.ScopeRole))
        {
            throw new PlanPublicationAccessDeniedException();
        }

        var items = await dbContext.PlanVersionObligations.AsNoTracking()
            .Where(item => item.PlanVersionId == publication.Id)
            .OrderBy(item => item.ObligationId)
            .Select(item => new PlanPublicationItem(item.ObligationId, item.AssignmentVersionId))
            .ToListAsync(token);
        IReadOnlyList<Guid> previousIds = publication.SupersedesId is null
            ? Array.Empty<Guid>()
            : await dbContext.PlanVersionObligations.AsNoTracking()
                .Where(item => item.PlanVersionId == publication.SupersedesId)
                .Select(item => item.ObligationId)
                .ToListAsync(token);
        var previousSet = previousIds.ToHashSet();
        var added = items.Where(item => !previousSet.Contains(item.ObligationId)).ToArray();
        return new PlanPublicationResult(
            PlanPublicationResults.Recovered,
            publication.PlanId,
            WorkPlanStatuses.Published,
            publication.Id,
            publication.VersionNo,
            PlanVersionStatuses.Current,
            publication.ScopeRole,
            publication.PublishedBy,
            publication.PublishedAt,
            items,
            added,
            publication.PlanRowVersion);
    }

    private void AddSuccessIdempotency(
        string scope,
        PublishWorkPlanCommand command,
        string requestHash,
        Guid publicationId,
        PlanPublicationResult response,
        DateTimeOffset at) =>
        dbContext.IdempotencyRecords.Add(NewIdempotency(
            scope, command, requestHash, PublicationResource, publicationId, 201, response, at,
            $"/api/v1/plans/{command.PlanId:D}/publications"));

    private static IdempotencyRecord NewIdempotency(
        string scope,
        PublishWorkPlanCommand command,
        string requestHash,
        string resourceType,
        Guid resourceId,
        int responseCode,
        object responsePayload,
        DateTimeOffset at,
        string? responseLocation = null) => IdempotencyProtocol.Completed(
            scope,
            command.IdempotencyKey,
            requestHash,
            resourceType,
            resourceId,
            responseCode,
            responsePayload,
            at,
            DateTimeOffset.MaxValue,
            responseLocation: responseLocation);

    private AuditEvent PublishedAudit(
        PublishWorkPlanCommand command,
        PlanPublicationResult result,
        PlanVersion? previous,
        string beforeStatus,
        long beforeRowVersion,
        DateTimeOffset at) => NewAudit(
            command,
            result.PlanId,
            "WORK_PLAN_PUBLISHED",
            result.Result,
            JsonSerializer.SerializeToDocument(new
            {
                schemaVersion = 1,
                planId = result.PlanId,
                planStatus = beforeStatus,
                rowVersion = beforeRowVersion,
                scopeRole = result.ScopeRole,
                publicationId = previous?.Id,
                versionNo = previous?.VersionNo,
                versionStatus = previous is null ? null : PlanVersionStatuses.Current,
            }, JsonSerializerOptions.Web),
            JsonSerializer.SerializeToDocument(new
            {
                schemaVersion = 1,
                planId = result.PlanId,
                planStatus = result.PlanStatus,
                rowVersion = result.RowVersion,
                publicationId = result.PublicationId,
                versionNo = result.VersionNo,
                versionStatus = result.VersionStatus,
                scopeRole = result.ScopeRole,
                publishedBy = result.PublishedBy,
                publishedAt = result.PublishedAt,
                obligationCount = result.Obligations.Count,
                addedObligations = result.AddedObligations,
            }, JsonSerializerOptions.Web),
            at);

    private AuditEvent RecoveredAudit(
        PublishWorkPlanCommand command,
        PlanPublicationResult result,
        DateTimeOffset at) => NewAudit(
            command,
            result.PlanId,
            "WORK_PLAN_PUBLICATION_RECOVERED",
            PlanPublicationResults.Recovered,
            null,
            JsonSerializer.SerializeToDocument(new
            {
                schemaVersion = 1,
                planId = result.PlanId,
                publicationId = result.PublicationId,
                versionNo = result.VersionNo,
                scopeRole = result.ScopeRole,
                planRowVersion = result.RowVersion,
                result = PlanPublicationResults.Recovered,
            }, JsonSerializerOptions.Web),
            at);

    private AuditEvent RejectedAudit(
        PublishWorkPlanCommand command,
        Guid? planId,
        string? scopeRole,
        string errorCode,
        DateTimeOffset at) => NewAudit(
            command,
            planId,
            "WORK_PLAN_PUBLICATION_REJECTED",
            errorCode,
            null,
            RejectionData(command, planId, scopeRole, errorCode),
            at);

    private AuditEvent IdempotencyConflictAudit(
        PublishWorkPlanCommand command,
        string? scopeRole,
        DateTimeOffset at) => IdempotencyProtocol.ConflictAudit(
            uuidGenerator.NewUuid(), at, command.ActorUserId, PlanResource, command.PlanId,
            BranchScope.LorettaId, command.CorrelationId, command.IdempotencyKey, "PLAN_PUBLICATION_CREATE");

    private static JsonDocument RejectionData(
        PublishWorkPlanCommand command,
        Guid? planId,
        string? scopeRole,
        string errorCode)
    {
        var data = new Dictionary<string, object?>
        {
            ["schemaVersion"] = 1,
            ["expectedRowVersion"] = command.ExpectedRowVersion,
            ["errorCode"] = errorCode,
        };
        if (planId is not null)
        {
            data["planId"] = planId;
        }

        if (scopeRole is not null)
        {
            data["scopeRole"] = scopeRole;
        }

        return JsonSerializer.SerializeToDocument(data, JsonSerializerOptions.Web);
    }

    private AuditEvent NewAudit(
        PublishWorkPlanCommand command,
        Guid? resourceId,
        string action,
        string outcome,
        JsonDocument? beforeData,
        JsonDocument afterData,
        DateTimeOffset at) => new()
        {
            Id = uuidGenerator.NewUuid(),
            OccurredAt = at,
            ActorUserId = command.ActorUserId,
            ActorType = "USER",
            Action = action,
            ResourceType = PlanResource,
            ResourceId = resourceId,
            BranchId = BranchScope.LorettaId,
            CorrelationId = command.CorrelationId,
            RequestId = command.IdempotencyKey.ToString("D"),
            BeforeData = beforeData,
            AfterData = afterData,
            Outcome = outcome,
        };

    private static PlanPublicationResult Result(
        string result,
        WorkPlan plan,
        PlanVersion publication,
        IReadOnlyList<PlanPublicationItem> snapshot,
        IReadOnlyList<PlanPublicationItem> added) => new(
            result,
            plan.Id,
            plan.Status,
            publication.Id,
            publication.VersionNo,
            publication.Status,
            publication.ScopeRole,
            publication.PublishedBy,
            publication.PublishedAt,
            snapshot,
            added,
            plan.RowVersion);

    private async Task<PlanPublicationException> ReplayRejectionAsync(
        PublishWorkPlanCommand command,
        IdempotencyRecord replay,
        CancellationToken token)
    {
        var requestId = command.IdempotencyKey.ToString("D");
        var query = dbContext.AuditEvents.AsNoTracking()
            .Where(item =>
                item.Action == "WORK_PLAN_PUBLICATION_REJECTED" &&
                item.RequestId == requestId &&
                item.ActorUserId == command.ActorUserId);
        if (replay.ResourceType == PlanResource)
        {
            query = query.Where(item => item.ResourceId == replay.ResourceId);
        }

        var audit = await query
            .OrderBy(item => item.OccurredAt).ThenBy(item => item.Id)
            .FirstOrDefaultAsync(token);
        var errorCode = audit?.AfterData?.RootElement.TryGetProperty("errorCode", out var value) == true
            ? value.GetString()
            : null;
        return ExceptionFor(errorCode, replay.ResponseCode);
    }

    private static PlanPublicationException ExceptionFor(string? errorCode, int responseCode) => errorCode switch
    {
        "ACCESO_DENEGADO" => new PlanPublicationAccessDeniedException(),
        "PLAN_NO_ENCONTRADO" => new PlanPublicationNotFoundException(),
        "IDEMPOTENCY_CONFLICT" => new PlanPublicationIdempotencyConflictException(),
        "ESTADO_PLAN_INCOMPATIBLE" => new PlanPublicationStateConflictException(),
        "CONTENIDO_PLAN_INCOMPATIBLE" => new PlanPublicationContentConflictException(),
        "VERSION_CONFLICT" => new PlanPublicationVersionConflictException(),
        "OBLIGACION_SIN_ASIGNACION" => new PlanPublicationUnassignedObligationException(),
        "ASIGNACION_NO_PUBLICABLE" => new PlanPublicationAssignmentInvalidException(),
        "SIN_OBLIGACIONES_PUBLICABLES" => new PlanPublicationEmptyException(),
        "SIN_NOVEDADES_PUBLICABLES" => new PlanPublicationNoChangesException(),
        "PLAN_PUBLICATION_CONCURRENCY_CONFLICT" => new PlanPublicationConcurrencyException(),
        _ when responseCode == 403 => new PlanPublicationAccessDeniedException(),
        _ when responseCode == 404 => new PlanPublicationNotFoundException(),
        _ when responseCode == 412 => new PlanPublicationVersionConflictException(),
        _ => new PlanPublicationConflictException(),
    };

    private static string LegacyScope(Guid actorUserId, Guid planId) =>
        $"{ScopePrefix}:{actorUserId:D}:{planId:D}";

    private static string LegacyHash(PublishWorkPlanCommand command)
    {
        var canonical = string.Join('\n',
            "WORK_PLAN_PUBLICATION",
            command.PlanId.ToString("D"),
            command.ExpectedRowVersion.ToString(CultureInfo.InvariantCulture),
            "{}");
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
    }

    private sealed record RejectionSnapshot(string ErrorCode);

    private static bool IsRetryable(Exception exception)
    {
        var postgres = exception as PostgresException ?? (exception as DbUpdateException)?.InnerException as PostgresException;
        return postgres?.SqlState is PostgresErrorCodes.DeadlockDetected or PostgresErrorCodes.SerializationFailure ||
            postgres?.SqlState == PostgresErrorCodes.UniqueViolation &&
            postgres.ConstraintName is IdempotencyPrimaryKey or
                PlanVersionConfiguration.VersionNumberIndex or
                PlanVersionConfiguration.PlanRowVersionIndex or
                PlanVersionConfiguration.CurrentScopeIndex or
                PlanVersionConfiguration.SupersedesIndex;
    }

    private static string? GetConstraintName(DbUpdateException exception) =>
        (exception.InnerException as PostgresException)?.ConstraintName;

    private sealed record ActorState(
        AppUser? User,
        IReadOnlyList<EmploymentVersion> Employments,
        IReadOnlyList<RoleAssignmentVersion> Roles)
    {
        public static ActorState Missing { get; } = new(null, [], []);
    }
}
