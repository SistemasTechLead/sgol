using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using Sgol.Evidence.Contracts;
using Sgol.Web.Infrastructure.Evidence;
using Sgol.Web.Infrastructure.Http;

namespace Sgol.Web.Presentation.Endpoints;

public static class EvidenceApiEndpoints
{
    private static readonly JsonSerializerOptions StrictJson = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    public static IEndpointRouteBuilder MapEvidenceApi(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/v1/files/upload-intents", CreateUploadAsync);
        endpoints.MapPost("/api/v1/files/{id:guid}/complete", CompleteAsync);
        endpoints.MapGet("/api/v1/files/{id:guid}/status", StatusAsync);
        endpoints.MapPost("/api/v1/obligations/{id:guid}/evidence", ContributeAsync);
        endpoints.MapPost("/api/v1/obligations/{id:guid}/evidence/{itemId:guid}/replacements", ReplaceAsync);
        endpoints.MapGet("/api/v1/obligations/{id:guid}/evidence", ListAsync);
        return endpoints;
    }

    public static async Task<IResult> CreateUploadAsync(HttpContext context, IAntiforgery antiforgery, IEvidenceContributionService service, CancellationToken token)
    {
        if (!TryMutation(context, out var actor, out var key, out var failure)) return failure!;
        if (await ValidateCsrfAsync(context, antiforgery) is { } csrfFailure) return csrfFailure;
        var body = await ReadAsync<CreateUploadBody>(context, token);
        if (body is null) return Problem(context, 400, "SOLICITUD_EVIDENCIA_INVALIDA", "La solicitud de evidencia no es válida");
        try
        {
            var result = await service.CreateUploadIntentAsync(new(actor, key, Correlation(context), body.ObligationId,
                body.RequirementCode!, body.OriginalFileName!, body.DeclaredMediaType!, body.SizeBytes, body.Sha256!, body.DocumentSubtype), token);
            return Results.Json(new { data = new { fileId = result.FileId, status = result.Status, upload = new { method = "PUT", url = result.Upload.Url, expiresAt = result.Upload.ExpiresAt.UtcDateTime, headers = new Dictionary<string, string> { ["Content-Type"] = result.Upload.Headers.ContentType, ["Content-Length"] = result.Upload.Headers.ContentLength, ["If-None-Match"] = result.Upload.Headers.IfNoneMatch, ["x-amz-meta-sgol-sha256"] = result.Upload.Headers.Sha256, ["x-amz-meta-sgol-media-type"] = result.Upload.Headers.MediaType, ["x-amz-meta-sgol-size-bytes"] = result.Upload.Headers.SizeBytes } } }, meta = Meta(context) }, statusCode: 201);
        }
        catch (Exception ex) { return Map(context, ex); }
    }

    public static async Task<IResult> CompleteAsync(HttpContext context, Guid id, IAntiforgery antiforgery, IEvidenceContributionService service, CancellationToken token)
    {
        if (!TryMutation(context, out var actor, out var key, out var failure)) return failure!;
        if (await ValidateCsrfAsync(context, antiforgery) is { } csrfFailure) return csrfFailure;
        var body = await ReadDocumentAsync(context, token);
        if (body is null || body.RootElement.ValueKind != JsonValueKind.Object || body.RootElement.EnumerateObject().Any())
            return Problem(context, 400, "SOLICITUD_EVIDENCIA_INVALIDA", "El cuerpo debe ser un objeto vacío");
        using (body)
            try
            {
                var result = await service.CompleteUploadAsync(new(actor, key, Correlation(context), id), token);
                return Results.Json(new { data = new { fileId = result.FileId, status = result.Status, statusUrl = $"/api/v1/files/{result.FileId:D}/status" }, meta = Meta(context) }, statusCode: 202);
            }
            catch (Exception ex) { return Map(context, ex); }
    }

    public static async Task<IResult> StatusAsync(HttpContext context, Guid id, IEvidenceContributionService service, CancellationToken token)
    {
        if (!TryActor(context, out var actor, out var failure)) return failure!;
        try { return Results.Ok(new { data = FileStatus(await service.GetFileStatusAsync(actor, id, token)), meta = Meta(context) }); }
        catch (Exception ex) { return Map(context, ex); }
    }

