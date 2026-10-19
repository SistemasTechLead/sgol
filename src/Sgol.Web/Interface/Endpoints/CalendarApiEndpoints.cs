using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using Sgol.BuildingBlocks.Versioning;
using Sgol.Configuration.Contracts;
using Sgol.Web.Infrastructure.Http;
using Sgol.Web.Infrastructure.Persistence.Idempotency;

namespace Sgol.Web.Presentation.Endpoints;

public static class CalendarApiEndpoints
{
    public static IEndpointRouteBuilder MapCalendarApi(this IEndpointRouteBuilder endpoints)
    {
        var calendar = endpoints.MapGroup("/api/v1/calendar");
        calendar.MapGet("", HandleGetAsync);
        calendar.MapGet("/drafts/{releaseId:guid}", HandleGetDraftAsync);
        calendar.MapPut("/{date}", HandlePutAsync);
        return endpoints;
    }

    public static async Task<IResult> HandleGetAsync(
        HttpContext context,
        ICalendarService service,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(context, out var actorUserId, out var denied))
        {
            return denied;
        }

        if (!TryGetLocalDate(context.Request.Query["from"], "from", context, out var fromDate, out var invalidFrom))
        {
            return invalidFrom;
        }

        if (!TryGetLocalDate(context.Request.Query["to"], "to", context, out var toDate, out var invalidTo))
        {
            return invalidTo;
        }

