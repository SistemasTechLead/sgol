using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Primitives;
using Sgol.Notifications.Contracts;
using Sgol.Web.Infrastructure.Http;

namespace Sgol.Web.Presentation.Endpoints;

public static class InboxApiEndpoints
{
    private static readonly HashSet<string> Parameters = new(
        ["periodId", "taskState", "taskCursor", "taskLimit", "noticeStatus", "noticeCursor", "noticeLimit"], StringComparer.Ordinal);

    public static IEndpointRouteBuilder MapInboxApi(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/me/inbox", ReadAsync);
        endpoints.MapPost("/api/v1/me/notices/{id}/read", MarkReadAsync);
        return endpoints;
    }

    public static async Task<IResult> ReadAsync(HttpContext context, IInboxReader reader, CancellationToken cancellationToken)
    {
        if (!TryActor(context, out var actor, out var failure)) return failure!;
        if (CanHaveBody(context.Request) || context.Request.Headers.ContainsKey("Idempotency-Key") ||
            context.Request.Headers.ContainsKey("If-Match") || !TryQuery(context.Request.Query, actor, out var query))
            return Problem(context, 400, "FILTRO_BANDEJA_INVALIDO", "Los filtros de la bandeja no son válidos");
        try
        {
            var page = await reader.ReadAsync(query!, cancellationToken);
            context.Response.Headers.CacheControl = "private, no-store";
            return Results.Ok(new
            {
                data = new
                {
                    page.Period,
                    tasks = new { items = page.Tasks.Items, page.Tasks.NextCursor, count = page.Tasks.Items.Count, isEmpty = page.Tasks.Items.Count == 0 },
                    notices = new { items = page.Notices.Items, page.Notices.NextCursor, count = page.Notices.Items.Count, isEmpty = page.Notices.Items.Count == 0 },
                    isEmpty = page.Tasks.Items.Count == 0 && page.Notices.Items.Count == 0,
                },
                meta = new { page.QueriedAt, correlationId = context.GetCorrelationId() },
            });
        }
        catch (InboxFilterInvalidException) { return Problem(context, 400, "FILTRO_BANDEJA_INVALIDO", "Los filtros de la bandeja no son válidos"); }
        catch (InboxAccessDeniedException) { return Problem(context, 403, "ACCESO_DENEGADO", $"Se requiere {InboxAuthorization.ViewOwn}"); }
        catch (InboxInconsistentException) { return Problem(context, 409, "BANDEJA_INCONSISTENTE", "La bandeja no pudo proyectarse de forma coherente"); }
    }

    public static async Task<IResult> MarkReadAsync(HttpContext context, string id, IAntiforgery antiforgery,
        IInternalNoticeService service, CancellationToken cancellationToken)
    {
        if (!TryGuid(id, out var noticeId)) return Problem(context, 400, "AVISO_ID_INVALIDO", "El identificador de aviso no es válido");
        if (context.Request.Query.Count != 0 || CanHaveBody(context.Request) || context.Request.Headers.ContainsKey("Idempotency-Key") || context.Request.Headers.ContainsKey("If-Match"))
            return Problem(context, 400, "SOLICITUD_LECTURA_AVISO_INVALIDA", "La solicitud no admite query, cuerpo ni precondiciones");
        if (!TryActor(context, out var actor, out var failure)) return failure!;
        try { await antiforgery.ValidateRequestAsync(context); }
        catch (AntiforgeryValidationException) { return Problem(context, 400, "CSRF_INVALIDO", "El token CSRF no es válido"); }
        try
        {
            var result = await service.MarkReadAsync(new(actor, noticeId, CorrelationId(context)), cancellationToken);
            return Results.Ok(new { data = result, meta = new { correlationId = context.GetCorrelationId() } });
        }
        catch (InboxAccessDeniedException) { return Problem(context, 403, "ACCESO_DENEGADO", $"Se requiere {InboxAuthorization.ViewOwn}"); }
        catch (InternalNoticeNotFoundException) { return Problem(context, 404, "AVISO_NO_ENCONTRADO", "El aviso no existe en el alcance autorizado"); }
        catch (InternalNoticeConcurrencyException) { return Problem(context, 409, "LECTURA_AVISO_CONCURRENCIA_CONFLICTO", "La lectura no pudo serializarse"); }
    }