    public static async Task<IResult> ContributeAsync(HttpContext context, Guid id, IAntiforgery antiforgery, IEvidenceContributionService service, CancellationToken token)
    {
        if (!TryMutation(context, out var actor, out var key, out var failure)) return failure!;
        if (await ValidateCsrfAsync(context, antiforgery) is { } csrfFailure) return csrfFailure;
        var body = await ReadAsync<ContributeBody>(context, token);
        if (body is null || string.IsNullOrWhiteSpace(body.RequirementCode) ||
            (body.FileId.HasValue == (body.StructuredPayload is not null)) || body.FileId == Guid.Empty)
            return Problem(context, 400, "SOLICITUD_EVIDENCIA_INVALIDA", "La solicitud de evidencia no es válida");
        try
        {
            var result = await service.ContributeAsync(new(actor, key, Correlation(context), id, body.RequirementCode!,
                body.FileId, body.StructuredPayload), token);
            context.Response.Headers.ETag = $"\"{result.ItemRowVersion}\"";
            return Results.Json(new { data = Evidence(result), meta = Meta(context) }, statusCode: 201);
        }
        catch (Exception ex) { return Map(context, ex); }
    }

    public static async Task<IResult> ReplaceAsync(HttpContext context, Guid id, Guid itemId, IAntiforgery antiforgery, IEvidenceContributionService service, CancellationToken token)
    {
        if (!TryMutation(context, out var actor, out var key, out var failure)) return failure!;
        if (await ValidateCsrfAsync(context, antiforgery) is { } csrfFailure) return csrfFailure;
        if (!TryEtag(context, out var etag)) return Problem(context, 400, "IF_MATCH_INVALIDO", "If-Match debe contener el ETag vigente");
        var body = await ReadAsync<ReplaceBody>(context, token);
        if (body is null || (body.FileId.HasValue == (body.StructuredPayload is not null)) || body.FileId == Guid.Empty)
            return Problem(context, 400, "SOLICITUD_EVIDENCIA_INVALIDA", "La solicitud de evidencia no es válida");
        try
        {
            var result = await service.ReplaceAsync(new(actor, key, Correlation(context), id, itemId, body.FileId,
                body.StructuredPayload, body.Reason, etag), token);
            context.Response.Headers.ETag = $"\"{result.ItemRowVersion}\"";
            return Results.Json(new { data = Evidence(result), meta = Meta(context) }, statusCode: 201);
        }
        catch (Exception ex) { return Map(context, ex); }
    }

    public static async Task<IResult> ListAsync(HttpContext context, Guid id, IEvidenceContributionService service, CancellationToken token)
    {
        if (!TryActor(context, out var actor, out var failure)) return failure!;
        var allowed = new HashSet<string>(["requirementCode", "status", "limit", "cursor"], StringComparer.Ordinal);
        if (context.Request.Query.Keys.Any(x => !allowed.Contains(x)) || context.Request.Query.Any(x => x.Value.Count != 1))
            return Problem(context, 400, "FILTRO_EVIDENCIA_INVALIDO", "Los filtros de evidencia no son válidos");
        var requirement = Text(context, "requirementCode");
        var status = Text(context, "status");
        var cursor = Text(context, "cursor");
        var limit = 25;
        if ((context.Request.Query.ContainsKey("requirementCode") && string.IsNullOrWhiteSpace(requirement)) ||
            (context.Request.Query.ContainsKey("status") && string.IsNullOrWhiteSpace(status)) ||
            (context.Request.Query.ContainsKey("cursor") && string.IsNullOrWhiteSpace(cursor)))
            return Problem(context, 400, "FILTRO_EVIDENCIA_INVALIDO", "Los filtros de evidencia no son válidos");
        if (context.Request.Query.ContainsKey("limit") && !int.TryParse(Text(context, "limit"), out limit))
            return Problem(context, 400, "FILTRO_EVIDENCIA_INVALIDO", "Los filtros de evidencia no son válidos");
        try
        {
            var page = await service.ListAsync(new(actor, id, requirement, status, cursor, limit), token);
            var meta = new Dictionary<string, object?>
            {
                ["count"] = page.Items.Count,
                ["correlationId"] = context.GetCorrelationId()
            };
            if (page.NextCursor is not null) meta["nextCursor"] = page.NextCursor;
            return Results.Ok(new { data = page.Items.Select(Evidence), meta });
        }
        catch (Exception ex) { return Map(context, ex); }
    }

