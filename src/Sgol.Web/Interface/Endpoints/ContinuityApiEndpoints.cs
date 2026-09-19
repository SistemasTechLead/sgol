using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.Extensions.Primitives;
using Sgol.Continuity.Contracts;
using Sgol.Web.Infrastructure.Http;

namespace Sgol.Web.Presentation.Endpoints;

public static class ContinuityApiEndpoints
{
    public static IEndpointRouteBuilder MapContinuityApi(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/v1/continuity/reconciliations", CreateAsync);
        endpoints.MapGet("/api/v1/continuity/reconciliations/{reconciliationId:guid}", GetAsync);
        endpoints.MapPost("/api/v1/continuity/reconciliations/{reconciliationId:guid}/approval", ApproveAsync);
        return endpoints;
    }

    public static async Task<IResult> CreateAsync(HttpContext context, IRecoveryReconciliationService service,
        CancellationToken cancellationToken)
    {
        if (!TryActor(context, out var actor, out var failure)) return failure!;
        var parsedIdempotency = IdempotencyKeyHeader.Parse(context.Request);
        if (!parsedIdempotency.IsValid)
            return Problem(context, 400, parsedIdempotency.ErrorCode!, parsedIdempotency.Detail!);
        var key = parsedIdempotency.Key;
        var reason = await ReadReasonAsync(context.Request, cancellationToken);
        if (reason is null) return Problem(context, 400, "SOLICITUD_RECONCILIACION_INVALIDA", "La solicitud no es válida");
        try
        {
            var result = await service.CreateAsync(new(actor, key, CorrelationId(context), reason), cancellationToken);
            WriteHeaders(context, result, $"/api/v1/continuity/reconciliations/{result.ReconciliationId:D}");
            return Results.Json(Response(result, context), statusCode: StatusCodes.Status201Created);
        }
        catch (RecoveryContractException exception) { return Map(context, exception); }
    }

    public static async Task<IResult> GetAsync(HttpContext context, Guid reconciliationId,
        IRecoveryReconciliationService service, CancellationToken cancellationToken)
    {
        if (!TryActor(context, out var actor, out var failure)) return failure!;
        try
        {
            var result = await service.GetAsync(new(actor, CorrelationId(context), reconciliationId), cancellationToken);
            WriteHeaders(context, result, null);
            return Results.Ok(Response(result, context));
        }
        catch (RecoveryContractException exception) { return Map(context, exception); }
    }

    public static async Task<IResult> ApproveAsync(HttpContext context, Guid reconciliationId,
        IRecoveryReconciliationService service, CancellationToken cancellationToken)
    {
        if (!TryActor(context, out var actor, out var failure)) return failure!;
        var parsedIdempotency = IdempotencyKeyHeader.Parse(context.Request);
        if (!parsedIdempotency.IsValid)
            return Problem(context, 400, parsedIdempotency.ErrorCode!, parsedIdempotency.Detail!);
        var key = parsedIdempotency.Key;
        if (!TryIfMatch(context.Request.Headers.IfMatch, out var sequence))
            return Problem(context, 428, "IF_MATCH_REQUERIDO", "If-Match es obligatorio");
        var reason = await ReadReasonAsync(context.Request, cancellationToken);
        if (reason is null) return Problem(context, 400, "APROBACION_RECONCILIACION_INVALIDA", "La aprobación no es válida");
        try
        {
            var result = await service.ApproveAsync(new(actor, key, CorrelationId(context), reconciliationId,
                sequence, reason), cancellationToken);
            WriteHeaders(context, result, null);
            return Results.Ok(Response(result, context));
        }
        catch (RecoveryContractException exception) { return Map(context, exception); }
    }

