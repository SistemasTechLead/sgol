using System.Globalization;
using System.Threading.RateLimiting;

namespace Sgol.Web.Infrastructure.Http;

// Fixed cardinality prevents attacker-controlled addresses from growing a partition dictionary.
public sealed class RequestContainmentMiddleware(RequestDelegate next) : IDisposable
{
    private readonly ConcurrencyLimiter capacity = new(new() { PermitLimit = 64, QueueLimit = 0 });
    private readonly FixedWindowRateLimiter[] origins = Enumerable.Range(0, 256).Select(_ =>
        new FixedWindowRateLimiter(new() { PermitLimit = 600, Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0, AutoReplenishment = true })).ToArray();

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path.Equals("/health/live", StringComparison.OrdinalIgnoreCase))
        { await next(context); return; }
        var key = context.Connection.RemoteIpAddress?.GetHashCode() ?? 0;
        using var origin = origins[(uint)key % origins.Length].AttemptAcquire();
        if (!origin.IsAcquired)
        { await RejectAsync(context, 429, "REQUEST_RATE_LIMIT", "Demasiadas solicitudes; espera antes de reintentar.", origin); return; }
        using var lease = capacity.AttemptAcquire();
        if (!lease.IsAcquired)
        { await RejectAsync(context, 503, "REQUEST_CAPACITY", "Capacidad temporal agotada; vuelve a intentar más tarde.", lease); return; }
        await next(context);
    }

    private static Task RejectAsync(HttpContext context, int status, string code, string title, RateLimitLease lease)
    {
        context.Response.StatusCode = status;
        context.Response.Headers.CacheControl = "no-store";
        context.Response.Headers.RetryAfter = lease.TryGetMetadata(MetadataName.RetryAfter, out var retry)
            ? Math.Max(1, (int)Math.Ceiling(retry.TotalSeconds)).ToString(CultureInfo.InvariantCulture) : "1";
        return context.Response.WriteAsJsonAsync(new { status, title, code, correlationId = context.GetCorrelationId() },
            options: (System.Text.Json.JsonSerializerOptions?)null, contentType: "application/problem+json");
    }

    public void Dispose() { capacity.Dispose(); foreach (var origin in origins) origin.Dispose(); }
}

public sealed class IdentityRequestContainmentMiddleware(RequestDelegate next) : IDisposable
{
    private readonly FixedWindowRateLimiter[] users = Enumerable.Range(0, 256).Select(_ =>
        new FixedWindowRateLimiter(new() { PermitLimit = 300, Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0, AutoReplenishment = true })).ToArray();

    public async Task InvokeAsync(HttpContext context)
    {
        var id = context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (id is null) { await next(context); return; }
        using var lease = users[(uint)StringComparer.Ordinal.GetHashCode(id) % users.Length].AttemptAcquire();
        if (lease.IsAcquired) { await next(context); return; }
        context.Response.StatusCode = 429;
        context.Response.Headers.CacheControl = "no-store";
        context.Response.Headers.RetryAfter = lease.TryGetMetadata(MetadataName.RetryAfter, out var retry)
            ? Math.Max(1, (int)Math.Ceiling(retry.TotalSeconds)).ToString(CultureInfo.InvariantCulture) : "60";
        await context.Response.WriteAsJsonAsync(new { status = 429, code = "USER_RATE_LIMIT",
            title = "Demasiadas solicitudes de esta sesión; espera antes de reintentar.", correlationId = context.GetCorrelationId() },
            options: (System.Text.Json.JsonSerializerOptions?)null, contentType: "application/problem+json");
    }
    public void Dispose() { foreach (var limiter in users) limiter.Dispose(); }
}