    private static bool TryMutation(HttpContext context, out Guid actor, out Guid key, out IResult? failure)
    {
        key = Guid.Empty;
        if (!TryActor(context, out actor, out failure)) return false;
        if (!context.Request.HasJsonContentType()) { failure = Problem(context, 400, "SOLICITUD_EVIDENCIA_INVALIDA", "Content-Type debe ser application/json"); return false; }
        if (context.Request.Headers["Idempotency-Key"].Count != 1 || !Guid.TryParseExact(context.Request.Headers["Idempotency-Key"], "D", out key) || key == Guid.Empty)
        { failure = Problem(context, 400, "IDEMPOTENCY_KEY_INVALIDA", "Idempotency-Key debe ser un UUID"); return false; }
        return true;
    }

    private static bool TryActor(HttpContext context, out Guid actor, out IResult? failure)
    {
        actor = Guid.Empty; failure = null;
        if (context.User.Identity?.IsAuthenticated != true) { failure = Problem(context, 401, "AUTENTICACION_REQUERIDA", "Se requiere una sesión activa"); return false; }
        if (!Guid.TryParseExact(context.User.FindFirstValue(ClaimTypes.NameIdentifier), "D", out actor) || actor == Guid.Empty)
        { failure = Problem(context, 403, "ACCESO_DENEGADO", "La sesión no identifica un actor autorizado"); return false; }
        return true;
    }

    private static bool TryEtag(HttpContext context, out long value)
    {
        value = 0; var raw = context.Request.Headers.IfMatch;
        return raw.Count == 1 && raw[0] is { Length: >= 3 } text && text[0] == '"' && text[^1] == '"' &&
            long.TryParse(text[1..^1], out value) && value > 0;
    }

    private static async Task<T?> ReadAsync<T>(HttpContext context, CancellationToken token) where T : class
    { try { return await JsonSerializer.DeserializeAsync<T>(context.Request.Body, StrictJson, token); } catch (JsonException) { return null; } }
    private static async Task<JsonDocument?> ReadDocumentAsync(HttpContext context, CancellationToken token)
    { try { return await JsonDocument.ParseAsync(context.Request.Body, cancellationToken: token); } catch (JsonException) { return null; } }
    private static async Task<IResult?> ValidateCsrfAsync(HttpContext context, IAntiforgery antiforgery)
    {
        try
        {
            await antiforgery.ValidateRequestAsync(context);
            return null;
        }
        catch (AntiforgeryValidationException)
        {
            return Problem(context, 400, "CSRF_INVALIDO", "El token CSRF no es válido");
        }
    }
    private static string? Text(HttpContext context, string key) => context.Request.Query.TryGetValue(key, out var value) ? value[0] : null;
    private static object Meta(HttpContext context) => new { correlationId = context.GetCorrelationId() };
    private static object FileStatus(EvidenceFileStatusDetails value) => new
    {
        value.FileId,
        value.Status,
        value.OriginalFileName,
        value.DeclaredMediaType,
        value.DetectedMediaType,
        value.SizeBytes,
        value.Sha256,
        createdAt = value.CreatedAt.UtcDateTime,
        uploadExpiresAt = value.UploadExpiresAt.UtcDateTime,
        uploadedAt = value.UploadedAt?.UtcDateTime,
        scannedAt = value.ScannedAt?.UtcDateTime,
        value.FailureCode,
        value.LinkedEvidenceItemId
    };
    private static object Evidence(EvidenceDetails value) => new
    {
        value.EvidenceItemId,
        value.ItemRowVersion,
        requirement = new
        {
            value.Requirement.RequirementVersionId,
            value.Requirement.RequirementCode,
            value.Requirement.Kind
        },
        version = new
        {
            value.Version.EvidenceVersionId,
            value.Version.VersionNo,
            value.Version.Status,
            value.Version.SubmittedByUserId,
            submittedAt = value.Version.SubmittedAt.UtcDateTime,
            value.Version.Reason,
            value.Version.SupersedesEvidenceVersionId
        },
        file = value.File is null ? null : new
        {
            value.File.FileId,
            value.File.OriginalFileName,
            value.File.MediaType,
            value.File.SizeBytes,
            value.File.Sha256,
            value.File.DocumentSubtype
        },
        structuredPayload = value.StructuredPayload?.RootElement
    };
    private static Guid Correlation(HttpContext context) => Guid.TryParse(context.GetCorrelationId(), out var value) ? value : Guid.CreateVersion7();