    private static object Response(RecoveryReconciliationDetails value, HttpContext context) => new
    {
        data = new
        {
            value.ReconciliationId,
            value.BranchId,
            value.Status,
            value.RequestedAt,
            value.TargetRecoveryAt,
            value.Sequence,
            value.ReferenceRootSha256,
            value.ActualRootSha256,
            value.DifferenceCount,
            value.DifferencesTruncated,
            value.ObservedRpoSeconds,
            value.ObservedRtoSeconds,
            value.ApprovedAt,
            value.ApprovedBy,
            value.Differences,
        },
        meta = new { value.Replayed, correlationId = context.GetCorrelationId() },
    };

    private static async Task<string?> ReadReasonAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        try
        {
            using var document = await JsonDocument.ParseAsync(request.Body,
                new JsonDocumentOptions { MaxDepth = 4 }, cancellationToken);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || root.EnumerateObject().Count() != 1 ||
                !root.TryGetProperty("reason", out var reason) || reason.ValueKind != JsonValueKind.String)
                return null;
            return reason.GetString();
        }
        catch (JsonException) { return null; }
    }

    private static bool TryActor(HttpContext context, out Guid actor, out IResult? failure)
    {
        actor = Guid.Empty;
        failure = null;
        if (context.User.Identity?.IsAuthenticated != true)
        {
            failure = Problem(context, 401, "AUTENTICACION_REQUERIDA", "Se requiere una sesión activa con MFA");
            return false;
        }
        var raw = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (raw is null || !Guid.TryParseExact(raw, "D", out actor) || actor == Guid.Empty || raw != actor.ToString("D"))
        {
            failure = Problem(context, 403, "ACCESO_DENEGADO", "La sesión no identifica un actor autorizado");
            return false;
        }
        return true;
    }

    private static bool TryIfMatch(StringValues values, out long sequence)
    {
        sequence = 0;
        if (values.Count != 1 || values[0] is not { } raw || raw.Length < 3 || raw[0] != '"' || raw[^1] != '"') return false;
        return long.TryParse(raw[1..^1], NumberStyles.None, CultureInfo.InvariantCulture, out sequence) && sequence > 0;
    }

    private static Guid CorrelationId(HttpContext context) =>
        Guid.TryParse(context.GetCorrelationId(), out var value) && value != Guid.Empty
            ? value
            : throw new InvalidOperationException("Correlation middleware did not provide a valid identifier.");

    private static void WriteHeaders(HttpContext context, RecoveryReconciliationDetails value, string? location)
    {
        context.Response.Headers.ETag = $"\"{value.Sequence.ToString(CultureInfo.InvariantCulture)}\"";
        context.Response.Headers.CacheControl = "private, no-store";
        context.Response.Headers.Pragma = "no-cache";
        if (location is not null) context.Response.Headers.Location = location;
    }

    private static IResult Map(HttpContext context, RecoveryContractException exception) => exception.ErrorCode switch
    {
        "ACCESO_DENEGADO" => Problem(context, 403, exception.ErrorCode, $"Se requiere {ContinuityAuthorization.View}"),
        "RECONCILIACION_NO_ENCONTRADA" => Problem(context, 404, exception.ErrorCode, "La reconciliación no existe"),
        "IDEMPOTENCY_CONFLICT" => Problem(context, 409, exception.ErrorCode, "La clave fue usada con otro contenido"),
        "VERSION_CONFLICT" => Problem(context, 412, exception.ErrorCode, "La reconciliación cambió"),
        "RECONCILIACION_NO_APROBABLE" => Problem(context, 409, exception.ErrorCode, "La reconciliación no puede aprobarse"),
        "MOTIVO_INVALIDO" => Problem(context, 400, exception.ErrorCode, "El motivo no es válido"),
        "RECONCILIATION_AUDIT_FAILED" => Problem(context, 500, exception.ErrorCode, "No fue posible auditar la consulta"),
        _ => Problem(context, 500, "RECONCILIACION_FALLO", "No fue posible procesar la reconciliación"),
    };

    private static IResult Problem(HttpContext context, int status, string code, string title) =>
        Results.Problem(statusCode: status, title: title, instance: context.Request.Path,
            extensions: new Dictionary<string, object?> { ["code"] = code, ["correlationId"] = context.GetCorrelationId() });
}
