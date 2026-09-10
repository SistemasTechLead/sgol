using System.Text;

namespace Sgol.Validation.Contracts;

public static class ValidationAuthorization
{
    public const string Issue = "PER-VALIDACION-EMITIR";
    public const string Escalate = "PER-VALIDACION-ESCALAR";
    public const string Replace = "PER-VALIDACION-SUSTITUIR";
    public const string View = "PER-TAREA-VER";
}

public static class ValidationResults
{
    public const string Fulfilled = "CUMPLIDA";
    public const string Incomplete = "INCOMPLETA";
    public const string NotFulfilled = "NO_CUMPLIDA";

    public static bool IsDefined(string? value) => value is Fulfilled or Incomplete or NotFulfilled;
}

public static class ValidationStatuses
{
    public const string Pending = "PENDIENTE";
    public const string Resolved = "RESUELTA";
    public const string Current = "VIGENTE";
    public const string Superseded = "SUSTITUIDA";
}

public static class ValidationAuthorityTypes
{
    public const string Ordinary = "ORDINARIA";
    public const string Escalation = "ESCALAMIENTO";
    public const string DirectionSelfValidation = "AUTOVALIDACION_DIRECCION";
    public const string OriginalReplacement = "SUSTITUCION_ORIGINAL";
    public const string SuperiorReplacement = "SUSTITUCION_SUPERIOR";
}

public static class ValidationText
{
    public static string Foundation(string? value) => Normalize(value, 1000, "FUNDAMENTO_INVALIDO");
    public static string Reason(string? value) => Normalize(value, 500, "MOTIVO_REQUERIDO");

    private static string Normalize(string? value, int maximum, string code)
    {
        var normalized = value?.Normalize(NormalizationForm.FormC).Trim();
        if (string.IsNullOrEmpty(normalized) || normalized.Length > maximum ||
            normalized.Any(character => character is '<' or '>' or '\r' or '\n' ||
                char.IsControl(character) && character != '\t'))
        {
            throw new ValidationDecisionException(code);
        }

        return normalized;
    }
}

public sealed class ValidationRequirement
{
    private ValidationRequirement() { }

    public ValidationRequirement(Guid id, Guid obligationId, Guid policyVersionId, DateTimeOffset createdAt)
    {
        if (id == Guid.Empty || obligationId == Guid.Empty || policyVersionId == Guid.Empty)
            throw new ArgumentException("Validation requirement identifiers are required.");
        Id = id; ObligationId = obligationId; PolicyVersionId = policyVersionId;
        Status = ValidationStatuses.Pending; CreatedAt = createdAt; RowVersion = 1;
    }

    public Guid Id { get; private init; }
    public Guid ObligationId { get; private init; }
    public Guid PolicyVersionId { get; private init; }
    public string Status { get; private set; } = null!;
    public DateTimeOffset CreatedAt { get; private init; }
    public DateTimeOffset? ResolvedAt { get; private set; }
    public long RowVersion { get; private set; }

    public void Resolve(DateTimeOffset at, long expectedRowVersion)
    {
        if (RowVersion != expectedRowVersion || Status != ValidationStatuses.Pending)
            throw new ValidationDecisionException("VERSION_CONFLICT");
        Status = ValidationStatuses.Resolved; ResolvedAt = at;
    }

    public void Advance(DateTimeOffset at, long expectedRowVersion)
    {
        if (RowVersion != expectedRowVersion || Status != ValidationStatuses.Resolved)
            throw new ValidationDecisionException("VERSION_CONFLICT");
        RowVersion = checked(RowVersion + 1); ResolvedAt = at;
    }
}

public sealed class ValidationDecisionVersion
{
    private ValidationDecisionVersion() { }

    public ValidationDecisionVersion(Guid id, Guid requirementId, int versionNo, string result, string foundation,
        string authorityType, Guid validatorUserId, Guid validatorPersonId, string validatorRole,
        Guid assignmentVersionId, Guid responsiblePersonId, DateTimeOffset decidedAt, string? reason,
        Guid? supersedesId, Guid evidenceReviewSnapshotId)
    {
        if (id == Guid.Empty || requirementId == Guid.Empty || validatorUserId == Guid.Empty ||
            validatorPersonId == Guid.Empty || assignmentVersionId == Guid.Empty || responsiblePersonId == Guid.Empty ||
            evidenceReviewSnapshotId == Guid.Empty || versionNo < 1 || !ValidationResults.IsDefined(result))
            throw new ArgumentException("Invalid validation decision.");
        Id = id; RequirementId = requirementId; VersionNo = versionNo; Result = result;
        Foundation = ValidationText.Foundation(foundation); Status = ValidationStatuses.Current;
        AuthorityType = authorityType; ValidatorUserId = validatorUserId; ValidatorPersonId = validatorPersonId;
        ValidatorRole = validatorRole; AssignmentVersionId = assignmentVersionId; ResponsiblePersonId = responsiblePersonId;
        DecidedAt = decidedAt; Reason = reason is null ? null : ValidationText.Reason(reason);
        SupersedesId = supersedesId; EvidenceReviewSnapshotId = evidenceReviewSnapshotId;
    }

