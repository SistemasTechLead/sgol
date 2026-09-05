using System.Security.Claims;
using System.Text.Json;
using Sgol.BuildingBlocks.Versioning;
using Sgol.Configuration.Contracts;
using Sgol.Web.Infrastructure.Http;

namespace Sgol.Web.Presentation.Endpoints;

public static class EvidencePolicyApiEndpoints
{
    public static IEndpointRouteBuilder MapEvidencePolicyApi(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPut(
            "/api/v1/task-definitions/{taskCode}/evidence-policy",
            HandlePutAsync);
        return endpoints;
    }

    public static async Task<IResult> HandlePutAsync(
        string taskCode,
        JsonElement request,
        HttpContext context,
        IEvidencePolicyService service,
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
                    "El cuerpo debe contener únicamente releaseId y requirements con la forma aprobada");
            }

            var policy = await service.PutAsync(
                new PutEvidencePolicyCommand(
                    actorUserId,
                    idempotencyKey,
                    CorrelationId(context),
                    taskCode,
                    body.ReleaseId,
                    body.Requirements,
                    expectedRowVersion),
                cancellationToken);
            context.Response.Headers.ETag = VersionEtag.Format(policy.RowVersion);
            return Results.Created(
                $"/api/v1/task-definitions/{taskCode}/evidence-policy",
                Envelope(context, policy));
        }
        catch (Exception exception)
        {
            return MapException(context, exception);
        }
    }

    private static Task RecordRejectionAsync(
        HttpContext context,
        IEvidencePolicyService service,
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
        if (properties.Length != 2 ||
            !TryFind(properties, "releaseId", out var release) ||
            release.ValueKind != JsonValueKind.String ||
            !Guid.TryParse(release.GetString(), out var releaseId) ||
            !TryFind(properties, "requirements", out var requirements) ||
            requirements.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        var parsed = new List<EvidenceRequirementInput>();
        foreach (var requirement in requirements.EnumerateArray())
        {
            if (!TryRequirement(requirement, out var item))
            {
                return false;
            }

            parsed.Add(item);
        }

        body = new PutRequest(releaseId, parsed);
        return true;
    }

    private static bool TryRequirement(JsonElement requirement, out EvidenceRequirementInput item)
    {
        item = null!;
        if (requirement.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        var properties = requirement.EnumerateObject().ToArray();
        if (properties.Length != 3 ||
            !TryFind(properties, "code", out var code) || code.ValueKind != JsonValueKind.String ||
            !TryFind(properties, "kind", out var kind) || kind.ValueKind != JsonValueKind.String ||
            !TryFind(properties, "condition", out var condition))
        {
            return false;
        }

        var conditionCode = EvidenceConditionCodes.Always;
        if (condition.ValueKind != JsonValueKind.Null)
        {
            if (condition.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            var conditionProperties = condition.EnumerateObject().ToArray();
            if (conditionProperties.Length != 1 ||
                !TryFind(conditionProperties, "code", out var conditionValue) ||
                conditionValue.ValueKind != JsonValueKind.String)
            {
                return false;
            }

            conditionCode = conditionValue.GetString()!;
        }

        item = new EvidenceRequirementInput(code.GetString()!, kind.GetString()!, conditionCode);
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
        EvidencePolicyAccessDeniedException or VersioningAccessDeniedException => Problem(
            context, 403, "ACCESO_DENEGADO", $"Se requiere {EvidencePolicyAuthorization.Administer} vigente en LOR-001"),
        TaskDefinitionNotMvpException => Problem(context, 404, "DEFINICION_NO_MVP", "La definición no pertenece al catálogo MVP"),
        EvidencePolicyReleaseNotDraftException => Problem(context, 409, "CONFIGURACION_BORRADOR_REQUERIDA", "La release debe permanecer en BORRADOR"),
        EvidencePolicyOverlapException => Problem(context, 409, "CONFIGURACION_SOLAPADA", "Ya existe una política de la TAR en la release"),
        EvidencePolicyDefinitionPreconditionException => Problem(context, 422, "VERSION_TAR_REQUERIDA", exception.Message),
        EvidencePolicyIfMatchRequiredException => Problem(context, 400, "IF_MATCH_REQUERIDO", "If-Match es obligatorio para sustituir la política vigente"),
        EvidencePolicyIdempotencyConflictException => Problem(context, 409, "IDEMPOTENCY_CONFLICT", "La clave ya fue usada con otro contenido"),
        VersionConflictException => Problem(context, 412, "VERSION_CONFLICT", "La versión cambió; vuelve a cargar el recurso"),
        EvidencePolicyValidationException or VersioningValidationException or VersioningStateException => Problem(
            context, 422, "POLITICA_EVIDENCIA_INVALIDA", exception.Message),
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
        IReadOnlyCollection<EvidenceRequirementInput> Requirements);
}
