using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using Sgol.Organization.Contracts;
using Sgol.Web.Infrastructure.Http;

namespace Sgol.Web.Presentation.Endpoints;

public static class PersonApiEndpoints
{
    public static IEndpointRouteBuilder MapPersonApi(this IEndpointRouteBuilder endpoints)
    {
        var people = endpoints.MapGroup("/api/v1/people");
        people.MapGet("", HandleListAsync);
        people.MapPost("", HandleCreateAsync);
        people.MapGet("/{personId:guid}", HandleFindAsync);
        people.MapPatch("/{personId:guid}/employment", HandlePatchEmploymentAsync);
        people.MapPost("/{personId:guid}/deactivate", HandleDeactivateAsync);
        people.MapPost("/{personId:guid}/reactivate", HandleReactivateAsync);
        people.MapGet("/{personId:guid}/availability", HandleGetAvailabilityAsync);
        people.MapPut("/{personId:guid}/availability/{date}", HandlePutAvailabilityAsync);
        return endpoints;
    }

    public static async Task<IResult> HandleGetAvailabilityAsync(
        Guid personId,
        HttpContext context,
        IAvailabilityAdministrationService service,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(context, out var actorUserId, out var denied))
        {
            return denied;
        }

        if (!TryGetLocalDate(context.Request.Query["fromDate"], "fromDate", context, out var fromDate, out var invalidFrom))
        {
            return invalidFrom;
        }

        if (!TryGetLocalDate(context.Request.Query["toDate"], "toDate", context, out var toDate, out var invalidTo))
        {
            return invalidTo;
        }