    private static IResult Map(HttpContext context, Exception exception)
    {
        return exception switch
        {
            EvidenceAccessDeniedException => Problem(context, 403, "ACCESO_DENEGADO", "No cuenta con el permiso requerido"),
            EvidenceObligationNotFoundException => Problem(context, 404, "OBLIGACION_NO_ENCONTRADA", "La obligación no existe"),
            EvidenceFileNotFoundException => Problem(context, 404, "ARCHIVO_NO_ENCONTRADO", "El archivo no existe"),
            EvidenceItemNotFoundException => Problem(context, 404, "EVIDENCIA_NO_ENCONTRADA", "La evidencia no existe"),
            EvidenceFileTooLargeException => Problem(context, 413, "ARCHIVO_DEMASIADO_GRANDE", "El archivo supera el tamaño admitido"),
            EvidenceUnsupportedMediaTypeException => Problem(context, 415, "TIPO_ARCHIVO_NO_ADMITIDO", "El tipo de archivo no está admitido"),
            EvidenceRequirementInvalidException => Problem(context, 422, "REQUISITO_EVIDENCIA_INVALIDO", "El requisito de evidencia no es válido"),
            EvidenceConditionUnresolvedException => Problem(context, 422, "CONDICION_EVIDENCIA_NO_RESUELTA", "La condición de evidencia no se puede resolver"),
            EvidenceConditionalRequirementException => Problem(context, 422, "REQUISITO_EVIDENCIA_NO_APLICABLE", "El requisito de evidencia no es aplicable"),
            EvidenceTypeNotImplementedException => Problem(context, 422, "TIPO_EVIDENCIA_NO_IMPLEMENTADO", "La clase de evidencia no está implementada"),
            EvidenceUploadExpiredException => Problem(context, 410, "INTENCION_CARGA_EXPIRADA", "La intención de carga expiró"),
            EvidenceUploadMissingException => Problem(context, 409, "CARGA_NO_ENCONTRADA", "No se encontró la carga confirmable"),
            EvidenceAlreadyExistsException => Problem(context, 409, "EVIDENCIA_YA_EXISTE", "La evidencia ya existe"),
            EvidenceFileAlreadyLinkedException => Problem(context, 409, "ARCHIVO_YA_VINCULADO", "El archivo ya fue vinculado"),
            EvidenceFileNotCleanException or EvidenceFileStateException => Problem(context, 422, "ARCHIVO_NO_LIMPIO", "El archivo no está limpio y vinculable"),
            EvidenceReasonRequiredException => Problem(context, 422, "MOTIVO_REQUERIDO", "El motivo es obligatorio"),
            EvidenceReplacementNotAllowedException => Problem(context, 422, "SUSTITUCION_NO_PERMITIDA", "La sustitución no está permitida"),
            EvidenceVersionConflictException => Problem(context, 412, "VERSION_CONFLICT", "La versión cambió"),
            EvidenceIdempotencyConflictException => Problem(context, 409, "IDEMPOTENCY_CONFLICT", "La clave ya fue usada con otro contenido"),
            EvidenceRateLimitException => Problem(context, 429, "LIMITE_INTENCIONES_EXCEDIDO", "Se alcanzó el límite de intenciones"),
            EvidenceRequestInvalidException => Problem(context, 400, "SOLICITUD_EVIDENCIA_INVALIDA", "La solicitud de evidencia no es válida"),
            _ => Problem(context, 503, "INFRAESTRUCTURA_EVIDENCIA_NO_DISPONIBLE", "La infraestructura de evidencia no está disponible")
        };
    }

    private static IResult Problem(HttpContext context, int status, string code, string title)
    {
        EvidenceTelemetry.Rejections.Add(1, tag: new("result", code));
        return Results.Problem(
            statusCode: status, title: title,
            extensions: new Dictionary<string, object?> { ["code"] = code, ["correlationId"] = context.GetCorrelationId() });
    }

    private sealed record CreateUploadBody(Guid ObligationId, string? RequirementCode, string? OriginalFileName,
        string? DeclaredMediaType, long SizeBytes, string? Sha256, string? DocumentSubtype);
    private sealed record ContributeBody(string? RequirementCode, Guid? FileId, JsonDocument? StructuredPayload);
    private sealed record ReplaceBody(Guid? FileId, JsonDocument? StructuredPayload, string? Reason);
}
