using System.Globalization;
using System.Security.Claims;
using Microsoft.Extensions.Primitives;
using Sgol.Auditing.Contracts;
using Sgol.Identity.Contracts;
using Sgol.Organization.Contracts;
using Sgol.Web.Infrastructure.Http;

namespace Sgol.Web.Presentation.Endpoints;

public static class AuditApiEndpoints
{
    private static readonly HashSet<string> AllowedParameters = new(
        ["from", "to", "actorUserId", "resourceType", "resourceId", "action", "outcome",
         "correlationId", "branchCode", "level", "traceObligationId", "cursor", "limit"],
        StringComparer.Ordinal);

    public static IEndpointRouteBuilder MapAuditApi(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/audit-events", ReadAsync);
        endpoints.MapGet("/api/v1/audit-events/{id:guid}", FindAsync);
        return endpoints;
    }

    public static async Task<IResult> ReadAsync(
        HttpContext context,
        IAuditEventReader reader,
        CancellationToken cancellationToken)
    {
        if (!TryActor(context, out var actor, out var failure)) return failure!;
        if (context.Request.ContentLength is > 0) return FilterProblem(context);
        if (!TryRequest(context.Request.Query, actor, out var request)) return FilterProblem(context);
        try
        {
            var page = await reader.ReadAsync(request!, cancellationToken);
            NoStore(context);
            return Results.Ok(new
            {
                data = page.Items.Select(Response),
                meta = new
                {
                    page.NextCursor,
                    count = page.Items.Count,
                    queriedAt = Utc(page.QueriedAt),
                    snapshot = new
                    {
                        upperOccurredAt = page.Snapshot.UpperOccurredAt.HasValue
                            ? Utc(page.Snapshot.UpperOccurredAt.Value)
                            : null,
                        page.Snapshot.UpperEventId,
                    },
                    page.Completeness,
                    correlationId = context.GetCorrelationId(),
                },
            });
        }
        catch (AuditFilterInvalidException) { return FilterProblem(context); }
        catch (AuditCursorInvalidException)
        {
            return Problem(context, 400, "AUDIT_CURSOR_INVALID", "El cursor de auditoría no es válido");
        }
        catch (AuditAccessDeniedException)
        {
            return Problem(context, 403, "ACCESO_DENEGADO", $"Se requiere {AuditAuthorization.View}");
        }
        catch (AuditEventNotFoundException)
        {
            return Problem(context, 404, "AUDIT_EVENT_NOT_FOUND", "El hecho de auditoría no está disponible");
        }
        catch (AuditScopeInconsistentException)
        {
            return Problem(context, 500, "AUDIT_SCOPE_INCONSISTENT", "No fue posible reconstruir la trazabilidad");
        }
    }

    public static async Task<IResult> FindAsync(
        HttpContext context,
        IAuditEventReader reader,
        Guid id,
        CancellationToken cancellationToken)
    {
        if (!TryActor(context, out var actor, out var failure)) return failure!;
        if (context.Request.ContentLength is > 0 || context.Request.Query.Count != 0 || id == Guid.Empty)
            return FilterProblem(context);
        try
        {
            var detail = await reader.FindAsync(actor, id, cancellationToken);
            NoStore(context);
            return Results.Ok(new
            {
                data = Response(detail.Event),
                meta = new { queriedAt = Utc(detail.QueriedAt), correlationId = context.GetCorrelationId() },
            });
        }
        catch (AuditFilterInvalidException) { return FilterProblem(context); }
        catch (AuditAccessDeniedException)
        {
            return Problem(context, 403, "ACCESO_DENEGADO", $"Se requiere {AuditAuthorization.View}");
        }
        catch (AuditEventNotFoundException)
        {
            return Problem(context, 404, "AUDIT_EVENT_NOT_FOUND", "El hecho de auditoría no está disponible");
        }
    }

    private static bool TryRequest(IQueryCollection query, Guid actor, out AuditQueryRequest? request)
    {
        request = null;
        if (query.Keys.Any(key => !AllowedParameters.Contains(key)) ||
            !TryRequiredInstant(query, "from", out var from) || !TryRequiredInstant(query, "to", out var to) ||
            !TryOptionalGuid(query, "actorUserId", out var actorFilter) ||
            !TryOptionalToken(query, "resourceType", out var resourceType) ||
            !TryOptionalGuid(query, "resourceId", out var resourceId) ||
            !TryOptionalToken(query, "action", out var action) ||
            !TryOptionalToken(query, "outcome", out var outcome) ||
            !TryOptionalGuid(query, "correlationId", out var correlationId) ||
            !TryOptionalText(query, "branchCode", out var branchCode) ||
            !TryOptionalText(query, "level", out var level) ||
            !TryOptionalGuid(query, "traceObligationId", out var traceObligationId) ||
            !TryOptionalText(query, "cursor", out var cursor) || !TryLimit(query, out var limit) ||
            branchCode is not (null or BranchScope.LorettaCode) ||
            level is not null && !CanonicalRole.IsDefined(level) ||
            resourceId.HasValue && resourceType is null || cursor is { Length: > 2048 } ||
            traceObligationId.HasValue && (actorFilter.HasValue || resourceType is not null ||
                resourceId.HasValue || correlationId.HasValue || level is not null) ||
            to <= from || to - from > TimeSpan.FromDays(31)) return false;

        request = new(actor, from, to, actorFilter, resourceType, resourceId, action, outcome,
            correlationId, branchCode ?? BranchScope.LorettaCode, level, traceObligationId, cursor, limit);
        return true;
    }

