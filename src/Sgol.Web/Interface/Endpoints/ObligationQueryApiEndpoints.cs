using System.Globalization;
using System.Security.Claims;
using Microsoft.Extensions.Primitives;
using Sgol.Configuration.Contracts;
using Sgol.Execution.Contracts;
using Sgol.Generation.Contracts;
using Sgol.Web.Infrastructure.Http;

namespace Sgol.Web.Presentation.Endpoints;

public static class ObligationQueryApiEndpoints
{
    private static readonly HashSet<string> ListParameters = new(
        ["periodId", "taskCode", "executionStatus", "condition", "responsiblePersonId", "cursor", "limit"],
        StringComparer.Ordinal);
    private static readonly HashSet<string> DetailParameters = new(
        ["historyCursor", "historyLimit"],
        StringComparer.Ordinal);

    public static IEndpointRouteBuilder MapObligationQueryApi(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/obligations", HandleListAsync);
        endpoints.MapGet("/api/v1/obligations/{id}", HandleDetailAsync);
        return endpoints;
    }

    public static async Task<IResult> HandleListAsync(
        HttpContext context,
        IObligationQueryReader reader,
        CancellationToken cancellationToken)
    {
        if (!TryActor(context, out var actorUserId, out var authenticationFailure))
        {
            return authenticationFailure!;
        }

        if (!TryParseList(context.Request.Query, actorUserId, out var request))
        {
            return Problem(context, 400, "FILTRO_OBLIGACIONES_INVALIDO", "Los filtros de obligaciones no son válidos");
        }

        try
        {
            var page = await reader.ListAsync(request!, cancellationToken);
            return Results.Ok(new
            {
                data = page.Items,
                meta = new
                {
                    nextCursor = page.NextCursor,
                    count = page.Items.Count,
                    queriedAt = page.QueriedAt,
                    correlationId = context.GetCorrelationId(),
                },
            });
        }
        catch (ObligationQueryFilterInvalidException)
        {
            return Problem(context, 400, "FILTRO_OBLIGACIONES_INVALIDO", "Los filtros de obligaciones no son válidos");
        }
        catch (ObligationQueryAccessDeniedException)
        {
            return Problem(context, 403, "ACCESO_DENEGADO", $"Se requiere {ObligationQueryAuthorization.View}");
        }
        catch (ObligationQueryInconsistentException)
        {
            return Problem(context, 500, "CONSULTA_OBLIGACION_INCONSISTENTE", "No fue posible consultar las obligaciones");
        }
    }

    public static async Task<IResult> HandleDetailAsync(
        HttpContext context,
        string id,
        IObligationQueryReader reader,
        CancellationToken cancellationToken)
    {
        if (!TryActor(context, out var actorUserId, out var authenticationFailure))
        {
            return authenticationFailure!;
        }

        if (!TryCanonicalGuid(id, out var obligationId))
        {
            return Problem(context, 400, "OBLIGACION_ID_INVALIDO", "El identificador de obligación no es válido");
        }

        if (!TryParseDetail(context.Request.Query, actorUserId, obligationId, out var request))
        {
            return Problem(context, 400, "FILTRO_HISTORIA_INVALIDO", "Los filtros de historia no son válidos");
        }

        try
        {
            var page = await reader.GetAsync(request!, cancellationToken);
            return Results.Ok(new
            {
                data = page.Detail,
                meta = new
                {
                    historyNextCursor = page.HistoryNextCursor,
                    historyCount = page.Detail.History.Count,
                    queriedAt = page.QueriedAt,
                    correlationId = context.GetCorrelationId(),
                },
            });
        }
        catch (ObligationHistoryFilterInvalidException)
        {
            return Problem(context, 400, "FILTRO_HISTORIA_INVALIDO", "Los filtros de historia no son válidos");
        }
        catch (ObligationQueryAccessDeniedException)
        {
            return Problem(context, 403, "ACCESO_DENEGADO", $"Se requiere {ObligationQueryAuthorization.View}");
        }
        catch (ObligationQueryNotFoundException)
        {
            return Problem(context, 404, "OBLIGACION_NO_ENCONTRADA", "La obligación no existe");
        }
        catch (ObligationQueryInconsistentException)
        {
            return Problem(context, 500, "CONSULTA_OBLIGACION_INCONSISTENTE", "No fue posible consultar la obligación");
        }
    }

