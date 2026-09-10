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
using Sgol.BuildingBlocks.Versioning;
using Sgol.Configuration.Contracts;
using Sgol.Evidence.Contracts;
using Sgol.Generation.Contracts;
using Sgol.Identity.Contracts;
using Sgol.Organization.Contracts;
using Sgol.Validation.Contracts;
using Sgol.Web.Infrastructure.Persistence.Auditing;
using Sgol.Web.Infrastructure.Persistence.Bootstrap;

namespace Sgol.Web.Infrastructure.Persistence.Validation;

public sealed class EfValidationDecisionService(
    SgolDbContext dbContext,
    IEvidenceConclusionReviewService evidenceReview,
    IClock clock,
    IUuidGenerator uuidGenerator) : IValidationDecisionService
{
    private const int MaximumAttempts = 3;
    private const string ResourceType = "VALIDATION_DECISION_VERSION";

    public Task<ValidationMutationResult> IssueAsync(IssueValidationDecisionCommand command, CancellationToken cancellationToken = default)
    {
        Validate(command.ActorUserId, command.IdempotencyKey, command.CorrelationId, command.ObligationId, command.ExpectedRowVersion);
        if (!ValidationResults.IsDefined(command.Result)) throw new ValidationDecisionException("RESULTADO_VALIDACION_INVALIDO");
        var foundation = ValidationText.Foundation(command.Foundation);
        var escalationReason = command.EscalationReason is null ? null : ValidationText.Reason(command.EscalationReason);
        var normalized = command with { Foundation = foundation, EscalationReason = escalationReason };
        return ExecuteAsync(() => IssueOnceAsync(normalized, cancellationToken), cancellationToken);
    }

    public Task<ValidationMutationResult> ReplaceAsync(ReplaceValidationDecisionCommand command, CancellationToken cancellationToken = default)
    {
        Validate(command.ActorUserId, command.IdempotencyKey, command.CorrelationId, command.DecisionVersionId, command.ExpectedRowVersion);
        if (!ValidationResults.IsDefined(command.Result)) throw new ValidationDecisionException("RESULTADO_VALIDACION_INVALIDO");
        var normalized = command with { Foundation = ValidationText.Foundation(command.Foundation), Reason = ValidationText.Reason(command.Reason) };
        return ExecuteAsync(() => ReplaceOnceAsync(normalized, cancellationToken), cancellationToken);
    }

    public async Task<ValidationHistoryDetails> GetAsync(GetValidationHistoryQuery query, CancellationToken cancellationToken = default)
    {
        if (query.ActorUserId == Guid.Empty || query.CorrelationId == Guid.Empty || query.ObligationId == Guid.Empty)
            throw new ArgumentException("Validation history identifiers are required.");
        var at = clock.UtcNow;
        var actor = await LoadActorAsync(query.ActorUserId, at, cancellationToken);
        var obligation = await dbContext.WorkObligations.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == query.ObligationId && x.BranchId == BranchScope.LorettaId, cancellationToken)
            ?? throw new ValidationObligationNotFoundException();
        var assignment = await dbContext.AssignmentVersions.AsNoTracking()
            .SingleOrDefaultAsync(x => x.ObligationId == obligation.Id && x.Status == AssignmentVersionStatuses.Current, cancellationToken)
            ?? throw new ValidationObligationNotFoundException();
        var targetRole = await LoadResponsibleRoleAsync(assignment.PersonId, at, cancellationToken);
        if (!RoleHierarchy.CanAccess(actor.RoleCode, targetRole.RoleCode, actor.PersonId == assignment.PersonId))
            throw new ValidationObligationNotFoundException();
        return await ProjectAsync(obligation, cancellationToken);
    }

    private async Task<ValidationMutationResult> IssueOnceAsync(IssueValidationDecisionCommand command, CancellationToken token)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
        var preflightActor = await LoadActorAsync(command.ActorUserId, clock.UtcNow, token);
        EnsureIssuePermission(preflightActor, command.EscalationReason);
        var obligation = await LockObligationAsync(command.ObligationId, token);
        var assignment = await LockAssignmentAsync(obligation.Id, token);
        var decidedAt = clock.UtcNow;
        var actor = await LoadActorAsync(command.ActorUserId, decidedAt, token);
        var responsible = await LoadResponsibleRoleAsync(assignment.PersonId, decidedAt, token);
        EnsureIssuePermission(actor, command.EscalationReason);
        var scope = $"validation:emit:{command.ActorUserId:D}:{obligation.Id:D}";
        var hash = Hash("POST", $"/api/v1/obligations/{obligation.Id:D}/validation-decisions", command.ExpectedRowVersion,
            command.Result, command.Foundation, command.EscalationReason);
        var replay = await FindReplayAsync(scope, command.IdempotencyKey, hash, token);
        if (replay is not null)
        {
            AuthorizeVisible(actor, responsible, assignment.PersonId);
            var recovered = await RecoverAsync(replay, obligation, command.ActorUserId, token);
            await transaction.CommitAsync(token); return recovered;
        }
        if (obligation.ExecutionStatus != WorkObligationStatuses.Concluded) throw new ValidationDecisionException("OBLIGACION_NO_CONCLUIDA");
        var policy = await LoadPolicyAsync(obligation, token);
        var authority = IssueAuthority(actor, responsible, assignment.PersonId, policy, command.EscalationReason);
        var requirement = await dbContext.ValidationRequirements
            .FromSqlInterpolated($"SELECT * FROM validation_requirement WHERE obligation_id = {obligation.Id} FOR UPDATE")
            .SingleOrDefaultAsync(token);
        var expected = requirement?.RowVersion ?? obligation.RowVersion;
        if (expected != command.ExpectedRowVersion) throw new ValidationDecisionException("VERSION_CONFLICT");
        if (requirement is not null && await dbContext.ValidationDecisionVersions.AnyAsync(x => x.RequirementId == requirement.Id && x.Status == ValidationStatuses.Current, token))
            throw new ValidationDecisionException("DECISION_VALIDACION_YA_EXISTE");
        requirement ??= new ValidationRequirement(uuidGenerator.NewUuid(), obligation.Id, policy.Id, obligation.ConcludedAt!.Value);
        if (dbContext.Entry(requirement).State == EntityState.Detached) dbContext.ValidationRequirements.Add(requirement);
        var review = await evidenceReview.ReviewAsync(new(command.ActorUserId, command.CorrelationId, obligation.Id, decidedAt), token);
        if (review.SnapshotId is null) throw new ValidationDecisionException("EVIDENCIA_VALIDACION_NO_DISPONIBLE");
        var evidenceVersionCount = await EvidenceVersionCountAsync(review.SnapshotId.Value, token);
        var decision = new ValidationDecisionVersion(uuidGenerator.NewUuid(), requirement.Id, 1, command.Result,
            command.Foundation, authority, actor.UserId, actor.PersonId, actor.RoleCode, assignment.Id,
            assignment.PersonId, decidedAt, command.EscalationReason, null, review.SnapshotId.Value);
        requirement.Resolve(decidedAt, requirement.RowVersion);
        dbContext.ValidationDecisionVersions.Add(decision);
        AddIdempotency(scope, command.IdempotencyKey, hash, decision.Id, decidedAt);
        AddAudit(command.ActorUserId, command.CorrelationId, command.IdempotencyKey, obligation, requirement, decision, null, evidenceVersionCount);
        await dbContext.SaveChangesAsync(token); await transaction.CommitAsync(token);
        return await MutationAsync(obligation, decision, false, token);
    }

    private async Task<ValidationMutationResult> ReplaceOnceAsync(ReplaceValidationDecisionCommand command, CancellationToken token)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
        var preflightActor = await LoadActorAsync(command.ActorUserId, clock.UtcNow, token);
        if (!RoleHierarchy.GrantsValidationReplacement(preflightActor.RoleCode)) throw new ValidationAccessDeniedException();
        var route = await dbContext.ValidationDecisionVersions.AsNoTracking()
            .Where(x => x.Id == command.DecisionVersionId)
            .Join(dbContext.ValidationRequirements.AsNoTracking(), x => x.RequirementId, x => x.Id, (decision, requirement) => new { decision, requirement })
            .SingleOrDefaultAsync(token) ?? throw new ValidationDecisionNotFoundException();
        var obligation = await LockObligationAsync(route.requirement.ObligationId, token);
        var assignment = await LockAssignmentAsync(obligation.Id, token);
        var decidedAt = clock.UtcNow;
        var actor = await LoadActorAsync(command.ActorUserId, decidedAt, token);
        var responsible = await LoadResponsibleRoleAsync(assignment.PersonId, decidedAt, token);
        if (!RoleHierarchy.GrantsValidationReplacement(actor.RoleCode)) throw new ValidationAccessDeniedException();
        var scope = $"validation:replace:{command.ActorUserId:D}:{command.DecisionVersionId:D}";
        var hash = Hash("POST", $"/api/v1/validation-decisions/{command.DecisionVersionId:D}/replacements",
            command.ExpectedRowVersion, command.Result, command.Foundation, command.Reason);
        var replay = await FindReplayAsync(scope, command.IdempotencyKey, hash, token);
        if (replay is not null)
        {
            AuthorizeVisible(actor, responsible, assignment.PersonId);
            var recovered = await RecoverAsync(replay, obligation, command.ActorUserId, token);
            await transaction.CommitAsync(token); return recovered;
        }
        var requirement = await dbContext.ValidationRequirements
            .FromSqlInterpolated($"SELECT * FROM validation_requirement WHERE id = {route.requirement.Id} FOR UPDATE")
            .SingleAsync(token);
        var current = await dbContext.ValidationDecisionVersions
            .FromSqlInterpolated($"SELECT * FROM validation_decision_version WHERE requirement_id = {requirement.Id} AND status = {ValidationStatuses.Current} FOR UPDATE")
            .SingleOrDefaultAsync(token);
        if (current is null || current.Id != command.DecisionVersionId) throw new ValidationDecisionNotFoundException();
        if (requirement.RowVersion != command.ExpectedRowVersion) throw new ValidationDecisionException("VERSION_CONFLICT");
        var authority = ReplacementAuthority(actor, responsible, assignment.PersonId, current);
        var review = await evidenceReview.ReviewAsync(new(command.ActorUserId, command.CorrelationId, obligation.Id, decidedAt), token);
        if (review.SnapshotId is null) throw new ValidationDecisionException("EVIDENCIA_VALIDACION_NO_DISPONIBLE");
        var evidenceVersionCount = await EvidenceVersionCountAsync(review.SnapshotId.Value, token);
        current.Supersede();
        await dbContext.SaveChangesAsync(token);
        var successor = new ValidationDecisionVersion(uuidGenerator.NewUuid(), requirement.Id, current.VersionNo + 1,
            command.Result, command.Foundation, authority, actor.UserId, actor.PersonId, actor.RoleCode,
            assignment.Id, assignment.PersonId, decidedAt, command.Reason, current.Id, review.SnapshotId.Value);
        requirement.Advance(decidedAt, command.ExpectedRowVersion);
        dbContext.ValidationDecisionVersions.Add(successor);
        AddIdempotency(scope, command.IdempotencyKey, hash, successor.Id, decidedAt);
        AddAudit(command.ActorUserId, command.CorrelationId, command.IdempotencyKey, obligation, requirement, successor, current, evidenceVersionCount);
        await dbContext.SaveChangesAsync(token); await transaction.CommitAsync(token);
        return await MutationAsync(obligation, successor, false, token);
    }

    private async Task<T> ExecuteAsync<T>(Func<Task<T>> operation, CancellationToken token)
    {
        for (var attempt = 1; attempt <= MaximumAttempts; attempt++)
        {
            try { return await operation(); }
            catch (Exception exception) when (Retryable(exception) && attempt < MaximumAttempts) { dbContext.ChangeTracker.Clear(); }
            catch (Exception exception) when (Retryable(exception)) { dbContext.ChangeTracker.Clear(); throw new ValidationConcurrencyException(); }
            catch (EvidenceReviewUnavailableException) { dbContext.ChangeTracker.Clear(); throw new ValidationDecisionException("EVIDENCIA_VALIDACION_NO_DISPONIBLE"); }
            catch { dbContext.ChangeTracker.Clear(); throw; }
        }
        throw new ValidationConcurrencyException();
    }

    private async Task<WorkObligation> LockObligationAsync(Guid id, CancellationToken token) =>
        await dbContext.WorkObligations.FromSqlInterpolated($"SELECT * FROM work_obligation WHERE id = {id} AND branch_id = {BranchScope.LorettaId} FOR UPDATE")
            .SingleOrDefaultAsync(token) ?? throw new ValidationObligationNotFoundException();
    private async Task<AssignmentVersion> LockAssignmentAsync(Guid id, CancellationToken token) =>
        await dbContext.AssignmentVersions.FromSqlInterpolated($"SELECT * FROM assignment_version WHERE obligation_id = {id} AND status = {AssignmentVersionStatuses.Current} FOR UPDATE")
            .AsNoTracking().SingleOrDefaultAsync(token) ?? throw new ValidationObligationNotFoundException();

    private async Task<ValidationPolicyVersion> LoadPolicyAsync(WorkObligation obligation, CancellationToken token)
    {
        if (obligation.ValidationPolicyVersionId is null) throw new ValidationDecisionException("POLITICA_VALIDACION_NO_DISPONIBLE");
        var policy = await dbContext.ValidationPolicyVersions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == obligation.ValidationPolicyVersionId, token)
            ?? throw new ValidationDecisionException("POLITICA_VALIDACION_NO_DISPONIBLE");
        var taskId = await dbContext.TaskDefinitionVersions.AsNoTracking().Where(x => x.Id == obligation.TaskDefinitionVersionId)
            .Select(x => x.TaskDefinitionId).SingleOrDefaultAsync(token);
        var task = TaskDefinitionCatalog.All.SingleOrDefault(x => x.Id == taskId);
        var approved = task is null ? null : ValidationPolicyCatalog.Require(task.TaskCode);
        var allowedResults = policy.AllowedResults.RootElement.EnumerateArray().Select(x => x.GetString()).ToArray();
        if (task is null || approved is null || policy.TaskDefinitionId != taskId || policy.TaskDefinitionVersionId != obligation.TaskDefinitionVersionId ||
            !policy.IsRequired || policy.ValidatorRelation != ValidationPolicyValues.ImmediateSuperior ||
            policy.ExecutorRole != approved.ExecutorRole || policy.ValidatorRole != approved.ValidatorRole ||
            allowedResults.Length != ValidationPolicyValues.AllowedResults.Count ||
            ValidationPolicyValues.AllowedResults.Any(x => !allowedResults.Contains(x, StringComparer.Ordinal)) ||
            policy.Status is not (VersionStatuses.Current or VersionStatuses.Superseded))
            throw new ValidationDecisionException("POLITICA_VALIDACION_NO_DISPONIBLE");
        return policy;
    }

    private async Task<Actor> LoadActorAsync(Guid userId, DateTimeOffset at, CancellationToken token)
    {
        var user = await dbContext.AppUsers.AsNoTracking().SingleOrDefaultAsync(x => x.Id == userId, token);
        if (user is not { Status: AccountStatus.Active, MfaEnrolledAt: not null }) throw new ValidationAccessDeniedException();
        var employment = await dbContext.EmploymentVersions.AsNoTracking().SingleOrDefaultAsync(x => x.PersonId == user.PersonId &&
            x.BranchId == BranchScope.LorettaId && x.Status == EmploymentStatus.Active && x.ValidFrom <= at && (x.ValidTo == null || at < x.ValidTo), token);
        var role = await dbContext.RoleAssignmentVersions.AsNoTracking().SingleOrDefaultAsync(x => x.UserId == user.Id &&
            x.BranchId == BranchScope.LorettaId && x.Status == RoleAssignmentStatus.Active && x.ValidFrom <= at && (x.ValidTo == null || at < x.ValidTo), token);
        if (employment is null || role is null || !CanonicalRole.IsDefined(role.RoleCode)) throw new ValidationAccessDeniedException();
        return new(user.Id, user.PersonId, role.RoleCode);
    }

    private async Task<Actor> LoadResponsibleRoleAsync(Guid personId, DateTimeOffset at, CancellationToken token)
    {
        var user = await dbContext.AppUsers.AsNoTracking().SingleOrDefaultAsync(x => x.PersonId == personId && x.Status == AccountStatus.Active, token)
            ?? throw new ValidationObligationNotFoundException();
        var role = await dbContext.RoleAssignmentVersions.AsNoTracking().SingleOrDefaultAsync(x => x.UserId == user.Id &&
            x.BranchId == BranchScope.LorettaId && x.Status == RoleAssignmentStatus.Active && x.ValidFrom <= at && (x.ValidTo == null || at < x.ValidTo), token)
            ?? throw new ValidationObligationNotFoundException();
        return new(user.Id, personId, role.RoleCode);
    }

    private static string IssueAuthority(Actor actor, Actor responsible, Guid responsiblePersonId,
        ValidationPolicyVersion policy, string? escalationReason)
    {
        var samePerson = actor.PersonId == responsiblePersonId;
        if (samePerson)
        {
            if (!RoleHierarchy.CanSelfValidateAsDirection(actor.RoleCode, samePerson)) throw new ValidationDecisionException("AUTOVALIDACION_NO_PERMITIDA");
            if (escalationReason is not null) throw new ValidationDecisionException("SOLICITUD_VALIDACION_INVALIDA");
            return ValidationAuthorityTypes.DirectionSelfValidation;
        }
        if (responsible.RoleCode != policy.ExecutorRole) throw new ValidationObligationNotFoundException();
        if (escalationReason is not null && actor.RoleCode == policy.ValidatorRole)
            throw new ValidationDecisionException("SOLICITUD_VALIDACION_INVALIDA");
        if (escalationReason is null && RoleHierarchy.CanIssueValidationOrdinarily(actor.RoleCode, responsible.RoleCode,
            policy.ExecutorRole, policy.ValidatorRole, samePerson))
            return ValidationAuthorityTypes.Ordinary;
        if (escalationReason is null && RoleHierarchy.IsStrictlySuperior(actor.RoleCode, policy.ValidatorRole) &&
            RoleHierarchy.IsStrictlySuperior(actor.RoleCode, responsible.RoleCode))
            throw new ValidationDecisionException("MOTIVO_REQUERIDO");
        if (escalationReason is not null && RoleHierarchy.CanEscalateValidation(actor.RoleCode, responsible.RoleCode,
            policy.ValidatorRole, samePerson))
            return ValidationAuthorityTypes.Escalation;
        throw new ValidationObligationNotFoundException();
    }

    private static string ReplacementAuthority(Actor actor, Actor responsible, Guid responsiblePersonId, ValidationDecisionVersion current)
    {
        var sameResponsiblePerson = actor.PersonId == responsiblePersonId;
        if (RoleHierarchy.CanReplaceValidationAsOriginal(actor.RoleCode, responsible.RoleCode, current.ValidatorRole,
            actor.UserId == current.ValidatorUserId, sameResponsiblePerson))
            return ValidationAuthorityTypes.OriginalReplacement;
        if (RoleHierarchy.CanReplaceValidationAsSuperior(actor.RoleCode, responsible.RoleCode, current.ValidatorRole,
            sameResponsiblePerson))
            return ValidationAuthorityTypes.SuperiorReplacement;
        throw new ValidationDecisionNotFoundException();
    }

    private static void AuthorizeVisible(Actor actor, Actor responsible, Guid responsiblePersonId)
    {
        if (!RoleHierarchy.CanAccess(actor.RoleCode, responsible.RoleCode, actor.PersonId == responsiblePersonId))
            throw new ValidationObligationNotFoundException();
    }

    private static void EnsureIssuePermission(Actor actor, string? escalationReason)
    {
        if (escalationReason is null
            ? !RoleHierarchy.GrantsValidationIssue(actor.RoleCode)
            : !RoleHierarchy.GrantsValidationEscalation(actor.RoleCode))
            throw new ValidationAccessDeniedException();
    }

    private async Task<IdempotencyRecord?> FindReplayAsync(string scope, Guid key, string hash, CancellationToken token)
    {
        var record = await dbContext.IdempotencyRecords.AsNoTracking().SingleOrDefaultAsync(x => x.Scope == scope && x.Key == key, token);
        if (record is not null && record.RequestHash != hash) throw new ValidationDecisionException("IDEMPOTENCY_CONFLICT");
        return record;
    }

    private void AddIdempotency(string scope, Guid key, string hash, Guid decisionId, DateTimeOffset at) =>
        dbContext.IdempotencyRecords.Add(new()
        {
            Scope = scope,
            Key = key,
            RequestHash = hash,
            Status = "COMPLETED",
            ResourceType = ResourceType,
            ResourceId = decisionId,
            ResponseCode = 201,
            CreatedAt = at,
            ExpiresAt = DateTimeOffset.MaxValue
        });

    private async Task<int> EvidenceVersionCountAsync(Guid snapshotId, CancellationToken token)
    {
        var local = dbContext.EvidenceReviewSnapshots.Local.SingleOrDefault(x => x.Id == snapshotId);
        if (local is not null) return local.EvidenceVersionIds.Length;
        var ids = await dbContext.EvidenceReviewSnapshots.AsNoTracking().Where(x => x.Id == snapshotId)
            .Select(x => x.EvidenceVersionIds).SingleOrDefaultAsync(token);
        return ids?.Length ?? throw new ValidationDecisionException("EVIDENCIA_VALIDACION_NO_DISPONIBLE");
    }

    private void AddAudit(Guid actorId, Guid correlationId, Guid requestId, WorkObligation obligation,
        ValidationRequirement requirement, ValidationDecisionVersion decision, ValidationDecisionVersion? previous,
        int evidenceVersionCount)
    {
        var action = decision.AuthorityType switch
        {
            ValidationAuthorityTypes.Escalation => "VALIDATION_DECISION_ESCALATED",
            ValidationAuthorityTypes.DirectionSelfValidation => "VALIDATION_DIRECTION_SELF_VALIDATED",
            ValidationAuthorityTypes.OriginalReplacement or ValidationAuthorityTypes.SuperiorReplacement => "VALIDATION_DECISION_REPLACED",
            _ => "VALIDATION_DECISION_ISSUED",
        };
        dbContext.AuditEvents.Add(new AuditEvent
        {
            Id = uuidGenerator.NewUuid(),
            OccurredAt = decision.DecidedAt,
            ActorUserId = actorId,
            ActorType = "USER",
            Action = action,
            ResourceType = "VALIDATION_REQUIREMENT",
            ResourceId = requirement.Id,
            BranchId = obligation.BranchId,
            CorrelationId = correlationId,
            RequestId = requestId.ToString("D"),
            BeforeData = previous is null ? null : JsonSerializer.SerializeToDocument(new
            { schemaVersion = 1, decisionVersionId = previous.Id, previous.Status, requirement.RowVersion }),
            AfterData = JsonSerializer.SerializeToDocument(new
            {
                schemaVersion = 1,
                obligationId = obligation.Id,
                requirementId = requirement.Id,
                policyVersionId = requirement.PolicyVersionId,
                decisionVersionId = decision.Id,
                decision.VersionNo,
                decision.Result,
                decision.Status,
                decision.AuthorityType,
                decision.ValidatorRole,
                decision.AssignmentVersionId,
                decision.ResponsiblePersonId,
                decision.EvidenceReviewSnapshotId,
                evidenceVersionCount,
                requirement.RowVersion,
                decidedAt = decision.DecidedAt.UtcDateTime,
                selfValidation = decision.AuthorityType == ValidationAuthorityTypes.DirectionSelfValidation
            }),
            Outcome = "SUCCESS"
        });
    }

    private async Task<ValidationMutationResult> RecoverAsync(IdempotencyRecord replay, WorkObligation obligation, Guid actorId, CancellationToken token)
    {
        if (replay.ResourceType != ResourceType || replay.ResponseCode != 201) throw new ValidationDecisionException("VALIDACION_INCONSISTENTE");
        var decision = await dbContext.ValidationDecisionVersions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == replay.ResourceId, token)
            ?? throw new ValidationDecisionException("VALIDACION_INCONSISTENTE");
        if (decision.ValidatorUserId != actorId) throw new ValidationObligationNotFoundException();
        return await MutationAsync(obligation, decision, true, token);
    }

    private async Task<ValidationMutationResult> MutationAsync(WorkObligation obligation, ValidationDecisionVersion decision, bool replayed, CancellationToken token)
    {
        var history = await ProjectAsync(obligation, token);
        var projected = history.Decisions.Single(x => x.DecisionVersionId == decision.Id);
        return new(history, projected, replayed);
    }

    private async Task<ValidationHistoryDetails> ProjectAsync(WorkObligation obligation, CancellationToken token)
    {
        var requirement = await dbContext.ValidationRequirements.AsNoTracking().SingleOrDefaultAsync(x => x.ObligationId == obligation.Id, token);
        if (requirement is null) return new(obligation.Id, obligation.ExecutionStatus, obligation.RowVersion, null, []);
        var decisions = await dbContext.ValidationDecisionVersions.AsNoTracking().Where(x => x.RequirementId == requirement.Id)
            .OrderByDescending(x => x.VersionNo).ThenBy(x => x.Id).ToListAsync(token);
        var snapshotIds = decisions.Select(x => x.EvidenceReviewSnapshotId).Distinct().ToArray();
        var snapshots = await dbContext.EvidenceReviewSnapshots.AsNoTracking().Where(x => snapshotIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, token);
        var details = decisions.Select(x => new ValidationDecisionDetails(x.Id, x.VersionNo, x.Result, x.Foundation, x.Status,
            x.AuthorityType, x.ValidatorUserId, x.ValidatorRole, x.DecidedAt, x.Reason, x.SupersedesId,
            x.EvidenceReviewSnapshotId, snapshots[x.EvidenceReviewSnapshotId].EvidenceVersionIds)).ToArray();
        return new(obligation.Id, obligation.ExecutionStatus, requirement.RowVersion,
            new(requirement.Id, requirement.PolicyVersionId, requirement.Status, requirement.CreatedAt, requirement.ResolvedAt, requirement.RowVersion), details);
    }

    private static void Validate(Guid actor, Guid key, Guid correlation, Guid resource, long version)
    {
        if (actor == Guid.Empty || key == Guid.Empty || correlation == Guid.Empty || resource == Guid.Empty || version < 1)
            throw new ArgumentException("Validation identifiers and expected version are required.");
    }

    private static string Hash(string method, string path, long version, params string?[] values)
    {
        var input = string.Join('\n', new[] { method, path, version.ToString(CultureInfo.InvariantCulture) }.Concat(values.Select(x => x ?? "null")));
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(input)));
    }

    private static bool Retryable(Exception exception)
    {
        var postgres = FindPostgresException(exception);
        return postgres?.SqlState is PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.DeadlockDetected ||
            exception is DbUpdateConcurrencyException || postgres?.SqlState == PostgresErrorCodes.UniqueViolation &&
            postgres.ConstraintName is ValidationRequirementConfiguration.ObligationIndex or ValidationDecisionVersionConfiguration.CurrentIndex or "PK_idempotency_record";
    }

    private static PostgresException? FindPostgresException(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is PostgresException postgres) return postgres;
        }
        return null;
    }

    private sealed record Actor(Guid UserId, Guid PersonId, string RoleCode);
}
