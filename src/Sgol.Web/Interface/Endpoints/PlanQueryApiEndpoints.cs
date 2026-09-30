using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Sgol.BuildingBlocks.Versioning;
using Sgol.Planning.Contracts;
using Sgol.Web.Infrastructure.Http;

namespace Sgol.Web.Presentation.Endpoints;

public static class PlanQueryApiEndpoints
{
    private static readonly HashSet<string> VersionParameters = new(["publicationId", "cursor", "limit"], StringComparer.Ordinal);
    public static IEndpointRouteBuilder MapPlanQueryApi(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/plans/{isoYear}/{isoWeek}", HandlePlanAsync);
        endpoints.MapGet("/api/v1/plans/{planId}/versions", HandleVersionsAsync);
        return endpoints;
    }

    public static async Task<IResult> HandlePlanAsync(string isoYear, string isoWeek, HttpContext context,
        IPlanQueryReader reader, CancellationToken cancellationToken)
    {
        if (!TryActor(context, out var actor, out var failure)) return failure!;
        if (context.Request.Query.Count != 0 || context.Request.ContentLength > 0 ||
            !int.TryParse(isoYear, NumberStyles.None, CultureInfo.InvariantCulture, out var year) ||
            !int.TryParse(isoWeek, NumberStyles.None, CultureInfo.InvariantCulture, out var week))
            return Problem(context, 400, "SOLICITUD_PLAN_INVALIDA");
        try
        {
            var result = await reader.ReadPlanAsync(actor, year, week, cancellationToken);
            ReadHeaders(context, result.Plan.RowVersion);
            return Results.Ok(new { data = result.Plan, meta = new { queriedAt = result.QueriedAt, correlationId = context.GetCorrelationId() } });
        }
        catch (WorkPlanException exception) { return Problem(context, exception.ResponseCode, exception.ErrorCode); }
        catch (PlanPublicationException exception) { return Problem(context, exception.ResponseCode, exception.ErrorCode); }
    }

    public static async Task<IResult> HandleVersionsAsync(string planId, HttpContext context,
        IPlanQueryReader reader, IDataProtectionProvider protection, CancellationToken cancellationToken)
    {
        if (!TryActor(context, out var actor, out var failure)) return failure!;
        var query = context.Request.Query;
        if (!CanonicalId(planId, out var id) || context.Request.ContentLength > 0 ||
            query.Any(p => !VersionParameters.Contains(p.Key) || p.Value.Count != 1) ||
            query.TryGetValue("publicationId", out var publicationText) && !CanonicalId(publicationText, out _) ||
            query.TryGetValue("limit", out var limitText) && (!int.TryParse(limitText, NumberStyles.None,
                CultureInfo.InvariantCulture, out var parsedLimit) || parsedLimit is < 1 or > 100))
            return Problem(context, 400, "CONSULTA_PLAN_INVALIDA");
        var publicationId = query.ContainsKey("publicationId") ? Guid.Parse(query["publicationId"]!) : (Guid?)null;
        var limit = query.ContainsKey("limit") ? int.Parse(query["limit"]!, CultureInfo.InvariantCulture) : 50;
        var protector = protection.CreateProtector("SGOL.FRONT-015.plan-read-cursor.v1");
        PlanReadCursor? cursor = null;
        if (query.TryGetValue("cursor", out var cursorText))
        {
            if (string.IsNullOrEmpty(cursorText) || cursorText.ToString().Length > 4096) return Problem(context, 400, "CURSOR_INVALIDO");
            try { cursor = JsonSerializer.Deserialize<PlanReadCursor>(protector.Unprotect(cursorText.ToString())); }
            catch (Exception exception) when (exception is CryptographicException or JsonException or FormatException)
            { return Problem(context, 400, "CURSOR_INVALIDO"); }
            if (cursor is null) return Problem(context, 400, "CURSOR_INVALIDO");
        }
        try
        {
            var result = await reader.ReadVersionsAsync(new(actor, id, publicationId, limit, cursor), cancellationToken);
            ReadHeaders(context, result.RowVersion);
            var nextCursor = result.NextCursor is null ? null : protector.Protect(JsonSerializer.Serialize(result.NextCursor));
            object data = result.Snapshot is { } snapshot ? snapshot : result.Versions;
            return Results.Ok(new { data, meta = new { nextCursor,
                count = result.Snapshot?.Obligations.Count ?? result.Versions.Count,
                queriedAt = result.QueriedAt, correlationId = context.GetCorrelationId() } });
        }
        catch (WorkPlanException exception) { return Problem(context, exception.ResponseCode, exception.ErrorCode); }
        catch (PlanPublicationException exception) { return Problem(context, exception.ResponseCode, exception.ErrorCode); }
    }

    private static void ReadHeaders(HttpContext context, long version)
    { context.Response.Headers.ETag = VersionEtag.Format(version); context.Response.Headers.CacheControl = "no-store"; }
    private static bool CanonicalId(string? value, out Guid id) => Guid.TryParseExact(value, "D", out id) && id != Guid.Empty;
    private static bool TryActor(HttpContext context, out Guid actor, out IResult? failure)
    {
        actor = Guid.Empty; failure = null;
        if (context.User.Identity?.IsAuthenticated != true) { failure = Problem(context, 401, "AUTENTICACION_REQUERIDA"); return false; }
        if (!CanonicalId(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out actor))
        { failure = Problem(context, 403, "ACCESO_DENEGADO"); return false; }
        return true;
    }
    private static IResult Problem(HttpContext context, int status, string code) => Results.Problem(statusCode: status,
        title: "No se pudo consultar el plan", extensions: new Dictionary<string, object?>
        { ["code"] = code, ["correlationId"] = context.GetCorrelationId() });
}
