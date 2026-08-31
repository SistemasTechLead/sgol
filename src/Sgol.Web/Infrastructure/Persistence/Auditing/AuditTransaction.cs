using Microsoft.EntityFrameworkCore;

namespace Sgol.Web.Infrastructure.Persistence.Auditing;

public sealed class AuditTransaction(SgolDbContext dbContext)
{
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
