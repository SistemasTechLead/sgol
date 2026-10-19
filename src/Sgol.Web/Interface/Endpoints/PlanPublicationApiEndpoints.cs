using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Sgol.BuildingBlocks.Versioning;
using Sgol.Planning.Contracts;
using Sgol.Web.Infrastructure.Http;

namespace Sgol.Web.Presentation.Endpoints;

public static class PlanPublicationApiEndpoints
{
    public static IEndpointRouteBuilder MapPlanPublicationApi(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/v1/plans/{planId}/publications", HandleRequestAsync);
        return endpoints;
    }

    private static async Task<IResult> HandleRequestAsync(
        string planId,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            return Problem(context, 401, "AUTENTICACION_REQUERIDA", "Se requiere una sesión activa");
        }

        JsonElement? body = null;
        using var reader = new StreamReader(
            context.Request.Body,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: false,
            leaveOpen: true);
        var bodyText = await reader.ReadToEndAsync(cancellationToken);
        if (bodyText.Length > 0)
        {
            if (!string.Equals(
                    context.Request.ContentType?.Split(';', 2)[0].Trim(),
                    "application/json",
                    StringComparison.OrdinalIgnoreCase))
            {
                return Problem(context, 400, "SOLICITUD_PUBLICACION_INVALIDA", "El cuerpo debe usar application/json");
            }

            try
            {
                using var document = JsonDocument.Parse(bodyText);
                body = document.RootElement.Clone();
            }
            catch (JsonException)
            {
                return Problem(context, 400, "SOLICITUD_PUBLICACION_INVALIDA", "El cuerpo debe ser un objeto JSON vacío");
            }
        }

        var service = context.RequestServices.GetRequiredService<IPlanPublicationService>();
        return await HandlePublishAsync(planId, body, context, service, cancellationToken);
    }

    public static async Task<IResult> HandlePublishAsync(
        string planId,
        JsonElement? body,
        HttpContext context,
        IPlanPublicationService service,
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

        if (!Guid.TryParse(planId, out var parsedPlanId) || parsedPlanId == Guid.Empty ||
            body is { } value && (value.ValueKind != JsonValueKind.Object || value.EnumerateObject().Any()))
        {
            return Problem(context, 400, "SOLICITUD_PUBLICACION_INVALIDA", "La ruta y el cuerpo de publicación son inválidos");
        }

        var parsedIdempotency = IdempotencyKeyHeader.Parse(context.Request);
        if (!parsedIdempotency.IsValid)
        {
            return Problem(context, 400, parsedIdempotency.ErrorCode!, parsedIdempotency.Detail!);
        }
        var idempotencyKey = parsedIdempotency.Key;

        if (context.Request.Headers.IfMatch.Count != 1)
        {
            return Problem(context, 400, "IF_MATCH_REQUERIDO", "If-Match es obligatorio");
        }

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
            return Problem(context, 400, "IF_MATCH_INVALIDO", "If-Match debe contener un ETag fuerte vigente");
        }

        try
        {
            var result = await service.PublishAsync(new PublishWorkPlanCommand(
                actorUserId,
                idempotencyKey,
                CorrelationId(context),
                parsedPlanId,
                expectedRowVersion), cancellationToken);
            context.Response.Headers.ETag = VersionEtag.Format(result.RowVersion);
            return Results.Json(
                new { data = result, meta = new { correlationId = context.GetCorrelationId() } },
                statusCode: result.Result == PlanPublicationResults.Recovered ? 200 : 201);
        }
        catch (PlanPublicationException exception)
        {
            return Problem(context, exception.ResponseCode, exception.ErrorCode, Title(exception));
        }
    }

    private static string Title(PlanPublicationException exception) => exception switch
    {
        PlanPublicationAccessDeniedException => "Acceso denegado",
        PlanPublicationNotFoundException => "El plan no existe en LOR-001",
        PlanPublicationIdempotencyConflictException => "Idempotency-Key ya fue usada con otro contenido",
        PlanPublicationStateConflictException => "El estado del plan no permite publicar",
        PlanPublicationContentConflictException => "El contenido del plan es incompatible",
        PlanPublicationVersionConflictException => "La versión del plan cambió",
        PlanPublicationUnassignedObligationException => "Una obligación aplicable no tiene asignación",
        PlanPublicationAssignmentInvalidException => "Una asignación aplicable dejó de ser publicable",
        PlanPublicationEmptyException => "No hay obligaciones publicables",
        PlanPublicationNoChangesException => "No hay novedades publicables",
        PlanPublicationConcurrencyException => "No fue posible serializar la publicación",
        _ => "La publicación entra en conflicto con la integridad persistida",
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
