using Microsoft.Extensions.Diagnostics.HealthChecks;
using Sgol.Web.Infrastructure.Http;

internal static class ReadinessChecks
{
    public static async Task RunAsync(Action<bool, string> check)
    {
        var probeService = new CountingHealth();
        var services = new ServiceCollection(); services.AddLogging();
        services.AddHealthChecks().AddCheck("controlled", probeService, tags: ["ready"]);
        await using var provider = services.BuildServiceProvider();
        var probe = new ReadinessProbe(provider.GetRequiredService<HealthCheckService>(), TimeProvider.System);
        var calls = Enumerable.Range(0, 32).Select(_ => probe.CheckAsync()).ToArray();
        using var aborted = new CancellationTokenSource(); aborted.Cancel();
        try { await probe.CheckAsync().WaitAsync(aborted.Token); } catch (OperationCanceledException) { }
        check((await Task.WhenAll(calls)).All(x => x) && probeService.Calls == 1, "H02 32 monitor calls share exactly one execution; aborted waiter isolated");
        probeService.Healthy = false;
        await Task.Delay(5100);
        check(!await probe.CheckAsync() && probeService.Calls == 2, "H02 unhealthy dependency replaces cached success within 5s");
        var slow = new CountingHealth { Delay = TimeSpan.FromSeconds(20) };
        var slowServices = new ServiceCollection(); slowServices.AddLogging(); slowServices.AddHealthChecks().AddCheck("slow", slow, tags: ["ready"]);
        await using var slowProvider = slowServices.BuildServiceProvider();
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var slowProbe = new ReadinessProbe(slowProvider.GetRequiredService<HealthCheckService>(), TimeProvider.System);
        check(!await slowProbe.CheckAsync() && watch.Elapsed < TimeSpan.FromSeconds(4), "H02 total timeout 3s returns unavailable");
    }
    private sealed class CountingHealth : IHealthCheck
    {
        public int Calls;
        public bool Healthy = true;
        public TimeSpan Delay = TimeSpan.FromMilliseconds(100);
        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        { Interlocked.Increment(ref Calls); await Task.Delay(Delay, cancellationToken); return Healthy ? HealthCheckResult.Healthy() : HealthCheckResult.Unhealthy(); }
    }
}
