using System.Globalization;
using Sgol.Execution.Contracts;
using Sgol.Notifications.Contracts;
using Sgol.Web.Presentation.ApiClient;
using Sgol.Web.Presentation.Components;
using Sgol.Web.Presentation.ProblemDetails;

namespace Sgol.Web.Presentation.MyWork;

public sealed record InboxSectionData<T>(IReadOnlyList<T> Items, string? NextCursor, int Count, bool IsEmpty);
public sealed record InboxData(InboxPeriod? Period, InboxSectionData<InboxTask> Tasks,
    InboxSectionData<InboxNotice> Notices, bool IsEmpty);

public static class MyWorkPresentation
{
    private static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("America/Mexico_City");
    public static string Time(DateTimeOffset? value) => value is null ? "Sin fecha registrada" :
        TimeZoneInfo.ConvertTime(value.Value, Zone).ToString("dd MMM yyyy, HH:mm", CultureInfo.GetCultureInfo("es-MX"));
    public static string Period(int year, int week, DateOnly from, DateOnly to) =>
        $"{year} / {week:00} · {from:yyyy-MM-dd} – {to:yyyy-MM-dd}";
    public static string Origin(string kind) => kind switch
    {
        "MANUAL" => "Manual",
        "RECURRENTE" => "Programada",
        _ => throw new ApiProtocolException()
    };
    public static StatusBadgeViewModel Badge(string value) => value switch
    {
        "PENDIENTE" => new("Pendiente", "◷", "advertencia"),
        "CONCLUIDA" => new("Concluida", "✓", "exito"),
        "FUTURA" => new("Futura", "▦", "info"),
        "DISPONIBLE" => new("Disponible", "ⓘ", "info"),
        "VENCIDA" => new("Vencida", "◷!", "peligro"),
        "COMPLETA" => new("Completa", "●✓", "exito"),
        "INCOMPLETA" => new("Incompleta", "△", "advertencia"),
        "UNREAD" => new("Sin leer", "✉", "info"),
        "READ" => new("Leído", "⌑", "neutro"),
        _ => throw new ApiProtocolException()
    };
    public static string HistoryName(string value) => value switch
    {
        "GENERACION_SOLICITADA" => "Generación solicitada",
        "ASIGNACION_AUTOMATICA" => "Asignación automática",
        "ASIGNACION_CORREGIDA" => "Asignación corregida",
        "PUBLICACION_INCLUIDA" => "Incluida en publicación",
        _ => throw new ApiProtocolException()
    };
    public static string MissingReason(string value) => value switch
    {
        "EVIDENCIA_VIGENTE_AUSENTE" => "Falta evidencia vigente",
        "EVIDENCIA_VIGENTE_NO_SATISFACE" => "La evidencia vigente no satisface el requisito",
        _ => throw new ApiProtocolException()
    };

    public static void Validate(InboxData data)
    {
        if (data.Tasks?.Items is null || data.Notices?.Items is null ||
            data.Tasks.Items.Count > 100 || data.Notices.Items.Count > 100 ||
            data.Tasks.Count != data.Tasks.Items.Count || data.Notices.Count != data.Notices.Items.Count ||
            data.Tasks.IsEmpty != (data.Tasks.Count == 0) || data.Notices.IsEmpty != (data.Notices.Count == 0) ||
            data.IsEmpty != (data.Tasks.IsEmpty && data.Notices.IsEmpty)) throw new ApiProtocolException();
        foreach (var task in data.Tasks.Items)
        {
            if (task.ObligationId == Guid.Empty || task.Task is null || task.Period is null ||
                task.Dates is null || task.Evidence?.MissingRequirements is null || task.AllowedActions is null ||
                task.Evidence.Result is not ("COMPLETA" or "INCOMPLETA") ||
                task.AllowedActions.Any(a => a is not ("VIEW_TASK" or "CONTRIBUTE_EVIDENCE" or "CONCLUDE_TASK")) ||
                task.ExecutionStatus is not ("PENDIENTE" or "CONCLUIDA") ||
                task.TaskState is not ("FUTURA" or "DISPONIBLE" or "VENCIDA" or "CONCLUIDA") ||
                (task.ExecutionStatus == "CONCLUIDA") != (task.TaskState == "CONCLUIDA") ||
                task.Scheduled != (task.OriginKind == "RECURRENTE")) throw new ApiProtocolException();
            _ = Origin(task.OriginKind); _ = Badge(task.Evidence.Result);
            foreach (var missing in task.Evidence.MissingRequirements) _ = MissingReason(missing.MissingReason);
        }
        foreach (var notice in data.Notices.Items)
        {
            if (notice.NoticeId == Guid.Empty || notice.NoticeType != "OBLIGATION_ASSIGNED" ||
                notice.Resource is null || notice.Resource.ResourceType != "ASSIGNMENT_VERSION" ||
                notice.AllowedActions is null || notice.AllowedActions.Any(a => a != "MARK_NOTICE_READ") ||
                notice.Status is not ("READ" or "UNREAD") ||
                (notice.Status == "READ") != notice.ReadAt.HasValue ||
                notice.Status == "READ" && notice.AllowedActions.Count != 0 ||
                notice.Resource.Available && (notice.Resource.ObligationId is null || notice.Resource.TaskCode is null || notice.Resource.TaskName is null) ||
                !notice.Resource.Available && (notice.Resource.ObligationId is not null || notice.Resource.TaskCode is not null || notice.Resource.TaskName is not null))
                throw new ApiProtocolException();
        }
    }

