using System.Globalization;
using System.Text.Json;
using Sgol.Evidence.Contracts;
using Sgol.Web.Presentation.ApiClient;
using Sgol.Web.Presentation.ProblemDetails;

namespace Sgol.Web.Presentation.MyWork;

public sealed record EvidenceInput(string Name, string Label, string Type = "text", string[]? Options = null, bool Nullable = false);

public static class EvidenceContributionPresentation
{
    private static EvidenceInput Text(string name, string label) => new(name, label);
    private static EvidenceInput Time(string name, string label, bool nullable = false) => new(name, label, "datetime-local", Nullable: nullable);
    private static EvidenceInput Summary(string name, string label, bool nullable = false) => new(name, label, "textarea", Nullable: nullable);
    private static EvidenceInput Boolean(string name, string label) => new(name, label, "boolean", ["true", "false"]);
    public static IReadOnlyList<EvidenceInput> Inputs(string requirement) => requirement switch
    {
        "CALCULO_AVANCE" => [new("expectedTarget", "Meta esperada", "number"), new("actualSales", "Venta real", "number"), Text("sourceReference", "Referencia de la fuente")],
        "ACCION_O_CONFORMIDAD" => [new("outcome", "Resultado", "select", ["ACCION", "CONFORMIDAD"]), Summary("actionDescription", "Descripción de la acción", true), new("responsiblePersonId", "Identificador de la persona responsable", "uuid", Nullable: true), Time("startsAt", "Inicio de la acción", true)],
        "LIBERACION" => [Time("releasedAt", "Fecha y hora de liberación"), Text("releaseReference", "Referencia de liberación")],
        "MERCANCIA" => [Text("merchandiseReference", "Referencia de mercancía")],
        "FECHA_HORA" => [Time("occurredAt", "Fecha y hora del hecho")],
        "RETORNO_EXHIBICION" => [Time("returnedAt", "Fecha y hora de retorno"), Text("returnReference", "Referencia de retorno")],
        "SECUENCIA" => [Summary("sequenceSummary", "Secuencia de hechos")],
        "DECISION" => [Summary("decisionSummary", "Decisión"), Time("decidedAt", "Fecha y hora de decisión")],
        "FUNDAMENTO" => [Summary("foundationSummary", "Fundamento")],
        "AVISO_INTERNO" or "CONSTANCIA_AVISO_INTERNO" => [Text("noticeReference", "Referencia del aviso interno"), Time("notifiedAt", "Fecha y hora del aviso")],
        "EVALUACION" => [Summary("assessmentSummary", "Evaluación"), Time("assessedAt", "Fecha y hora de evaluación")],
        "REPARACION_O_CAMBIO" => [new("solutionType", "Solución", "select", ["REPARACION", "CAMBIO"]), Text("solutionReference", "Referencia de la solución"), Time("completedAt", "Fecha y hora de terminación")],
        "ENTREGA" => [Text("deliveryReference", "Referencia de entrega"), Time("deliveredAt", "Fecha y hora de entrega")],
        "CHECKLIST_COMPLETO" => [Boolean("productCorrect", "Producto correcto"), Boolean("zoneAndFamilyCorrect", "Zona y familia correctas"), Boolean("stableFormation", "Formación estable"), Boolean("labelsVisible", "Etiquetas visibles"), Boolean("alignmentConsistent", "Alineación consistente"), Boolean("occupancyJustified", "Ocupación justificada"), Boolean("clean", "Limpio"), Boolean("intact", "Íntegro"), Boolean("signageCorrect", "Señalización correcta"), Boolean("matchesPlanogramOrList", "Coincide con el planograma o lista")],
        "FORM_ADM_02" => [Text("formReference", "Referencia del formulario"), Time("completedAt", "Fecha y hora de llenado")],
        "F_ENT_001" => [Text("formReference", "Referencia del formulario"), Time("completedAt", "Fecha y hora de llenado"), Boolean("hasDifference", "¿Hay diferencia?"), Boolean("hasDamage", "¿Hay daño?")],
        "ANOTACION_F_ENT_001" => [Text("formReference", "Referencia del formulario"), Text("annotationReference", "Referencia de la anotación"), Time("recordedAt", "Fecha y hora de registro")],
        _ => throw new ApiProtocolException()
    };

