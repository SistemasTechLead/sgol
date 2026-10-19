using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Sgol.Assignment.Contracts;

public static class AssignmentCorrectionAuthorization
{
    public const string Correct = "PER-ASIGNACION-CORREGIR";
}

public static class AssignmentCorrectionResults
{
    public const string Created = "CREADA";
    public const string Recovered = "RECUPERADA";
}

public static class AssignmentCorrectionErrors
{
    public const string AccessDenied = "ACCESO_DENEGADO";
    public const string IdempotencyConflict = "IDEMPOTENCY_CONFLICT";
    public const string ObligationNotFound = "OBLIGACION_NO_ENCONTRADA";
    public const string ObligationNotCorrectable = "OBLIGACION_NO_CORREGIBLE";
    public const string VersionConflict = "VERSION_CONFLICT";
    public const string CurrentAssignmentNotFound = "ASIGNACION_VIGENTE_NO_ENCONTRADA";
    public const string EvaluationNotFound = "EVALUACION_ELEGIBILIDAD_NO_ENCONTRADA";
    public const string EvaluationStale = "EVALUACION_ELEGIBILIDAD_DESACTUALIZADA";
    public const string EvaluationIncompatible = "EVALUACION_ELEGIBILIDAD_INCOMPATIBLE";
    public const string ResponsibleIneligible = "RESPONSABLE_INELEGIBLE";
    public const string NoChange = "ASIGNACION_SIN_CAMBIO";
    public const string ConcurrencyConflict = "ASSIGNMENT_CORRECTION_CONCURRENCY_CONFLICT";
    public const string IntegrityConflict = "ASSIGNMENT_CORRECTION_CONFLICT";
}

public static partial class AssignmentCorrectionReason
{
    public const int MinimumLength = 10;
    public const int MaximumLength = 500;

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex Whitespace();

    public static string Normalize(string? value)
    {
        if (value is null)
        {
            throw new AssignmentCorrectionReasonInvalidException();
        }

        var normalized = Whitespace().Replace(value.Normalize(NormalizationForm.FormC), " ").Trim();
        if (normalized.Length is < MinimumLength or > MaximumLength)
        {
            throw new AssignmentCorrectionReasonInvalidException();
        }

        return normalized;
    }
}

public sealed record CorrectAssignmentCommand(
    Guid ActorUserId,
    Guid IdempotencyKey,
    Guid CorrelationId,
    Guid ObligationId,
    Guid NewResponsiblePersonId,
    Guid EligibilityEvaluationId,
    string Reason,
    long ExpectedRowVersion);

public sealed record AssignmentCorrectionResult(
    string Result,
    Guid AssignmentId,
    Guid ObligationId,
    Guid PreviousAssignmentId,
    Guid NewResponsiblePersonId,
    string Status,
    string AssignmentType,
    string Reason,
    Guid AssignedBy,
    DateTimeOffset AssignedAt,
    Guid SupersedesId,
    long RowVersion);

public interface IAssignmentCorrectionService
{
    Task<AssignmentCorrectionResult> CorrectAsync(
        CorrectAssignmentCommand command,
        CancellationToken cancellationToken = default);
}

public class AssignmentCorrectionException(string errorCode) : Exception(errorCode)
{
    public string ErrorCode { get; } = errorCode;
}

public sealed class AssignmentCorrectionAccessDeniedException()
    : AssignmentCorrectionException(AssignmentCorrectionErrors.AccessDenied);
public sealed class AssignmentCorrectionIdempotencyConflictException()
    : AssignmentCorrectionException(AssignmentCorrectionErrors.IdempotencyConflict);
public sealed class AssignmentCorrectionObligationNotFoundException()
    : AssignmentCorrectionException(AssignmentCorrectionErrors.ObligationNotFound);
public sealed class AssignmentCorrectionObligationNotCorrectableException()
    : AssignmentCorrectionException(AssignmentCorrectionErrors.ObligationNotCorrectable);
public sealed class AssignmentCorrectionVersionConflictException()
    : AssignmentCorrectionException(AssignmentCorrectionErrors.VersionConflict);
public sealed class AssignmentCorrectionCurrentAssignmentNotFoundException()
    : AssignmentCorrectionException(AssignmentCorrectionErrors.CurrentAssignmentNotFound);
public sealed class AssignmentCorrectionEvaluationNotFoundException()
    : AssignmentCorrectionException(AssignmentCorrectionErrors.EvaluationNotFound);
public sealed class AssignmentCorrectionEvaluationStaleException()
    : AssignmentCorrectionException(AssignmentCorrectionErrors.EvaluationStale);
public sealed class AssignmentCorrectionEvaluationIncompatibleException()
    : AssignmentCorrectionException(AssignmentCorrectionErrors.EvaluationIncompatible);
public sealed class AssignmentCorrectionEvaluationMismatchException()
    : AssignmentCorrectionException(AssignmentCorrectionErrors.EvaluationIncompatible);
public sealed class AssignmentCorrectionResponsibleIneligibleException()
    : AssignmentCorrectionException(AssignmentCorrectionErrors.ResponsibleIneligible);
public sealed class AssignmentCorrectionNoChangeException()
    : AssignmentCorrectionException(AssignmentCorrectionErrors.NoChange);
public sealed class AssignmentCorrectionConcurrencyConflictException()
    : AssignmentCorrectionException(AssignmentCorrectionErrors.ConcurrencyConflict);
public sealed class AssignmentCorrectionIntegrityConflictException()
    : AssignmentCorrectionException(AssignmentCorrectionErrors.IntegrityConflict);
public sealed class AssignmentCorrectionReasonInvalidException() : Exception("MOTIVO_INVALIDO");
