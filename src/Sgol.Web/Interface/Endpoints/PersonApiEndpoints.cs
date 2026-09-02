using System.Globalization;
using System.Security.Claims;
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
        return endpoints;
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
            return Ok(context, people);
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
            requiresIdempotencyKey: false,
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
            requiresIdempotencyKey: true,
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
            requiresIdempotencyKey: true,
            context,
            service,
            cancellationToken);

    private static async Task<IResult> HandleEmploymentChangeAsync(
        Guid personId,
        string status,
        string reason,
        string? positionText,
        string? shiftText,
        bool requiresIdempotencyKey,
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

        Guid? idempotencyKey = null;
        if (requiresIdempotencyKey)
        {
            if (!TryGetIdempotencyKey(context, out var parsedKey, out var invalidKey))
            {
                return invalidKey;
            }

            idempotencyKey = parsedKey;
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
                    shiftText),
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
        if (!Guid.TryParse(context.Request.Headers["Idempotency-Key"], out key))
        {
            invalid = Problem(
                context,
                StatusCodes.Status400BadRequest,
                "IDEMPOTENCY_KEY_INVALIDA",
                "Idempotency-Key debe ser un UUID");
            return false;
        }

        invalid = null!;
        return true;
    }

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