    public static JsonDocument Payload(string taskCode, string requirement, string kind,
        IReadOnlyDictionary<string, string> values, ICollection<string> invalid)
    {
        var body = new Dictionary<string, object?> { ["schemaVersion"] = 1 };
        if (requirement is "FORM_ADM_02" or "F_ENT_001" or "ANOTACION_F_ENT_001")
            body["formCode"] = requirement == "FORM_ADM_02" ? "FORM-ADM-02" : "F-ENT-001";
        foreach (var field in Inputs(requirement))
        {
            var value = values.GetValueOrDefault(field.Name, "");
            if (field.Nullable && values.GetValueOrDefault("outcome") == "CONFORMIDAD") { body[field.Name] = null; continue; }
            try
            {
                body[field.Name] = field.Type switch
                {
                    "number" => decimal.Parse(value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture),
                    "boolean" when value is "true" or "false" => value == "true",
                    "boolean" => throw new FormatException(),
                    "datetime-local" => Utc(value),
                    "uuid" when Guid.TryParseExact(value, "D", out var id) && id != Guid.Empty && value == id.ToString("D") => value,
                    "uuid" => throw new FormatException(),
                    _ => value
                };
                if (string.IsNullOrWhiteSpace(value)) invalid.Add(field.Name);
            }
            catch (Exception ex) when (ex is FormatException or OverflowException or ArgumentException) { invalid.Add(field.Name); }
        }
        if (invalid.Count > 0) throw new EvidenceRequestInvalidException();
        using var raw = JsonSerializer.SerializeToDocument(body);
        try { return StructuredEvidencePayloadValidator.ValidateAndCanonicalize(taskCode, requirement, kind, raw.RootElement); }
        catch (EvidenceRequestInvalidException) { foreach (var field in Inputs(requirement)) invalid.Add(field.Name); throw; }
    }

    private static string Utc(string value)
    {
        if (!DateTime.TryParseExact(value, ["yyyy-MM-dd'T'HH:mm", "yyyy-MM-dd'T'HH:mm:ss"], CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var local)) throw new FormatException();
        return TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified),
            TimeZoneInfo.FindSystemTimeZoneById("America/Mexico_City")).ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
    }

    public static string Option(string value) => value switch
    { "true" => "Sí", "false" => "No", "ACCION" => "Acción", "CONFORMIDAD" => "Conformidad", "REPARACION" => "Reparación", "CAMBIO" => "Cambio", _ => throw new ApiProtocolException() };

    public static string Message(int status, string? code) => code switch
    {
        "INTENCION_CARGA_EXPIRADA" => "La intención de carga venció. Prepara una nueva carga.",
        "ARCHIVO_DEMASIADO_GRANDE" => "El archivo supera 15 MiB.",
        "TIPO_ARCHIVO_NO_ADMITIDO" => "El tipo de archivo no está permitido para este requisito.",
        "CARGA_NO_ENCONTRADA" => "No se encontró la carga para confirmarla.",
        "EVIDENCIA_YA_EXISTE" or "ARCHIVO_YA_VINCULADO" => "La evidencia ya está registrada o el archivo ya fue vinculado.",
        "IDEMPOTENCY_CONFLICT" => "La solicitud original no coincide con estos datos.",
        "CSRF_INVALID" or "CSRF_INVALIDO" => "No se pudo verificar la solicitud. Recarga la página antes de volver a enviarla.",
        "ARCHIVO_NO_LIMPIO" => "El archivo todavía no puede aportarse como evidencia.",
        "CONDICION_EVIDENCIA_NO_RESUELTA" => "Primero aporta un formulario F-ENT-001 válido para determinar si corresponde la fotografía.",
        "REQUISITO_EVIDENCIA_NO_APLICABLE" => "La fotografía no aplica según el formulario F-ENT-001 vigente.",
        "LIMITE_INTENCIONES_EXCEDIDO" => "Se alcanzó el límite de intenciones de carga. Inténtalo más tarde.",
        "PAYLOAD_EVIDENCIA_INVALIDO" or "REQUISITO_EVIDENCIA_INVALIDO" or "SOLICITUD_EVIDENCIA_INVALIDA" => "Revisa los datos del requisito seleccionado.",
        _ => status switch { 404 => "No existe o no está disponible en tu alcance", 403 => "No tienes permiso para realizar esta operación", 401 => "Tu sesión terminó", _ => "No fue posible completar esta operación." }
    };
    public static string Scan(string state) => state switch
    {
        "PENDIENTE_CARGA" => "La carga todavía no está confirmada.",
        "PENDIENTE_ESCANEO" => "Esperando análisis antimalware.",
        "LIMPIO" => "El archivo terminó el análisis y puede vincularse como evidencia.",
        "INFECTADO" => "El archivo fue rechazado por seguridad y no se vinculó.",
        "INVALIDO" => "El archivo no cumple el tipo o formato permitido y no se vinculó.",
        "ERROR_ESCANEO" => "No se pudo completar el análisis. El archivo permanece sin vincular.",
        _ => throw new ApiProtocolException()
    };
}
