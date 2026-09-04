using System.Text.Json;

namespace Sgol.Assignment.Contracts;

public static class EligibilityAuthorization
{
    public const string Explain = "PER-ASIGNACION-EXPLICAR";
}

public static class EligibilityResults
{
    public const string EligibleCandidates = "CANDIDATOS_ELEGIBLES";
    public const string NoEligibleCandidate = "SIN_CANDIDATO_ELEGIBLE";
}

public static class EligibilityDateSources
{
    public const string ManualRequest = "MANUAL_REQUEST";
    public const string ScheduledOccurrence = "SCHEDULED_OCCURRENCE";

    public static bool IsDefined(string? value) => value is ManualRequest or ScheduledOccurrence;
}

public static class EligibilityExclusionReasons
{
    public const string InactivePerson = "PERSONA_INACTIVA";
    public const string EmploymentNotCurrent = "EMPLEO_NO_VIGENTE";
    public const string BranchMismatch = "SUCURSAL_NO_COINCIDE";
    public const string ActiveRoleMissing = "ROL_ACTIVO_AUSENTE";
    public const string RequiredRoleMismatch = "ROL_REQUERIDO_NO_COINCIDE";
    public const string AvailabilityMissing = "DISPONIBILIDAD_AUSENTE";
    public const string AvailabilityNotPositive = "DISPONIBILIDAD_NO_POSITIVA";
    public const string ShiftMismatch = "TURNO_NO_COINCIDE";
}

public sealed record EligibilityPersonInput(
    bool HasCurrentEmployment,
    bool IsPersonActive,
    bool BranchMatches,
    bool HasActiveRole,
    string? ActiveRoleCode,
    bool? IsAvailable,
    string? ShiftText);

public static class EligibilityEvaluator
{
    public static IReadOnlyList<string> Explain(
        EligibilityPersonInput input,
        string requiredRole,
        string? requiredShift)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentException.ThrowIfNullOrWhiteSpace(requiredRole);
        var reasons = new List<string>();
        if (!input.HasCurrentEmployment)
        {
            reasons.Add(EligibilityExclusionReasons.EmploymentNotCurrent);
        }
        else
        {
            if (!input.IsPersonActive)
            {
                reasons.Add(EligibilityExclusionReasons.InactivePerson);
            }

            if (!input.BranchMatches)
            {
                reasons.Add(EligibilityExclusionReasons.BranchMismatch);
            }
        }

        if (!input.HasActiveRole)
        {
            reasons.Add(EligibilityExclusionReasons.ActiveRoleMissing);
        }
        else if (!string.Equals(input.ActiveRoleCode, requiredRole, StringComparison.Ordinal))
        {
            reasons.Add(EligibilityExclusionReasons.RequiredRoleMismatch);
        }

        if (input.IsAvailable is null)
        {
            reasons.Add(EligibilityExclusionReasons.AvailabilityMissing);
        }
        else if (!input.IsAvailable.Value)
        {
            reasons.Add(EligibilityExclusionReasons.AvailabilityNotPositive);
        }

        if (requiredShift is not null &&
            !string.Equals(input.ShiftText, requiredShift, StringComparison.Ordinal))
        {
            reasons.Add(EligibilityExclusionReasons.ShiftMismatch);
        }

        return reasons;
    }
}

public sealed class EligibilityEvaluation
{
    private EligibilityEvaluation()
    {
    }

    public EligibilityEvaluation(
        Guid id,
        Guid evaluationRequestId,
        Guid obligationId,
        DateTimeOffset evaluatedAt,
        DateOnly eligibilityDate,
        string eligibilityDateSource,
        Guid policyVersionId,
        JsonDocument inputSnapshot,
        string result)
    {
        if (!EligibilityDateSources.IsDefined(eligibilityDateSource))
        {
            throw new ArgumentOutOfRangeException(nameof(eligibilityDateSource));
        }

        if (result is not EligibilityResults.EligibleCandidates and not EligibilityResults.NoEligibleCandidate)
        {
            throw new ArgumentOutOfRangeException(nameof(result));
        }

        Id = id;
        EvaluationRequestId = evaluationRequestId;
        ObligationId = obligationId;
        EvaluatedAt = evaluatedAt;
        EligibilityDate = eligibilityDate;
        EligibilityDateSource = eligibilityDateSource;
        PolicyVersionId = policyVersionId;
        InputSnapshot = inputSnapshot;
        Result = result;
    }

