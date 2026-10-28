using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Sgol.Configuration.Contracts;
using Sgol.Planning.Contracts;
using Sgol.Web.Infrastructure.Http;

namespace Sgol.Web.Presentation.Endpoints;

public static class DirectionPreparedPlanApiEndpoints
{
    public static IEndpointRouteBuilder MapDirectionPreparedPlanApi(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/direction-plans");
        group.MapGet("", ListAsync);
        group.MapGet("/task-options", OptionsAsync);
        group.MapGet("/predefined-tasks", PredefinedAsync);
        group.MapGet("/{planId}", ReadAsync);
        group.MapPost("", CreateAsync);
        group.MapGet("/{planId}/deletion-preview", PreviewDeletionAsync);
        group.MapDelete("/{planId}", DeleteAsync);
        return endpoints;
    }

    public static async Task<IResult> ListAsync(HttpContext context, IDirectionPreparedPlanService service,
        IDataProtectionProvider protection, CancellationToken token)
    {
        if (!Actor(context, out var actor, out var denied)) return denied!;
        if (!Query(context, ["limit", "cursor"])) return Problem(context, 400, "DIRECTION_PLAN_REQUEST_INVALID");
        var limit = 20;
        if (context.Request.Query.TryGetValue("limit", out var limitText) && (!int.TryParse(limitText, NumberStyles.None, CultureInfo.InvariantCulture, out limit) || limit is < 1 or > 100))
            return Problem(context, 400, "DIRECTION_PLAN_REQUEST_INVALID");
        var protector = protection.CreateProtector("SGOL.direction-plans.cursor.v1");
        DirectionPlanCursor? cursor = null;
        if (context.Request.Query.TryGetValue("cursor", out var cursorText))
        {
            try
            {
                if (cursorText.ToString().Length is 0 or > 4096) throw new FormatException();
                cursor = JsonSerializer.Deserialize<DirectionPlanCursor>(protector.Unprotect(cursorText.ToString())) ?? throw new FormatException();
            }
            catch (Exception e) when (e is CryptographicException or JsonException or FormatException)
            { return Problem(context, 400, "CURSOR_INVALIDO"); }
        }
        try
        {
            var page = await service.ListAsync(actor, limit, cursor, token);
            return Ok(context, page.Plans, page.NextCursor is null ? null : protector.Protect(JsonSerializer.Serialize(page.NextCursor)));
        }
        catch (DirectionPlanException e) { return Problem(context, e.Status, e.Code); }
    }

    public static async Task<IResult> ReadAsync(string planId, HttpContext context, IDirectionPreparedPlanService service, CancellationToken token)
    {
        if (!Actor(context, out var actor, out var denied)) return denied!;
        if (!Query(context, []) || !Guid.TryParseExact(planId, "D", out var id) || id == Guid.Empty) return Problem(context, 400, "DIRECTION_PLAN_REQUEST_INVALID");
        try { return Ok(context, await service.ReadAsync(actor, id, token)); }
        catch (DirectionPlanException e) { return Problem(context, e.Status, e.Code); }
    }

    public static async Task<IResult> OptionsAsync(HttpContext context, IDirectionPreparedPlanService service,
        IDirectionPlanningCatalogReader catalog, CancellationToken token)
    {
        if (!Actor(context, out var actor, out var denied)) return denied!;
        if (!Query(context, [])) return Problem(context, 400, "DIRECTION_PLAN_REQUEST_INVALID");
        try { await service.AuthorizeWeeklyOperationsAsync(actor, token); return Ok(context, (await catalog.ReadTaskOptionsAsync(token))); }
        catch (DirectionPlanException e) { return Problem(context, e.Status, e.Code); }
    }

    public static async Task<IResult> PredefinedAsync(HttpContext context, IDirectionPreparedPlanService service,
        IDirectionPlanningCatalogReader catalog, CancellationToken token)
    {
        if (!Actor(context, out var actor, out var denied)) return denied!;
        var period = context.Request.Query["periodType"].ToString();
        if (!Query(context, ["periodType"]) || !DirectionPlanContract.PeriodTypes.Contains(period)) return Problem(context, 400, "DIRECTION_PLAN_REQUEST_INVALID");
        try { await service.AuthorizeAsync(actor, token); return Ok(context, await catalog.ReadPredefinedTasksAsync(period, token)); }
        catch (DirectionPlanException e) { return Problem(context, e.Status, e.Code); }
    }