    private static bool TryActor(
        HttpContext context,
        out Guid actorUserId,
        out IResult? failure)
    {
        actorUserId = Guid.Empty;
        failure = null;
        if (context.User.Identity?.IsAuthenticated != true)
        {
            failure = Problem(context, 401, "AUTENTICACION_REQUERIDA", "Se requiere una sesión activa");
            return false;
        }

        if (!TryCanonicalGuid(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out actorUserId))
        {
            failure = Problem(context, 403, "ACCESO_DENEGADO", "La sesión no identifica un actor autorizado");
            return false;
        }

        return true;
    }

    private static bool TryParseList(
        IQueryCollection query,
        Guid actorUserId,
        out ObligationListRequest? request)
    {
        request = null;
        if (query.Keys.Any(key => !ListParameters.Contains(key)) ||
            !TryOptionalGuid(query, "periodId", out var periodId) ||
            !TryOptionalGuid(query, "responsiblePersonId", out var responsiblePersonId) ||
            !TryOptionalText(query, "taskCode", out var taskCode) ||
            !TryOptionalText(query, "executionStatus", out var executionStatus) ||
            !TryOptionalText(query, "condition", out var condition) ||
            !TryOptionalText(query, "cursor", out var cursor) ||
            !TryLimit(query, "limit", 25, out var limit) ||
            (taskCode is not null && !TaskDefinitionCatalog.All.Any(item => item.TaskCode == taskCode)) ||
            (executionStatus is not null &&
             executionStatus is not (WorkObligationStatuses.Pending or WorkObligationStatuses.Concluded)) ||
            (condition is not null &&
             condition is not (ObligationConditions.Overdue or ObligationConditions.NotOverdue)))
        {
            return false;
        }

        request = new ObligationListRequest(
            actorUserId,
            periodId,
            taskCode,
            executionStatus,
            condition,
            responsiblePersonId,
            cursor,
            limit);
        return true;
    }

    private static bool TryParseDetail(
        IQueryCollection query,
        Guid actorUserId,
        Guid obligationId,
        out ObligationDetailRequest? request)
    {
        request = null;
        if (query.Keys.Any(key => !DetailParameters.Contains(key)) ||
            !TryOptionalText(query, "historyCursor", out var historyCursor) ||
            !TryLimit(query, "historyLimit", 25, out var historyLimit))
        {
            return false;
        }

        request = new ObligationDetailRequest(actorUserId, obligationId, historyCursor, historyLimit);
        return true;
    }

    private static bool TryOptionalGuid(IQueryCollection query, string name, out Guid? value)
    {
        value = null;
        if (!query.TryGetValue(name, out var values))
        {
            return true;
        }

        if (!TrySingle(values, out var raw) || !TryCanonicalGuid(raw, out var parsed))
        {
            return false;
        }

        value = parsed;
        return true;
    }

    private static bool TryOptionalText(IQueryCollection query, string name, out string? value)
    {
        value = null;
        if (!query.TryGetValue(name, out var values))
        {
            return true;
        }

        return TrySingle(values, out value) && !string.IsNullOrWhiteSpace(value);
    }

    private static bool TryLimit(
        IQueryCollection query,
        string name,
        int defaultValue,
        out int value)
    {
        value = defaultValue;
        if (!query.TryGetValue(name, out var values))
        {
            return true;
        }

        return TrySingle(values, out var raw) &&
            int.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out value) &&
            value is >= 1 and <= 100;
    }

    private static bool TrySingle(StringValues values, out string? value)
    {
        value = values.Count == 1 ? values[0] : null;
        return value is not null;
    }

    private static bool TryCanonicalGuid(string? raw, out Guid value)
    {
        value = Guid.Empty;
        return raw is not null &&
            Guid.TryParseExact(raw, "D", out value) &&
            value != Guid.Empty &&
            string.Equals(raw, value.ToString("D"), StringComparison.Ordinal);
    }

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
