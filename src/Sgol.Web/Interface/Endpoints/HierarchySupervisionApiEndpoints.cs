using System.Globalization;
using System.Security.Claims;
using Microsoft.Extensions.Primitives;
using Sgol.Execution.Contracts;
using Sgol.Generation.Contracts;
using Sgol.Identity.Contracts;
using Sgol.Reporting.Contracts;
using Sgol.Web.Infrastructure.Http;

namespace Sgol.Web.Presentation.Endpoints;

public static class HierarchySupervisionApiEndpoints
{
    private static readonly HashSet<string> SupervisionParameters = new(
        ["level", "responsiblePersonId", "isoYear", "isoWeek", "executionStatus", "cursor", "limit"],
        StringComparer.Ordinal);
    private static readonly HashSet<string> PendingParameters = new(
        ["level", "responsiblePersonId", "isoYear", "isoWeek", "cursor", "limit"],
        StringComparer.Ordinal);

    public static IEndpointRouteBuilder MapHierarchySupervisionApi(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/supervision/obligations", ReadSupervisionAsync);
        endpoints.MapGet("/api/v1/validations/pending", ReadPendingAsync);
        return endpoints;
    }

    public static async Task<IResult> ReadSupervisionAsync(HttpContext context,
        IHierarchySupervisionReader reader, CancellationToken cancellationToken)
    {
        if (!TryActor(context, out var actor, out var failure)) return failure!;
        if (!TrySupervisionRequest(context.Request.Query, actor, out var request))
            return Problem(context, 400, "FILTRO_SUPERVISION_INVALIDO", "Los filtros de supervisión no son válidos");
        try
        {
            var page = await reader.ReadSupervisionAsync(request!, cancellationToken);
            NoStore(context);
            return Results.Ok(new
            {
                data = page.Items.Select(item => new
                {
                    obligation = Obligation(item.Obligation),
                    item.ResponsibleLevel,
                    item.CurrentEvidence,
                    item.Validation,
                    item.Links,
                }),
                meta = new { page.NextCursor, count = page.Items.Count, page.QueriedAt, correlationId = context.GetCorrelationId() },
            });
        }
        catch (SupervisionFilterInvalidException)
        {
            return Problem(context, 400, "FILTRO_SUPERVISION_INVALIDO", "Los filtros de supervisión no son válidos");
        }
        catch (SupervisionAccessDeniedException)
        {
            return Problem(context, 403, "ACCESO_DENEGADO", $"Se requiere {SupervisionAuthorization.View}");
        }
        catch (SupervisionQueryInconsistentException)
        {
            return Problem(context, 500, "CONSULTA_SUPERVISION_INCONSISTENTE", "No fue posible consultar la supervisión");
        }
    }

    public static async Task<IResult> ReadPendingAsync(HttpContext context,
        IHierarchySupervisionReader reader, CancellationToken cancellationToken)
    {
        if (!TryActor(context, out var actor, out var failure)) return failure!;
        if (!TryPendingRequest(context.Request.Query, actor, out var request))
            return Problem(context, 400, "FILTRO_VALIDACIONES_PENDIENTES_INVALIDO", "Los filtros de validaciones pendientes no son válidos");
        try
        {
            var page = await reader.ReadPendingValidationsAsync(request!, cancellationToken);
            NoStore(context);
            return Results.Ok(new
            {
                data = page.Items.Select(item => new
                {
                    obligation = Obligation(item.Obligation),
                    item.ResponsibleLevel,
                    item.PendingSince,
                    item.MaterializationStatus,
                    item.ValidationRequirement,
                    item.AvailableAuthority,
                    item.DecisionEtag,
                    item.Links,
                }),
                meta = new { page.NextCursor, count = page.Items.Count, page.QueriedAt, correlationId = context.GetCorrelationId() },
            });
        }
        catch (PendingValidationsFilterInvalidException)
        {
            return Problem(context, 400, "FILTRO_VALIDACIONES_PENDIENTES_INVALIDO", "Los filtros de validaciones pendientes no son válidos");
        }
        catch (SupervisionAccessDeniedException)
        {
            return Problem(context, 403, "ACCESO_DENEGADO", "No existe autoridad para consultar validaciones pendientes");
        }
        catch (PendingValidationQueryInconsistentException)
        {
            return Problem(context, 500, "CONSULTA_VALIDACION_INCONSISTENTE", "No fue posible consultar las validaciones pendientes");
        }
        catch (SupervisionQueryInconsistentException)
        {
            return Problem(context, 500, "CONSULTA_VALIDACION_INCONSISTENTE", "No fue posible consultar las validaciones pendientes");
        }
    }

