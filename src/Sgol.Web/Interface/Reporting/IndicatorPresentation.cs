using Sgol.Reporting.Contracts;
using Sgol.Web.Presentation.ApiClient;
using Sgol.Web.Presentation.ProblemDetails;

namespace Sgol.Web.Presentation.Reporting;

public static class IndicatorPresentation
{
    public static void Validate(IndicatorSnapshot data)
    {
        if (data is null || data.Period is null || data.Scope is null || data.Scope.BranchCode != "LOR-001" ||
            data.Period.TimeZone != "America/Mexico_City" || data.Scope.IncludedLevels is null || data.Scope.IncludedLevels.Any(l => !Sgol.Identity.Contracts.CanonicalRole.IsDefined(l)) || data.BaseObligationsCount < 0 || data.ActiveLoadByPerson is null ||
            data.ActiveLoadByPerson.Items is null || data.ActiveLoadByPerson.Items.Count > 100) throw new ApiProtocolException();
        foreach (var count in new[] { data.Pending, data.Concluded, data.Validated, data.NonCompliant })
            if (count is null || count.Count < 0 || count.Count > data.BaseObligationsCount || count.Denominator != data.BaseObligationsCount) throw new ApiProtocolException();
        if (data.Pending.Count != data.BaseObligationsCount - data.Concluded.Count || data.NonCompliant.Count > data.Validated.Count ||
            data.ActiveLoadByPerson.Denominator != data.Pending.Count) throw new ApiProtocolException();
        foreach (var item in data.ActiveLoadByPerson.Items)
            if (item is null || item.Person is null || item.Person.Id == Guid.Empty || item.Count < 0 || item.Count > data.Pending.Count ||
                string.IsNullOrWhiteSpace(item.Person.StableCode) || string.IsNullOrWhiteSpace(item.Person.DisplayName) ||
                !Sgol.Identity.Contracts.CanonicalRole.IsDefined(item.Level)) throw new ApiProtocolException();
    }
    public static ProblemDetailsPresentation Error(int status, string? code, string? correlation, bool continuity = false)
    {
        var message = code switch
        {
            "FILTRO_INDICADORES_INVALIDO" or "FILTRO_DIRECCION_INVALIDO" => "Revisa el año, la semana y los filtros de la consulta.",
            "CONSULTA_INDICADORES_INCONSISTENTE" or "CONSULTA_DIRECCION_INCONSISTENTE" => "No se pudieron conciliar los datos de esta consulta. No se muestran conteos parciales.",
            "AUDIT_FILTER_INVALID" => "Revisa el intervalo UTC y la combinación de filtros.",
            "AUDIT_CURSOR_INVALID" => "La continuación de la consulta ya no es válida. Inicia una nueva consulta.",
            "AUDIT_SCOPE_INCONSISTENT" => "No se pudo reconstruir esta traza de forma íntegra. No se muestran eslabones parciales.",
            "MOTIVO_INVALIDO" => "Escribe un motivo de 1 a 500 caracteres en una sola línea, sin los signos < y >.",
            "SOLICITUD_RECONCILIACION_INVALIDA" or "APROBACION_RECONCILIACION_INVALIDA" => "Revisa el motivo y prepara de nuevo la solicitud o aprobación.",
            "IDEMPOTENCY_CONFLICT" => "Esta intención ya se utilizó con otros datos. No se ha creado otra operación.",
            "VERSION_CONFLICT" => "Esta reconciliación cambió. Recarga su resultado antes de preparar otra aprobación.",
            "IF_MATCH_REQUERIDO" => "Falta la versión de la reconciliación. Recarga la reconciliación antes de aprobarla.",
            "RECONCILIACION_NO_APROBABLE" => "Este resultado no reúne las condiciones para aprobarse.",
            "RECONCILIATION_AUDIT_FAILED" => "No se pudo registrar la consulta en auditoría. No se entrega el reporte.",
            "CSRF_INVALID" or "CSRF_INVALIDO" => "No se pudo verificar la solicitud. Recarga la página antes de volver a enviarla.",
            _ when status == 403 => continuity ? "La continuidad está disponible únicamente para Dirección con autorización vigente." : "No tienes permiso vigente para consultar esta sección.",
            _ when status == 404 => "No existe o no está disponible en tu alcance",
            _ => "No se pudo completar la operación. Conserva el identificador de correlación para solicitar revisión."
        };
        return new(message, "", correlation);
    }
}
