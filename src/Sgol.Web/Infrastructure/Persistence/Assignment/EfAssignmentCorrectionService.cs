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
using Sgol.Generation.Contracts;
using Sgol.Identity.Contracts;
using Sgol.Organization.Contracts;
using Sgol.Notifications.Contracts;
using Sgol.Web.Infrastructure.Persistence.Auditing;
using Sgol.Web.Infrastructure.Persistence.Bootstrap;
using Sgol.Web.Infrastructure.Persistence.Idempotency;

namespace Sgol.Web.Infrastructure.Persistence.Assignment;

public sealed class EfAssignmentCorrectionService(
    SgolDbContext dbContext,
    AuditTransaction auditTransaction,
    IClock clock,
    IUuidGenerator uuidGenerator,
    IInternalNoticeWriter noticeWriter) : IAssignmentCorrectionService
{
    private const string ScopePrefix = "assignment:correction";
    private const string AssignmentResource = "ASSIGNMENT_VERSION";
    private const string ErrorResourcePrefix = "ASSIGNMENT_CORRECTION_ERROR:";
    private const string IdempotencyPrimaryKey = "PK_idempotency_record";
    private const string AuditPrimaryKey = "PK_audit_event";
    private const int MaximumAttempts = 3;

    public async Task<AssignmentCorrectionResult> CorrectAsync(
        CorrectAssignmentCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.ActorUserId == Guid.Empty || command.IdempotencyKey == Guid.Empty ||
            command.CorrelationId == Guid.Empty || command.ObligationId == Guid.Empty ||
            command.NewResponsiblePersonId == Guid.Empty || command.EligibilityEvaluationId == Guid.Empty ||
            command.ExpectedRowVersion < 1)
        {
            throw new ArgumentException("Assignment-correction identifiers and version are required.", nameof(command));
        }

        var normalized = command with { Reason = AssignmentCorrectionReason.Normalize(command.Reason) };
        await EnsureAuthorizedBeforeIdempotencyAsync(normalized, cancellationToken);
        const string operation = "ASSIGNMENT_CORRECTION_CREATE";
        var resource = normalized.ObligationId.ToString("D");
        var scope = IdempotencyProtocol.Scope(normalized.ActorUserId.ToString("D"), operation, resource);
        var requestHash = IdempotencyProtocol.HashCanonical(
            operation,
            normalized.ActorUserId.ToString("D"),
            resource,
            new
            {
                normalized.ObligationId,
                normalized.NewResponsiblePersonId,
                normalized.EligibilityEvaluationId,
                normalized.Reason,
            },
            normalized.ExpectedRowVersion);
        var legacyScope = LegacyScope(normalized.ActorUserId, normalized.ObligationId);
        var legacyRequestHash = LegacyHash(normalized);

        for (var attempt = 1; attempt <= MaximumAttempts; attempt++)
        {
            AssignmentCorrectionResult? result = null;
            AssignmentCorrectionException? rejection = null;
            try
            {
                await auditTransaction.ExecuteAsync(
                    IsolationLevel.ReadCommitted,
                    async token =>
                    {
                        var obligation = await dbContext.WorkObligations
                            .FromSqlInterpolated($"SELECT * FROM work_obligation WHERE id = {normalized.ObligationId} FOR UPDATE")
                            .AsTracking()
                            .SingleOrDefaultAsync(token);

                        var replay = await dbContext.IdempotencyRecords.AsNoTracking()
                            .SingleOrDefaultAsync(record =>
                                record.Scope == scope && record.Key == normalized.IdempotencyKey,
                                token)
                            ?? await dbContext.IdempotencyRecords.AsNoTracking()
                                .SingleOrDefaultAsync(record =>
                                    record.Scope == legacyScope && record.Key == normalized.IdempotencyKey,
                                    token);
                        if (replay is not null)
                        {
                            var expectedHash = replay.ProtocolVersion == IdempotencyProtocol.CurrentVersion
                                ? requestHash
                                : legacyRequestHash;
                            if (!string.Equals(replay.RequestHash, expectedHash, StringComparison.Ordinal))
                            {
                                rejection = new AssignmentCorrectionIdempotencyConflictException();
                                return IdempotencyConflictAudit(normalized, obligation?.BranchId, clock.UtcNow);
                            }

                            if (replay.ResourceType.StartsWith(ErrorResourcePrefix, StringComparison.Ordinal))
                            {
                                var errorCode = replay.ProtocolVersion == IdempotencyProtocol.CurrentVersion
                                    ? IdempotencyProtocol.ReadPayload<RejectionSnapshot>(replay).ErrorCode
                                    : replay.ResourceType[ErrorResourcePrefix.Length..];
                                rejection = ExceptionFor(errorCode, replay.ResponseCode);
                                return RejectedAudit(normalized, obligation?.BranchId, rejection.ErrorCode, clock.UtcNow);
                            }

                            result = replay.ProtocolVersion == IdempotencyProtocol.CurrentVersion
                                ? IdempotencyProtocol.ReadPayload<AssignmentCorrectionResult>(replay)
                                    with
                                { Result = AssignmentCorrectionResults.Recovered }
                                : await ReplayAsync(normalized, replay, token);
                            return RecoveredAudit(normalized, obligation?.BranchId, result, clock.UtcNow);
                        }

                        if (obligation is null || obligation.BranchId != BranchScope.LorettaId)
                        {
                            rejection = new AssignmentCorrectionObligationNotFoundException();
                            return NewRejection(normalized, scope, requestHash, obligation?.BranchId, rejection, clock.UtcNow);
                        }

                        if (obligation.ExecutionStatus != WorkObligationStatuses.Pending)
                        {
                            rejection = new AssignmentCorrectionObligationNotCorrectableException();
                            return NewRejection(normalized, scope, requestHash, obligation.BranchId, rejection, clock.UtcNow);
                        }

                        if (obligation.RowVersion != normalized.ExpectedRowVersion)
                        {
                            rejection = new AssignmentCorrectionVersionConflictException();
                            return NewRejection(normalized, scope, requestHash, obligation.BranchId, rejection, clock.UtcNow);
                        }

                        var current = await dbContext.AssignmentVersions
                            .FromSqlInterpolated($"SELECT * FROM assignment_version WHERE obligation_id = {obligation.Id} AND status = {AssignmentVersionStatuses.Current} FOR UPDATE")
                            .AsTracking()
                            .SingleOrDefaultAsync(token);
                        if (current is null)
                        {
                            rejection = new AssignmentCorrectionCurrentAssignmentNotFoundException();
                            return NewRejection(normalized, scope, requestHash, obligation.BranchId, rejection, clock.UtcNow);
                        }

                        var evaluation = await dbContext.EligibilityEvaluations.AsNoTracking()
                            .SingleOrDefaultAsync(item => item.Id == normalized.EligibilityEvaluationId, token);
                        if (evaluation is null)
                        {
                            rejection = new AssignmentCorrectionEvaluationNotFoundException();
                            return NewRejection(normalized, scope, requestHash, obligation.BranchId, rejection, clock.UtcNow);
                        }

                        if (evaluation.ObligationId != obligation.Id)
                        {
                            rejection = new AssignmentCorrectionEvaluationMismatchException();
                            return NewRejection(normalized, scope, requestHash, obligation.BranchId, rejection, clock.UtcNow);
                        }

                        var latestEvaluationId = await dbContext.EligibilityEvaluations.AsNoTracking()
                            .Where(item => item.ObligationId == obligation.Id)
                            .OrderByDescending(item => item.EvaluatedAt)
                            .ThenByDescending(item => item.Id)
                            .Select(item => item.Id)
                            .FirstAsync(token);
                        if (latestEvaluationId != evaluation.Id)
                        {
                            rejection = new AssignmentCorrectionEvaluationStaleException();
                            return NewRejection(normalized, scope, requestHash, obligation.BranchId, rejection, clock.UtcNow);
                        }

                        var policy = await dbContext.EligibilityPolicyVersions.AsNoTracking()
                            .SingleOrDefaultAsync(item => item.Id == evaluation.PolicyVersionId, token);
                        if (policy is null || policy.TaskDefinitionVersionId != obligation.TaskDefinitionVersionId)
                        {
                            rejection = new AssignmentCorrectionEvaluationMismatchException();
                            return NewRejection(normalized, scope, requestHash, obligation.BranchId, rejection, clock.UtcNow);
                        }

                        if (evaluation.Result != EligibilityResults.EligibleCandidates)
                        {
                            rejection = new AssignmentCorrectionEvaluationIncompatibleException();
                            return NewRejection(normalized, scope, requestHash, obligation.BranchId, rejection, clock.UtcNow);
                        }

                        var snapshotCandidate = await dbContext.EligibilityCandidates.AsNoTracking()
                            .SingleOrDefaultAsync(item =>
                                item.EvaluationId == evaluation.Id &&
                                item.PersonId == normalized.NewResponsiblePersonId,
                                token);
                        if (snapshotCandidate is not { IsEligible: true })
                        {
                            rejection = new AssignmentCorrectionResponsibleIneligibleException();
                            return NewRejection(normalized, scope, requestHash, obligation.BranchId, rejection, clock.UtcNow);
                        }

                        var actorUserSeed = await dbContext.AppUsers.AsNoTracking()
                            .SingleOrDefaultAsync(item => item.Id == normalized.ActorUserId, token);
                        var candidateUserSeed = await dbContext.AppUsers.AsNoTracking()
                            .SingleOrDefaultAsync(item => item.PersonId == normalized.NewResponsiblePersonId, token);
                        if (actorUserSeed is null)
                        {
                            rejection = new AssignmentCorrectionAccessDeniedException();
                            return NewRejection(normalized, scope, requestHash, obligation.BranchId, rejection, clock.UtcNow);
                        }

                        if (candidateUserSeed is null)
                        {
                            rejection = new AssignmentCorrectionResponsibleIneligibleException();
                            return NewRejection(normalized, scope, requestHash, obligation.BranchId, rejection, clock.UtcNow);
                        }

                        foreach (var personId in new[] { actorUserSeed.PersonId, normalized.NewResponsiblePersonId }.Distinct().Order())
                        {
                            var person = await dbContext.People
                                .FromSqlInterpolated($"SELECT * FROM person WHERE id = {personId} FOR UPDATE")
                                .AsNoTracking()
                                .SingleOrDefaultAsync(token);
                            if (person is null)
                            {
                                rejection = personId == actorUserSeed.PersonId
                                    ? new AssignmentCorrectionAccessDeniedException()
                                    : new AssignmentCorrectionResponsibleIneligibleException();
                                return NewRejection(normalized, scope, requestHash, obligation.BranchId, rejection, clock.UtcNow);
                            }
                        }

                        var lockedUsers = new Dictionary<Guid, AppUser>();
                        foreach (var userId in new[] { actorUserSeed.Id, candidateUserSeed.Id }.Distinct().Order())
                        {
                            var user = await dbContext.AppUsers
                                .FromSqlInterpolated($"SELECT * FROM app_user WHERE id = {userId} FOR UPDATE")
                                .AsNoTracking()
                                .SingleAsync(token);
                            lockedUsers.Add(user.Id, user);
                        }

                        var actorEmployment = await LockEmploymentAsync(actorUserSeed.PersonId, token);
                        var candidateEmployment = await LockEmploymentAsync(normalized.NewResponsiblePersonId, token);
                        var actorRole = await LockRoleAsync(actorUserSeed.Id, token);
                        var candidateRole = await LockRoleAsync(candidateUserSeed.Id, token);
                        var availability = await LockAvailabilityAsync(
                            normalized.NewResponsiblePersonId, evaluation.EligibilityDate, token);
                        var correctedAt = clock.UtcNow;

                        var actorAuthorized = lockedUsers[actorUserSeed.Id].Status == AccountStatus.Active &&
                            IsCurrent(actorEmployment, correctedAt) &&
                            IsCurrent(actorRole, correctedAt) &&
                            RoleHierarchy.GrantsAssignmentCorrection(actorRole!.RoleCode) &&
                            RoleHierarchy.IsStrictlySuperior(actorRole.RoleCode, policy.RequiredRole);
                        if (!actorAuthorized)
                        {
                            rejection = new AssignmentCorrectionAccessDeniedException();
                            return NewRejection(normalized, scope, requestHash, obligation.BranchId, rejection, correctedAt);
                        }

                        var candidateInput = new EligibilityPersonInput(
                            candidateEmployment is not null,
                            IsCurrent(candidateEmployment, correctedAt),
                            candidateEmployment?.BranchId == BranchScope.LorettaId,
                            IsCurrent(candidateRole, correctedAt),
                            candidateRole?.RoleCode,
                            availability is { Status: AvailabilityVersionStatus.Current } ? availability.IsAvailable : null,
                            candidateEmployment?.ShiftText);
                        var candidateEligible = lockedUsers[candidateUserSeed.Id].Status == AccountStatus.Active &&
                            EligibilityEvaluator.Explain(candidateInput, policy.RequiredRole, policy.RequiredShift).Count == 0;
                        if (!candidateEligible)
                        {
                            rejection = new AssignmentCorrectionResponsibleIneligibleException();
                            return NewRejection(normalized, scope, requestHash, obligation.BranchId, rejection, correctedAt);
                        }

                        if (current.PersonId == normalized.NewResponsiblePersonId)
                        {
                            rejection = new AssignmentCorrectionNoChangeException();
                            return NewRejection(normalized, scope, requestHash, obligation.BranchId, rejection, correctedAt);
                        }

                        current.Supersede();
                        await dbContext.SaveChangesAsync(token);

                        obligation.AdvanceRowVersion(normalized.ExpectedRowVersion);
                        var assignmentId = uuidGenerator.NewUuid();
                        var successor = new AssignmentVersion(
                            assignmentId,
                            obligation.Id,
                            normalized.NewResponsiblePersonId,
                            AssignmentVersionStatuses.Current,
                            AssignmentTypes.Correction,
                            Explanation(
                                normalized,
                                current,
                                evaluation,
                                policy,
                                actorEmployment!,
                                actorRole!,
                                candidateEmployment!,
                                candidateRole!,
                                availability!,
                                correctedAt),
                            correctedAt,
                            normalized.Reason,
                            normalized.ActorUserId,
                            current.Id);
                        dbContext.AssignmentVersions.Add(successor);
                        await noticeWriter.AddAssignmentNoticeAsync(successor.Id, successor.PersonId, correctedAt, token);
                        result = Result(normalized, successor, current.Id, obligation.RowVersion, AssignmentCorrectionResults.Created);
                        AddIdempotency(
                            scope, normalized, requestHash, AssignmentResource, assignmentId, 201,
                            result, correctedAt,
                            $"/api/v1/obligations/{normalized.ObligationId:D}/assignment-corrections");
                        return CreatedAudit(normalized, obligation.BranchId, current, result, correctedAt);
                    },
                    cancellationToken);

                dbContext.ChangeTracker.Clear();
                if (rejection is not null)
                {
                    throw rejection;
                }

                var completed = result ?? throw new InvalidOperationException("Assignment correction produced no result.");
                if (completed.Result == AssignmentCorrectionResults.Created)
                    noticeWriter.RecordAssignmentNoticeCommitted();
                return completed;
            }
            catch (Exception exception) when (rejection is AssignmentCorrectionIdempotencyConflictException &&
                exception is not AssignmentCorrectionIdempotencyConflictException)
            {
                dbContext.ChangeTracker.Clear();
                throw new IdempotencyConflictAuditException(exception);
            }
            catch (Exception exception) when (IsRetryable(exception) && attempt < MaximumAttempts)
            {
                dbContext.ChangeTracker.Clear();
                await IdempotencyProtocol.DelayBeforeRetryAsync(
                    "ASSIGNMENT_CORRECTION_CREATE", exception, attempt, cancellationToken);
            }
            catch (Exception exception) when (IsRetryable(exception))
            {
                dbContext.ChangeTracker.Clear();
                await PersistTerminalRejectionAsync(
                    normalized, scope, requestHash, AssignmentCorrectionErrors.ConcurrencyConflict,
                    cancellationToken);
                throw new AssignmentCorrectionConcurrencyConflictException();
            }
            catch (DbUpdateException exception) when (IsUniqueViolation(exception))
            {
                dbContext.ChangeTracker.Clear();
                await PersistTerminalRejectionAsync(
                    normalized, scope, requestHash, AssignmentCorrectionErrors.IntegrityConflict,
                    cancellationToken);
                throw new AssignmentCorrectionIntegrityConflictException();
            }
            catch (DbUpdateConcurrencyException)
            {
                dbContext.ChangeTracker.Clear();
                throw new AssignmentCorrectionVersionConflictException();
            }
            catch
            {
                dbContext.ChangeTracker.Clear();
                throw;
            }
        }

        throw new AssignmentCorrectionConcurrencyConflictException();
    }

    private async Task<EmploymentVersion?> LockEmploymentAsync(Guid personId, CancellationToken token) =>
        await dbContext.EmploymentVersions
            .FromSqlInterpolated($"SELECT * FROM employment_version WHERE person_id = {personId} AND branch_id = {BranchScope.LorettaId} AND valid_to IS NULL FOR UPDATE")
            .AsNoTracking()
            .SingleOrDefaultAsync(token);

    private async Task EnsureAuthorizedBeforeIdempotencyAsync(
        CorrectAssignmentCommand command,
        CancellationToken token)
    {
        var roleCode = await (
            from user in dbContext.AppUsers.AsNoTracking()
            join employment in dbContext.EmploymentVersions.AsNoTracking() on user.PersonId equals employment.PersonId
            join role in dbContext.RoleAssignmentVersions.AsNoTracking() on user.Id equals role.UserId
            where user.Id == command.ActorUserId &&
                user.Status == AccountStatus.Active &&
                employment.BranchId == BranchScope.LorettaId &&
                employment.Status == EmploymentStatus.Active &&
                employment.ValidTo == null &&
                role.BranchId == BranchScope.LorettaId &&
                role.Status == RoleAssignmentStatus.Active &&
                role.ValidTo == null
            select role.RoleCode)
            .SingleOrDefaultAsync(token);
        if (roleCode is not null && RoleHierarchy.GrantsAssignmentCorrection(roleCode))
        {
            return;
        }

        await auditTransaction.ExecuteAsync(
            RejectedAudit(command, BranchScope.LorettaId, AssignmentCorrectionErrors.AccessDenied, clock.UtcNow),
            _ => Task.CompletedTask,
            token);
        dbContext.ChangeTracker.Clear();
        throw new AssignmentCorrectionAccessDeniedException();
    }

    private async Task<RoleAssignmentVersion?> LockRoleAsync(Guid userId, CancellationToken token) =>
        await dbContext.RoleAssignmentVersions
            .FromSqlInterpolated($"SELECT * FROM role_assignment_version WHERE user_id = {userId} AND branch_id = {BranchScope.LorettaId} AND valid_to IS NULL FOR UPDATE")
            .AsNoTracking()
            .SingleOrDefaultAsync(token);

    private async Task<AvailabilityDayVersion?> LockAvailabilityAsync(
        Guid personId,
        DateOnly date,
        CancellationToken token) =>
        await dbContext.AvailabilityDayVersions
            .FromSqlInterpolated($"SELECT * FROM availability_day_version WHERE person_id = {personId} AND branch_id = {BranchScope.LorettaId} AND local_date = {date} AND status = {AvailabilityVersionStatus.Current} FOR UPDATE")
            .AsNoTracking()
            .SingleOrDefaultAsync(token);

    private static bool IsCurrent(EmploymentVersion? employment, DateTimeOffset at) =>
        employment is { Status: EmploymentStatus.Active } &&
        employment.BranchId == BranchScope.LorettaId &&
        employment.ValidFrom <= at && (employment.ValidTo is null || at < employment.ValidTo);

    private static bool IsCurrent(RoleAssignmentVersion? role, DateTimeOffset at) =>
        role is { Status: RoleAssignmentStatus.Active } &&
        role.BranchId == BranchScope.LorettaId && role.ValidFrom <= at &&
        (role.ValidTo is null || at < role.ValidTo);

    private async Task<AssignmentCorrectionResult> ReplayAsync(
        CorrectAssignmentCommand command,
        IdempotencyRecord record,
        CancellationToken token)
    {
        if (record.ResourceType != AssignmentResource)
        {
            throw new InvalidOperationException("The assignment-correction idempotency record is invalid.");
        }

        var assignment = await dbContext.AssignmentVersions.AsNoTracking()
            .SingleAsync(item => item.Id == record.ResourceId, token);
        var creationAudit = await dbContext.AuditEvents.AsNoTracking()
            .Where(item => item.Action == "ASSIGNMENT_CORRECTED" &&
                item.RequestId == command.IdempotencyKey.ToString("D") && item.ResourceId == assignment.Id)
            .SingleAsync(token);
        var rowVersion = creationAudit.AfterData!.RootElement.GetProperty("rowVersion").GetInt64();
        return Result(command, assignment, assignment.SupersedesId!.Value, rowVersion, AssignmentCorrectionResults.Recovered);
    }

    private static AssignmentCorrectionResult Result(
        CorrectAssignmentCommand command,
        AssignmentVersion assignment,
        Guid previousAssignmentId,
        long rowVersion,
        string result) => new(
            result,
            assignment.Id,
            assignment.ObligationId,
            previousAssignmentId,
            assignment.PersonId,
            assignment.Status,
            assignment.AssignmentType,
            assignment.Reason!,
            assignment.AssignedBy!.Value,
            assignment.AssignedAt,
            assignment.SupersedesId!.Value,
            rowVersion);

    private static JsonDocument Explanation(
        CorrectAssignmentCommand command,
        AssignmentVersion previous,
        EligibilityEvaluation evaluation,
        EligibilityPolicyVersion policy,
        EmploymentVersion actorEmployment,
        RoleAssignmentVersion actorRole,
        EmploymentVersion candidateEmployment,
        RoleAssignmentVersion candidateRole,
        AvailabilityDayVersion availability,
        DateTimeOffset correctedAt) =>
        JsonSerializer.SerializeToDocument(new
        {
            schemaVersion = 1,
            operation = "ASSIGNMENT_CORRECTION",
            obligationId = command.ObligationId,
            supersededAssignmentId = previous.Id,
            previousResponsiblePersonId = previous.PersonId,
            newResponsiblePersonId = command.NewResponsiblePersonId,
            actorUserId = command.ActorUserId,
            correctedAt,
            reasonField = "assignment_version.reason",
            eligibilityEvaluationId = evaluation.Id,
            eligibilityPolicyVersionId = policy.Id,
            actorEmploymentVersionId = actorEmployment.Id,
            actorRoleAssignmentVersionId = actorRole.Id,
            candidateEmploymentVersionId = candidateEmployment.Id,
            candidateRoleAssignmentVersionId = candidateRole.Id,
            candidateAvailabilityVersionId = availability.Id,
            actorRoleCode = actorRole.RoleCode,
            targetRoleCode = policy.RequiredRole,
            hierarchyResult = "AUTHORIZED_STRICTLY_SUPERIOR",
        }, JsonSerializerOptions.Web);

    private AuditEvent NewRejection(
        CorrectAssignmentCommand command,
        string scope,
        string requestHash,
        Guid? branchId,
        AssignmentCorrectionException exception,
        DateTimeOffset occurredAt)
        => RejectedAudit(command, branchId, exception.ErrorCode, occurredAt);

    private void AddIdempotency(
        string scope,
        CorrectAssignmentCommand command,
        string requestHash,
        string resourceType,
        Guid resourceId,
        int responseCode,
        object responsePayload,
        DateTimeOffset now,
        string? responseLocation = null) =>
        dbContext.IdempotencyRecords.Add(IdempotencyProtocol.Completed(
            scope,
            command.IdempotencyKey,
            requestHash,
            resourceType,
            resourceId,
            responseCode,
            responsePayload,
            now,
            DateTimeOffset.MaxValue,
            responseLocation: responseLocation));

    private AuditEvent CreatedAudit(
        CorrectAssignmentCommand command,
        Guid branchId,
        AssignmentVersion previous,
        AssignmentCorrectionResult result,
        DateTimeOffset at) => new()
        {
            Id = uuidGenerator.NewUuid(),
            OccurredAt = at,
            ActorUserId = command.ActorUserId,
            ActorType = "USER",
            Action = "ASSIGNMENT_CORRECTED",
            ResourceType = AssignmentResource,
            ResourceId = result.AssignmentId,
            BranchId = branchId,
            CorrelationId = command.CorrelationId,
            RequestId = command.IdempotencyKey.ToString("D"),
            Reason = command.Reason,
            BeforeData = JsonSerializer.SerializeToDocument(new
            {
                schemaVersion = 1,
                assignmentId = previous.Id,
                obligationId = previous.ObligationId,
                personId = previous.PersonId,
                status = AssignmentVersionStatuses.Current,
                assignmentType = previous.AssignmentType,
            }, JsonSerializerOptions.Web),
            AfterData = JsonSerializer.SerializeToDocument(new
            {
                schemaVersion = 1,
                assignmentId = result.AssignmentId,
                obligationId = result.ObligationId,
                personId = result.NewResponsiblePersonId,
                status = result.Status,
                assignmentType = result.AssignmentType,
                supersedesId = result.SupersedesId,
                eligibilityEvaluationId = command.EligibilityEvaluationId,
                rowVersion = result.RowVersion,
            }, JsonSerializerOptions.Web),
            Outcome = AssignmentCorrectionResults.Created,
        };

    private AuditEvent RecoveredAudit(
        CorrectAssignmentCommand command,
        Guid? branchId,
        AssignmentCorrectionResult result,
        DateTimeOffset at) => new()
        {
            Id = uuidGenerator.NewUuid(),
            OccurredAt = at,
            ActorUserId = command.ActorUserId,
            ActorType = "USER",
            Action = "ASSIGNMENT_CORRECTION_RECOVERED",
            ResourceType = AssignmentResource,
            ResourceId = result.AssignmentId,
            BranchId = branchId,
            CorrelationId = command.CorrelationId,
            RequestId = command.IdempotencyKey.ToString("D"),
            AfterData = JsonSerializer.SerializeToDocument(new
            {
                schemaVersion = 1,
                obligationId = result.ObligationId,
                assignmentId = result.AssignmentId,
                result = AssignmentCorrectionResults.Recovered,
                rowVersion = result.RowVersion,
            }, JsonSerializerOptions.Web),
            Outcome = AssignmentCorrectionResults.Recovered,
        };

    private AuditEvent RejectedAudit(
        CorrectAssignmentCommand command,
        Guid? branchId,
        string errorCode,
        DateTimeOffset at) => new()
        {
            Id = uuidGenerator.NewUuid(),
            OccurredAt = at,
            ActorUserId = command.ActorUserId,
            ActorType = "USER",
            Action = "ASSIGNMENT_CORRECTION_REJECTED",
            ResourceType = "WORK_OBLIGATION",
            ResourceId = command.ObligationId,
            BranchId = branchId,
            CorrelationId = command.CorrelationId,
            RequestId = command.IdempotencyKey.ToString("D"),
            AfterData = JsonSerializer.SerializeToDocument(new
            {
                schemaVersion = 1,
                obligationId = command.ObligationId,
                errorCode,
            }, JsonSerializerOptions.Web),
            Outcome = errorCode,
        };

    private AuditEvent IdempotencyConflictAudit(
        CorrectAssignmentCommand command,
        Guid? branchId,
        DateTimeOffset at) => IdempotencyProtocol.ConflictAudit(
            uuidGenerator.NewUuid(), at, command.ActorUserId, "WORK_OBLIGATION", command.ObligationId,
            branchId ?? BranchScope.LorettaId, command.CorrelationId, command.IdempotencyKey,
            "ASSIGNMENT_CORRECTION_CREATE");

    private async Task PersistTerminalRejectionAsync(
        CorrectAssignmentCommand command,
        string scope,
        string requestHash,
        string errorCode,
        CancellationToken token)
    {
        await auditTransaction.ExecuteAsync(async innerToken =>
        {
            var now = clock.UtcNow;
            var branch = await dbContext.WorkObligations.AsNoTracking()
                .Where(item => item.Id == command.ObligationId)
                .Select(item => (Guid?)item.BranchId)
                .SingleOrDefaultAsync(innerToken);
            return RejectedAudit(command, branch, errorCode, now);
        }, token);
    }

    private static int HttpStatus(AssignmentCorrectionException exception) => exception switch
    {
        AssignmentCorrectionAccessDeniedException => 403,
        AssignmentCorrectionObligationNotFoundException or AssignmentCorrectionEvaluationNotFoundException => 404,
        AssignmentCorrectionVersionConflictException => 412,
        AssignmentCorrectionObligationNotCorrectableException or
            AssignmentCorrectionResponsibleIneligibleException or AssignmentCorrectionNoChangeException or
            AssignmentCorrectionEvaluationMismatchException => 422,
        _ => 409,
    };

    private static AssignmentCorrectionException ExceptionFor(string code, int responseCode) => code switch
    {
        AssignmentCorrectionErrors.AccessDenied => new AssignmentCorrectionAccessDeniedException(),
        AssignmentCorrectionErrors.ObligationNotFound => new AssignmentCorrectionObligationNotFoundException(),
        AssignmentCorrectionErrors.ObligationNotCorrectable => new AssignmentCorrectionObligationNotCorrectableException(),
        AssignmentCorrectionErrors.VersionConflict => new AssignmentCorrectionVersionConflictException(),
        AssignmentCorrectionErrors.CurrentAssignmentNotFound => new AssignmentCorrectionCurrentAssignmentNotFoundException(),
        AssignmentCorrectionErrors.EvaluationNotFound => new AssignmentCorrectionEvaluationNotFoundException(),
        AssignmentCorrectionErrors.EvaluationStale => new AssignmentCorrectionEvaluationStaleException(),
        AssignmentCorrectionErrors.EvaluationIncompatible when responseCode == 422 =>
            new AssignmentCorrectionEvaluationMismatchException(),
        AssignmentCorrectionErrors.EvaluationIncompatible => new AssignmentCorrectionEvaluationIncompatibleException(),
        AssignmentCorrectionErrors.ResponsibleIneligible => new AssignmentCorrectionResponsibleIneligibleException(),
        AssignmentCorrectionErrors.NoChange => new AssignmentCorrectionNoChangeException(),
        AssignmentCorrectionErrors.ConcurrencyConflict => new AssignmentCorrectionConcurrencyConflictException(),
        AssignmentCorrectionErrors.IntegrityConflict => new AssignmentCorrectionIntegrityConflictException(),
        _ => throw new InvalidOperationException("Unknown assignment-correction idempotent error."),
    };

    private static string LegacyScope(Guid actor, Guid obligation) =>
        $"{ScopePrefix}:{actor:D}:{obligation:D}";

    private static string LegacyHash(CorrectAssignmentCommand command)
    {
        var value = string.Join('\n',
            command.ObligationId.ToString("D"),
            command.NewResponsiblePersonId.ToString("D"),
            command.EligibilityEvaluationId.ToString("D"),
            command.Reason,
            command.ExpectedRowVersion.ToString(CultureInfo.InvariantCulture));
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    }

    private sealed record RejectionSnapshot(string ErrorCode);

    private static bool IsRetryable(Exception exception)
    {
        var postgres = exception switch
        {
            PostgresException direct => direct,
            DbUpdateException { InnerException: PostgresException inner } => inner,
            _ => null,
        };
        return postgres?.SqlState is "40P01" or "40001" ||
            (postgres?.SqlState == PostgresErrorCodes.UniqueViolation &&
             postgres.ConstraintName is IdempotencyPrimaryKey or
                 AssignmentVersionConfiguration.CurrentObligationIndex or
                 AssignmentVersionConfiguration.SupersedesIndex);
    }

    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: not AuditPrimaryKey,
        };
}