    private static bool TryRequiredInstant(IQueryCollection query, string name, out DateTimeOffset value)
    {
        value = default;
        return query.TryGetValue(name, out var values) && TrySingle(values, out var raw) &&
            raw == raw.Trim() && DateTimeOffset.TryParseExact(raw,
                ["yyyy-MM-dd'T'HH:mm:ss'Z'", "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF'Z'"],
                CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out value);
    }

    private static bool TryOptionalGuid(IQueryCollection query, string name, out Guid? value)
    {
        value = null;
        if (!query.TryGetValue(name, out var values)) return true;
        if (!TrySingle(values, out var raw) || !TryGuid(raw, out var parsed)) return false;
        value = parsed; return true;
    }

    private static bool TryOptionalToken(IQueryCollection query, string name, out string? value)
    {
        if (!TryOptionalText(query, name, out value) || value is null) return value is null;
        return value.Length <= 128 && value.All(character =>
            character is >= 'A' and <= 'Z' or >= '0' and <= '9' or '_' or '.' or ':' or '-');
    }

    private static bool TryOptionalText(IQueryCollection query, string name, out string? value)
    {
        value = null;
        return !query.TryGetValue(name, out var values) || TrySingle(values, out value) &&
            !string.IsNullOrWhiteSpace(value) && value == value.Trim();
    }

    private static bool TryLimit(IQueryCollection query, out int value)
    {
        value = 25;
        if (!query.TryGetValue("limit", out var values)) return true;
        return TrySingle(values, out var raw) && int.TryParse(raw, NumberStyles.None,
            CultureInfo.InvariantCulture, out value) && value is >= 1 and <= 100;
    }

    private static bool TrySingle(StringValues values, out string value)
    {
        value = values.Count == 1 ? values[0] ?? string.Empty : string.Empty;
        return values.Count == 1 && value.Length > 0;
    }

    private static bool TryActor(HttpContext context, out Guid actor, out IResult? failure)
    {
        actor = Guid.Empty; failure = null;
        if (context.User.Identity?.IsAuthenticated != true)
        {
            failure = Problem(context, 401, "AUTENTICACION_REQUERIDA", "Se requiere una sesión activa con MFA");
            return false;
        }
        if (!Guid.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out actor) ||
            actor == Guid.Empty)
        {
            failure = Problem(context, 403, "ACCESO_DENEGADO", "La sesión no identifica un actor autorizado");
            return false;
        }
        return true;
    }

    private static bool TryGuid(string? raw, out Guid value)
    {
        value = Guid.Empty;
        return raw is not null && Guid.TryParseExact(raw, "D", out value) && value != Guid.Empty &&
            raw == value.ToString("D");
    }

    private static IResult FilterProblem(HttpContext context) =>
        Problem(context, 400, "AUDIT_FILTER_INVALID", "Los filtros de auditoría no son válidos");

    private static void NoStore(HttpContext context)
    {
        context.Response.Headers.CacheControl = "private, no-store";
        context.Response.Headers.Pragma = "no-cache";
    }

    private static object Response(AuditEventDetails item) => new
    {
        item.Id,
        occurredAt = Utc(item.OccurredAt),
        item.Actor,
        item.Action,
        item.Resource,
        item.Scope,
        item.Change,
        item.Reason,
        item.Outcome,
        item.CorrelationId,
    };

    private static string Utc(DateTimeOffset value) => value.ToUniversalTime()
        .ToString("yyyy-MM-dd'T'HH:mm:ss.FFFFFFF'Z'", CultureInfo.InvariantCulture);

    internal static IResult Problem(HttpContext context, int status, string code, string title)
    {
        NoStore(context);
        return Results.Problem(type: "about:blank", statusCode: status, title: title, detail: title,
            instance: context.Request.Path,
            extensions: new Dictionary<string, object?>
            { ["code"] = code, ["correlationId"] = context.GetCorrelationId() });
    }
}