    public static async Task<IResult> CreateAsync(HttpContext context, IDirectionPreparedPlanService service, CancellationToken token)
    {
        if (!Actor(context, out var actor, out var denied)) return denied!;
        if (context.Request.Query.Count != 0 || !context.Request.HasJsonContentType()) return Problem(context, 400, "DIRECTION_PLAN_REQUEST_INVALID");
        var parsedKey = IdempotencyKeyHeader.Parse(context.Request);
        if (!parsedKey.IsValid) return Problem(context, 400, parsedKey.ErrorCode!);
        DirectionPlanInput input;
        try
        {
            using var body = await JsonDocument.ParseAsync(context.Request.Body, cancellationToken: token);
            input = Parse(body.RootElement);
        }
        catch (Exception e) when (e is JsonException or FormatException or InvalidOperationException)
        { return Problem(context, 400, "DIRECTION_PLAN_REQUEST_INVALID"); }
        try
        {
            var saved = await service.CreateAsync(actor, parsedKey.Key, Guid.TryParse(context.GetCorrelationId(), out var correlation) ? correlation : Guid.CreateVersion7(), input, token);
            return Ok(context, saved, status: saved.Result == "CREATED" ? 201 : 200);
        }
        catch (DirectionPlanException e) { return Problem(context, e.Status, e.Code); }
    }

    public static async Task<IResult> PreviewDeletionAsync(string planId, HttpContext context, IDirectionPreparedPlanService service, CancellationToken token)
    {
        if (!Actor(context, out var actor, out var denied)) return denied!;
        if (!Query(context, []) || !Guid.TryParseExact(planId, "D", out var id) || id == Guid.Empty) return Problem(context, 400, "DIRECTION_PLAN_REQUEST_INVALID");
        try { return Ok(context, await service.PreviewDeletionAsync(actor, id, token)); }
        catch (DirectionPlanException e) { return Problem(context, e.Status, e.Code); }
    }

    public static async Task<IResult> DeleteAsync(string planId, HttpContext context, IDirectionPreparedPlanService service, CancellationToken token)
    {
        if (!Actor(context, out var actor, out var denied)) return denied!;
        if (context.Request.Query.Count != 0 || !context.Request.HasJsonContentType() || !Guid.TryParseExact(planId, "D", out var id) || id == Guid.Empty)
            return Problem(context, 400, "DIRECTION_PLAN_REQUEST_INVALID");
        var key = IdempotencyKeyHeader.Parse(context.Request);
        if (!key.IsValid) return Problem(context, 400, key.ErrorCode!);
        string fingerprint;
        try
        {
            using var body = await JsonDocument.ParseAsync(context.Request.Body, cancellationToken: token);
            var root = body.RootElement;
            if (root.ValueKind != JsonValueKind.Object || root.EnumerateObject().Count() != 1 ||
                !root.TryGetProperty("fingerprint", out var value) || value.ValueKind != JsonValueKind.String || value.GetString()?.Length != 64)
                return Problem(context, 400, "DIRECTION_PLAN_REQUEST_INVALID");
            fingerprint = value.GetString()!;
        }
        catch (JsonException) { return Problem(context, 400, "DIRECTION_PLAN_REQUEST_INVALID"); }
        try { return Ok(context, await service.DeleteAsync(actor, id, key.Key,
            Guid.TryParse(context.GetCorrelationId(), out var correlation) ? correlation : Guid.CreateVersion7(), fingerprint, token)); }
        catch (DirectionPlanException e) { return Problem(context, e.Status, e.Code); }
    }

