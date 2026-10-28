using System.Net;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Sgol.Web.Infrastructure.Http;

// One check per instance, shared across clients. A disconnected monitor cannot cancel it.
public sealed class ReadinessProbe(HealthCheckService checks, TimeProvider time)
{
    private readonly object gate = new();
    private Task<bool>? pending;
    private Task<HealthReport>? runningChecks;
    private DateTimeOffset expires;

    public Task<bool> CheckAsync()
    {
        lock (gate)
        {
            if (pending is null || pending.IsCompleted && time.GetUtcNow() >= expires)
            {
                expires = time.GetUtcNow().AddSeconds(5);
                // A misbehaving check that ignores cancellation cannot accumulate
                // another execution at every cache expiration.
                pending = runningChecks is { IsCompleted: false } ? Task.FromResult(false) : ExecuteAsync();
            }
            return pending;
        }
    }

    private async Task<bool> ExecuteAsync()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        try
        {
            runningChecks = checks.CheckHealthAsync(r => r.Tags.Contains("ready"), timeout.Token);
            var report = await runningChecks.WaitAsync(timeout.Token);
            return report.Status == HealthStatus.Healthy;
        }
        catch (Exception) { return false; }
    }

    public static bool IsLocalMonitor(HttpContext context) =>
        context.Connection.RemoteIpAddress is { } address && IPAddress.IsLoopback(address) &&
        !context.Request.Headers.ContainsKey("X-Forwarded-For") &&
        !context.Request.Headers.ContainsKey("X-Forwarded-Host") &&
        !context.Request.Headers.ContainsKey("Forwarded") &&
        !context.Request.Headers.ContainsKey("X-Original-For");
}
