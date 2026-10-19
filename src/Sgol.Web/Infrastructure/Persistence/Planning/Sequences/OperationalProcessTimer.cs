namespace Sgol.Web.Infrastructure.Persistence.Planning;

public sealed class OperationalProcessTimer(IServiceScopeFactory scopes, ILogger<OperationalProcessTimer> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(15));
        do
        {
            try { using var scope = scopes.CreateScope(); await scope.ServiceProvider.GetRequiredService<OperationalProcessService>().TickAsync(stoppingToken); }
            catch (OperationCanceledException) when(stoppingToken.IsCancellationRequested) { break; }
            catch(Exception e) { logger.LogError(e,"Process timer failed; pending stages will be retried"); }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