    public static void Validate(ObligationListItem item)
    {
        if (item.ObligationId == Guid.Empty || item.Task is null || item.Task.Version is null || item.Period is null ||
            item.Dates is null || item.Origin is null || item.Links is null ||
            item.CurrentAssignment is { Responsible: null } ||
            item.ExecutionStatus is not ("PENDIENTE" or "CONCLUIDA") || item.Condition is not ("VENCIDA" or "NO_VENCIDA") ||
            item.ExecutionStatus == "CONCLUIDA" && item.Condition == "VENCIDA") throw new ApiProtocolException();
        _ = Origin(item.Origin.Kind);
    }
    public static void Validate(ObligationDetail item)
    {
        Validate(new ObligationListItem(item.ObligationId, item.Task, item.Origin, item.Period,
            item.Dates, item.ExecutionStatus, item.Condition, item.CurrentAssignment, item.Links));
        if (item.GenerationRequest is null || item.History is null || item.History.Count > 100) throw new ApiProtocolException();
        foreach (var ev in item.History)
        {
            _ = HistoryName(ev.EventType);
            if (ev.ActorType is not ("HUMAN" or "SYSTEM") || ev.Assignment is not null && ev.Publication is not null ||
                ev.Assignment is { Responsible: null } or { Status: not ("VIGENTE" or "SUSTITUIDA") } ||
                ev.Publication is { ScopeRole: not ("DIRECCION" or "ADMINISTRACION" or "SUBCOORDINACION" or "PISO_VENTAS") }) throw new ApiProtocolException();
        }
    }

    public static ProblemDetailsPresentation Message<T>(ApiResponse<T> response, bool inbox) =>
        Message(response.Status, response.ErrorCode, response.CorrelationId, inbox);
    public static ProblemDetailsPresentation Message(int status, string? code, string? correlation, bool inbox) => (status, code) switch
    {
        (400, "CSRF_INVALIDO" or "CSRF_INVALID") => new("No se pudo verificar la solicitud", "Recarga la página antes de volver a enviarla.", correlation),
        (400, "FILTRO_BANDEJA_INVALIDO" or "FILTRO_OBLIGACIONES_INVALIDO" or "FILTRO_HISTORIA_INVALIDO") => new("Revisa los filtros de esta consulta", "Limpia los filtros y vuelve a consultar.", correlation),
        (400, "AVISO_ID_INVALIDO" or "SOLICITUD_LECTURA_AVISO_INVALIDA" or "OBLIGACION_ID_INVALIDO") => new("La solicitud no es válida", "Vuelve a la consulta e inténtalo de nuevo.", correlation),
        (403, "ACCESO_DENEGADO") => new(inbox ? "No tienes permiso para consultar tu bandeja" : "No tienes permiso para consultar tareas", "", correlation),
        (404, "AVISO_NO_ENCONTRADO" or "OBLIGACION_NO_ENCONTRADA") => new("No existe o no está disponible en tu alcance", "Vuelve a la consulta.", correlation),
        (409, "BANDEJA_INCONSISTENTE") => new("No se pudo consultar tu bandeja", "Recárgala antes de continuar.", correlation),
        (409, "LECTURA_AVISO_CONCURRENCIA_CONFLICTO") => new("No se pudo confirmar la lectura del aviso", "Consulta tus avisos antes de volver a marcarlo.", correlation),
        _ => new("No se pudo completar la operación", "Recarga la consulta antes de continuar.", correlation)
    };
}
