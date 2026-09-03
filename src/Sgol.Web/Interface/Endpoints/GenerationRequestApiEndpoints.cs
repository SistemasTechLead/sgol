using System.Security.Claims;
using System.Text.Json;
using Sgol.Generation.Contracts;
using Sgol.Web.Infrastructure.Http;

namespace Sgol.Web.Presentation.Endpoints;

public static class GenerationRequestApiEndpoints
{
    public static IEndpointRouteBuilder MapGenerationRequestApi(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/v1/generation-requests", HandlePostAsync);
        endpoints.MapGet("/api/v1/generation-requests/{id:guid}", HandleGetAsync);
        return endpoints;
    }

    public static async Task<IResult> HandlePostAsync(
        JsonElement request,
        HttpContext context,
        IGenerationRequestService service,
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

        if (!TryRequest(request, out var body))
        {
            return Problem(
                context,
                400,
                "GENERATION_REQUEST_INVALIDA",
                "El cuerpo debe contener únicamente ruleVersionId, branchId, periodId, originType y originReference");
        }

        try
        {
            var result = await service.CreateAsync(
                new CreateGenerationRequestCommand(
                    actorUserId,
                    idempotencyKey,
                    CorrelationId(context),
                    body.RuleVersionId,
                    body.BranchId,
                    body.PeriodId,
                    body.OriginType,
                    body.OriginReference),
                cancellationToken);
            return result.Result == GenerationRequestResults.Accepted
                ? Results.Created($"/api/v1/generation-requests/{result.GenerationRequestId}", Envelope(context, result))
                : Results.Ok(Envelope(context, result));
        }
        catch (Exception exception)
        {
            return MapException(context, exception);
        }
    }

    public static async Task<IResult> HandleGetAsync(
        Guid id,
        HttpContext context,
        IGenerationRequestService service,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(context, out var actorUserId, out var denied))
        {
            return denied;
        }

        try
        {
            var result = await service.GetAsync(actorUserId, CorrelationId(context), id, cancellationToken);
            return Results.Ok(Envelope(context, result));
        }
        catch (Exception exception)
        {
            return MapException(context, exception);
        }
    }

    private static bool TryRequest(JsonElement request, out PostRequest body)
    {
        body = default;
        if (request.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        var properties = request.EnumerateObject().ToArray();
        if (properties.Length != 5 ||
            !TryGuid(properties, "ruleVersionId", out var ruleVersionId) ||
            !TryGuid(properties, "branchId", out var branchId) ||
            !TryGuid(properties, "periodId", out var periodId) ||
            !TryString(properties, "originType", out var originType) ||
            !TryString(properties, "originReference", out var originReference))
        {
            return false;
        }

        body = new PostRequest(ruleVersionId, branchId, periodId, originType, originReference);
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

    private static IResult MapException(HttpContext context, Exception exception) => exception switch
    {
        GenerationRequestAccessDeniedException => Problem(
            context, 403, "ACCESO_DENEGADO", $"Se requiere {GenerationRequestAuthorization.Create} para el alcance solicitado"),
        GenerationRequestNotFoundException or GenerationRequestRuleNotFoundException => Problem(
            context, 404, "GENERATION_REQUEST_NO_ENCONTRADA", "La solicitud o regla no existe en el alcance visible"),
        GenerationRequestIdempotencyConflictException => Problem(
            context, 409, "IDEMPOTENCY_CONFLICT", "La clave ya fue usada con otro contenido"),
        GenerationRequestRuleNotManualException => Problem(
            context, 409, "REGLA_MANUAL_REQUERIDA", exception.Message),
        GenerationRequestTaskInactiveException => Problem(
            context, 409, "TAR_INACTIVA", exception.Message),
        GenerationRequestPeriodNotFoundException => Problem(
            context, 422, "PERIODO_INVALIDO", exception.Message),
        GenerationRequestOriginInvalidException => Problem(
            context, 422, "ORIGEN_INVALIDO", exception.Message),
        GenerationRequestValidationException => Problem(
            context, 422, "GENERATION_REQUEST_INVALIDA", exception.Message),
        _ => throw exception,
    };

    private static bool TryGuid(JsonProperty[] properties, string name, out Guid value)
    {
        value = default;
        return TryFind(properties, name, out var property) &&
            property.ValueKind == JsonValueKind.String &&
            Guid.TryParse(property.GetString(), out value);
    }

    private static bool TryString(JsonProperty[] properties, string name, out string value)
    {
        value = string.Empty;
        if (!TryFind(properties, name, out var property) || property.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = property.GetString()!;
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

    private readonly record struct PostRequest(
        Guid RuleVersionId,
        Guid BranchId,
        Guid PeriodId,
        string OriginType,
        string OriginReference);
}
