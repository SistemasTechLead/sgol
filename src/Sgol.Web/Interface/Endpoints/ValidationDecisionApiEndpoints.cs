using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Primitives;
using Sgol.Validation.Contracts;
using Sgol.Web.Infrastructure.Http;

namespace Sgol.Web.Presentation.Endpoints;

public static class ValidationDecisionApiEndpoints
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    public static IEndpointRouteBuilder MapValidationDecisionApi(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/v1/obligations/{id}/validation-decisions", IssueAsync);
        endpoints.MapPost("/api/v1/validation-decisions/{id}/replacements", ReplaceAsync);
        endpoints.MapGet("/api/v1/obligations/{id}/validations", GetAsync);
        return endpoints;
    }

    public static async Task<IResult> IssueAsync(HttpContext context, string id, IAntiforgery antiforgery,
        IValidationDecisionService service, CancellationToken cancellationToken)
    {
        if (!TryGuid(id, out var obligationId)) return Problem(context, 400, "OBLIGACION_ID_INVALIDO", "El identificador de obligación no es válido");
        if (!TryMutationHeaders(context, out var actor, out var key, out var version, out var failure)) return failure!;
        var body = await ReadAsync<IssueBody>(context, cancellationToken);
        if (body is null || body.Result is null || body.Foundation is null)
            return Problem(context, 400, "SOLICITUD_VALIDACION_INVALIDA", "La solicitud de validación no es válida");
        try { await antiforgery.ValidateRequestAsync(context); }
        catch (AntiforgeryValidationException) { return Problem(context, 400, "CSRF_INVALIDO", "El token CSRF no es válido"); }
        try
        {
            var result = await service.IssueAsync(new(actor, key, CorrelationId(context), obligationId, version,
                body.Result!, body.Foundation!, body.EscalationReason), cancellationToken);
            return Created(context, result);
        }
        catch (ValidationDecisionException exception) { return Map(context, exception); }
    }

    public static async Task<IResult> ReplaceAsync(HttpContext context, string id, IAntiforgery antiforgery,
        IValidationDecisionService service, CancellationToken cancellationToken)
    {
        if (!TryGuid(id, out var decisionId)) return Problem(context, 400, "DECISION_ID_INVALIDO", "El identificador de decisión no es válido");
        if (!TryMutationHeaders(context, out var actor, out var key, out var version, out var failure)) return failure!;
        var body = await ReadAsync<ReplaceBody>(context, cancellationToken);
        if (body is null || body.Result is null || body.Foundation is null || body.Reason is null)
            return Problem(context, 400, "SOLICITUD_VALIDACION_INVALIDA", "La solicitud de sustitución no es válida");
        try { await antiforgery.ValidateRequestAsync(context); }
        catch (AntiforgeryValidationException) { return Problem(context, 400, "CSRF_INVALIDO", "El token CSRF no es válido"); }
        try
        {
            var result = await service.ReplaceAsync(new(actor, key, CorrelationId(context), decisionId, version,
                body.Result!, body.Foundation!, body.Reason!), cancellationToken);
            return Created(context, result);
        }
        catch (ValidationDecisionException exception) { return Map(context, exception); }
    }

    public static async Task<IResult> GetAsync(HttpContext context, string id, IValidationDecisionService service,
        CancellationToken cancellationToken)
    {
        if (!TryGuid(id, out var obligationId)) return Problem(context, 400, "OBLIGACION_ID_INVALIDO", "El identificador de obligación no es válido");
        if (context.Request.Query.Count != 0 || CanHaveBody(context.Request) || context.Request.Headers.ContainsKey("Idempotency-Key") ||
            context.Request.Headers.ContainsKey("If-Match")) return Problem(context, 400, "SOLICITUD_VALIDACION_INVALIDA", "La consulta no admite query, cuerpo ni precondiciones");
        if (!TryActor(context, out var actor, out var failure)) return failure!;
        try
        {
            var result = await service.GetAsync(new(actor, CorrelationId(context), obligationId), cancellationToken);
            context.Response.Headers.ETag = $"\"{result.RowVersion}\"";
            context.Response.Headers.CacheControl = "private, no-store";
            return Results.Ok(Envelope(context, result));
        }
        catch (ValidationDecisionException exception) { return Map(context, exception); }
    }

    private static IResult Created(HttpContext context, ValidationMutationResult result)
    {
        context.Response.Headers.ETag = $"\"{result.History.RowVersion}\"";
        context.Response.Headers.Location = $"/api/v1/obligations/{result.History.ObligationId:D}/validations";
        return Results.Json(new
        {
            data = new
            {
                result.History.ObligationId,
                result.History.ExecutionStatus,
                validationRequirement = result.History.ValidationRequirement,
                decision = result.Decision
            },
            meta = new { correlationId = context.GetCorrelationId() }
        }, statusCode: 201);
    }

    private static object Envelope(HttpContext context, ValidationHistoryDetails result) => new
    {
        data = new { result.ObligationId, result.ExecutionStatus, validationRequirement = result.ValidationRequirement, decisions = result.Decisions },
        meta = new { correlationId = context.GetCorrelationId() },
    };

    private static async Task<T?> ReadAsync<T>(HttpContext context, CancellationToken token) where T : class
    {
        if (context.Request.Query.Count != 0 || context.Request.ContentType is null ||
            !string.Equals(context.Request.ContentType.Split(';', 2)[0].Trim(), "application/json", StringComparison.OrdinalIgnoreCase)) return null;
        try
        {
            using var document = await JsonDocument.ParseAsync(context.Request.Body, cancellationToken: token);
            if (document.RootElement.ValueKind != JsonValueKind.Object) return null;
            var expected = typeof(T).GetProperties().Select(property => JsonNamingPolicy.CamelCase.ConvertName(property.Name))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var actual = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (!actual.Add(property.Name)) return null;
            }
            if (!actual.SetEquals(expected)) return null;
            return document.RootElement.Deserialize<T>(JsonOptions);
        }
        catch (JsonException) { return null; }
    }

    private static bool TryMutationHeaders(HttpContext context, out Guid actor, out Guid key, out long version, out IResult? failure)
    {
        actor = key = Guid.Empty; version = 0; failure = null;
        if (!TryActor(context, out actor, out failure)) return false;
        var values = context.Request.Headers["Idempotency-Key"];
        if (values.Count == 0) { failure = Problem(context, 400, "IDEMPOTENCY_KEY_INVALIDA", "Idempotency-Key es obligatoria"); return false; }
        if (values.Count != 1 || !TryGuid(values[0], out key)) { failure = Problem(context, 400, "IDEMPOTENCY_KEY_INVALIDA", "Idempotency-Key debe ser un UUID canónico"); return false; }
        var ifMatch = context.Request.Headers.IfMatch;
        if (ifMatch.Count == 0) { failure = Problem(context, 400, "IF_MATCH_REQUERIDO", "If-Match es obligatorio"); return false; }
        if (!TryEtag(ifMatch, out version)) { failure = Problem(context, 400, "IF_MATCH_INVALIDO", "If-Match debe contener un ETag fuerte vigente"); return false; }
        return true;
    }

    private static bool TryActor(HttpContext context, out Guid actor, out IResult? failure)
    {
        actor = Guid.Empty; failure = null;
        if (context.User.Identity?.IsAuthenticated != true) { failure = Problem(context, 401, "AUTENTICACION_REQUERIDA", "Se requiere una sesión activa con MFA"); return false; }
        if (!TryGuid(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out actor)) { failure = Problem(context, 403, "ACCESO_DENEGADO", "La sesión no identifica un actor autorizado"); return false; }
        return true;
    }

    private static bool TryGuid(string? raw, out Guid value)
    {
        value = Guid.Empty; return raw is not null && Guid.TryParseExact(raw, "D", out value) && value != Guid.Empty && raw == value.ToString("D");
    }

    private static bool TryEtag(StringValues header, out long value)
    {
        value = 0;
        if (header.Count != 1 || header[0] is not { Length: >= 3 } text || text.StartsWith("W/", StringComparison.Ordinal) || text[0] != '"' || text[^1] != '"') return false;
        var digits = text.AsSpan(1, text.Length - 2);
        return !digits.IsEmpty && digits[0] != '0' && digits.ToArray().All(char.IsAsciiDigit) &&
            long.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out value) && value > 0;
    }

    private static bool CanHaveBody(HttpRequest request) => request.ContentLength is > 0 || request.Headers.TransferEncoding.Count != 0 ||
        request.HttpContext.Features.Get<IHttpRequestBodyDetectionFeature>()?.CanHaveBody == true || request.Body.CanSeek && request.Body.Length != 0;
    private static Guid CorrelationId(HttpContext context) => Guid.TryParse(context.GetCorrelationId(), out var id) ? id : Guid.CreateVersion7();

    private static IResult Map(HttpContext context, ValidationDecisionException exception) => exception.Code switch
    {
        "ACCESO_DENEGADO" => Problem(context, 403, exception.Code, "No existe autoridad base para validar"),
        "OBLIGACION_NO_ENCONTRADA" => Problem(context, 404, exception.Code, "La obligación no existe en el alcance autorizado"),
        "DECISION_VALIDACION_NO_ENCONTRADA" => Problem(context, 404, exception.Code, "La decisión no existe en el alcance autorizado"),
        "VERSION_CONFLICT" => Problem(context, 412, exception.Code, "La versión de validación cambió"),
        "RESULTADO_VALIDACION_INVALIDO" or "FUNDAMENTO_INVALIDO" or "MOTIVO_REQUERIDO" or "AUTOVALIDACION_NO_PERMITIDA" => Problem(context, 422, exception.Code, "La decisión no cumple el contrato"),
        "SOLICITUD_VALIDACION_INVALIDA" => Problem(context, 400, exception.Code, "La solicitud de validación no es válida"),
        _ => Problem(context, 409, exception.Code, "La validación no pudo confirmarse"),
    };

    private static IResult Problem(HttpContext context, int status, string code, string title) => Results.Problem(statusCode: status,
        title: title, instance: context.Request.Path, extensions: new Dictionary<string, object?>
        { ["code"] = code, ["correlationId"] = context.GetCorrelationId() });

    private sealed record IssueBody(string? Result, string? Foundation, string? EscalationReason);
    private sealed record ReplaceBody(string? Result, string? Foundation, string? Reason);
}
