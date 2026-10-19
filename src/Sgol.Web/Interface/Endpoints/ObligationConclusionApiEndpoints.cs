using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http.Features;
using Sgol.Execution.Contracts;
using Sgol.Web.Infrastructure.Http;

namespace Sgol.Web.Presentation.Endpoints;

public static class ObligationConclusionApiEndpoints
{
    public static IEndpointRouteBuilder MapObligationConclusionApi(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/v1/obligations/{id}/conclusion", HandleAsync);
        return endpoints;
    }

    public static async Task<IResult> HandleAsync(
        HttpContext context,
        string id,
        IAntiforgery antiforgery,
        IObligationConclusionService service,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParseExact(id, "D", out var obligationId) || obligationId == Guid.Empty)
        {
            return Problem(context, 400, "OBLIGACION_ID_INVALIDO", "El identificador de obligación no es válido");
        }

        if (context.Request.Query.Count != 0 || context.Request.ContentLength is > 0 ||
            context.Request.Headers.TransferEncoding.Count != 0 ||
            context.Features.Get<IHttpRequestBodyDetectionFeature>()?.CanHaveBody == true ||
            context.Request.Body.CanSeek && context.Request.Body.Length != 0)
        {
            return Problem(context, 400, "SOLICITUD_CONCLUSION_INVALIDA", "La solicitud de conclusión no admite query ni cuerpo");
        }

        var parsedIdempotency = IdempotencyKeyHeader.Parse(context.Request);
        if (!parsedIdempotency.IsValid)
        {
            return Problem(context, 400, parsedIdempotency.ErrorCode!, parsedIdempotency.Detail!);
        }
        var idempotencyKey = parsedIdempotency.Key;

        var ifMatch = context.Request.Headers.IfMatch;
        if (ifMatch.Count == 0)
        {
            return Problem(context, 400, "IF_MATCH_REQUERIDO", "If-Match es obligatorio");
        }

        if (!TryEtag(ifMatch, out var expectedRowVersion))
        {
            return Problem(context, 400, "IF_MATCH_INVALIDO", "If-Match debe contener un ETag fuerte vigente");
        }

        if (context.User.Identity?.IsAuthenticated != true)
        {
            return Problem(context, 401, "AUTENTICACION_REQUERIDA", "Se requiere una sesión activa con MFA");
        }

        if (!Guid.TryParseExact(context.User.FindFirstValue(ClaimTypes.NameIdentifier), "D", out var actorUserId) ||
            actorUserId == Guid.Empty)
        {
            return Problem(context, 404, "OBLIGACION_NO_ENCONTRADA", "La obligación no existe en el alcance autorizado");
        }

        try
        {
            await antiforgery.ValidateRequestAsync(context);
        }
        catch (AntiforgeryValidationException)
        {
            return Problem(context, 400, "CSRF_INVALIDO", "El token CSRF no es válido");
        }

        try
        {
            var result = await service.ConcludeAsync(new(
                actorUserId,
                idempotencyKey,
                CorrelationId(context),
                obligationId,
                expectedRowVersion), cancellationToken);
            context.Response.Headers.ETag = $"\"{result.RowVersion}\"";
            return Results.Ok(new
            {
                data = new
                {
                    result.ObligationId,
                    result.ExecutionStatus,
                    concludedAt = result.ConcludedAt.UtcDateTime,
                    result.ConcludedBy,
                    result.RowVersion,
                    executionResult = new
                    {
                        result.ExecutionResult.Id,
                        result.ExecutionResult.ResultCode,
                        resultPayload = result.ExecutionResult.ResultPayload.RootElement,
                        result.ExecutionResult.EvidenceReviewSnapshotId,
                        result.ExecutionResult.RecordedBy,
                        recordedAt = result.ExecutionResult.RecordedAt.UtcDateTime,
                    },
                },
                meta = new { correlationId = context.GetCorrelationId() },
            });
        }
        catch (ObligationEvidenceMissingException exception)
        {
            return Problem(context, 422, exception.Code, "Faltan requisitos obligatorios", new Dictionary<string, object?>
            {
                ["errors"] = exception.RequirementCodes.Select(reference => new
                {
                    field = "evidence",
                    code = "MISSING",
                    reference,
                }).ToArray(),
            });
        }
        catch (ObligationConclusionException exception)
        {
            return exception switch
            {
                ObligationConclusionNotFoundException =>
                    Problem(context, 404, exception.Code, "La obligación no existe en el alcance autorizado"),
                ObligationConclusionVersionConflictException =>
                    Problem(context, 412, exception.Code, "La versión de la obligación cambió"),
                ObligationAlreadyConcludedException =>
                    Problem(context, 409, exception.Code, "La obligación ya fue concluida"),
                ObligationConclusionIdempotencyConflictException =>
                    Problem(context, 409, exception.Code, "La clave fue usada con otro contenido"),
                ObligationConclusionConcurrencyException =>
                    Problem(context, 409, exception.Code, "La conclusión no pudo serializarse"),
                _ => Problem(context, 409, "CONCLUSION_INCONSISTENTE", "La conclusión no pudo comprobarse de forma coherente"),
            };
        }
    }

    private static bool TryEtag(Microsoft.Extensions.Primitives.StringValues header, out long value)
    {
        value = 0;
        if (header.Count != 1 || header[0] is not { Length: >= 3 } text ||
            text.StartsWith("W/", StringComparison.Ordinal) || text[0] != '"' || text[^1] != '"')
        {
            return false;
        }

        var digits = text.AsSpan(1, text.Length - 2);
        if (digits.IsEmpty || digits[0] == '0')
        {
            return false;
        }

        foreach (var digit in digits)
        {
            if (digit is < '0' or > '9')
            {
                return false;
            }
        }

        return long.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out value) && value > 0;
    }

    private static Guid CorrelationId(HttpContext context) =>
        Guid.TryParse(context.GetCorrelationId(), out var id) ? id : Guid.CreateVersion7();

    private static IResult Problem(
        HttpContext context,
        int status,
        string code,
        string title,
        IDictionary<string, object?>? extra = null)
    {
        var extensions = new Dictionary<string, object?>
        {
            ["code"] = code,
            ["correlationId"] = context.GetCorrelationId(),
        };
        if (extra is not null)
        {
            foreach (var item in extra)
            {
                extensions[item.Key] = item.Value;
            }
        }

        return Results.Problem(
            statusCode: status,
            title: title,
            instance: context.Request.Path,
            extensions: extensions);
    }
}
