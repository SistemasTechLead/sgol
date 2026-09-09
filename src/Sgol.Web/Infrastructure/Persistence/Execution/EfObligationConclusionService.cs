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
using Sgol.Evidence.Contracts;
using Sgol.Execution.Contracts;
using Sgol.Generation.Contracts;
using Sgol.Identity.Contracts;
using Sgol.Organization.Contracts;
using Sgol.Web.Infrastructure.Persistence.Auditing;
using Sgol.Web.Infrastructure.Persistence.Bootstrap;

namespace Sgol.Web.Infrastructure.Persistence.Execution;

public sealed class EfObligationConclusionService(
    SgolDbContext dbContext,
    IEvidenceConclusionReviewService evidenceReview,
    IClock clock,
    IUuidGenerator uuidGenerator) : IObligationConclusionService
{
    private const string ScopePrefix = "obligation:conclusion";
    private const string ResourceType = "EXECUTION_RESULT";
    private const int MaximumAttempts = 3;

    public async Task<ObligationConclusionResult> ConcludeAsync(
        ConcludeObligationCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.ActorUserId == Guid.Empty || command.IdempotencyKey == Guid.Empty ||
            command.CorrelationId == Guid.Empty || command.ObligationId == Guid.Empty ||
            command.ExpectedRowVersion < 1)
        {
            throw new ArgumentException("Conclusion identifiers and expected version are required.", nameof(command));
        }

        var scope = Scope(command.ActorUserId, command.ObligationId);
        var requestHash = Hash(command.ObligationId, command.ExpectedRowVersion);
        for (var attempt = 1; attempt <= MaximumAttempts; attempt++)
        {
            try
            {
                var result = await ConcludeOnceAsync(command, scope, requestHash, cancellationToken);
                ConclusionTelemetry.Conclusions.Add(1, tag: new("result", "success"));
                return result;
            }
            catch (Exception exception) when (IsRetryable(exception) && attempt < MaximumAttempts)
            {
                dbContext.ChangeTracker.Clear();
            }
            catch (Exception exception) when (IsRetryable(exception))
            {
                dbContext.ChangeTracker.Clear();
                ConclusionTelemetry.Conclusions.Add(1, tag: new("result", "failure"));
                throw new ObligationConclusionConcurrencyException();
            }
            catch (EvidenceReviewUnavailableException)
            {
                dbContext.ChangeTracker.Clear();
                ConclusionTelemetry.Conclusions.Add(1, tag: new("result", "inconsistency"));
                throw new ObligationConclusionInconsistentException();
            }
            catch (ObligationConclusionException exception)
            {
                dbContext.ChangeTracker.Clear();
                ConclusionTelemetry.Conclusions.Add(1, tag: new("result", MetricResult(exception)));
                throw;
            }
            catch (OperationCanceledException)
            {
                dbContext.ChangeTracker.Clear();
                throw;
            }
            catch
            {
                dbContext.ChangeTracker.Clear();
                ConclusionTelemetry.Conclusions.Add(1, tag: new("result", "failure"));
                throw new ObligationConclusionInconsistentException();
            }
        }

        throw new ObligationConclusionConcurrencyException();
    }

    private async Task<ObligationConclusionResult> ConcludeOnceAsync(
        ConcludeObligationCommand command,
        string scope,
        string requestHash,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var obligation = await dbContext.WorkObligations
            .FromSqlInterpolated($"SELECT * FROM work_obligation WHERE id = {command.ObligationId} AND branch_id = {BranchScope.LorettaId} FOR UPDATE")
            .AsTracking()
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new ObligationConclusionNotFoundException();
        var assignment = await dbContext.AssignmentVersions
            .FromSqlInterpolated($"SELECT * FROM assignment_version WHERE obligation_id = {obligation.Id} AND status = {AssignmentVersionStatuses.Current} FOR UPDATE")
            .AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new ObligationConclusionNotFoundException();
        var concludedAt = clock.UtcNow;
        await AuthorizeAsync(command.ActorUserId, assignment.PersonId, concludedAt, cancellationToken);

        var replay = await dbContext.IdempotencyRecords.AsNoTracking()
            .SingleOrDefaultAsync(record => record.Scope == scope && record.Key == command.IdempotencyKey, cancellationToken);
        if (replay is not null)
        {
            if (!string.Equals(replay.RequestHash, requestHash, StringComparison.Ordinal))
            {
                throw new ObligationConclusionIdempotencyConflictException();
            }

            var recovered = await RecoverAsync(command, obligation, replay, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return recovered;
        }

        if (obligation.RowVersion != command.ExpectedRowVersion)
        {
            throw new ObligationConclusionVersionConflictException();
        }

        if (obligation.ExecutionStatus != WorkObligationStatuses.Pending)
        {
            throw new ObligationAlreadyConcludedException();
        }

        var review = await evidenceReview.ReviewAsync(new(
            command.ActorUserId,
            command.CorrelationId,
            obligation.Id,
            concludedAt), cancellationToken);
        if (review.Result != EvidenceReviewResults.Complete || review.SnapshotId is null)
        {
            throw new ObligationEvidenceMissingException(
                review.MissingRequirements.Select(item => item.RequirementCode).ToArray());
        }

        var previousRowVersion = obligation.RowVersion;
        obligation.Conclude(command.ActorUserId, concludedAt, command.ExpectedRowVersion);
        var result = new ExecutionResult(
            uuidGenerator.NewUuid(), obligation.Id, review.SnapshotId.Value, command.ActorUserId, concludedAt);
        dbContext.ExecutionResults.Add(result);
        dbContext.IdempotencyRecords.Add(new IdempotencyRecord
        {
            Scope = scope,
            Key = command.IdempotencyKey,
            RequestHash = requestHash,
            Status = "COMPLETED",
            ResourceType = ResourceType,
            ResourceId = result.Id,
            ResponseCode = 200,
            CreatedAt = concludedAt,
            ExpiresAt = DateTimeOffset.MaxValue,
        });
        dbContext.AuditEvents.Add(CreateAudit(command, obligation, assignment.Id, result, previousRowVersion));
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToResult(obligation, result);
    }

    private async Task AuthorizeAsync(
        Guid actorUserId,
        Guid responsiblePersonId,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        var user = await dbContext.AppUsers
            .FromSqlInterpolated($"SELECT * FROM app_user WHERE id = {actorUserId} FOR UPDATE")
            .AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken);
        if (user is null || user.Status != AccountStatus.Active || user.MfaEnrolledAt is null ||
            user.PersonId != responsiblePersonId)
        {
            throw new ObligationConclusionNotFoundException();
        }

        var person = await dbContext.People
            .FromSqlInterpolated($"SELECT * FROM person WHERE id = {user.PersonId} FOR UPDATE")
            .AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken);
        if (person is null)
        {
            throw new ObligationConclusionNotFoundException();
        }

        var employment = await dbContext.EmploymentVersions
            .FromSqlInterpolated($"SELECT * FROM employment_version WHERE person_id = {user.PersonId} AND branch_id = {BranchScope.LorettaId} AND valid_to IS NULL FOR UPDATE")
            .AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken);
        var role = await dbContext.RoleAssignmentVersions
            .FromSqlInterpolated($"SELECT * FROM role_assignment_version WHERE user_id = {user.Id} AND branch_id = {BranchScope.LorettaId} AND valid_to IS NULL FOR UPDATE")
            .AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken);
        if (employment is not { Status: EmploymentStatus.Active } || employment.ValidFrom > at ||
            employment.ValidTo is not null && at >= employment.ValidTo ||
            role is not { Status: RoleAssignmentStatus.Active } || role.ValidFrom > at ||
            role.ValidTo is not null && at >= role.ValidTo || !RoleHierarchy.GrantsTaskExecution(role.RoleCode))
        {
            throw new ObligationConclusionNotFoundException();
        }
    }

    private async Task<ObligationConclusionResult> RecoverAsync(
        ConcludeObligationCommand command,
        WorkObligation obligation,
        IdempotencyRecord replay,
        CancellationToken cancellationToken)
    {
        if (replay.ResourceType != ResourceType || replay.ResponseCode != 200)
        {
            throw new ObligationConclusionInconsistentException();
        }

        var result = await dbContext.ExecutionResults.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == replay.ResourceId, cancellationToken)
            ?? throw new ObligationConclusionInconsistentException();
        if (result.ObligationId != command.ObligationId || result.RecordedBy != command.ActorUserId ||
            obligation.ExecutionStatus != WorkObligationStatuses.Concluded ||
            obligation.ConcludedBy != result.RecordedBy || obligation.ConcludedAt != result.RecordedAt)
        {
            throw new ObligationConclusionInconsistentException();
        }

        return ToResult(obligation, result);
    }

    private AuditEvent CreateAudit(
        ConcludeObligationCommand command,
        WorkObligation obligation,
        Guid assignmentId,
        ExecutionResult result,
        long previousRowVersion) => new()
        {
            Id = uuidGenerator.NewUuid(),
            OccurredAt = result.RecordedAt,
            ActorUserId = command.ActorUserId,
            ActorType = "USER",
            Action = "OBLIGATION_CONCLUDED",
            ResourceType = "WORK_OBLIGATION",
            ResourceId = obligation.Id,
            BranchId = obligation.BranchId,
            CorrelationId = command.CorrelationId,
            RequestId = command.IdempotencyKey.ToString("D"),
            BeforeData = JsonSerializer.SerializeToDocument(new
            {
                schemaVersion = 1,
                executionStatus = WorkObligationStatuses.Pending,
                rowVersion = previousRowVersion,
                assignmentVersionId = assignmentId,
            }),
            AfterData = JsonSerializer.SerializeToDocument(new
            {
                schemaVersion = 1,
                executionStatus = WorkObligationStatuses.Concluded,
                rowVersion = obligation.RowVersion,
                executionResultId = result.Id,
                evidenceReviewSnapshotId = result.EvidenceReviewId,
                concludedBy = result.RecordedBy,
                concludedAt = result.RecordedAt.UtcDateTime,
            }),
            Outcome = ObligationConclusionResultCodes.Concluded,
        };

    private static ObligationConclusionResult ToResult(WorkObligation obligation, ExecutionResult result) => new(
        obligation.Id,
        obligation.ExecutionStatus,
        obligation.ConcludedAt!.Value,
        obligation.ConcludedBy!.Value,
        obligation.RowVersion,
        new(
            result.Id,
            result.ResultCode,
            JsonDocument.Parse(result.ResultPayload.RootElement.GetRawText()),
            result.EvidenceReviewId,
            result.RecordedBy,
            result.RecordedAt));

    private static string Scope(Guid actorUserId, Guid obligationId) =>
        $"{ScopePrefix}:{actorUserId:D}:{obligationId:D}";

    private static string Hash(Guid obligationId, long expectedRowVersion)
    {
        var value = string.Join('\n',
            "POST",
            $"/api/v1/obligations/{obligationId:D}/conclusion",
            expectedRowVersion.ToString(CultureInfo.InvariantCulture));
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    }

    private static string MetricResult(ObligationConclusionException exception) => exception switch
    {
        ObligationEvidenceMissingException => "incomplete",
        ObligationConclusionVersionConflictException => "version_conflict",
        ObligationConclusionIdempotencyConflictException => "idempotency_conflict",
        ObligationConclusionInconsistentException => "inconsistency",
        _ => "failure",
    };

    private static bool IsRetryable(Exception exception)
    {
        var postgres = exception switch
        {
            PostgresException direct => direct,
            DbUpdateException { InnerException: PostgresException inner } => inner,
            _ => null,
        };
        if (postgres?.SqlState is PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.DeadlockDetected ||
            exception is DbUpdateConcurrencyException)
        {
            return true;
        }

        return postgres?.SqlState == PostgresErrorCodes.UniqueViolation && postgres.ConstraintName is
            "PK_idempotency_record" or
            "UX_evidence_review_snapshot_obligation_fingerprint" or
            "UX_evidence_review_snapshot_obligation_input" or
            "UX_execution_result_obligation" or
            "UX_execution_result_evidence_review";
    }
}
