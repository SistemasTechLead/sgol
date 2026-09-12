using System.Globalization;
using System.Security.Claims;
using Sgol.BuildingBlocks.Versioning;
using Sgol.Configuration.Contracts;
using Sgol.Web.Infrastructure.Http;

namespace Sgol.Web.Presentation.Endpoints;

public static class ConfigurationApiEndpoints
{
    public static IEndpointRouteBuilder MapConfigurationApi(this IEndpointRouteBuilder endpoints)
    {
        var releases = endpoints.MapGroup("/api/v1/configuration/releases");
        releases.MapGet("", HandleListAsync);
        releases.MapPost("", HandleCreateDraftAsync);
        releases.MapPost("/{releaseId:guid}/publish", HandlePublishAsync);
        return endpoints;
    }

    public static async Task<IResult> HandleListAsync(
        HttpContext context,
        IConfigurationReleaseService service,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(context, out var actorUserId, out var denied))
        {
            return denied;
        }

        try
        {
            var releases = await service.ListAsync(
                actorUserId,
                GetCorrelationId(context),
                cancellationToken);
            return Ok(context, releases);
        }
        catch (Exception exception)
        {
            return MapException(context, exception);
        }
    }

    public static async Task<IResult> HandleCreateDraftAsync(
        HttpContext context,
        IConfigurationReleaseService service,
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
            var release = await service.CreateDraftAsync(
                new CreateConfigurationReleaseCommand(
                    actorUserId,
                    idempotencyKey,
                    GetCorrelationId(context)),
                cancellationToken);
            SetETag(context, release.RowVersion);
            return Results.Created(
                $"/api/v1/configuration/releases/{release.Id:D}",
                Envelope(context, release));
        }
        catch (Exception exception)
        {
            return MapException(context, exception);
        }
    }

    public static async Task<IResult> HandlePublishAsync(
        Guid releaseId,
        PublishConfigurationReleaseRequest request,
        HttpContext context,
        IConfigurationReleaseService service,
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

        long rowVersion;
        try
        {
            rowVersion = VersionEtag.ParseRequired(context.Request.Headers.IfMatch);
        }
        catch (VersionIfMatchRequiredException)
        {
            return Problem(context, 400, "IF_MATCH_REQUERIDO", "If-Match es obligatorio para publicar");
        }
        catch (VersionEtagInvalidException)
        {
            return Problem(context, 400, "IF_MATCH_INVALIDO", "If-Match debe contener el ETag vigente");
        }

        try
        {
            var release = await service.PublishAsync(
                new PublishConfigurationReleaseCommand(
                    actorUserId,
                    idempotencyKey,
                    GetCorrelationId(context),
                    releaseId,
                    rowVersion,
                    request.EffectiveFrom,
                    request.Reason),
                cancellationToken);
            SetETag(context, release.RowVersion);
            return Ok(context, release);
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

    private static bool TryGetIdempotencyKey(HttpContext context, out Guid key, out IResult invalid)
    {
        var parsed = IdempotencyKeyHeader.Parse(context.Request);
        key = parsed.Key;
        if (!parsed.IsValid)
        {
            invalid = Problem(
                context,
                400,
                parsed.ErrorCode!,
                parsed.Detail!);
            return false;
        }

        invalid = null!;
        return true;
    }

    private static IResult MapException(HttpContext context, Exception exception) => exception switch
    {
        ConfigurationAccessDeniedException or VersioningAccessDeniedException => Problem(
            context,
            403,
            "ACCESO_DENEGADO",
            $"Se requiere {ConfigurationAuthorization.Administer} vigente en LOR-001"),
        ConfigurationReleaseNotFoundException => Problem(
            context,
            404,
            "CONFIGURACION_NO_ENCONTRADA",
            "No se encontró la versión de configuración"),
        ConfigurationIdempotencyConflictException => Problem(
            context,
            409,
            "IDEMPOTENCY_CONFLICT",
            "La clave ya fue usada con otro contenido"),
        VersionConflictException => Problem(
            context,
            412,
            "VERSION_CONFLICT",
            "La versión cambió; vuelve a cargar el recurso"),
        VersioningOverlapException => Problem(
            context,
            422,
            "VIGENCIA_SOLAPADA",
            "La vigencia se solapa con otra versión publicada"),
        EligibilityPolicyCoverageException or EligibilityPolicyDefinitionPreconditionException => Problem(
            context,
            422,
            "POLITICA_ELEGIBILIDAD_INCOMPLETA",
            exception.Message),
        EvidencePolicyCoverageException or EvidencePolicyDefinitionPreconditionException => Problem(
            context,
            422,
            "POLITICA_EVIDENCIA_INCOMPLETA",
            exception.Message),
        ValidationPolicyCoverageException or ValidationPolicyDefinitionPreconditionException => Problem(
            context,
            422,
            "POLITICA_VALIDACION_INCOMPLETA",
            exception.Message),
        VersioningValidationException or VersioningStateException => Problem(
            context,
            422,
            "PUBLICACION_INVALIDA",
            exception.Message),
        ArgumentException => Problem(
            context,
            400,
            "INSTANTE_INVALIDO",
            "effectiveFrom debe ser un instante UTC válido"),
        _ => throw exception,
    };

    private static Guid GetCorrelationId(HttpContext context) =>
        Guid.TryParse(context.GetCorrelationId(), out var correlationId)
            ? correlationId
            : Guid.CreateVersion7();

    private static void SetETag(HttpContext context, long rowVersion) =>
        context.Response.Headers.ETag = VersionEtag.Format(rowVersion);

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

public sealed record PublishConfigurationReleaseRequest(DateTimeOffset EffectiveFrom, string Reason);
