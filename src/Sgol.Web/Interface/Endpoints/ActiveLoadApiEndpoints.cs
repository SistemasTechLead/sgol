using System.Globalization;
using System.Security.Claims;
using Microsoft.Extensions.Primitives;
using Sgol.Assignment.Contracts;
using Sgol.Web.Infrastructure.Http;

namespace Sgol.Web.Presentation.Endpoints;

public static class ActiveLoadApiEndpoints
{
    private static readonly HashSet<string> AllowedParameters =
        new(["cursor", "limit", "personId"], StringComparer.Ordinal);

    public static IEndpointRouteBuilder MapActiveLoadApi(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/loads", HandleGetAsync);
        return endpoints;
    }

    public static async Task<IResult> HandleGetAsync(
        HttpContext context,
        IActiveLoadReader reader,
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

        if (!TryParseQuery(context.Request.Query, out var cursor, out var limit, out var personId))
        {
            return InvalidFilter(context);
        }

        try
        {
            var page = await reader.ListAsync(
                new ActiveLoadRequest(actorUserId, cursor, limit, personId),
                cancellationToken);
            return Results.Ok(new
            {
                data = page.Items,
                meta = new
                {
                    nextCursor = page.NextCursor,
                    count = page.Items.Count,
                    correlationId = context.GetCorrelationId(),
                },
            });
        }
        catch (ActiveLoadFilterInvalidException)
        {
            return InvalidFilter(context);
        }
        catch (ActiveLoadAccessDeniedException)
        {
            return Problem(
                context,
                403,
                "ACCESO_DENEGADO",
                $"Se requiere {ActiveLoadAuthorization.View}");
        }
    }

    private static bool TryParseQuery(
        IQueryCollection query,
        out string? cursor,
        out int limit,
        out Guid? personId)
    {
        cursor = null;
        limit = 25;
        personId = null;
        if (query.Keys.Any(key => !AllowedParameters.Contains(key)))
        {
            return false;
        }

        if (query.TryGetValue("cursor", out var cursorValues))
        {
            if (!TrySingle(cursorValues, out cursor) || string.IsNullOrWhiteSpace(cursor))
            {
                return false;
            }
        }

        if (query.TryGetValue("limit", out var limitValues))
        {
            if (!TrySingle(limitValues, out var rawLimit) ||
                !int.TryParse(rawLimit, NumberStyles.None, CultureInfo.InvariantCulture, out limit) ||
                limit is < 1 or > 100)
            {
                return false;
            }
        }

        if (query.TryGetValue("personId", out var personValues))
        {
            if (!TrySingle(personValues, out var rawPersonId) ||
                !Guid.TryParse(rawPersonId, out var parsedPersonId))
            {
                return false;
            }

            personId = parsedPersonId;
        }

        return true;
    }

    private static bool TrySingle(StringValues values, out string? value)
    {
        value = values.Count == 1 ? values[0] : null;
        return value is not null;
    }

    private static IResult InvalidFilter(HttpContext context) => Problem(
        context,
        400,
        "FILTRO_CARGA_INVALIDO",
        "Los parámetros de consulta no son válidos");

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