    public Guid Id { get; private init; }
    public Guid EvaluationRequestId { get; private init; }
    public Guid ObligationId { get; private init; }
    public DateTimeOffset EvaluatedAt { get; private init; }
    public DateOnly EligibilityDate { get; private init; }
    public string EligibilityDateSource { get; private init; } = null!;
    public Guid PolicyVersionId { get; private init; }
    public JsonDocument InputSnapshot { get; private init; } = null!;
    public string Result { get; private init; } = null!;
    public Guid? WinnerPersonId { get; private init; }
}

public sealed class EligibilityCandidate
{
    private EligibilityCandidate()
    {
    }

    public EligibilityCandidate(
        Guid evaluationId,
        Guid personId,
        string stableCode,
        bool isEligible,
        JsonDocument reasons)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stableCode);
        EvaluationId = evaluationId;
        PersonId = personId;
        StableCode = stableCode;
        IsEligible = isEligible;
        Reasons = reasons;
    }

    public Guid EvaluationId { get; private init; }
    public Guid PersonId { get; private init; }
    public bool IsEligible { get; private init; }
    public JsonDocument Reasons { get; private init; } = null!;
    public int? ActiveLoad { get; private init; }
    public DateTimeOffset? LastAutoAssignmentAt { get; private init; }
    public string StableCode { get; private init; } = null!;
    public int? Rank { get; private init; }
}

public sealed record EvaluateEligibilityCommand(
    Guid EvaluationRequestId,
    Guid CorrelationId,
    Guid ObligationId,
    DateOnly EligibilityDate,
    string EligibilityDateSource);

public sealed record EligibilityCandidateExplanation(
    Guid PersonId,
    string StableCode,
    bool IsEligible,
    IReadOnlyList<string> Reasons,
    Guid? EmploymentVersionId,
    string? EmploymentStatus,
    Guid? EmploymentBranchId,
    string? PositionText,
    string? ShiftText,
    Guid? RoleAssignmentVersionId,
    string? ActiveRoleCode,
    Guid? AvailabilityVersionId,
    bool? IsAvailable,
    int? ActiveLoad,
    DateTimeOffset? LastAutoAssignmentAt,
    int? Rank);

public sealed record EligibilityEvaluationDetails(
    Guid EvaluationId,
    Guid EvaluationRequestId,
    Guid ObligationId,
    DateTimeOffset EvaluatedAt,
    DateOnly EligibilityDate,
    string EligibilityDateSource,
    Guid PolicyVersionId,
    string RequiredRole,
    string? RequiredShift,
    string Result,
    Guid? WinnerPersonId,
    IReadOnlyList<EligibilityCandidateExplanation> Candidates,
    bool Replayed);

public interface IEligibilityEvaluationService
{
    Task<EligibilityEvaluationDetails> EvaluateAsync(
        EvaluateEligibilityCommand command,
        CancellationToken cancellationToken = default);

    Task<EligibilityEvaluationDetails> GetLatestAsync(
        Guid actorUserId,
        Guid correlationId,
        Guid obligationId,
        CancellationToken cancellationToken = default);
}

public sealed class EligibilityEvaluationNotFoundException()
    : Exception("The obligation or eligibility snapshot does not exist in the visible scope.");
public sealed class EligibilityEvaluationAccessDeniedException()
    : Exception($"{EligibilityAuthorization.Explain} is required.");
public sealed class EligibilityObligationNotPendingException()
    : Exception("Only a persisted PENDIENTE obligation can be evaluated.");
public sealed class EligibilityPolicyConfigurationException()
    : Exception("Exactly one eligibility policy must correspond to the obligation task version.");
public sealed class EligibilityDateInvalidException()
    : Exception("The eligibility date does not match the approved internal trigger.");
public sealed class EligibilityEvaluationIdempotencyConflictException()
    : Exception("The evaluation request identifier was already used with different content.");