        try
        {
            var days = await service.GetAsync(
                actorUserId,
                GetCorrelationId(context),
                fromDate,
                toDate,
                cancellationToken);
            return OkCollection(context, days);
        }
        catch (Exception exception)
        {
            return MapException(context, exception);
        }
    }

    public static async Task<IResult> HandlePutAsync(
        string date,
        JsonElement request,
        HttpContext context,
        ICalendarService service,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(context, out var actorUserId, out var denied))
        {
            return denied;
        }

        if (!TryGetLocalDate(date, "date", context, out var localDate, out var invalidDate))
        {
            return invalidDate;
        }

        if (!TryGetRequest(request, context, out var body, out var invalidBody))
        {
            return invalidBody;
        }

        var parsedKey = IdempotencyKeyHeader.Parse(context.Request);
        if (!parsedKey.IsValid)
        {
            return Problem(context, 400, parsedKey.ErrorCode!, parsedKey.Detail!);
        }

        if (!TryGetOptionalRowVersion(context, out var rowVersion, out var invalidVersion))
        {
            return invalidVersion;
        }

        try
        {
            var day = await service.PutAsync(
                new PutCalendarDayCommand(
                    actorUserId,
                    parsedKey.Key,
                    GetCorrelationId(context),
                    localDate,
                    body.ReleaseId,
                    body.DayType,
                    body.IsWorkingDay,
                    body.Reason,
                    rowVersion),
                cancellationToken);
            context.Response.Headers.ETag = VersionEtag.Format(day.RowVersion);
            return Ok(context, day);
        }
        catch (Exception exception)
        {
            return MapException(context, exception);
        }
    }

    public static async Task<IResult> HandleGetDraftAsync(
        Guid releaseId,
        HttpContext context,
        ICalendarService service,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(context, out var actorUserId, out var denied))
        {
            return denied;
        }

        if (!TryGetLocalDate(context.Request.Query["from"], "from", context, out var fromDate, out var invalidFrom))
        {
            return invalidFrom;
        }

        if (!TryGetLocalDate(context.Request.Query["to"], "to", context, out var toDate, out var invalidTo))
        {
            return invalidTo;
        }

        try
        {
            var days = await service.GetDraftAsync(actorUserId, GetCorrelationId(context), releaseId,
                fromDate, toDate, cancellationToken);
            return OkCollection(context, days);
        }
        catch (Exception exception)
        {
            return MapException(context, exception);
        }
    }

    private static bool TryGetRequest(
        JsonElement request,
        HttpContext context,
        out PutCalendarDayRequest body,
        out IResult invalid)
    {
        body = default!;
        if (request.ValueKind != JsonValueKind.Object)
        {
            invalid = InvalidBody(context);
            return false;
        }

        var properties = request.EnumerateObject().ToArray();
        if (properties.Length != 4 ||
            !TryFind(properties, "releaseId", out var releaseElement) ||
            releaseElement.ValueKind != JsonValueKind.String ||
            !Guid.TryParse(releaseElement.GetString(), out var releaseId) ||
            !TryFind(properties, "dayType", out var dayTypeElement) ||
            dayTypeElement.ValueKind != JsonValueKind.String ||
            !TryFind(properties, "isWorkingDay", out var workingElement) ||
            workingElement.ValueKind is not JsonValueKind.True and not JsonValueKind.False ||
            !TryFind(properties, "reason", out var reasonElement) ||
            reasonElement.ValueKind != JsonValueKind.String)
        {
            invalid = InvalidBody(context);
            return false;
        }

        body = new PutCalendarDayRequest(
            releaseId,
            dayTypeElement.GetString()!,
            workingElement.GetBoolean(),
            reasonElement.GetString()!);
        invalid = null!;
        return true;
    }

    private static bool TryFind(JsonProperty[] properties, string name, out JsonElement value)
    {
        foreach (var property in properties)
        {
            if (property.NameEquals(name))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private static IResult InvalidBody(HttpContext context) => Problem(
        context,
        400,
        "CALENDARIO_INVALIDO",
        "El cuerpo debe contener únicamente releaseId, dayType, isWorkingDay y reason válidos");

    private static bool TryGetActor(HttpContext context, out Guid actorUserId, out IResult denied)
    {
        actorUserId = default;
        if (context.User.Identity?.IsAuthenticated != true)
        {
            denied = Problem(context, 401, "AUTENTICACION_REQUERIDA", "Se requiere una sesión activa");
            return false;
        }

        if (!Guid.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out actorUserId))
        {
            denied = Problem(context, 403, "ACCESO_DENEGADO", "La sesión no identifica un actor autorizado");
            return false;
        }

        denied = null!;
        return true;
    }

    private static bool TryGetLocalDate(
        string? value,
        string fieldName,
        HttpContext context,
        out DateOnly date,
        out IResult invalid)
    {
        if (DateOnly.TryParseExact(
                value,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out date))
        {
            invalid = null!;
            return true;
        }

        invalid = Problem(context, 400, "FECHA_INVALIDA", $"{fieldName} debe usar el formato YYYY-MM-DD");
        return false;
    }

    private static bool TryGetOptionalRowVersion(
        HttpContext context,
        out long? rowVersion,
        out IResult invalid)
    {
        var value = context.Request.Headers.IfMatch.ToString();
        if (string.IsNullOrWhiteSpace(value))
        {
            rowVersion = null;
            invalid = null!;
            return true;
        }

        try
        {
            rowVersion = VersionEtag.ParseRequired(value);
            invalid = null!;
            return true;
        }
        catch (VersionEtagInvalidException)
        {
            rowVersion = null;
            invalid = Problem(context, 400, "IF_MATCH_INVALIDO", "If-Match debe contener el ETag vigente");
            return false;
        }
    }

    private static IResult MapException(HttpContext context, Exception exception) => exception switch
    {
        CalendarAccessDeniedException => Problem(
            context,
            403,
            "ACCESO_DENEGADO",
            $"Se requiere acceso vigente a LOR-001; para modificar se requiere {CalendarAuthorization.Administer}"),
        CalendarReleaseNotFoundException => Problem(
            context,
            409,
            "CONFIGURACION_BORRADOR_REQUERIDA",
            "La release debe existir, pertenecer a LOR-001 y permanecer en BORRADOR"),
        CalendarIdempotencyConflictException => Problem(
            context, 409, "IDEMPOTENCY_CONFLICT", "La clave ya fue usada con otro contenido"),
        IdempotencyConflictAuditException => Problem(
            context, 500, "IDEMPOTENCY_CONFLICT_AUDIT_FAILED", "El conflicto no pudo registrarse"),
        IdempotencyReplayUnavailableException => Problem(
            context, 503, "IDEMPOTENCY_REPLAY_UNAVAILABLE", "No se pudo recuperar la respuesta original"),
        CalendarIfMatchRequiredException => Problem(
            context,
            400,
            "IF_MATCH_REQUERIDO",
            "If-Match es obligatorio para corregir el borrador del día"),
        CalendarVersionConflictException or VersionConflictException => Problem(
            context,
            412,
            "VERSION_CONFLICT",
            "La versión cambió; vuelve a cargar el recurso"),
        CalendarValidationException or VersioningValidationException or VersioningStateException => Problem(
            context,
            422,
            "CALENDARIO_INVALIDO",
            exception.Message),
        _ => throw exception,
    };

    private static Guid GetCorrelationId(HttpContext context) =>
        Guid.TryParse(context.GetCorrelationId(), out var correlationId)
            ? correlationId
            : Guid.CreateVersion7();

    private static IResult Ok(HttpContext context, object data) => Results.Ok(new
    {
        data,
        meta = new { correlationId = context.GetCorrelationId() },
    });

    private static IResult OkCollection(HttpContext context, IReadOnlyList<CalendarDayDetails> days) => Results.Ok(new
    {
        data = days,
        meta = new { correlationId = context.GetCorrelationId(), count = days.Count },
    });

    private static IResult Problem(HttpContext context, int status, string code, string title) =>
        Results.Problem(
            statusCode: status,
            title: title,
            extensions: new Dictionary<string, object?>
            {
                ["code"] = code,
                ["correlationId"] = context.GetCorrelationId(),
            });

    private sealed record PutCalendarDayRequest(
        Guid ReleaseId,
        string DayType,
        bool IsWorkingDay,
        string Reason);
}
