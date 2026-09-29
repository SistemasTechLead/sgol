using System.Security.Claims;
using System.Text.Json;
using Sgol.Generation.Contracts;
using Sgol.Web.Infrastructure.Http;

namespace Sgol.Web.Presentation.Endpoints;

public static class GenerationRequestApiEndpoints
{
    public static IEndpointRouteBuilder MapGenerationRequestApi(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/v1/generation-requests", ReadPostAsync);
        endpoints.MapGet("/api/v1/generation-requests/options", HandleOptionsAsync);
        endpoints.MapGet("/api/v1/generation-requests/receipt-origins", HandleReceiptOriginsAsync);
        endpoints.MapGet("/api/v1/generation-requests/{id:guid}", HandleGetAsync);
        return endpoints;
    }

    private static async Task<IResult> ReadPostAsync(HttpContext context, IGenerationRequestService service, CancellationToken token)
    {
        context.Response.Headers.CacheControl = "no-store";
        const int limit = 32 * 1024;
        if (context.Request.ContentLength > limit)
            return Problem(context, 413, "GENERATION_REQUEST_DEMASIADO_GRANDE", "La solicitud excede el tamaño permitido");
        using var buffer = new MemoryStream();
        var bytes = new byte[4096];
        int count;
        while ((count = await context.Request.Body.ReadAsync(bytes, token)) != 0)
        {
            if (buffer.Length + count > limit)
                return Problem(context, 413, "GENERATION_REQUEST_DEMASIADO_GRANDE", "La solicitud excede el tamaño permitido");
            buffer.Write(bytes, 0, count);
        }
        try
        {
            using var json = JsonDocument.Parse(buffer.ToArray());
            return await HandlePostAsync(json.RootElement, context, service, token);
        }
        catch (JsonException) { return Problem(context, 400, "GENERATION_REQUEST_INVALIDA", "El cuerpo no es JSON válido"); }
    }

    public static async Task<IResult> HandleOptionsAsync(HttpContext context, IManualGenerationReader reader, CancellationToken token)
    {
        context.Response.Headers.CacheControl = "no-store";
        if (!TryGetActor(context, out var actor, out var denied)) return denied;
        if (context.Request.Query.Count != 0) return Problem(context, 400, "CONSULTA_ORIGEN_INVALIDA", "La consulta no admite parámetros");
        try
        {
            var items = await reader.GetOptionsAsync(actor, token);
            return Results.Ok(new { data = items, meta = new { count = items.Count, correlationId = context.GetCorrelationId() } });
        }
        catch (Exception e) { return MapException(context, e); }
    }

    public static async Task<IResult> HandleReceiptOriginsAsync(HttpContext context, IManualGenerationReader reader, CancellationToken token)
    {
        context.Response.Headers.CacheControl = "no-store";
        if (!TryGetActor(context, out var actor, out var denied)) return denied;
        var query = context.Request.Query;
        var size = 25;
        if (query.Any(q => q.Key is not ("receiptReference" or "cursor" or "pageSize") || q.Value.Count != 1) ||
            (query.TryGetValue("pageSize", out var rawSize) && !int.TryParse(rawSize, out size)))
            return Problem(context, 400, "CONSULTA_ORIGEN_INVALIDA", "La consulta de recepciones no es válida");
        try
        {
            var page = await reader.GetReceiptOriginsAsync(actor, query.TryGetValue("receiptReference", out var filter) ? filter.ToString() : null,
                query.TryGetValue("cursor", out var cursor) ? cursor.ToString() : null, size, token);
            return Results.Ok(new { data = page.Items, meta = new { count = page.Items.Count, nextCursor = page.NextCursor, correlationId = context.GetCorrelationId() } });
        }
        catch (Exception e) { return MapException(context, e); }
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

        var idempotency = IdempotencyKeyHeader.Parse(context.Request);
        if (!idempotency.IsValid)
        {
            return Problem(context, 400, idempotency.ErrorCode!, idempotency.Detail!);
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
                    idempotency.Key,
                    CorrelationId(context),
                    body.RuleVersionId,
                    body.BranchId,
                    body.PeriodId,
                    body.OriginType,
                    body.OriginReference, body.InputPayload),
                cancellationToken);
            return result.ResponseCode == StatusCodes.Status201Created
                ? Results.Created($"/api/v1/generation-requests/{result.GenerationRequestId}", Envelope(context, GenerationRequestSerialization.Snapshot(result)))
                : Results.Ok(Envelope(context, GenerationRequestSerialization.Snapshot(result)));
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
            context.Response.Headers.CacheControl = "no-store";
            var result = await service.GetAsync(actorUserId, CorrelationId(context), id, cancellationToken);
            return Results.Ok(Envelope(context, GenerationRequestSerialization.Snapshot(result)));
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
        if (properties.Select(p => p.Name).Distinct(StringComparer.Ordinal).Count() != properties.Length) return false;
        if (request.TryGetProperty("schemaVersion", out var version))
        {
            string[] allowed = ["schemaVersion", "ruleVersionId", "branchId", "periodId", "originType", "inputPayload"];
            if (properties.Length != 6 || properties.Any(p => !allowed.Contains(p.Name)) ||
                version.ValueKind != JsonValueKind.Number || !version.TryGetInt32(out var v) || v != 2 ||
                !TryGuid(properties, "ruleVersionId", out var rule) || !TryGuid(properties, "branchId", out var branch) ||
                !TryGuid(properties, "periodId", out var period) || !TryString(properties, "originType", out var origin) ||
                !request.TryGetProperty("inputPayload", out var input) || input.ValueKind != JsonValueKind.Object) return false;
            body = new(rule, branch, period, origin, string.Empty, input.Clone());
            return true;
        }
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
        ManualGenerationException manual => Results.Problem(statusCode: manual.Status, title: "No se pudo completar la solicitud",
            extensions: new Dictionary<string, object?> { ["code"] = manual.Code, ["correlationId"] = context.GetCorrelationId(), ["fieldErrors"] = manual.FieldErrors }),
        GenerationRequestAccessDeniedException => Problem(
            context, 403, "ACCESO_DENEGADO", $"Se requiere {GenerationRequestAuthorization.Create} para el alcance solicitado"),
        GenerationRequestNotFoundException or GenerationRequestRuleNotFoundException => Problem(
            context, 404, "GENERATION_REQUEST_NO_ENCONTRADA", "La solicitud o regla no existe en el alcance visible"),
        GenerationRequestIdempotencyConflictException => Problem(
            context, 409, "IDEMPOTENCY_CONFLICT", "La clave ya fue usada con otro contenido"),
        GenerationRequestConflictAuditException => Problem(
            context, 500, "IDEMPOTENCY_CONFLICT_AUDIT_FAILED", "El conflicto no pudo registrarse"),
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
            Guid.TryParseExact(property.GetString(), "D", out value) && value != Guid.Empty;
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
        string OriginReference, JsonElement? InputPayload = null);
}
