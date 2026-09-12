using System.Security.Claims;
using System.Text.Json;
using Sgol.Assignment.Contracts;
using Sgol.BuildingBlocks.Versioning;
using Sgol.Web.Infrastructure.Http;

namespace Sgol.Web.Presentation.Endpoints;

public static class AssignmentCorrectionApiEndpoints
{
    private static readonly string[] BodyProperties =
        ["eligibilityEvaluationId", "newResponsiblePersonId", "reason"];

    public static IEndpointRouteBuilder MapAssignmentCorrectionApi(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/v1/obligations/{id:guid}/assignment-corrections", HandlePostAsync);
        return endpoints;
    }

    public static async Task<IResult> HandlePostAsync(
        Guid id,
        JsonElement request,
        HttpContext context,
        IAssignmentCorrectionService service,
        CancellationToken cancellationToken)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            return Problem(context, 401, "AUTENTICACION_REQUERIDA", "Se requiere una sesión activa");
        }

        if (!Guid.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var actorUserId))
        {
            return Problem(context, 403, "ACCESO_DENEGADO", "La sesión no identifica un actor autorizado");
        }

        var parsedIdempotency = IdempotencyKeyHeader.Parse(context.Request);
        if (!parsedIdempotency.IsValid)
        {
            return Problem(context, 400, parsedIdempotency.ErrorCode!, parsedIdempotency.Detail!);
        }
        var idempotencyKey = parsedIdempotency.Key;

        long expectedRowVersion;
        try
        {
            expectedRowVersion = VersionEtag.ParseRequired(context.Request.Headers.IfMatch);
        }
        catch (VersionIfMatchRequiredException)
        {
            return Problem(context, 400, "IF_MATCH_REQUERIDO", "If-Match es obligatorio");
        }
        catch (VersionEtagInvalidException)
        {
            return Problem(context, 400, "IF_MATCH_INVALIDO", "If-Match debe contener el ETag vigente");
        }

        if (!TryBody(request, out var newResponsiblePersonId, out var eligibilityEvaluationId, out var reason))
        {
            return Problem(context, 400, "SOLICITUD_CORRECCION_INVALIDA", "El cuerpo de corrección es inválido");
        }

        try
        {
            reason = AssignmentCorrectionReason.Normalize(reason);
        }
        catch (AssignmentCorrectionReasonInvalidException)
        {
            return Problem(context, 400, "MOTIVO_INVALIDO", "El motivo normalizado debe tener entre 10 y 500 caracteres");
        }

        try
        {
            var result = await service.CorrectAsync(new CorrectAssignmentCommand(
                actorUserId,
                idempotencyKey,
                CorrelationId(context),
                id,
                newResponsiblePersonId,
                eligibilityEvaluationId,
                reason,
                expectedRowVersion), cancellationToken);
            context.Response.Headers.ETag = VersionEtag.Format(result.RowVersion);
            if (result.Result == AssignmentCorrectionResults.Created)
            {
                context.Response.Headers.Location = $"/api/v1/obligations/{result.ObligationId:D}";
            }

            var envelope = new { data = result, meta = new { correlationId = context.GetCorrelationId() } };
            return Results.Json(envelope, statusCode: result.Result == AssignmentCorrectionResults.Created ? 201 : 200);
        }
        catch (AssignmentCorrectionException exception)
        {
            return MapException(context, exception);
        }
    }

    private static bool TryBody(
        JsonElement request,
        out Guid newResponsiblePersonId,
        out Guid eligibilityEvaluationId,
        out string? reason)
    {
        newResponsiblePersonId = default;
        eligibilityEvaluationId = default;
        reason = null;
        var properties = request.ValueKind == JsonValueKind.Object
            ? request.EnumerateObject().Select(item => item.Name).ToArray()
            : [];
        if (properties.Length != BodyProperties.Length ||
            properties.Any(item => !BodyProperties.Contains(item, StringComparer.Ordinal)))
        {
            return false;
        }

        return request.TryGetProperty("newResponsiblePersonId", out var person) &&
            person.ValueKind == JsonValueKind.String && person.TryGetGuid(out newResponsiblePersonId) &&
            newResponsiblePersonId != Guid.Empty &&
            request.TryGetProperty("eligibilityEvaluationId", out var evaluation) &&
            evaluation.ValueKind == JsonValueKind.String && evaluation.TryGetGuid(out eligibilityEvaluationId) &&
            eligibilityEvaluationId != Guid.Empty &&
            request.TryGetProperty("reason", out var reasonValue) &&
            reasonValue.ValueKind == JsonValueKind.String && (reason = reasonValue.GetString()) is not null;
    }

    private static IResult MapException(HttpContext context, AssignmentCorrectionException exception) => exception switch
    {
        AssignmentCorrectionAccessDeniedException => Problem(context, 403, exception.ErrorCode, "Acceso denegado"),
        AssignmentCorrectionObligationNotFoundException or AssignmentCorrectionEvaluationNotFoundException =>
            Problem(context, 404, exception.ErrorCode, "No se encontró el recurso en el alcance visible"),
        AssignmentCorrectionVersionConflictException =>
            Problem(context, 412, exception.ErrorCode, "La versión cambió; vuelve a cargar el recurso"),
        AssignmentCorrectionObligationNotCorrectableException or AssignmentCorrectionResponsibleIneligibleException or
            AssignmentCorrectionNoChangeException or AssignmentCorrectionEvaluationMismatchException =>
            Problem(context, 422, exception.ErrorCode, "La corrección incumple una guarda funcional"),
        _ => Problem(context, 409, exception.ErrorCode, "La corrección no pudo confirmarse"),
    };

    private static Guid CorrelationId(HttpContext context) =>
        Guid.TryParse(context.GetCorrelationId(), out var id) ? id : Guid.CreateVersion7();

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
