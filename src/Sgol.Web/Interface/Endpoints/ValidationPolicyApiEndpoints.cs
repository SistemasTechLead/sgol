using System.Security.Claims;
using System.Text.Json;
using Sgol.BuildingBlocks.Versioning;
using Sgol.Configuration.Contracts;
using Sgol.Web.Infrastructure.Http;

namespace Sgol.Web.Presentation.Endpoints;

public static class ValidationPolicyApiEndpoints
{
    public static IEndpointRouteBuilder MapValidationPolicyApi(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPut(
            "/api/v1/task-definitions/{taskCode}/validation-policy",
            HandlePutAsync);
        return endpoints;
    }

    public static async Task<IResult> HandlePutAsync(
        string taskCode,
        JsonElement request,
        HttpContext context,
        IValidationPolicyService service,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(context, out var actorUserId, out var denied))
        {
            return denied;
        }

        try
        {
            if (!Guid.TryParse(context.Request.Headers["Idempotency-Key"], out var idempotencyKey))
            {
                await RecordRejectionAsync(context, service, actorUserId, cancellationToken);
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
                    await RecordRejectionAsync(context, service, actorUserId, cancellationToken);
                    return Problem(context, 400, "IF_MATCH_INVALIDO", "If-Match debe contener el ETag vigente");
                }
            }

            if (!TryRequest(request, out var body))
            {
                await RecordRejectionAsync(context, service, actorUserId, cancellationToken);
                return Problem(
                    context,
                    400,
                    "SOLICITUD_INVALIDA",
                    "El cuerpo debe contener únicamente la política de validación aprobada");
            }

            var policy = await service.PutAsync(
                new PutValidationPolicyCommand(
                    actorUserId,
                    idempotencyKey,
                    CorrelationId(context),
                    taskCode,
                    body.ReleaseId,
                    body.IsRequired,
                    body.ExecutorRole,
                    body.ValidatorRelation,
                    body.ValidatorRole,
                    body.AllowedResults,
                    expectedRowVersion),
                cancellationToken);
            context.Response.Headers.ETag = VersionEtag.Format(policy.RowVersion);
            return Results.Created(
                $"/api/v1/task-definitions/{taskCode}/validation-policy",
                Envelope(context, policy));
        }
        catch (Exception exception)
        {
            return MapException(context, exception);
        }
    }

    private static Task RecordRejectionAsync(
        HttpContext context,
        IValidationPolicyService service,
        Guid actorUserId,
        CancellationToken cancellationToken) =>
        service.RecordRejectionAsync(actorUserId, CorrelationId(context), cancellationToken);

    private static bool TryRequest(JsonElement request, out PutRequest body)
    {
        body = default;
        if (request.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        var properties = request.EnumerateObject().ToArray();
        if (properties.Length != 6 ||
            !TryFind(properties, "releaseId", out var release) || release.ValueKind != JsonValueKind.String ||
            !Guid.TryParse(release.GetString(), out var releaseId) ||
            !TryFind(properties, "isRequired", out var required) || required.ValueKind is not (JsonValueKind.True or JsonValueKind.False) ||
            !TryFind(properties, "executorRole", out var executor) || executor.ValueKind != JsonValueKind.String ||
            !TryFind(properties, "validatorRelation", out var relation) || relation.ValueKind != JsonValueKind.String ||
            !TryFind(properties, "validatorRole", out var validator) || validator.ValueKind != JsonValueKind.String ||
            !TryFind(properties, "allowedResults", out var results) || results.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        var allowedResults = new List<string>();
        foreach (var result in results.EnumerateArray())
        {
            if (result.ValueKind != JsonValueKind.String)
            {
                return false;
            }

            allowedResults.Add(result.GetString()!);
        }

        body = new PutRequest(
            releaseId,
            required.GetBoolean(),
            executor.GetString()!,
            relation.GetString()!,
            validator.GetString()!,
            allowedResults);
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
        ValidationPolicyAccessDeniedException or VersioningAccessDeniedException => Problem(
            context, 403, "ACCESO_DENEGADO", $"Se requiere {ValidationPolicyAuthorization.Administer} vigente en LOR-001"),
        TaskDefinitionNotMvpException => Problem(context, 404, "DEFINICION_NO_MVP", "La definición no pertenece al catálogo MVP"),
        ValidationPolicyReleaseNotDraftException => Problem(context, 409, "CONFIGURACION_BORRADOR_REQUERIDA", "La release debe permanecer en BORRADOR"),
        ValidationPolicyOverlapException => Problem(context, 409, "CONFIGURACION_SOLAPADA", "Ya existe una política de la TAR en la release"),
        ValidationPolicyDefinitionPreconditionException => Problem(context, 422, "VERSION_TAR_REQUERIDA", exception.Message),
        ValidationPolicyIfMatchRequiredException => Problem(context, 400, "IF_MATCH_REQUERIDO", "If-Match es obligatorio para sustituir la política vigente"),
        ValidationPolicyIdempotencyConflictException => Problem(context, 409, "IDEMPOTENCY_CONFLICT", "La clave ya fue usada con otro contenido"),
        VersionConflictException => Problem(context, 412, "VERSION_CONFLICT", "La versión cambió; vuelve a cargar el recurso"),
        ValidationPolicyValidationException or VersioningValidationException or VersioningStateException => Problem(
            context, 422, "POLITICA_VALIDACION_INVALIDA", exception.Message),
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
            instance: context.Request.Path,
            extensions: new Dictionary<string, object?>
            {
                ["code"] = code,
                ["correlationId"] = context.GetCorrelationId(),
            });

    private readonly record struct PutRequest(
        Guid ReleaseId,
        bool IsRequired,
        string ExecutorRole,
        string ValidatorRelation,
        string ValidatorRole,
        IReadOnlyCollection<string> AllowedResults);
}
