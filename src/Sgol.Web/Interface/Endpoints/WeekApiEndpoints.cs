using System.Globalization;
using System.Security.Claims;
using Sgol.Planning.Contracts;
using Sgol.Web.Infrastructure.Http;

namespace Sgol.Web.Presentation.Endpoints;

public static class WeekApiEndpoints
{
    public static IEndpointRouteBuilder MapWeekApi(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/weeks/{isoYear}/{isoWeek}", HandleRequestAsync);
        return endpoints;
    }

    private static Task<IResult> HandleRequestAsync(
        string isoYear,
        string isoWeek,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(context, out _, out var denied))
        {
            return Task.FromResult(denied);
        }

        var service = context.RequestServices.GetRequiredService<IWeekPeriodService>();
        return HandleGetAsync(isoYear, isoWeek, context, service, cancellationToken);
    }

    public static async Task<IResult> HandleGetAsync(
        string isoYear,
        string isoWeek,
        HttpContext context,
        IWeekPeriodService service,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(context, out var actorUserId, out var denied))
        {
            return denied;
        }

        if (!int.TryParse(isoYear, NumberStyles.None, CultureInfo.InvariantCulture, out var parsedYear) ||
            !int.TryParse(isoWeek, NumberStyles.None, CultureInfo.InvariantCulture, out var parsedWeek))
        {
            return Problem(
                context,
                StatusCodes.Status422UnprocessableEntity,
                "CALENDARIO_INVALIDO",
                "isoYear e isoWeek deben ser enteros de una semana ISO válida");
        }

        try
        {
            var period = await service.GetAsync(
                actorUserId,
                GetCorrelationId(context),
                parsedYear,
                parsedWeek,
                cancellationToken);
            return Results.Ok(new
            {
                data = period,
                meta = new { correlationId = context.GetCorrelationId() },
            });
        }
        catch (WeekAccessDeniedException)
        {
            return Problem(
                context,
                StatusCodes.Status403Forbidden,
                "ACCESO_DENEGADO",
                $"Se requiere {WeekAuthorization.View} vigente en LOR-001");
        }
        catch (WeekValidationException exception)
        {
            return Problem(
                context,
                StatusCodes.Status422UnprocessableEntity,
                "CALENDARIO_INVALIDO",
                exception.Message);
        }
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

    private static Guid GetCorrelationId(HttpContext context) =>
        Guid.TryParse(context.GetCorrelationId(), out var correlationId)
            ? correlationId
            : Guid.CreateVersion7();

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
