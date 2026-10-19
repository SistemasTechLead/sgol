using System.Security.Claims;
using System.Text.Json;
using Sgol.BuildingBlocks.Versioning;
using Sgol.Configuration.Contracts;
using Sgol.Web.Infrastructure.Http;

namespace Sgol.Web.Presentation.Endpoints;

public static class TaskDefinitionApiEndpoints
{
    public static IEndpointRouteBuilder MapTaskDefinitionApi(this IEndpointRouteBuilder endpoints)
    {
        var definitions = endpoints.MapGroup("/api/v1/task-definitions");
        definitions.MapGet("", HandleListAsync);
        definitions.MapGet("/{taskCode}", HandleGetAsync);
        definitions.MapPost("/{taskCode}/versions", HandleCreateVersionAsync);
        definitions.MapPost("/{taskCode}/versions/{versionId:guid}/publish", HandlePublishVersionAsync);
        definitions.MapPost("/{taskCode}/deactivate-new", HandleDeactivateNewAsync);
        return endpoints;
    }

    public static async Task<IResult> HandleListAsync(
        HttpContext context,
        ITaskDefinitionService service,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(context, out var actorUserId, out var denied))
        {
            return denied;
        }

        try
        {
            return OkCollection(context, await service.ListAsync(actorUserId, CorrelationId(context), cancellationToken));
        }
        catch (Exception exception)
        {
            return MapException(context, exception);
        }
    }

    public static async Task<IResult> HandleGetAsync(
        string taskCode,
        HttpContext context,
        ITaskDefinitionService service,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(context, out var actorUserId, out var denied))
        {
            return denied;
        }

        try
        {
            return Ok(context, await service.GetAsync(actorUserId, CorrelationId(context), taskCode, cancellationToken));
        }
        catch (Exception exception)
        {
            return MapException(context, exception);
        }
    }

    public static async Task<IResult> HandleCreateVersionAsync(
        string taskCode,
        JsonElement request,
        HttpContext context,
        ITaskDefinitionService service,
        CancellationToken cancellationToken)
    {
        if (!TryMutationContext(context, requireIfMatch: false, out var actorUserId, out var key, out _, out var invalid))
        {
            return invalid;
        }

        if (!TryCreateRequest(request, out var body))
        {
            return Problem(context, 400, "DEFINICION_INVALIDA", "El cuerpo debe contener únicamente releaseId, schemaVersion y taskPayload válidos");
        }

        try
        {
            var version = await service.CreateVersionAsync(
                new CreateTaskDefinitionVersionCommand(
                    actorUserId,
                    key,
                    CorrelationId(context),
                    taskCode,
                    body.ReleaseId,
                    body.SchemaVersion,
                    body.TaskPayload),
                cancellationToken);
            SetEtag(context, version.RowVersion);
            return Results.Created(
                $"/api/v1/task-definitions/{taskCode}/versions/{version.Id:D}",
                Envelope(context, version));
        }
        catch (Exception exception)
        {
            return MapException(context, exception);
        }
    }

    public static async Task<IResult> HandlePublishVersionAsync(
        string taskCode,
        Guid versionId,
        JsonElement request,
        HttpContext context,
        ITaskDefinitionService service,
        CancellationToken cancellationToken)
    {
        if (!TryMutationContext(context, requireIfMatch: true, out var actorUserId, out var key, out var rowVersion, out var invalid))
        {
            return invalid;
        }

        if (!TryPublicationRequest(request, includeRelease: false, out var body))
        {
            return InvalidPublication(context, includeRelease: false);
        }

        try
        {
            var version = await service.PublishVersionAsync(
                new PublishTaskDefinitionVersionCommand(
                    actorUserId,
                    key,
                    CorrelationId(context),
                    taskCode,
                    versionId,
                    rowVersion!.Value,
                    body.EffectiveFrom,
                    body.Reason),
                cancellationToken);
            SetEtag(context, version.RowVersion);
            return Ok(context, version);
        }
        catch (Exception exception)
        {
            return MapException(context, exception);
        }
    }

    public static async Task<IResult> HandleDeactivateNewAsync(
        string taskCode,
        JsonElement request,
        HttpContext context,
        ITaskDefinitionService service,
        CancellationToken cancellationToken)
    {
        if (!TryMutationContext(context, requireIfMatch: true, out var actorUserId, out var key, out var rowVersion, out var invalid))
        {
            return invalid;
        }

        if (!TryPublicationRequest(request, includeRelease: true, out var body))
        {
            return InvalidPublication(context, includeRelease: true);
        }

        try
        {
            var version = await service.DeactivateNewAsync(
                new DeactivateTaskDefinitionCommand(
                    actorUserId,
                    key,
                    CorrelationId(context),
                    taskCode,
                    body.ReleaseId!.Value,
                    rowVersion!.Value,
                    body.EffectiveFrom,
                    body.Reason),
                cancellationToken);
            SetEtag(context, version.RowVersion);
            return Ok(context, version);
        }
        catch (Exception exception)
        {
            return MapException(context, exception);
        }
    }

    private static bool TryCreateRequest(JsonElement request, out CreateVersionRequest body)
    {
        body = default;
        if (request.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        var properties = request.EnumerateObject().ToArray();
        if (properties.Length != 3 ||
            !TryFind(properties, "releaseId", out var release) || release.ValueKind != JsonValueKind.String ||
            !Guid.TryParse(release.GetString(), out var releaseId) ||
            !TryFind(properties, "schemaVersion", out var schema) || !schema.TryGetInt32(out var schemaVersion) ||
            !TryFind(properties, "taskPayload", out var payload))
        {
            return false;
        }

        body = new CreateVersionRequest(releaseId, schemaVersion, payload.Clone());
        return true;
    }

    private static bool TryPublicationRequest(JsonElement request, bool includeRelease, out PublicationRequest body)
    {
        body = default;
        if (request.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        var properties = request.EnumerateObject().ToArray();
        if (properties.Length != (includeRelease ? 3 : 2) ||
            !TryFind(properties, "effectiveFrom", out var effective) ||
            effective.ValueKind != JsonValueKind.String ||
            !effective.TryGetDateTimeOffset(out var effectiveFrom) || effectiveFrom.Offset != TimeSpan.Zero ||
            !TryFind(properties, "reason", out var reason) || reason.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        Guid? releaseId = null;
        var parsedRelease = Guid.Empty;
        if (includeRelease &&
            (!TryFind(properties, "releaseId", out var release) || release.ValueKind != JsonValueKind.String ||
             !Guid.TryParse(release.GetString(), out parsedRelease)))
        {
            return false;
        }
        else if (includeRelease)
        {
            releaseId = parsedRelease;
        }

        body = new PublicationRequest(releaseId, effectiveFrom, reason.GetString()!);
        return true;
    }

    private static bool TryMutationContext(
        HttpContext context,
        bool requireIfMatch,
        out Guid actorUserId,
        out Guid idempotencyKey,
        out long? rowVersion,
        out IResult invalid)
    {
        rowVersion = null;
        if (!TryGetActor(context, out actorUserId, out invalid))
        {
            idempotencyKey = default;
            return false;
        }

        var parsed = IdempotencyKeyHeader.Parse(context.Request);
        idempotencyKey = parsed.Key;
        if (!parsed.IsValid)
        {
            invalid = Problem(context, 400, parsed.ErrorCode!, parsed.Detail!);
            return false;
        }

        if (!requireIfMatch)
        {
            invalid = null!;
            return true;
        }

        try
        {
            rowVersion = VersionEtag.ParseRequired(context.Request.Headers.IfMatch);
            invalid = null!;
            return true;
        }
        catch (VersionIfMatchRequiredException)
        {
            invalid = Problem(context, 400, "IF_MATCH_REQUERIDO", "If-Match es obligatorio");
            return false;
        }
        catch (VersionEtagInvalidException)
        {
            invalid = Problem(context, 400, "IF_MATCH_INVALIDO", "If-Match debe contener el ETag vigente");
            return false;
        }
    }

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

    private static IResult MapException(HttpContext context, Exception exception) => exception switch
    {
        TaskDefinitionAccessDeniedException or VersioningAccessDeniedException => Problem(
            context, 403, "ACCESO_DENEGADO", $"Se requiere {TaskDefinitionAuthorization.Administer} vigente en LOR-001"),
        TaskDefinitionNotMvpException => Problem(context, 422, "DEFINICION_NO_MVP", "La definición no pertenece al catálogo MVP"),
        TaskDefinitionNotFoundException => Problem(context, 404, "DEFINICION_NO_ENCONTRADA", "No se encontró la definición o versión"),
        TaskDefinitionReleaseNotDraftException => Problem(context, 409, "CONFIGURACION_BORRADOR_REQUERIDA", "La release debe permanecer en BORRADOR"),
        TaskDefinitionIdempotencyConflictException or ConfigurationIdempotencyConflictException => Problem(
            context, 409, "IDEMPOTENCY_CONFLICT", "La clave ya fue usada con otro contenido"),
        VersionConflictException => Problem(context, 412, "VERSION_CONFLICT", "La versión cambió; vuelve a cargar el recurso"),
        VersioningOverlapException => Problem(context, 422, "CONFIGURACION_SOLAPADA", "La vigencia se solapa con otra versión"),
        TaskDefinitionValidationException or VersioningValidationException or VersioningStateException => Problem(
            context, 422, "DEFINICION_INVALIDA", exception.Message),
        _ => throw exception,
    };

    private static IResult InvalidPublication(HttpContext context, bool includeRelease) => Problem(
        context,
        400,
        "DEFINICION_INVALIDA",
        includeRelease
            ? "El cuerpo debe contener únicamente releaseId, effectiveFrom UTC y reason"
            : "El cuerpo debe contener únicamente effectiveFrom UTC y reason");

    private static Guid CorrelationId(HttpContext context) =>
        Guid.TryParse(context.GetCorrelationId(), out var id) ? id : Guid.CreateVersion7();

    private static void SetEtag(HttpContext context, long rowVersion) =>
        context.Response.Headers.ETag = VersionEtag.Format(rowVersion);

    private static IResult Ok(HttpContext context, object data) => Results.Ok(Envelope(context, data));

    private static IResult OkCollection<T>(HttpContext context, IReadOnlyList<T> data) => Results.Ok(new
    {
        data,
        meta = new { correlationId = context.GetCorrelationId(), count = data.Count },
    });

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

    private readonly record struct CreateVersionRequest(Guid ReleaseId, int SchemaVersion, JsonElement TaskPayload);

    private readonly record struct PublicationRequest(Guid? ReleaseId, DateTimeOffset EffectiveFrom, string Reason);
}
