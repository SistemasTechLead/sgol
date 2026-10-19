using Microsoft.EntityFrameworkCore;
using Sgol.Web.Infrastructure.Persistence;

namespace Sgol.Web.Infrastructure.Evidence;

public sealed class ExpiredCameraCleanupService(IServiceScopeFactory scopes, ILogger<ExpiredCameraCleanupService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(15));
        do
        {
            try
            {
                using var scope = scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<SgolDbContext>();
                var now = DateTimeOffset.UtcNow;
                await db.OperationalPhotographs.Where(p => p.AttachedAt == null && p.CaptureSessionId != null &&
                    db.OperationalCaptureSessions.Any(s => s.Id == p.CaptureSessionId && (s.ExpiresAt <= now || s.ConsumedAt != null)))
                    .ExecuteDeleteAsync(stoppingToken);
                // Keep attached session context for audit; remove only abandoned sessions after a day.
                await db.OperationalCaptureSessions.Where(s => s.ExpiresAt < now.AddDays(-1) && s.ConsumedAt == null).ExecuteDeleteAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception) { logger.LogWarning("Camera cleanup could not complete; it will retry."); }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
