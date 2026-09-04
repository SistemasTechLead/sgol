namespace Sgol.Assignment.Contracts;

public static class AutomaticAssignmentResults
{
    public const string Created = "CREADA";
    public const string Recovered = "RECUPERADA";
    public const string NoEligibleCandidate = EligibilityResults.NoEligibleCandidate;
}

public static class AutomaticAssignmentErrors
{
    public const string IdempotencyConflict = "ASSIGNMENT_IDEMPOTENCY_CONFLICT";
    public const string ObligationNotFound = "ASSIGNMENT_OBLIGATION_NOT_FOUND";
    public const string ObligationNotAssignable = "ASSIGNMENT_OBLIGATION_NOT_ASSIGNABLE";
    public const string EvaluationNotFound = "ASSIGNMENT_EVALUATION_NOT_FOUND";
    public const string EvaluationMismatch = "ASSIGNMENT_EVALUATION_MISMATCH";
    public const string EvaluationStale = "ASSIGNMENT_EVALUATION_STALE";
    public const string EvaluationIncompatible = "ASSIGNMENT_EVALUATION_INCOMPATIBLE";
    public const string AlreadyExists = "ASSIGNMENT_ALREADY_EXISTS";
    public const string ConcurrencyConflict = "ASSIGNMENT_CONCURRENCY_CONFLICT";
    public const string NoEligibleCandidate = EligibilityResults.NoEligibleCandidate;
}

public static class AutomaticAssignmentDecisiveRules
{
    public const string OnlyEligibleCandidate = "ONLY_ELIGIBLE_CANDIDATE";
    public const string ActiveLoad = "ACTIVE_LOAD";
    public const string LastAutoAssignmentAt = "LAST_AUTO_ASSIGNMENT_AT";
    public const string StableCode = "STABLE_CODE";
}

public static class AutomaticAssignmentOrderingRules
{
    public static readonly IReadOnlyList<string> All =
    [
        "ACTIVE_LOAD_ASC",
        "NEVER_AUTOMATICALLY_ASSIGNED_FIRST",
        "LAST_AUTO_ASSIGNMENT_AT_ASC",
        "STABLE_CODE_ORDINAL_ASC",
    ];
}

public sealed record AssignObligationCommand(
    Guid AssignmentRequestId,
    Guid ObligationId,
    Guid EligibilityEvaluationId,
    Guid CorrelationId);

public sealed record AutomaticAssignmentResult(
    string Result,
    Guid AssignmentRequestId,
    Guid ObligationId,
    Guid EligibilityEvaluationId,
    Guid? AssignmentId,
    Guid? WinnerPersonId,
    DateTimeOffset? AssignedAt,
    string? ErrorCode);

public sealed record AutomaticAssignmentCandidate(
    Guid PersonId,
    string StableCode,
    int ActiveLoad,
    DateTimeOffset? LastAutoAssignmentAt);

public sealed record RankedAutomaticAssignmentCandidate(
    Guid PersonId,
    string StableCode,
    int ActiveLoad,
    DateTimeOffset? LastAutoAssignmentAt,
    int Rank);

public sealed record AutomaticAssignmentRanking(
    IReadOnlyList<RankedAutomaticAssignmentCandidate> Candidates,
    RankedAutomaticAssignmentCandidate Winner,
    string DecisiveRule);

public static class AutomaticAssignmentRanker
{
    public static AutomaticAssignmentRanking Rank(
        IReadOnlyCollection<AutomaticAssignmentCandidate> candidates)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        if (candidates.Count == 0)
        {
            throw new ArgumentException("At least one eligible candidate is required.", nameof(candidates));
        }

        if (candidates.Any(candidate =>
                candidate.PersonId == Guid.Empty ||
                string.IsNullOrWhiteSpace(candidate.StableCode) ||
                candidate.ActiveLoad < 0))
        {
            throw new ArgumentException("Automatic-assignment candidates are invalid.", nameof(candidates));
        }

        var ordered = candidates
            .OrderBy(candidate => candidate.ActiveLoad)
            .ThenBy(candidate => candidate.LastAutoAssignmentAt is null ? 0 : 1)
            .ThenBy(candidate => candidate.LastAutoAssignmentAt)
            .ThenBy(candidate => candidate.StableCode, StringComparer.Ordinal)
            .Select((candidate, index) => new RankedAutomaticAssignmentCandidate(
                candidate.PersonId,
                candidate.StableCode,
                candidate.ActiveLoad,
                candidate.LastAutoAssignmentAt,
                index + 1))
            .ToArray();

        if (ordered.Select(candidate => candidate.PersonId).Distinct().Count() != ordered.Length ||
            ordered.Select(candidate => candidate.StableCode).Distinct(StringComparer.Ordinal).Count() != ordered.Length)
        {
            throw new ArgumentException("Candidate identities and stable codes must be unique.", nameof(candidates));
        }

        return new AutomaticAssignmentRanking(ordered, ordered[0], DecisiveRule(ordered));
    }

    private static string DecisiveRule(RankedAutomaticAssignmentCandidate[] ordered)
    {
        if (ordered.Length == 1)
        {
            return AutomaticAssignmentDecisiveRules.OnlyEligibleCandidate;
        }

        var first = ordered[0];
        var second = ordered[1];
        if (first.ActiveLoad != second.ActiveLoad)
        {
            return AutomaticAssignmentDecisiveRules.ActiveLoad;
        }

        if (first.LastAutoAssignmentAt != second.LastAutoAssignmentAt)
        {
            return AutomaticAssignmentDecisiveRules.LastAutoAssignmentAt;
        }

        return AutomaticAssignmentDecisiveRules.StableCode;
    }
}

public interface IAutomaticAssignmentService
{
    Task<AutomaticAssignmentResult> AssignAsync(
        AssignObligationCommand command,
        CancellationToken cancellationToken = default);
}

public class AutomaticAssignmentException(string errorCode)
    : Exception(errorCode)
{
    public string ErrorCode { get; } = errorCode;
}

public sealed class AutomaticAssignmentIdempotencyConflictException()
    : AutomaticAssignmentException(AutomaticAssignmentErrors.IdempotencyConflict);

public sealed class AutomaticAssignmentObligationNotFoundException()
    : AutomaticAssignmentException(AutomaticAssignmentErrors.ObligationNotFound);

public sealed class AutomaticAssignmentObligationNotAssignableException()
    : AutomaticAssignmentException(AutomaticAssignmentErrors.ObligationNotAssignable);

public sealed class AutomaticAssignmentEvaluationNotFoundException()
    : AutomaticAssignmentException(AutomaticAssignmentErrors.EvaluationNotFound);

public sealed class AutomaticAssignmentEvaluationMismatchException()
    : AutomaticAssignmentException(AutomaticAssignmentErrors.EvaluationMismatch);

public sealed class AutomaticAssignmentEvaluationStaleException()
    : AutomaticAssignmentException(AutomaticAssignmentErrors.EvaluationStale);

public sealed class AutomaticAssignmentEvaluationIncompatibleException()
    : AutomaticAssignmentException(AutomaticAssignmentErrors.EvaluationIncompatible);

public sealed class AutomaticAssignmentAlreadyExistsException()
    : AutomaticAssignmentException(AutomaticAssignmentErrors.AlreadyExists);

public sealed class AutomaticAssignmentConcurrencyConflictException()
    : AutomaticAssignmentException(AutomaticAssignmentErrors.ConcurrencyConflict);