    private static bool TryQuery(IQueryCollection values, Guid actor, out InboxQuery? query)
    {
        query = null;
        if (values.Keys.Any(key => !Parameters.Contains(key)) || !OptionalGuid(values, "periodId", out var periodId) ||
            !Optional(values, "taskState", out var taskState) || !Optional(values, "taskCursor", out var taskCursor) ||
            !Limit(values, "taskLimit", out var taskLimit) || !Optional(values, "noticeStatus", out var noticeStatus) ||
            !Optional(values, "noticeCursor", out var noticeCursor) || !Limit(values, "noticeLimit", out var noticeLimit)) return false;
        noticeStatus ??= "ALL";
        if (taskState is not (null or InboxTaskStates.Future or InboxTaskStates.Available or InboxTaskStates.Overdue or InboxTaskStates.Concluded) ||
            noticeStatus is not ("ALL" or InternalNoticeStatuses.Unread or InternalNoticeStatuses.Read)) return false;
        query = new(actor, periodId, taskState, taskCursor, taskLimit, noticeStatus, noticeCursor, noticeLimit);
        return true;
    }

    private static bool Optional(IQueryCollection query, string name, out string? value)
    {
        value = null;
        if (!query.TryGetValue(name, out var values)) return true;
        return Single(values, out value) && !string.IsNullOrWhiteSpace(value);
    }
    private static bool OptionalGuid(IQueryCollection query, string name, out Guid? value)
    {
        value = null;
        if (!query.TryGetValue(name, out var values)) return true;
        if (!Single(values, out var raw) || !TryGuid(raw, out var parsed)) return false;
        value = parsed; return true;
    }
    private static bool Limit(IQueryCollection query, string name, out int value)
    {
        value = 25;
        if (!query.TryGetValue(name, out var values)) return true;
        return Single(values, out var raw) && int.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out value) && value is >= 1 and <= 100;
    }
    private static bool Single(StringValues values, out string? value) { value = values.Count == 1 ? values[0] : null; return value is not null; }
    private static bool TryGuid(string? raw, out Guid value)
    {
        value = Guid.Empty;
        return raw is not null && Guid.TryParseExact(raw, "D", out value) && value != Guid.Empty && raw == value.ToString("D");
    }
    private static bool CanHaveBody(HttpRequest request) => request.ContentLength is > 0 || request.Headers.TransferEncoding.Count != 0 ||
        request.HttpContext.Features.Get<IHttpRequestBodyDetectionFeature>()?.CanHaveBody == true || request.Body.CanSeek && request.Body.Length != 0;
    private static bool TryActor(HttpContext context, out Guid actor, out IResult? failure)
    {
        actor = Guid.Empty; failure = null;
        if (context.User.Identity?.IsAuthenticated != true) { failure = Problem(context, 401, "AUTENTICACION_REQUERIDA", "Se requiere una sesión activa con MFA"); return false; }
        if (!TryGuid(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out actor)) { failure = Problem(context, 403, "ACCESO_DENEGADO", "La sesión no identifica un actor autorizado"); return false; }
        return true;
    }
    private static Guid CorrelationId(HttpContext context) => Guid.TryParse(context.GetCorrelationId(), out var id) ? id : Guid.CreateVersion7();
    private static IResult Problem(HttpContext context, int status, string code, string title) => Results.Problem(statusCode: status, title: title,
        instance: context.Request.Path, extensions: new Dictionary<string, object?> { ["code"] = code, ["correlationId"] = context.GetCorrelationId() });
}
