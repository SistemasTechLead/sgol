using System.Text.Json;
using Sgol.Evidence.Contracts;
using Sgol.Execution.Contracts;
using Sgol.Web.Presentation.ApiClient;
using Sgol.Web.Presentation.Components;
using Sgol.Web.Presentation.ProblemDetails;

namespace Sgol.Web.Presentation.MyWork;

public static class EvidenceReviewPresentation
{
    public static StatusBadgeViewModel VersionBadge(string status) => status switch
    {
        "VIGENTE" => new("Vigente", "✓", "exito"),
        "SUSTITUIDA" => new("Sustituida", "◷", "info"),
        _ => throw new ApiProtocolException()
    };
    public static string Applicability(string value) => value switch
    { "APLICABLE" => "Aplicable", "NO_APLICABLE" => "No aplicable", "NO_RESUELTA" => "Sin resolver", _ => throw new ApiProtocolException() };
    public static string Satisfaction(EvidenceReviewRequirementDetails item) => item.Applicability switch
    { "NO_APLICABLE" => "No requerido", "NO_RESUELTA" => "Sin resolver", "APLICABLE" => item.Satisfied ? "Satisfecho" : "Faltante", _ => throw new ApiProtocolException() };
    public static string Missing(string value) => value switch
    { "EVIDENCIA_VIGENTE_AUSENTE" => "Sin evidencia vigente", "EVIDENCIA_VIGENTE_NO_SATISFACE" => "La evidencia vigente no satisface el requisito", _ => throw new ApiProtocolException() };
    public static void Validate(EvidenceDetails item, ObligationDetail detail)
    {
        // JsonDocument's converter represents JSON null as a document whose root is null.
        var hasPayload = item.StructuredPayload is { } document && document.RootElement.ValueKind != JsonValueKind.Null;
        if (item.EvidenceItemId == Guid.Empty || item.ItemRowVersion < 1 || item.Requirement is null || item.Version is null ||
            item.Version.EvidenceVersionId == Guid.Empty || item.Version.VersionNo < 1 || item.Version.SubmittedByUserId == Guid.Empty ||
            (item.File is not null) == hasPayload || detail.EvidencePolicy?.Requirements.Any(r =>
            r.RequirementVersionId == item.Requirement.RequirementVersionId && r.RequirementCode == item.Requirement.RequirementCode && r.Kind == item.Requirement.Kind) != true)
            throw new ApiProtocolException();
        _ = VersionBadge(item.Version.Status);
        if (hasPayload && item.StructuredPayload is { } payload)
        {
            try { using var valid = StructuredEvidencePayloadValidator.ValidateAndCanonicalize(detail.Task.TaskCode, item.Requirement.RequirementCode, item.Requirement.Kind, payload.RootElement); }
            catch (EvidenceRequestInvalidException) { throw new ApiProtocolException(); }
        }
        else if (item.File is not { FileId: var id } || id == Guid.Empty || item.File.SizeBytes is < 1 or > 15728640 ||
                 !(item.Requirement.Kind == "FOTOGRAFIA" && item.File.MediaType is "image/jpeg" or "image/png" ||
                   item.Requirement.Kind == "DOCUMENTO_REFERENCIADO" && item.File.MediaType == "application/pdf") ||
                 (item.Requirement.RequirementCode == "DOCUMENTO_RECEPCION" ? item.File.DocumentSubtype is not ("NOTA" or "REMISION" or "FACTURA") : item.File.DocumentSubtype is not null)) throw new ApiProtocolException();
    }
    public static void Validate(EvidenceReviewDetails review, ObligationDetail detail)
    {
        if (review.SnapshotId == Guid.Empty || review.ObligationId != detail.ObligationId || review.EvidencePolicyVersionId != detail.EvidencePolicy?.EvidencePolicyVersionId ||
            review.Result is not ("COMPLETA" or "INCOMPLETA") || review.Requirements is null || review.MissingRequirements is null ||
            review.Requirements.Count != detail.EvidencePolicy.Requirements.Count) throw new ApiProtocolException();
        for (var i = 0; i < review.Requirements.Count; i++)
        {
            var r = review.Requirements[i]; var expected = detail.EvidencePolicy.Requirements[i];
            if (r.RequirementVersionId != expected.RequirementVersionId || r.RequirementCode != expected.RequirementCode || r.Kind != expected.Kind ||
                r.ConditionCode != expected.ConditionCode || r.Ordinal != expected.Ordinal || r.EvidenceVersionId == Guid.Empty) throw new ApiProtocolException();
            _ = Applicability(r.Applicability);
            if (r.Applicability == "NO_APLICABLE" && (!r.Satisfied || r.EvidenceVersionId is not null) ||
                r.Applicability == "NO_RESUELTA" && (r.Satisfied || r.EvidenceVersionId is not null) ||
                r.Applicability == "APLICABLE" && r.Satisfied && r.EvidenceVersionId is null ||
                r.MissingReason is not null && (r.Applicability != "APLICABLE" || r.Satisfied)) throw new ApiProtocolException();
            if (r.Applicability == "APLICABLE" && !r.Satisfied) _ = Missing(r.MissingReason ?? "");
        }
        var missing = review.Requirements.Where(r => r.Applicability == "APLICABLE" && !r.Satisfied)
            .Select(r => new EvidenceReviewMissingRequirement(r.RequirementVersionId, r.RequirementCode, r.Kind, r.ConditionCode, r.Ordinal, r.MissingReason!));
        if (!missing.SequenceEqual(review.MissingRequirements) || (review.Result == "COMPLETA") !=
            (review.MissingRequirements.Count == 0 && review.Requirements.All(r => r.Applicability != "NO_RESUELTA"))) throw new ApiProtocolException();
    }
    public static string Value(JsonElement payload, EvidenceInput field)
    {
        if (!payload.TryGetProperty(field.Name, out var value)) throw new ApiProtocolException();
        if (value.ValueKind == JsonValueKind.Null) return "—";
        return field.Type switch
        {
            "boolean" => EvidenceContributionPresentation.Option(value.GetBoolean() ? "true" : "false"),
            "select" => EvidenceContributionPresentation.Option(value.GetString()!),
            "datetime-local" => MyWorkPresentation.Time(value.GetDateTimeOffset()),
            _ => value.ValueKind == JsonValueKind.String ? value.GetString()! : value.GetRawText()
        };
    }
    public static ProblemDetailsPresentation Error(int status, string? code, string? correlation) => new(code switch
    {
        "REVISION_EVIDENCIA_NO_DISPONIBLE" => "No se pudo comprobar la evidencia de esta tarea. No puede concluirse desde esta consulta",
        "MOTIVO_REQUERIDO" => "Escribe un motivo válido de hasta 500 caracteres",
        "SUSTITUCION_NO_PERMITIDA" => "No puedes sustituir esta evidencia con tu autorización actual",
        "EVIDENCIA_FALTANTE" => "La tarea sigue pendiente porque falta evidencia",
        "OBLIGACION_YA_CONCLUIDA" => "La tarea ya está concluida",
        "VERSION_CONFLICT" => "Este registro cambió mientras lo editabas",
        "IF_MATCH_INVALIDO" or "IF_MATCH_REQUERIDO" => "La versión del registro no es válida",
        "CONCLUSION_CONCURRENCIA_CONFLICTO" => "No se pudo confirmar la conclusión por un cambio simultáneo. Consulta el estado de la tarea antes de continuar",
        "CONCLUSION_INCONSISTENTE" => "No se pudo comprobar la conclusión de esta tarea",
        "FILTRO_EVIDENCIA_INVALIDO" => "Revisa los filtros de versiones",
        _ => EvidenceContributionPresentation.Message(status, code)
    }, code == "VERSION_CONFLICT" ? "Probablemente alguien más lo actualizó. Recarga para ver la versión más reciente antes de guardar, o tus cambios podrían sobrescribir los de la otra persona." : "", correlation);
}
