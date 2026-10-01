using Sgol.Validation.Contracts;
using Sgol.Web.Presentation.ApiClient;
using Sgol.Web.Presentation.Components;
using Sgol.Web.Presentation.ProblemDetails;

namespace Sgol.Web.Presentation.Validation;

public sealed record ValidationHistoryData(Guid ObligationId, string ExecutionStatus,
    ValidationRequirementDetails? ValidationRequirement, IReadOnlyList<ValidationDecisionDetails> Decisions, ValidationActions? ValidationActions);
public sealed record ValidationMutationData(Guid ObligationId, string ExecutionStatus,
    ValidationRequirementDetails ValidationRequirement, ValidationDecisionDetails Decision);

public static class ValidationPresentation
{
    public static string Result(string value) => value switch { "CUMPLIDA" => "Cumplida", "INCOMPLETA" => "Incompleta", "NO_CUMPLIDA" => "No cumplida", _ => throw new ApiProtocolException() };
    public static string Authority(string value) => value switch
    {
        "ORDINARIA" => "Superior inmediato", "ESCALAMIENTO" => "Escalamiento", "AUTOVALIDACION_DIRECCION" => "Autovalidación excepcional de Dirección",
        "SUSTITUCION_ORIGINAL" => "Sustitución por validador original", "SUSTITUCION_SUPERIOR" => "Sustitución por superior", _ => throw new ApiProtocolException()
    };
    public static StatusBadgeViewModel Badge(string value) => value switch
    {
        "PENDIENTE" => new("Pendiente de validación", "◷", "advertencia"), "RESUELTA" => new("Resuelta", "✓", "info"),
        "VIGENTE" => new("Vigente", "✓", "exito"), "SUSTITUIDA" => new("Sustituida", "◷", "info"),
        "CUMPLIDA" => new(Result(value), "✓", "exito"), "INCOMPLETA" => new(Result(value), "△", "advertencia"),
        "NO_CUMPLIDA" => new(Result(value), "✕", "peligro"), _ => throw new ApiProtocolException()
    };
    public static bool StrongEtag(string? value) => value is { Length: >= 3 } && value[0] == '"' && value[^1] == '"' &&
        long.TryParse(value.AsSpan(1, value.Length - 2), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var version) && version > 0 && value == $"\"{version}\"";
    public static void Validate(ValidationHistoryData data, Guid id, string? etag)
    {
        if (data.ObligationId != id || !StrongEtag(etag) || data.ExecutionStatus is not ("PENDIENTE" or "CONCLUIDA") || data.Decisions is null) throw new ApiProtocolException();
        if (data.ValidationRequirement is { } requirement)
        {
            if (requirement.RequirementId == Guid.Empty || requirement.PolicyVersionId == Guid.Empty ||
                requirement.Status is not ("PENDIENTE" or "RESUELTA") || etag != $"\"{requirement.RowVersion}\"" ||
                (requirement.Status == "RESUELTA") != (data.Decisions.Count > 0)) throw new ApiProtocolException();
        }
        else if (data.Decisions.Count != 0) throw new ApiProtocolException();
        foreach (var decision in data.Decisions) Validate(decision);
        if (data.Decisions.Count > 0 && (data.ExecutionStatus != "CONCLUIDA" || data.Decisions.Count(d => d.Status == "VIGENTE") != 1 ||
            data.Decisions[0].Status != "VIGENTE" || data.Decisions.Select(d => d.DecisionVersionId).Distinct().Count() != data.Decisions.Count ||
            !data.Decisions.Select(d => d.VersionNo).SequenceEqual(Enumerable.Range(1, data.Decisions.Count).Reverse()))) throw new ApiProtocolException();
        for (var index = 0; index < data.Decisions.Count; index++)
            if (data.Decisions[index].SupersedesDecisionVersionId != (index + 1 < data.Decisions.Count ? data.Decisions[index + 1].DecisionVersionId : (Guid?)null)) throw new ApiProtocolException();
        if (data.ValidationActions is { } actions && (actions.IssueAuthority is not (null or "ORDINARIA" or "ESCALAMIENTO" or "AUTOVALIDACION_DIRECCION") ||
            actions.IssueAuthority is not null && (data.ExecutionStatus != "CONCLUIDA" || data.Decisions.Count != 0) ||
            actions.CanReplace && data.Decisions.Count == 0)) throw new ApiProtocolException();
    }
    public static void Validate(ValidationDecisionDetails decision)
    {
        if (decision is null || decision.DecisionVersionId == Guid.Empty || decision.ValidatorUserId == Guid.Empty || decision.EvidenceReviewSnapshotId == Guid.Empty ||
            decision.VersionNo < 1 || decision.Status is not ("VIGENTE" or "SUSTITUIDA") || !ValidationResults.IsDefined(decision.Result) ||
            string.IsNullOrWhiteSpace(decision.Foundation) || decision.EvidenceVersionIds is null || decision.EvidenceVersionIds.Any(v => v == Guid.Empty) ||
            decision.ValidatorRole is not ("DIRECCION" or "ADMINISTRACION" or "SUBCOORDINACION")) throw new ApiProtocolException();
        _ = Authority(decision.AuthorityType);
        if (decision.AuthorityType is "ESCALAMIENTO" or "SUSTITUCION_ORIGINAL" or "SUSTITUCION_SUPERIOR" && string.IsNullOrWhiteSpace(decision.Reason)) throw new ApiProtocolException();
    }
    public static ProblemDetailsPresentation Error(int status, string? code, string? correlation, string section = "decision")
    {
        var text = code switch
        {
            "FILTRO_SUPERVISION_INVALIDO" => "Revisa los filtros de supervisión",
            "FILTRO_VALIDACIONES_PENDIENTES_INVALIDO" => "Revisa los filtros de pendientes",
            "OBLIGACION_NO_ENCONTRADA" or "DECISION_VALIDACION_NO_ENCONTRADA" => "No existe o no está disponible en tu alcance",
            "OBLIGACION_NO_CONCLUIDA" => "La tarea debe estar concluida antes de validarla",
            "POLITICA_VALIDACION_NO_DISPONIBLE" => "Esta tarea no tiene una política de validación disponible",
            "EVIDENCIA_VALIDACION_NO_DISPONIBLE" => "No se pudo verificar la evidencia que sustenta esta decisión. Recarga la tarea antes de continuar.",
            "DECISION_VALIDACION_YA_EXISTE" => "Esta tarea ya tiene una decisión vigente. Consulta el historial; una sustitución exige autorización y motivo.",
            "IDEMPOTENCY_CONFLICT" => "Esta intención ya se utilizó con otros datos. Consulta el historial antes de preparar otra decisión.",
            "VALIDACION_CONCURRENCIA_CONFLICTO" => "No se pudo confirmar la decisión por un conflicto de concurrencia. Consulta el historial antes de continuar.",
            "VERSION_CONFLICT" or "IF_MATCH_INVALIDO" or "IF_MATCH_REQUERIDO" => "Esta validación cambió mientras la preparabas. Recarga las validaciones y decide de nuevo.",
            "RESULTADO_VALIDACION_INVALIDO" => "Elige Cumplida, Incompleta o No cumplida",
            "FUNDAMENTO_INVALIDO" => "Ingresa un fundamento válido de 1 a 1000 caracteres, sin datos prohibidos",
            "MOTIVO_REQUERIDO" => "Ingresa un motivo válido de 1 a 500 caracteres, sin datos prohibidos",
            "AUTOVALIDACION_NO_PERMITIDA" => "Sólo Dirección puede autovalidar su propia tarea",
            "SOLICITUD_VALIDACION_INVALIDA" or "OBLIGACION_ID_INVALIDO" or "DECISION_ID_INVALIDO" => "No se pudo preparar la validación. Revisa los datos y recarga la tarea si el problema continúa.",
            "IDEMPOTENCY_KEY_INVALIDA" => "La intención de la solicitud no es válida. Consulta el historial antes de preparar otra decisión.",
            "CSRF_INVALID" or "CSRF_INVALIDO" => "No se pudo verificar la solicitud. Recarga la página antes de volver a enviarla.",
            _ when status == 401 => "Tu sesión terminó. Inicia sesión nuevamente.",
            _ when status == 404 => "No existe o no está disponible en tu alcance",
            _ when status == 403 => section switch { "pending" => "No tienes permiso para consultar pendientes de validación", "supervision" => "No tienes permiso para supervisar estas tareas", _ => "No tienes permiso para realizar esta validación" },
            _ => "No se pudo obtener información consistente. Recarga la sección; si el problema continúa, informa el identificador de seguimiento."
        };
        return new(text, "", correlation);
    }
}
