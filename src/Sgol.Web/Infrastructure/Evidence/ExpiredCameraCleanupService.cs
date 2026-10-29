using Microsoft.EntityFrameworkCore;
using Sgol.Web.Infrastructure.Persistence;
using Sgol.Web.Infrastructure.Persistence.Planning;

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
                await CleanupAsync(db, now, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception) { logger.LogWarning("Camera cleanup could not complete; it will retry."); }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    public static async Task CleanupAsync(SgolDbContext db, DateTimeOffset now, CancellationToken token)
    {
        await using var tx = db.Database.CurrentTransaction == null ? await db.Database.BeginTransactionAsync(token) : null;
        // Returning a process and expiring its camera material must never race.
        await OperationalTaskService.LockAsync(db, token);
        var preserved = db.Set<OperationalSequenceAttempt>().Where(a => a.AssignmentId != null &&
            (db.Set<OperationalSequenceAttempt>().Any(next => next.ContinuationAttemptId == a.Id) ||
             db.Set<OperationalSequenceStage>().Any(stage => stage.RunId == a.RunId && stage.OutputItemRateId != null))).Select(a => a.AssignmentId);
        await db.OperationalPhotographs.Where(p => p.AttachedAt == null && p.CaptureSessionId != null && !preserved.Contains(p.AssignmentId) &&
            db.OperationalCaptureSessions.Any(s => s.Id == p.CaptureSessionId && (s.ExpiresAt <= now || s.ConsumedAt != null))).ExecuteDeleteAsync(token);
        // Continuation material is work history, including photos not yet delivered.
        await db.OperationalCaptureSessions.Where(s => s.ExpiresAt < now.AddDays(-1) && s.ConsumedAt == null && !preserved.Contains(s.AssignmentId)).ExecuteDeleteAsync(token);
        if (tx != null) await tx.CommitAsync(token);
    }
}
