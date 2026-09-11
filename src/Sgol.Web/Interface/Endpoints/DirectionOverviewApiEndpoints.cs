using System.Globalization;
using System.Security.Claims;
using Microsoft.Extensions.Primitives;
using Sgol.Identity.Contracts;
using Sgol.Reporting.Contracts;
using Sgol.Web.Infrastructure.Http;

namespace Sgol.Web.Presentation.Endpoints;

public static class DirectionOverviewApiEndpoints
{
    private static readonly HashSet<string> AllowedParameters = new(
        ["isoYear", "isoWeek", "level", "responsiblePersonId", "cursor", "limit"],
        StringComparer.Ordinal);

    public static IEndpointRouteBuilder MapDirectionOverviewApi(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/direction/overview", ReadAsync);
        return endpoints;
    }

    public static async Task<IResult> ReadAsync(
        HttpContext context,
        IIndicatorReader reader,
        CancellationToken cancellationToken)
    {
        if (!TryActor(context, out var actor, out var failure)) return failure!;
        if (!TryRequest(context.Request.Query, actor, out var request))
            return Problem(context, 400, "FILTRO_DIRECCION_INVALIDO", "Los filtros de Dirección no son válidos");

        try
        {
            var result = await reader.ReadDirectionOverviewAsync(request!, cancellationToken);
            var snapshot = result.Snapshot;
            context.Response.Headers.CacheControl = "private, no-store";
            context.Response.Headers.Pragma = "no-cache";
            return Results.Ok(new
            {
                data = new
                {
                    snapshot.Period,
                    snapshot.Scope,
                    snapshot.BaseObligationsCount,
                    snapshot.Pending,
                    snapshot.Concluded,
                    snapshot.Validated,
                    snapshot.NonCompliant,
                    activeLoadByPerson = new
                    {
                        snapshot.ActiveLoadByPerson.Denominator,
                        items = snapshot.ActiveLoadByPerson.Items.Select(item => new
                        {
                            item.Person,
                            item.Level,
                            item.Count,
                        }),
                    },
                },
                meta = new
                {
                    result.NextCursor,
                    count = snapshot.ActiveLoadByPerson.Items.Count,
                    result.QueriedAt,
                    correlationId = context.GetCorrelationId(),
                },
            });
        }
        catch (DirectionOverviewFilterInvalidException)
        {
            return Problem(context, 400, "FILTRO_DIRECCION_INVALIDO", "Los filtros de Dirección no son válidos");
        }
        catch (DirectionOverviewAccessDeniedException)
        {
            return Problem(context, 403, "ACCESO_DENEGADO", $"Se requiere {DirectionOverviewAuthorization.View}");
        }
        catch (DirectionOverviewQueryInconsistentException)
        {
            return Problem(context, 500, "CONSULTA_DIRECCION_INCONSISTENTE", "No fue posible consultar la vista de Dirección");
        }
    }

    private static bool TryRequest(IQueryCollection query, Guid actor, out IndicatorRequest? request)
    {
        request = null;
        if (query.Keys.Any(key => !AllowedParameters.Contains(key)) ||
            !TryRequiredInt(query, "isoYear", out var year) ||
            !TryRequiredInt(query, "isoWeek", out var week) ||
            year is < 1 or > 9999 || week is < 1 ||
            week > ISOWeek.GetWeeksInYear(year) ||
            !TryOptionalText(query, "level", out var level) ||
            level is not null && !CanonicalRole.IsDefined(level) ||
            !TryOptionalGuid(query, "responsiblePersonId", out var person) ||
            !TryOptionalText(query, "cursor", out var cursor) ||
            !TryLimit(query, out var limit))
        {
            return false;
        }

        request = new(actor, year, week, level, person, cursor, limit);
        return true;
    }

    private static bool TryRequiredInt(IQueryCollection query, string name, out int value)
    {
        value = 0;
        return query.TryGetValue(name, out var values) && TrySingle(values, out var raw) &&
            int.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out value);
    }

    private static bool TryOptionalGuid(IQueryCollection query, string name, out Guid? value)
    {
        value = null;
        if (!query.TryGetValue(name, out var values)) return true;
        if (!TrySingle(values, out var raw) || !TryGuid(raw, out var parsed)) return false;
        value = parsed;
        return true;
    }

    private static bool TryOptionalText(IQueryCollection query, string name, out string? value)
    {
        value = null;
        return !query.TryGetValue(name, out var values) ||
            TrySingle(values, out value) && !string.IsNullOrWhiteSpace(value);
    }

    private static bool TryLimit(IQueryCollection query, out int value)
    {
        value = 25;
        if (!query.TryGetValue("limit", out var values)) return true;
        return TrySingle(values, out var raw) &&
            int.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out value) &&
            value is >= 1 and <= 100;
    }

    private static bool TrySingle(StringValues values, out string? value)
    {
        value = values.Count == 1 ? values[0] : null;
        return value is not null;
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

        if (!TryGuid(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out actor))
        {
            failure = Problem(context, 403, "ACCESO_DENEGADO", "La sesión no identifica un actor autorizado");
            return false;
        }

        return true;
    }

    private static bool TryGuid(string? raw, out Guid value)
    {
        value = Guid.Empty;
        return raw is not null && Guid.TryParseExact(raw, "D", out value) &&
            value != Guid.Empty && raw == value.ToString("D");
    }

    private static IResult Problem(HttpContext context, int status, string code, string title) =>
        Results.Problem(
            statusCode: status,
            title: title,
            instance: context.Request.Path,
            extensions: new Dictionary<string, object?>
            {
                ["code"] = code,
                ["correlationId"] = context.GetCorrelationId(),
            });
}
