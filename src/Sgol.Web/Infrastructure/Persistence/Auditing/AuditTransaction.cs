using Microsoft.EntityFrameworkCore;
using System.Data;

namespace Sgol.Web.Infrastructure.Persistence.Auditing;

public sealed class AuditTransaction(SgolDbContext dbContext)
{
    public async Task ExecuteAsync(
        Func<CancellationToken, Task<AuditEvent>> criticalWrite,
        CancellationToken cancellationToken = default)
        => await ExecuteAsync(IsolationLevel.ReadCommitted, criticalWrite, cancellationToken);

    public async Task ExecuteAsync(
        IsolationLevel isolationLevel,
        Func<CancellationToken, Task<AuditEvent>> criticalWrite,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(criticalWrite);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(isolationLevel, cancellationToken);

        var auditEvent = await criticalWrite(cancellationToken);
        ArgumentNullException.ThrowIfNull(auditEvent);
        dbContext.AuditEvents.Add(auditEvent);
        await dbContext.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task ExecuteAsync(
        AuditEvent auditEvent,
        Func<CancellationToken, Task> criticalWrite,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(auditEvent);
        ArgumentNullException.ThrowIfNull(criticalWrite);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        await criticalWrite(cancellationToken);
        dbContext.AuditEvents.Add(auditEvent);
        await dbContext.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }
}
