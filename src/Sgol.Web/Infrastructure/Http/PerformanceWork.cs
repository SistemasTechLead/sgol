using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Threading.RateLimiting;
using Sgol.Planning.Contracts;

namespace Sgol.Web.Infrastructure.Http;

internal static class PerformanceWork
{
    internal static readonly Meter Meter = new("Sgol.Performance", "1.0");
    internal static readonly Histogram<double> Duration = Meter.CreateHistogram<double>("sgol.work.duration", "ms");
    internal static readonly UpDownCounter<long> Active = Meter.CreateUpDownCounter<long>("sgol.work.active");
    internal static readonly Counter<long> Rejected = Meter.CreateCounter<long>("sgol.work.rejected");
    internal static readonly Counter<long> ParsedNodes = Meter.CreateCounter<long>("sgol.excel.parsed_nodes");
    internal static readonly WorkGate Planning = new("planning", 2, 4);
    internal static readonly WorkGate Excel = new("excel", 2, 4);
    internal static readonly WorkGate Photographs = new("photograph", 4, 8);
    internal static IDisposable Measure(string stage) => new Timing(stage);
    private sealed class Timing(string stage) : IDisposable
    {
        private readonly long start = Stopwatch.GetTimestamp();
        public void Dispose() => Duration.Record(Stopwatch.GetElapsedTime(start).TotalMilliseconds, new KeyValuePair<string, object?>("stage", stage));
    }
}

internal sealed class WorkGate(string name, int permits, int queue) : IDisposable
{
    private readonly ConcurrencyLimiter limiter = new(new ConcurrencyLimiterOptions {
        PermitLimit = permits, QueueLimit = queue, QueueProcessingOrder = QueueProcessingOrder.OldestFirst });
    internal async ValueTask<IDisposable> EnterAsync(CancellationToken token)
    {
        using var timing = PerformanceWork.Measure(name + ".queue");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        RateLimitLease lease;
        try { lease = await limiter.AcquireAsync(1, timeout.Token); }
        catch (OperationCanceledException) when (!token.IsCancellationRequested) { throw Busy(); }
        if (lease.IsAcquired) return lease;
        lease.Dispose(); throw Busy();
    }
    private DirectionPlanException Busy()
    {
        PerformanceWork.Rejected.Add(1, new KeyValuePair<string, object?>("operation", name));
        return new("OPERATION_BUSY", 503);
    }
    public void Dispose() => limiter.Dispose();
}

// Runs before Razor form binding. Service gates also protect non-HTTP callers.
public sealed class PerformanceAdmissionMiddleware
{
    private readonly RequestDelegate next;
    private readonly WorkGate uploads;
    private readonly WorkGate reports;
    private readonly WorkGate login;
    public PerformanceAdmissionMiddleware(RequestDelegate next, IConfiguration configuration)
    {
        this.next = next;
        WorkGate Gate(string key, int permits, int queue) => new(key,
            Math.Clamp(configuration.GetValue<int?>("Performance:" + key + ":Concurrency") ?? permits, 1, 32),
            Math.Clamp(configuration.GetValue<int?>("Performance:" + key + ":Queue") ?? queue, 0, 64));
        uploads = Gate("uploads", 4, 8); reports = Gate("reports", 2, 4); login = Gate("login", 4, 8);
    }
    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path;
        var gate = context.Request.Method == "POST" && context.Request.ContentType?.StartsWith("multipart/form-data", StringComparison.OrdinalIgnoreCase) == true ? uploads :
            path.StartsWithSegments("/reportes") ? reports :
            context.Request.Method == "POST" && path == "/api/v1/auth/login" ? login : null;
        if (gate is null) { await next(context); return; }
        IDisposable lease;
        try { lease = await gate.EnterAsync(context.RequestAborted); }
        catch (DirectionPlanException)
        {
            context.Response.StatusCode = 503;
            context.Response.Headers.RetryAfter = "5";
            context.Response.Headers.CacheControl = "no-store";
            await context.Response.WriteAsJsonAsync(new { status = 503, code = "OPERATION_BUSY",
                title = "Esta operación está ocupada. Espera unos segundos y vuelve a enviar la solicitud.",
                correlationId = context.GetCorrelationId() }, options: (System.Text.Json.JsonSerializerOptions?)null, contentType: "application/problem+json", cancellationToken: context.RequestAborted);
            return;
        }
        PerformanceWork.Active.Add(1, new KeyValuePair<string, object?>("operation", gate == uploads ? "upload" : gate == reports ? "report" : "login"));
        try
        {
        using (lease)
        using (PerformanceWork.Measure("http." + (gate == uploads ? "upload" : gate == reports ? "report" : "login")))
            await next(context);
        }
        finally { PerformanceWork.Active.Add(-1, new KeyValuePair<string, object?>("operation", gate == uploads ? "upload" : gate == reports ? "report" : "login")); }
    }
}