        try
        {
            var values = await service.GetAsync(
                actorUserId,
                GetCorrelationId(context),
                personId,
                fromDate,
                toDate,
                cancellationToken);
            return Results.Ok(new
            {
                data = values,
                meta = new { correlationId = context.GetCorrelationId(), count = values.Count },
            });
        }
        catch (Exception exception)
        {
            return MapAvailabilityException(context, exception);
        }
    }

    public static async Task<IResult> HandlePutAvailabilityAsync(
        Guid personId,
        string date,
        JsonElement request,
        HttpContext context,
        IAvailabilityAdministrationService service,
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

        if (!TryGetAvailabilityValue(request, context, out var isAvailable, out var invalidBody))
        {
            return invalidBody;
        }

        if (!TryGetOptionalRowVersion(context, out var rowVersion, out var invalidVersion))
        {
            return invalidVersion;
        }

        if (!TryGetIdempotencyKey(context, out var idempotencyKey, out var invalidKey))
        {
            return invalidKey;
        }

        try
        {
            var value = await service.PutAsync(
                new PutAvailabilityCommand(
                    actorUserId,
                    GetCorrelationId(context),
                    idempotencyKey,
                    personId,
                    localDate,
                    isAvailable,
                    rowVersion),
                cancellationToken);
            context.Response.Headers.ETag = $"\"{value.RowVersion.ToString(CultureInfo.InvariantCulture)}\"";
            return Ok(context, value);
        }
        catch (Exception exception)
        {
            return MapAvailabilityException(context, exception);
        }
    }

    public static async Task<IResult> HandleListAsync(
        HttpContext context,
        IPersonAdministrationService service,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(context, out var actorUserId, out var denied))
        {
            return denied;
        }

        try
        {
            var people = await service.ListAsync(
                actorUserId,
                GetCorrelationId(context),
                cancellationToken);
            return Results.Ok(new
            {
                data = people,
                meta = new { correlationId = context.GetCorrelationId(), count = people.Count },
            });
        }
        catch (Exception exception)
        {
            return MapException(context, exception);
        }
    }

    public static async Task<IResult> HandleFindAsync(
        Guid personId,
        HttpContext context,
        IPersonAdministrationService service,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(context, out var actorUserId, out var denied))
        {
            return denied;
        }

        try
        {
            var person = await service.FindAsync(
                actorUserId,
                GetCorrelationId(context),
                personId,
                cancellationToken);
            if (person is null)
            {
                return Problem(context, StatusCodes.Status404NotFound, "PERSONA_NO_ENCONTRADA", "No se encontró la persona");
            }

            SetETag(context, person);
            return Ok(context, person);
        }
        catch (Exception exception)
        {
            return MapException(context, exception);
        }
    }

    public static async Task<IResult> HandleCreateAsync(
        CreatePersonRequest request,
        HttpContext context,
        IPersonAdministrationService service,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(context, out var actorUserId, out var denied))
        {
            return denied;
        }

        if (!TryGetIdempotencyKey(context, out var idempotencyKey, out var invalidKey))
        {
            return invalidKey;
        }

        try
        {
            var result = await service.CreateAsync(
                new CreatePersonCommand(
                    actorUserId,
                    idempotencyKey,
                    GetCorrelationId(context),
                    request.StableCode,
                    request.DisplayName),
                cancellationToken);
            SetETag(context, result.Person);
            return Results.Created(
                $"/api/v1/people/{result.Person.Id:D}",
                Envelope(context, result.Person));
        }
        catch (Exception exception)
        {
            return MapException(context, exception);
        }
    }

    public static Task<IResult> HandlePatchEmploymentAsync(
        Guid personId,
        ChangeEmploymentRequest request,
        HttpContext context,
        IPersonAdministrationService service,
        CancellationToken cancellationToken) =>
        HandleEmploymentChangeAsync(
            personId,
            request.Status,
            request.Reason,
            request.PositionText,
            request.ShiftText,
            operation: "PERSON_EMPLOYMENT_PATCH",
            context,
            service,
            cancellationToken);

    public static Task<IResult> HandleDeactivateAsync(
        Guid personId,
        EmploymentReasonRequest request,
        HttpContext context,
        IPersonAdministrationService service,
        CancellationToken cancellationToken) =>
        HandleEmploymentChangeAsync(
            personId,
            EmploymentStatus.Inactive,
            request.Reason,
            positionText: null,
            shiftText: null,
            operation: "PERSON_DEACTIVATE",
            context,
            service,
            cancellationToken);

    public static Task<IResult> HandleReactivateAsync(
        Guid personId,
        EmploymentReasonRequest request,
        HttpContext context,
        IPersonAdministrationService service,
        CancellationToken cancellationToken) =>
        HandleEmploymentChangeAsync(
            personId,
            EmploymentStatus.Active,
            request.Reason,
            positionText: null,
            shiftText: null,
            operation: "PERSON_REACTIVATE",
            context,
            service,
            cancellationToken);

    private static async Task<IResult> HandleEmploymentChangeAsync(
        Guid personId,
        string status,
        string reason,
        string? positionText,
        string? shiftText,
        string operation,
        HttpContext context,
        IPersonAdministrationService service,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(context, out var actorUserId, out var denied))
        {
            return denied;
        }

        if (!TryGetRowVersion(context, out var rowVersion, out var invalidVersion))
        {
            return invalidVersion;
        }

        if (!TryGetIdempotencyKey(context, out var idempotencyKey, out var invalidKey))
        {
            return invalidKey;
        }

        try
        {
            var result = await service.ChangeEmploymentAsync(
                new ChangeEmploymentCommand(
                    actorUserId,
                    GetCorrelationId(context),
                    personId,
                    status,
                    rowVersion,
                    reason,
                    idempotencyKey,
                    positionText,
                    shiftText,
                    operation),
                cancellationToken);
            SetETag(context, result.Person);
            return Ok(context, result.Person);
        }
        catch (Exception exception)
        {
            return MapException(context, exception);
        }
    }

    private static bool TryGetActor(HttpContext context, out Guid actorUserId, out IResult denied)
    {
        actorUserId = default;
        if (context.User.Identity?.IsAuthenticated != true)
        {
            denied = Problem(
                context,
                StatusCodes.Status401Unauthorized,
                "AUTENTICACION_REQUERIDA",
                "Se requiere una sesión activa");
            return false;
        }

        if (!Guid.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out actorUserId))
        {
            denied = Problem(
                context,
                StatusCodes.Status403Forbidden,
                "ACCESO_DENEGADO",
                "La sesión no identifica un actor autorizado");
            return false;
        }

        denied = null!;
        return true;
    }

    private static bool TryGetIdempotencyKey(HttpContext context, out Guid key, out IResult invalid)
    {
        var parsed = IdempotencyKeyHeader.Parse(context.Request);
        key = parsed.Key;
        if (!parsed.IsValid)
        {
            invalid = Problem(
                context,
                StatusCodes.Status400BadRequest,
                parsed.ErrorCode!,
                parsed.Detail!);
            return false;
        }

        invalid = null!;
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

        invalid = Problem(
            context,
            StatusCodes.Status400BadRequest,
            "FECHA_INVALIDA",
            $"{fieldName} debe usar el formato YYYY-MM-DD");
        return false;
    }

    private static bool TryGetAvailabilityValue(
        JsonElement request,
        HttpContext context,
        out bool isAvailable,
        out IResult invalid)
    {
        isAvailable = default;
        if (request.ValueKind != JsonValueKind.Object)
        {
            invalid = InvalidAvailabilityBody(context);
            return false;
        }

        var properties = request.EnumerateObject().ToArray();
        if (properties.Length != 1 ||
            !properties[0].NameEquals("isAvailable") ||
            properties[0].Value.ValueKind is not JsonValueKind.True and not JsonValueKind.False)
        {
            invalid = InvalidAvailabilityBody(context);
            return false;
        }

        isAvailable = properties[0].Value.GetBoolean();
        invalid = null!;
        return true;
    }

    private static IResult InvalidAvailabilityBody(HttpContext context) => Problem(
        context,
        StatusCodes.Status400BadRequest,
        "DISPONIBILIDAD_INVALIDA",
        "El cuerpo debe contener únicamente isAvailable con un valor booleano");

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

        if (long.TryParse(
                value.Trim('"'),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var parsed) && parsed >= 1)
        {
            rowVersion = parsed;
            invalid = null!;
            return true;
        }

        rowVersion = null;
        invalid = Problem(
            context,
            StatusCodes.Status400BadRequest,
            "IF_MATCH_INVALIDO",
            "If-Match debe contener el ETag vigente");
        return false;
    }

    private static IResult MapAvailabilityException(HttpContext context, Exception exception) => exception switch
    {
        AvailabilityValidationException => Problem(context, 400, "CONSULTA_DISPONIBILIDAD_INVALIDA", exception.Message),
        AvailabilityAccessDeniedException => Problem(context, 403, "ACCESO_DENEGADO", $"Se requiere {AvailabilityAuthorization.Administer} vigente en LOR-001"),
        AvailabilityPersonNotFoundException => Problem(context, 404, "PERSONA_NO_ENCONTRADA", "No se encontró la persona"),
        AvailabilityPersonInactiveException => Problem(context, 409, "PERSONA_INACTIVA", "La persona no está activa"),
        AvailabilityPersonOutOfScopeException => Problem(context, 409, "PERSONA_FUERA_DE_ALCANCE", "La persona no pertenece a LOR-001"),
        AvailabilityIfMatchRequiredException => Problem(context, 400, "IF_MATCH_INVALIDO", "If-Match es obligatorio para corregir disponibilidad"),
        AvailabilityVersionConflictException => Problem(context, 412, "VERSION_CONFLICT", "La versión cambió; vuelve a cargar el recurso"),
        AvailabilityIdempotencyConflictException => Problem(context, 409, "IDEMPOTENCY_CONFLICT", "La clave idempotente ya se usó con otro contenido"),
        _ => throw exception,
    };

    private static bool TryGetRowVersion(HttpContext context, out long rowVersion, out IResult invalid)
    {
        var value = context.Request.Headers.IfMatch.ToString().Trim('"');
        if (!long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out rowVersion) || rowVersion < 1)
        {
            invalid = Problem(
                context,
                StatusCodes.Status400BadRequest,
                "IF_MATCH_INVALIDO",
                "If-Match debe contener el ETag vigente");
            return false;
        }

        invalid = null!;
        return true;
    }

    private static IResult MapException(HttpContext context, Exception exception) => exception switch
    {
        PersonValidationException => Problem(context, 400, "DATOS_PERSONA_INVALIDOS", exception.Message),
        PersonAccessDeniedException => Problem(context, 403, "ACCESO_DENEGADO", "Se requiere PER-PERSONA-ADMIN vigente en LOR-001"),
        PersonNotFoundException => Problem(context, 404, "PERSONA_NO_ENCONTRADA", "No se encontró la persona"),
        PersonCodeConflictException => Problem(context, 409, "CODIGO_PERSONA_DUPLICADO", "El código estable ya está registrado"),
        PersonStateConflictException => Problem(context, 409, "VIGENCIA_SIN_CAMBIO", "La vigencia solicitada ya es la vigente"),
        PersonEmploymentNoChangeException => Problem(context, 409, "DATOS_LABORALES_SIN_CAMBIO", "Puesto y turno ya son los vigentes"),
        PersonIdempotencyConflictException => Problem(context, 409, "IDEMPOTENCY_KEY_CONFLICT", "La clave ya fue usada con otro contenido"),
        PersonVersionConflictException => Problem(context, 412, "VERSION_CONFLICT", "La versión cambió; vuelve a cargar el recurso"),
        _ => throw exception,
    };

    private static Guid GetCorrelationId(HttpContext context) =>
        Guid.TryParse(context.GetCorrelationId(), out var correlationId)
            ? correlationId
            : Guid.CreateVersion7();

    private static void SetETag(HttpContext context, PersonDetails person)
    {
        var current = person.EmploymentHistory.Single(version => version.ValidTo is null);
        context.Response.Headers.ETag = $"\"{current.RowVersion.ToString(CultureInfo.InvariantCulture)}\"";
    }

    private static IResult Ok(HttpContext context, object data) => Results.Ok(Envelope(context, data));

    private static object Envelope(HttpContext context, object data) => new
    {
        data,
        meta = new { correlationId = context.GetCorrelationId() },
    };

    private static IResult Problem(HttpContext context, int status, string code, string title) =>
        Results.Problem(
            statusCode: status,
            title: title,
            extensions: new Dictionary<string, object?>
            {
                ["code"] = code,
                ["correlationId"] = context.GetCorrelationId(),
            });
}

public sealed record CreatePersonRequest(string StableCode, string DisplayName);

public sealed record ChangeEmploymentRequest(
    string Status,
    string Reason,
    string? PositionText = null,
    string? ShiftText = null);

public sealed record EmploymentReasonRequest(string Reason);