    public Guid Id { get; private init; }
    public Guid RequirementId { get; private init; }
    public int VersionNo { get; private init; }
    public string Result { get; private init; } = null!;
    public string Foundation { get; private init; } = null!;
    public string Status { get; private set; } = null!;
    public string AuthorityType { get; private init; } = null!;
    public Guid ValidatorUserId { get; private init; }
    public Guid ValidatorPersonId { get; private init; }
    public string ValidatorRole { get; private init; } = null!;
    public Guid AssignmentVersionId { get; private init; }
    public Guid ResponsiblePersonId { get; private init; }
    public DateTimeOffset DecidedAt { get; private init; }
    public string? Reason { get; private init; }
    public Guid? SupersedesId { get; private init; }
    public Guid EvidenceReviewSnapshotId { get; private init; }

    public void Supersede()
    {
        if (Status != ValidationStatuses.Current) throw new ValidationDecisionException("DECISION_VALIDACION_NO_ENCONTRADA");
        Status = ValidationStatuses.Superseded;
    }
}

public sealed record IssueValidationDecisionCommand(Guid ActorUserId, Guid IdempotencyKey, Guid CorrelationId,
    Guid ObligationId, long ExpectedRowVersion, string Result, string Foundation, string? EscalationReason);
public sealed record ReplaceValidationDecisionCommand(Guid ActorUserId, Guid IdempotencyKey, Guid CorrelationId,
    Guid DecisionVersionId, long ExpectedRowVersion, string Result, string Foundation, string Reason);
public sealed record GetValidationHistoryQuery(Guid ActorUserId, Guid CorrelationId, Guid ObligationId);
public sealed record EnsureValidationRequirementCommand(Guid ObligationId, Guid? PolicyVersionId, DateTimeOffset ConcludedAt);

public sealed record ValidationRequirementDetails(Guid RequirementId, Guid PolicyVersionId, string Status,
    DateTimeOffset CreatedAt, DateTimeOffset? ResolvedAt, long RowVersion);
public sealed record ValidationDecisionDetails(Guid DecisionVersionId, int VersionNo, string Result, string Foundation,
    string Status, string AuthorityType, Guid ValidatorUserId, string ValidatorRole, DateTimeOffset DecidedAt,
    string? Reason, Guid? SupersedesDecisionVersionId, Guid EvidenceReviewSnapshotId,
    IReadOnlyList<Guid> EvidenceVersionIds);
public sealed record ValidationHistoryDetails(Guid ObligationId, string ExecutionStatus, long RowVersion,
    ValidationRequirementDetails? ValidationRequirement, IReadOnlyList<ValidationDecisionDetails> Decisions);
public sealed record ValidationMutationResult(ValidationHistoryDetails History, ValidationDecisionDetails Decision, bool Replayed);

public interface IValidationDecisionService
{
    Task<ValidationMutationResult> IssueAsync(IssueValidationDecisionCommand command, CancellationToken cancellationToken = default);
    Task<ValidationMutationResult> ReplaceAsync(ReplaceValidationDecisionCommand command, CancellationToken cancellationToken = default);
    Task<ValidationHistoryDetails> GetAsync(GetValidationHistoryQuery query, CancellationToken cancellationToken = default);
}

public interface IValidationRequirementWriter
{
    Task EnsureAsync(EnsureValidationRequirementCommand command, CancellationToken cancellationToken = default);
}

public class ValidationDecisionException(string code) : Exception(code) { public string Code { get; } = code; }
public sealed class ValidationAccessDeniedException() : ValidationDecisionException("ACCESO_DENEGADO");
public sealed class ValidationObligationNotFoundException() : ValidationDecisionException("OBLIGACION_NO_ENCONTRADA");
public sealed class ValidationDecisionNotFoundException() : ValidationDecisionException("DECISION_VALIDACION_NO_ENCONTRADA");
public sealed class ValidationConcurrencyException() : ValidationDecisionException("VALIDACION_CONCURRENCIA_CONFLICTO");
