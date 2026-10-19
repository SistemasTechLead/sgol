using System.Security.Claims;
using Sgol.Assignment.Contracts;
using Sgol.Web.Infrastructure.Http;

namespace Sgol.Web.Presentation.Endpoints;

public static class EligibilityEvaluationApiEndpoints
{
    public static IEndpointRouteBuilder MapEligibilityEvaluationApi(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/obligations/{id:guid}/eligibility", HandleGetAsync);
        return endpoints;
    }

    public static async Task<IResult> HandleGetAsync(
        Guid id,
        HttpContext context,
        IEligibilityEvaluationService service,
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

        try
        {
            var result = await service.GetLatestAsync(
                actorUserId,
                CorrelationId(context),
                id,
                cancellationToken);
            return Results.Ok(new
            {
                data = result,
                meta = new { correlationId = context.GetCorrelationId() },
            });
        }
        catch (EligibilityEvaluationAccessDeniedException)
        {
            return Problem(
                context,
                403,
                "ACCESO_DENEGADO",
                $"Se requiere {EligibilityAuthorization.Explain}");
        }
        catch (EligibilityEvaluationNotFoundException)
        {
            return Problem(
                context,
                404,
                "ELEGIBILIDAD_NO_ENCONTRADA",
                "La obligación o su snapshot no existe en el alcance visible");
        }
    }

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