    private static object Obligation(ObligationListItem value) => new
    {
        value.ObligationId,
        value.Task,
        value.Origin,
        value.Period,
        value.Dates,
        value.ExecutionStatus,
        value.Condition,
        value.CurrentAssignment,
    };

    private static bool TrySupervisionRequest(IQueryCollection query, Guid actor, out SupervisionRequest? request)
    {
        request = null;
        if (!TryCommon(query, SupervisionParameters, out var level, out var person, out var year, out var week,
            out var cursor, out var limit) || !TryOptionalText(query, "executionStatus", out var status) ||
            level is not null && !CanonicalRole.IsDefined(level) ||
            status is not null && status is not (WorkObligationStatuses.Pending or WorkObligationStatuses.Concluded)) return false;
        request = new(actor, level, person, year, week, status, cursor, limit);
        return true;
    }

    private static bool TryPendingRequest(IQueryCollection query, Guid actor, out PendingValidationsRequest? request)
    {
        request = null;
        if (!TryCommon(query, PendingParameters, out var level, out var person, out var year, out var week,
            out var cursor, out var limit)) return false;
        request = new(actor, level, person, year, week, cursor, limit);
        return true;
    }

    private static bool TryCommon(IQueryCollection query, HashSet<string> allowed, out string? level,
        out Guid? person, out int? year, out int? week, out string? cursor, out int limit)
    {
        level = cursor = null; person = null; year = week = null; limit = 25;
        var parsed = !query.Keys.Any(key => !allowed.Contains(key)) &&
            TryOptionalText(query, "level", out level) &&
            TryOptionalGuid(query, "responsiblePersonId", out person) &&
            TryOptionalInt(query, "isoYear", out year) &&
            TryOptionalInt(query, "isoWeek", out week) &&
            TryOptionalText(query, "cursor", out cursor) &&
            TryLimit(query, out limit);
        return parsed && (level is null || CanonicalRole.IsDefined(level)) && ValidIsoWeek(year, week);
    }

    private static bool ValidIsoWeek(int? year, int? week) =>
        year.HasValue == week.HasValue && (year is null || year is >= 1 and <= 9999 && week is >= 1 &&
            week <= ISOWeek.GetWeeksInYear(year.Value));

    private static bool TryOptionalGuid(IQueryCollection query, string name, out Guid? value)
    {
        value = null;
        if (!query.TryGetValue(name, out var values)) return true;
        if (!TrySingle(values, out var raw) || !TryGuid(raw, out var parsed)) return false;
        value = parsed; return true;
    }

    private static bool TryOptionalInt(IQueryCollection query, string name, out int? value)
    {
        value = null;
        if (!query.TryGetValue(name, out var values)) return true;
        if (!TrySingle(values, out var raw) || !int.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed)) return false;
        value = parsed; return true;
    }

    private static bool TryOptionalText(IQueryCollection query, string name, out string? value)
    {
        value = null;
        return !query.TryGetValue(name, out var values) || TrySingle(values, out value) && !string.IsNullOrWhiteSpace(value);
    }

    private static bool TryLimit(IQueryCollection query, out int value)
    {
        value = 25;
        if (!query.TryGetValue("limit", out var values)) return true;
        return TrySingle(values, out var raw) && int.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out value) && value is >= 1 and <= 100;
    }

    private static bool TrySingle(StringValues values, out string? value)
    {
        value = values.Count == 1 ? values[0] : null;
        return value is not null;
    }

    private static bool TryActor(HttpContext context, out Guid actor, out IResult? failure)
    {
        actor = Guid.Empty; failure = null;
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
        return raw is not null && Guid.TryParseExact(raw, "D", out value) && value != Guid.Empty && raw == value.ToString("D");
    }

    private static void NoStore(HttpContext context)
    {
        context.Response.Headers.CacheControl = "private, no-store";
        context.Response.Headers.Pragma = "no-cache";
    }

    private static IResult Problem(HttpContext context, int status, string code, string title) =>
        Results.Problem(statusCode: status, title: title, instance: context.Request.Path,
            extensions: new Dictionary<string, object?>
            { ["code"] = code, ["correlationId"] = context.GetCorrelationId() });
}
