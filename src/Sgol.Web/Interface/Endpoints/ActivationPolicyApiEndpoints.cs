using System.Security.Claims;
using System.Text.Json;
using Sgol.BuildingBlocks.Versioning;
using Sgol.Configuration.Contracts;
using Sgol.Web.Infrastructure.Http;

namespace Sgol.Web.Presentation.Endpoints;

public static class ActivationPolicyApiEndpoints
{
    public static IEndpointRouteBuilder MapActivationPolicyApi(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPut(
            "/api/v1/task-definitions/{taskCode}/activation-policy",
            HandlePutAsync);
        return endpoints;
    }

    public static async Task<IResult> HandlePutAsync(
        string taskCode,
        JsonElement request,
        HttpContext context,
        IActivationPolicyService service,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(context, out var actorUserId, out var denied))
        {
            return denied;
        }

        if (!Guid.TryParse(context.Request.Headers["Idempotency-Key"], out var idempotencyKey))
        {
            return Problem(context, 400, "IDEMPOTENCY_KEY_INVALIDA", "Idempotency-Key debe ser un UUID");
        }

        long? expectedRowVersion = null;
        if (context.Request.Headers.ContainsKey("If-Match"))
        {
            try
            {
                expectedRowVersion = VersionEtag.ParseRequired(context.Request.Headers.IfMatch);
            }
            catch (Exception exception) when (exception is VersionIfMatchRequiredException or VersionEtagInvalidException)
            {
                return Problem(context, 400, "IF_MATCH_INVALIDO", "If-Match debe contener el ETag vigente");
            }
        }

        if (!TryRequest(request, out var body))
        {
            return Problem(
                context,
                400,
                "POLITICA_ACTIVACION_INVALIDA",
                "El cuerpo debe contener únicamente taskDefinitionVersionId, releaseId, mode, schedule y originKeySchema");
        }

        try
        {
            var policy = await service.PutAsync(
                new PutActivationPolicyCommand(
                    actorUserId,
                    idempotencyKey,
                    CorrelationId(context),
                    taskCode,
                    body.TaskDefinitionVersionId,
                    body.ReleaseId,
                    body.Mode,
                    body.Schedule,
                    body.OriginKeySchema,
                    expectedRowVersion),
                cancellationToken);
            context.Response.Headers.ETag = VersionEtag.Format(policy.RowVersion);
            return Results.Created(
                $"/api/v1/task-definitions/{taskCode}/activation-policy",
                Envelope(context, policy));
        }
        catch (Exception exception)
        {
            return MapException(context, exception);
        }
    }

    private static bool TryRequest(JsonElement request, out PutRequest body)
    {
        body = default;
        if (request.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        var properties = request.EnumerateObject().ToArray();
        if (properties.Length != 5 ||
            !TryFind(properties, "taskDefinitionVersionId", out var taskVersion) || taskVersion.ValueKind != JsonValueKind.String ||
            !Guid.TryParse(taskVersion.GetString(), out var taskDefinitionVersionId) ||
            !TryFind(properties, "releaseId", out var release) || release.ValueKind != JsonValueKind.String ||
            !Guid.TryParse(release.GetString(), out var releaseId) ||
            !TryFind(properties, "mode", out var mode) || mode.ValueKind != JsonValueKind.String ||
            !TryFind(properties, "schedule", out var schedule) ||
            !TryFind(properties, "originKeySchema", out var originKeySchema) || originKeySchema.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        body = new PutRequest(
            taskDefinitionVersionId,
            releaseId,
            mode.GetString()!,
            schedule.Clone(),
            originKeySchema.GetString()!);
        return true;
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
        ActivationPolicyAccessDeniedException or VersioningAccessDeniedException => Problem(
            context, 403, "ACCESO_DENEGADO", $"Se requiere {ActivationPolicyAuthorization.Administer} vigente en LOR-001"),
        TaskDefinitionNotMvpException => Problem(context, 422, "DEFINICION_NO_MVP", "La definición no pertenece al catálogo MVP"),
        ActivationPolicyReleaseNotDraftException => Problem(context, 409, "CONFIGURACION_BORRADOR_REQUERIDA", "La release debe permanecer en BORRADOR"),
        ActivationPolicyDefinitionPreconditionException => Problem(context, 422, "VERSION_TAR_REQUERIDA", exception.Message),
        ActivationPolicyIfMatchRequiredException => Problem(context, 400, "IF_MATCH_REQUERIDO", "If-Match es obligatorio para sustituir la política vigente"),
        ActivationPolicyIdempotencyConflictException => Problem(context, 409, "IDEMPOTENCY_CONFLICT", "La clave ya fue usada con otro contenido"),
        VersionConflictException => Problem(context, 412, "VERSION_CONFLICT", "La versión cambió; vuelve a cargar el recurso"),
        ActivationPolicyValidationException or VersioningValidationException or VersioningStateException => Problem(
            context, 422, "POLITICA_ACTIVACION_INVALIDA", exception.Message),
        _ => throw exception,
    };

    private static Guid CorrelationId(HttpContext context) =>
        Guid.TryParse(context.GetCorrelationId(), out var id) ? id : Guid.CreateVersion7();

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

    private readonly record struct PutRequest(
        Guid TaskDefinitionVersionId,
        Guid ReleaseId,
        string Mode,
        JsonElement Schedule,
        string OriginKeySchema);
}
