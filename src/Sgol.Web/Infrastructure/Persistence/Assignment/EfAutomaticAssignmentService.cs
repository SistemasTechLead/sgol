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
using Sgol.Generation.Contracts;
using Sgol.Organization.Contracts;
using Sgol.Web.Infrastructure.Persistence.Auditing;
using Sgol.Web.Infrastructure.Persistence.Bootstrap;

namespace Sgol.Web.Infrastructure.Persistence.Assignment;

public sealed class EfAutomaticAssignmentService(
    SgolDbContext dbContext,
    AuditTransaction auditTransaction,
    IClock clock,
    IUuidGenerator uuidGenerator) : IAutomaticAssignmentService
{
    private const string IdempotencyScope = "assignment:automatic";
    private const string AssignmentResource = "ASSIGNMENT_VERSION";
    private const string NoCandidateResource = "WORK_OBLIGATION_NO_ELIGIBLE_CANDIDATE";
    private const string ErrorResourcePrefix = "AUTOMATIC_ASSIGNMENT_ERROR:";
    private const string IdempotencyPrimaryKey = "PK_idempotency_record";
    private const int MaximumAttempts = 3;

    public async Task<AutomaticAssignmentResult> AssignAsync(
        AssignObligationCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.AssignmentRequestId == Guid.Empty ||
            command.ObligationId == Guid.Empty ||
            command.EligibilityEvaluationId == Guid.Empty ||
            command.CorrelationId == Guid.Empty)
        {
            throw new ArgumentException("Automatic-assignment identifiers cannot be empty.", nameof(command));
        }

        var requestHash = Hash(command.ObligationId, command.EligibilityEvaluationId);
        for (var attempt = 1; attempt <= MaximumAttempts; attempt++)
        {
            AutomaticAssignmentResult? result = null;
            AutomaticAssignmentException? rejection = null;
            try
            {
                await auditTransaction.ExecuteAsync(
                    IsolationLevel.ReadCommitted,
                    async token =>
                    {
                        var obligation = await dbContext.WorkObligations
                            .FromSqlInterpolated($"SELECT * FROM work_obligation WHERE id = {command.ObligationId} FOR UPDATE")
                            .AsNoTracking()
                            .SingleOrDefaultAsync(token);
                        if (obligation is null)
                        {
                            var missingReplay = await dbContext.IdempotencyRecords.AsNoTracking()
                                .SingleOrDefaultAsync(record =>
                                    record.Scope == IdempotencyScope &&
                                    record.Key == command.AssignmentRequestId,
                                    token);
                            if (missingReplay is not null)
                            {
                                if (!string.Equals(missingReplay.RequestHash, requestHash, StringComparison.Ordinal))
                                {
                                    rejection = new AutomaticAssignmentIdempotencyConflictException();
                                    return RejectedAudit(command, null, rejection.ErrorCode, clock.UtcNow);
                                }

                                if (missingReplay.ResourceType.StartsWith(ErrorResourcePrefix, StringComparison.Ordinal))
                                {
                                    rejection = ExceptionFor(
                                        missingReplay.ResourceType[ErrorResourcePrefix.Length..]);
                                    return RejectedAudit(command, null, rejection.ErrorCode, clock.UtcNow);
                                }

                                throw new InvalidOperationException(
                                    "The automatic-assignment idempotency record references a missing obligation.");
                            }

                            rejection = new AutomaticAssignmentObligationNotFoundException();
                            return NewRejectionAudit(command, requestHash, null, rejection, clock.UtcNow);
                        }

                        var idempotency = await dbContext.IdempotencyRecords.AsNoTracking()
                            .SingleOrDefaultAsync(record =>
                                record.Scope == IdempotencyScope &&
                                record.Key == command.AssignmentRequestId,
                                token);
                        if (idempotency is not null)
                        {
                            if (!string.Equals(idempotency.RequestHash, requestHash, StringComparison.Ordinal))
                            {
                                rejection = new AutomaticAssignmentIdempotencyConflictException();
                                return RejectedAudit(command, obligation.BranchId, rejection.ErrorCode, clock.UtcNow);
                            }

                            if (idempotency.ResourceType.StartsWith(ErrorResourcePrefix, StringComparison.Ordinal))
                            {
                                rejection = ExceptionFor(idempotency.ResourceType[ErrorResourcePrefix.Length..]);
                                return RejectedAudit(command, obligation.BranchId, rejection.ErrorCode, clock.UtcNow);
                            }

                            result = await ReplayAsync(command, idempotency, token);
                            return result.Result == AutomaticAssignmentResults.NoEligibleCandidate
                                ? NoCandidateAudit(command, obligation.BranchId, clock.UtcNow)
                                : RecoveredAudit(command, obligation.BranchId, result, clock.UtcNow);
                        }

                        if (obligation.BranchId != BranchScope.LorettaId ||
                            !string.Equals(
                                obligation.ExecutionStatus,
                                WorkObligationStatuses.Pending,
                                StringComparison.Ordinal))
                        {
                            rejection = new AutomaticAssignmentObligationNotAssignableException();
                            return NewRejectionAudit(
                                command, requestHash, obligation.BranchId, rejection, clock.UtcNow);
                        }

                        var currentAssignment = await dbContext.AssignmentVersions.AsNoTracking()
                            .SingleOrDefaultAsync(assignment =>
                                assignment.ObligationId == obligation.Id &&
                                assignment.Status == AssignmentVersionStatuses.Current,
                                token);
                        if (currentAssignment is not null)
                        {
                            if (currentAssignment.AssignmentType == AssignmentTypes.Automatic &&
                                UsesEvaluation(currentAssignment, command.EligibilityEvaluationId))
                            {
                                var now = clock.UtcNow;
                                AddIdempotency(
                                    command,
                                    requestHash,
                                    AssignmentResource,
                                    currentAssignment.Id,
                                    StatusCodes.Status200OK,
                                    now);
                                result = Recovered(command, currentAssignment);
                                return RecoveredAudit(command, obligation.BranchId, result, now);
                            }

                            rejection = new AutomaticAssignmentAlreadyExistsException();
                            return NewRejectionAudit(
                                command, requestHash, obligation.BranchId, rejection, clock.UtcNow);
                        }

                        var evaluation = await dbContext.EligibilityEvaluations.AsNoTracking()
                            .SingleOrDefaultAsync(
                                item => item.Id == command.EligibilityEvaluationId,
                                token);
                        if (evaluation is null)
                        {
                            rejection = new AutomaticAssignmentEvaluationNotFoundException();
                            return NewRejectionAudit(
                                command, requestHash, obligation.BranchId, rejection, clock.UtcNow);
                        }

                        if (evaluation.ObligationId != obligation.Id)
                        {
                            rejection = new AutomaticAssignmentEvaluationMismatchException();
                            return NewRejectionAudit(
                                command, requestHash, obligation.BranchId, rejection, clock.UtcNow);
                        }

                        var latestEvaluationId = await dbContext.EligibilityEvaluations.AsNoTracking()
                            .Where(item => item.ObligationId == obligation.Id)
                            .OrderByDescending(item => item.EvaluatedAt)
                            .ThenByDescending(item => item.Id)
                            .Select(item => item.Id)
                            .FirstAsync(token);
                        if (latestEvaluationId != evaluation.Id)
                        {
                            rejection = new AutomaticAssignmentEvaluationStaleException();
                            return NewRejectionAudit(
                                command, requestHash, obligation.BranchId, rejection, clock.UtcNow);
                        }

                        var snapshotCandidates = await dbContext.EligibilityCandidates.AsNoTracking()
                            .Where(candidate => candidate.EvaluationId == evaluation.Id)
                            .ToListAsync(token);
                        var eligibleCandidates = snapshotCandidates
                            .Where(candidate => candidate.IsEligible)
                            .OrderBy(candidate => candidate.StableCode, StringComparer.Ordinal)
                            .ThenBy(candidate => candidate.PersonId)
                            .ToArray();
                        if (!IsCompatible(evaluation.Result, eligibleCandidates.Length))
                        {
                            rejection = new AutomaticAssignmentEvaluationIncompatibleException();
                            return NewRejectionAudit(
                                command, requestHash, obligation.BranchId, rejection, clock.UtcNow);
                        }

                        if (evaluation.Result == EligibilityResults.NoEligibleCandidate)
                        {
                            var now = clock.UtcNow;
                            AddIdempotency(
                                command,
                                requestHash,
                                NoCandidateResource,
                                obligation.Id,
                                StatusCodes.Status200OK,
                                now);
                            result = NoCandidate(command);
                            return NoCandidateAudit(command, obligation.BranchId, now);
                        }

                        foreach (var candidate in eligibleCandidates)
                        {
                            var person = await dbContext.People
                                .FromSqlInterpolated($"SELECT * FROM person WHERE id = {candidate.PersonId} FOR UPDATE")
                                .AsNoTracking()
                                .SingleOrDefaultAsync(token);
                            if (person is null)
                            {
                                rejection = new AutomaticAssignmentEvaluationIncompatibleException();
                                return NewRejectionAudit(
                                    command, requestHash, obligation.BranchId, rejection, clock.UtcNow);
                            }
                        }

                        var calculatedAt = clock.UtcNow;
                        var personIds = eligibleCandidates.Select(candidate => candidate.PersonId).ToArray();
                        var metrics = await EfAssignmentMetricsReader.ReadAsync(
                            dbContext,
                            personIds,
                            calculatedAt,
                            token);
                        var ranking = AutomaticAssignmentRanker.Rank(eligibleCandidates.Select(candidate =>
                        {
                            var candidateMetrics = metrics[candidate.PersonId];
                            return new AutomaticAssignmentCandidate(
                                candidate.PersonId,
                                candidate.StableCode,
                                candidateMetrics.ActiveLoad,
                                candidateMetrics.LastAutoAssignmentAt);
                        }).ToArray());

                        var assignmentId = uuidGenerator.NewUuid();
                        var explanation = CreateExplanation(evaluation.Id, calculatedAt, ranking);
                        var assignment = new AssignmentVersion(
                            assignmentId,
                            obligation.Id,
                            ranking.Winner.PersonId,
                            AssignmentVersionStatuses.Current,
                            AssignmentTypes.Automatic,
                            explanation,
                            calculatedAt);
                        dbContext.AssignmentVersions.Add(assignment);
                        AddIdempotency(
                            command,
                            requestHash,
                            AssignmentResource,
                            assignmentId,
                            StatusCodes.Status201Created,
                            calculatedAt);
                        result = new AutomaticAssignmentResult(
                            AutomaticAssignmentResults.Created,
                            command.AssignmentRequestId,
                            obligation.Id,
                            evaluation.Id,
                            assignmentId,
                            ranking.Winner.PersonId,
                            calculatedAt,
                            null);
                        return CreatedAudit(command, obligation.BranchId, result, calculatedAt);
                    },
                    cancellationToken);

                if (rejection is not null)
                {
                    throw rejection;
                }

                return result ?? throw new InvalidOperationException("Automatic assignment produced no result.");
            }
            catch (Exception exception) when (IsRetryable(exception) && attempt < MaximumAttempts)
            {
                dbContext.ChangeTracker.Clear();
            }
            catch (Exception exception) when (IsRetryable(exception))
            {
                dbContext.ChangeTracker.Clear();
                await PersistConcurrencyRejectionAsync(command, cancellationToken);
                throw new AutomaticAssignmentConcurrencyConflictException();
            }
            catch
            {
                dbContext.ChangeTracker.Clear();
                throw;
            }
        }

        throw new AutomaticAssignmentConcurrencyConflictException();
    }

    private async Task<AutomaticAssignmentResult> ReplayAsync(
        AssignObligationCommand command,
        IdempotencyRecord record,
        CancellationToken cancellationToken)
    {
        if (record.ResourceType == NoCandidateResource && record.ResourceId == command.ObligationId)
        {
            return NoCandidate(command);
        }

        if (record.ResourceType != AssignmentResource)
        {
            throw new InvalidOperationException("The automatic-assignment idempotency record is invalid.");
        }

        var assignment = await dbContext.AssignmentVersions.AsNoTracking()
            .SingleAsync(item => item.Id == record.ResourceId, cancellationToken);
        return Recovered(command, assignment);
    }

    private void AddIdempotency(
        AssignObligationCommand command,
        string requestHash,
        string resourceType,
        Guid resourceId,
        int responseCode,
        DateTimeOffset now)
    {
        dbContext.IdempotencyRecords.Add(new IdempotencyRecord
        {
            Scope = IdempotencyScope,
            Key = command.AssignmentRequestId,
            RequestHash = requestHash,
            Status = "COMPLETED",
            ResourceType = resourceType,
            ResourceId = resourceId,
            ResponseCode = responseCode,
            CreatedAt = now,
            ExpiresAt = DateTimeOffset.MaxValue,
        });
    }

    private async Task PersistConcurrencyRejectionAsync(
        AssignObligationCommand command,
        CancellationToken cancellationToken)
    {
        var branchId = await dbContext.WorkObligations.AsNoTracking()
            .Where(obligation => obligation.Id == command.ObligationId)
            .Select(obligation => (Guid?)obligation.BranchId)
            .SingleOrDefaultAsync(cancellationToken);
        await auditTransaction.ExecuteAsync(
            async token =>
            {
                var requestHash = Hash(command.ObligationId, command.EligibilityEvaluationId);
                var exists = await dbContext.IdempotencyRecords.AsNoTracking().AnyAsync(record =>
                    record.Scope == IdempotencyScope && record.Key == command.AssignmentRequestId,
                    token);
                var now = clock.UtcNow;
                if (!exists)
                {
                    AddIdempotency(
                        command,
                        requestHash,
                        ErrorResourcePrefix + AutomaticAssignmentErrors.ConcurrencyConflict,
                        command.ObligationId,
                        StatusCodes.Status409Conflict,
                        now);
                }

                return RejectedAudit(
                    command,
                    branchId,
                    AutomaticAssignmentErrors.ConcurrencyConflict,
                    now);
            },
            cancellationToken);
    }

    private AuditEvent NewRejectionAudit(
        AssignObligationCommand command,
        string requestHash,
        Guid? branchId,
        AutomaticAssignmentException exception,
        DateTimeOffset occurredAt)
    {
        AddIdempotency(
            command,
            requestHash,
            ErrorResourcePrefix + exception.ErrorCode,
            command.ObligationId,
            exception is AutomaticAssignmentObligationNotFoundException
                ? StatusCodes.Status404NotFound
                : StatusCodes.Status409Conflict,
            occurredAt);
        return RejectedAudit(command, branchId, exception.ErrorCode, occurredAt);
    }

    private static AutomaticAssignmentException ExceptionFor(string errorCode) => errorCode switch
    {
        AutomaticAssignmentErrors.ObligationNotFound => new AutomaticAssignmentObligationNotFoundException(),
        AutomaticAssignmentErrors.ObligationNotAssignable => new AutomaticAssignmentObligationNotAssignableException(),
        AutomaticAssignmentErrors.EvaluationNotFound => new AutomaticAssignmentEvaluationNotFoundException(),
        AutomaticAssignmentErrors.EvaluationMismatch => new AutomaticAssignmentEvaluationMismatchException(),
        AutomaticAssignmentErrors.EvaluationStale => new AutomaticAssignmentEvaluationStaleException(),
        AutomaticAssignmentErrors.EvaluationIncompatible => new AutomaticAssignmentEvaluationIncompatibleException(),
        AutomaticAssignmentErrors.AlreadyExists => new AutomaticAssignmentAlreadyExistsException(),
        AutomaticAssignmentErrors.ConcurrencyConflict => new AutomaticAssignmentConcurrencyConflictException(),
        _ => throw new InvalidOperationException("The automatic-assignment idempotency error is invalid."),
    };

    private static bool IsCompatible(string result, int eligibleCount) =>
        (result == EligibilityResults.EligibleCandidates && eligibleCount > 0) ||
        (result == EligibilityResults.NoEligibleCandidate && eligibleCount == 0);

    private static bool UsesEvaluation(AssignmentVersion assignment, Guid evaluationId) =>
        assignment.Explanation.RootElement.TryGetProperty("eligibilityEvaluationId", out var property) &&
        property.ValueKind == JsonValueKind.String &&
        property.TryGetGuid(out var storedId) &&
        storedId == evaluationId;

    private static JsonDocument CreateExplanation(
        Guid evaluationId,
        DateTimeOffset calculatedAt,
        AutomaticAssignmentRanking ranking) =>
        JsonSerializer.SerializeToDocument(new
        {
            schemaVersion = 1,
            eligibilityEvaluationId = evaluationId,
            calculatedAt,
            orderingRules = AutomaticAssignmentOrderingRules.All,
            candidates = ranking.Candidates.Select(candidate => new
            {
                candidate.PersonId,
                candidate.StableCode,
                candidate.ActiveLoad,
                candidate.LastAutoAssignmentAt,
                candidate.Rank,
            }),
            winner = new
            {
                ranking.Winner.PersonId,
                ranking.Winner.StableCode,
                ranking.Winner.Rank,
            },
            decisiveRule = ranking.DecisiveRule,
        }, JsonSerializerOptions.Web);

    private static AutomaticAssignmentResult Recovered(
        AssignObligationCommand command,
        AssignmentVersion assignment) =>
        new(
            AutomaticAssignmentResults.Recovered,
            command.AssignmentRequestId,
            command.ObligationId,
            command.EligibilityEvaluationId,
            assignment.Id,
            assignment.PersonId,
            assignment.AssignedAt,
            null);

    private static AutomaticAssignmentResult NoCandidate(AssignObligationCommand command) =>
        new(
            AutomaticAssignmentResults.NoEligibleCandidate,
            command.AssignmentRequestId,
            command.ObligationId,
            command.EligibilityEvaluationId,
            null,
            null,
            null,
            AutomaticAssignmentErrors.NoEligibleCandidate);

    private AuditEvent CreatedAudit(
        AssignObligationCommand command,
        Guid branchId,
        AutomaticAssignmentResult result,
        DateTimeOffset occurredAt) =>
        new()
        {
            Id = uuidGenerator.NewUuid(),
            OccurredAt = occurredAt,
            ActorType = "SYSTEM",
            Action = "AUTOMATIC_ASSIGNMENT_CREATED",
            ResourceType = AssignmentResource,
            ResourceId = result.AssignmentId,
            BranchId = branchId,
            CorrelationId = command.CorrelationId,
            RequestId = command.AssignmentRequestId.ToString("D", CultureInfo.InvariantCulture),
            BeforeData = JsonSerializer.SerializeToDocument(new
            {
                schemaVersion = 1,
                obligationId = command.ObligationId,
                assignmentId = (Guid?)null,
            }, JsonSerializerOptions.Web),
            AfterData = JsonSerializer.SerializeToDocument(new
            {
                schemaVersion = 1,
                assignmentId = result.AssignmentId,
                obligationId = command.ObligationId,
                personId = result.WinnerPersonId,
                eligibilityEvaluationId = command.EligibilityEvaluationId,
                status = AssignmentVersionStatuses.Current,
                assignmentType = AssignmentTypes.Automatic,
            }, JsonSerializerOptions.Web),
            Outcome = AutomaticAssignmentResults.Created,
        };

    private AuditEvent RecoveredAudit(
        AssignObligationCommand command,
        Guid branchId,
        AutomaticAssignmentResult result,
        DateTimeOffset occurredAt) =>
        ResultAudit(
            command,
            branchId,
            occurredAt,
            "AUTOMATIC_ASSIGNMENT_RECOVERED",
            AssignmentResource,
            result.AssignmentId,
            AutomaticAssignmentResults.Recovered,
            result.AssignmentId,
            AutomaticAssignmentResults.Recovered,
            null);

    private AuditEvent NoCandidateAudit(
        AssignObligationCommand command,
        Guid branchId,
        DateTimeOffset occurredAt) =>
        ResultAudit(
            command,
            branchId,
            occurredAt,
            "AUTOMATIC_ASSIGNMENT_NOT_CREATED",
            "WORK_OBLIGATION",
            command.ObligationId,
            AutomaticAssignmentResults.NoEligibleCandidate,
            null,
            AutomaticAssignmentResults.NoEligibleCandidate,
            AutomaticAssignmentErrors.NoEligibleCandidate);

    private AuditEvent RejectedAudit(
        AssignObligationCommand command,
        Guid? branchId,
        string errorCode,
        DateTimeOffset occurredAt) =>
        ResultAudit(
            command,
            branchId,
            occurredAt,
            "AUTOMATIC_ASSIGNMENT_REJECTED",
            "WORK_OBLIGATION",
            command.ObligationId,
            errorCode,
            null,
            null,
            errorCode);

    private AuditEvent ResultAudit(
        AssignObligationCommand command,
        Guid? branchId,
        DateTimeOffset occurredAt,
        string action,
        string resourceType,
        Guid? resourceId,
        string outcome,
        Guid? assignmentId,
        string? result,
        string? errorCode) =>
        new()
        {
            Id = uuidGenerator.NewUuid(),
            OccurredAt = occurredAt,
            ActorType = "SYSTEM",
            Action = action,
            ResourceType = resourceType,
            ResourceId = resourceId,
            BranchId = branchId,
            CorrelationId = command.CorrelationId,
            RequestId = command.AssignmentRequestId.ToString("D", CultureInfo.InvariantCulture),
            AfterData = JsonSerializer.SerializeToDocument(new
            {
                schemaVersion = 1,
                obligationId = command.ObligationId,
                eligibilityEvaluationId = command.EligibilityEvaluationId,
                assignmentId,
                result,
                errorCode,
            }, JsonSerializerOptions.Web),
            Outcome = outcome,
        };

    private static string Hash(Guid obligationId, Guid evaluationId)
    {
        var value = string.Join(
            '\n',
            obligationId.ToString("D", CultureInfo.InvariantCulture),
            evaluationId.ToString("D", CultureInfo.InvariantCulture));
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    }

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
             postgres.ConstraintName is IdempotencyPrimaryKey or AssignmentVersionConfiguration.CurrentObligationIndex);
    }
}