    public static DirectionPlanInput Parse(JsonElement body)
    {
        string[] fields = ["name", "area", "periodType", "year", "periodNumber", "taskCodes"];
        string[] optional = ["capacityAdjustments", "capacityFingerprint", "isWeeklyOperationsPlan", "schedulingNotBefore", "schedulingSearchMultiplier", "rollingMonthStartsOn"];
        if (body.ValueKind != JsonValueKind.Object) throw new FormatException();
        var props = body.EnumerateObject().ToArray();
        if (props.Select(p => p.Name).Distinct(StringComparer.Ordinal).Count() != props.Length ||
            fields.Any(f => !body.TryGetProperty(f, out _)) || props.Any(p => !fields.Contains(p.Name) && !optional.Contains(p.Name))) throw new FormatException();
        string Text(string field) => body.GetProperty(field).ValueKind == JsonValueKind.String ? body.GetProperty(field).GetString()! : throw new FormatException();
        if (body.GetProperty("year").ValueKind != JsonValueKind.Number || !body.GetProperty("year").TryGetInt32(out var year)) throw new FormatException();
        var numberJson = body.GetProperty("periodNumber");
        int? number = numberJson.ValueKind == JsonValueKind.Null ? null : numberJson.ValueKind == JsonValueKind.Number && numberJson.TryGetInt32(out var n) ? n : throw new FormatException();
        var tasks = body.GetProperty("taskCodes");
        if (tasks.ValueKind != JsonValueKind.Array || tasks.GetArrayLength() > 1000 || tasks.EnumerateArray().Any(t => t.ValueKind != JsonValueKind.String)) throw new FormatException();
        PlanCapacityAdjustment[]? adjustments = null;
        if (body.TryGetProperty("capacityAdjustments", out var adjustmentJson) && adjustmentJson.ValueKind != JsonValueKind.Null)
        {
            if (adjustmentJson.ValueKind != JsonValueKind.Array || adjustmentJson.GetArrayLength() > 1000) throw new FormatException();
            adjustments = adjustmentJson.EnumerateArray().Select(a => {
                if (a.ValueKind != JsonValueKind.Object || a.EnumerateObject().Count() != 2 ||
                    !a.TryGetProperty("personId", out var id) || id.ValueKind != JsonValueKind.String || !id.TryGetGuid(out var person) ||
                    !a.TryGetProperty("percent", out var percent) || percent.ValueKind != JsonValueKind.Number || !percent.TryGetInt32(out var value)) throw new FormatException();
                return new PlanCapacityAdjustment(person, value);
            }).ToArray();
        }
        var fingerprint = body.TryGetProperty("capacityFingerprint", out var fingerprintJson) && fingerprintJson.ValueKind != JsonValueKind.Null
            ? Text("capacityFingerprint") : null;
        var master = body.TryGetProperty("isWeeklyOperationsPlan", out var masterJson) ? masterJson.GetBoolean() : false;
        DateTimeOffset? notBefore = null;
        if (body.TryGetProperty("schedulingNotBefore", out var notBeforeJson) && notBeforeJson.ValueKind != JsonValueKind.Null)
        {
            if (notBeforeJson.ValueKind != JsonValueKind.String || !notBeforeJson.TryGetDateTimeOffset(out var parsedNotBefore)) throw new FormatException();
            notBefore = parsedNotBefore;
        }
        var multiplier = 1;
        if (body.TryGetProperty("schedulingSearchMultiplier", out var multiplierJson) &&
            (multiplierJson.ValueKind != JsonValueKind.Number || !multiplierJson.TryGetInt32(out multiplier) || multiplier is < 1 or > 4)) throw new FormatException();
        DateOnly? rollingStart = null;
        if (body.TryGetProperty("rollingMonthStartsOn", out var rollingJson) && rollingJson.ValueKind != JsonValueKind.Null)
        {
            if (rollingJson.ValueKind != JsonValueKind.String || !DateOnly.TryParseExact(rollingJson.GetString(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedStart)) throw new FormatException();
            rollingStart = parsedStart;
        }
        return new(Text("name"), Text("area"), Text("periodType"), year, number, tasks.EnumerateArray().Select(t => t.GetString()!).ToArray(), adjustments, fingerprint, master, notBefore, multiplier, rollingStart);
    }

    private static bool Query(HttpContext c, string[] allowed) => c.Request.ContentLength is null or 0 && c.Request.Query.All(q => allowed.Contains(q.Key) && q.Value.Count == 1);
    private static bool Actor(HttpContext c, out Guid actor, out IResult? denied)
    {
        actor = Guid.Empty; denied = null;
        if (c.User.Identity?.IsAuthenticated != true) { denied = Problem(c, 401, "AUTENTICACION_REQUERIDA"); return false; }
        if (!Guid.TryParse(c.User.FindFirstValue(ClaimTypes.NameIdentifier), out actor) || actor == Guid.Empty) { denied = Problem(c, 403, "ACCESO_DENEGADO"); return false; }
        return true;
    }
    private static IResult Ok(HttpContext c, object data, string? next = null, int status = 200)
    {
        c.Response.Headers.CacheControl = "no-store";
        int? count = data switch { IReadOnlyCollection<DirectionPlanDetails> plans => plans.Count, IReadOnlyCollection<DirectionPlanningTaskOption> tasks => tasks.Count, _ => null };
        return Results.Json(new { data, meta = new { count, nextCursor = next, correlationId = c.GetCorrelationId() } }, statusCode: status);
    }
    private static IResult Problem(HttpContext c, int status, string code)
    {
        c.Response.Headers.CacheControl = "no-store";
        return Results.Problem(statusCode: status, title: "No se pudo completar la operación del plan", extensions: new Dictionary<string, object?>
        { ["code"] = code, ["correlationId"] = c.GetCorrelationId() });
    }
}
