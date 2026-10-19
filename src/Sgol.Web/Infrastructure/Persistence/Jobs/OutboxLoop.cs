using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sgol.Web.Infrastructure.Persistence;

namespace Sgol.JobInfrastructure;

public interface IOutboxLoop
{
    Task RunAsync(CancellationToken cancellationToken);

    Task ProbeDatabaseAsync(CancellationToken cancellationToken);
}

public sealed class OutboxLoop(IServiceScopeFactory scopeFactory) : IOutboxLoop
{
    private const int BatchSize = 25;
    private static readonly TimeSpan EmptyDelay = TimeSpan.FromSeconds(5);

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var processedAny = false;
            for (var index = 0; index < BatchSize && !cancellationToken.IsCancellationRequested; index++)
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var processor = scope.ServiceProvider.GetRequiredService<OutboxProcessor>();
                var result = await processor.ProcessNextAsync(cancellationToken);
                if (result == OutboxProcessResult.NoWork)
                {
                    break;
                }

                processedAny = true;
            }

            if (!processedAny)
            {
                await Task.Delay(EmptyDelay, cancellationToken);
            }
        }
    }

    public async Task ProbeDatabaseAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<SgolDbContext>();
        await context.Database.ExecuteSqlRawAsync("SELECT 1", cancellationToken);
    }
}
