using Sgol.BuildingBlocks.Versioning;
using Sgol.Web.Infrastructure.Persistence.Auditing;

namespace Sgol.Web.Infrastructure.Persistence.Versioning;

public sealed class VersioningTransaction(
    SgolDbContext dbContext,
    AuditTransaction auditTransaction)
{
    public async Task<TResult> ExecuteAsync<TResult>(
        Func<CancellationToken, Task<bool>> authorize,
        Func<AuditEvent> deniedAuditEvent,
        string reason,
        Func<CancellationToken, Task<(TResult Result, AuditEvent AuditEvent)>> criticalWrite,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(authorize);
        ArgumentNullException.ThrowIfNull(deniedAuditEvent);
        ArgumentNullException.ThrowIfNull(criticalWrite);

        try
        {
            var isAuthorized = await authorize(cancellationToken);
            if (!isAuthorized)
            {
                var denial = deniedAuditEvent();
                ArgumentNullException.ThrowIfNull(denial);
                await auditTransaction.ExecuteAsync(
                    denial,
                    _ => Task.CompletedTask,
                    cancellationToken);
                throw new VersioningAccessDeniedException();
            }

            var normalizedReason = VersioningRules.NormalizeRequiredReason(reason);
            TResult? result = default;
            await auditTransaction.ExecuteAsync(
                async token =>
                {
                    var mutation = await criticalWrite(token);
                    ArgumentNullException.ThrowIfNull(mutation.AuditEvent);
                    if (!string.Equals(
                        mutation.AuditEvent.Reason,
                        normalizedReason,
                        StringComparison.Ordinal) ||
                        mutation.AuditEvent.BeforeData is null ||
                        mutation.AuditEvent.AfterData is null)
                    {
                        throw new VersioningAuditMismatchException();
                    }

                    result = mutation.Result;
                    return mutation.AuditEvent;
                },
                cancellationToken);

            return result!;
        }
        catch
        {
            dbContext.ChangeTracker.Clear();
            throw;
        }
        finally
        {
            dbContext.ChangeTracker.Clear();
        }
    }
}

public sealed class VersioningAuditMismatchException()
    : Exception("Version publication reason must match its audit event.");
