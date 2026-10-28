using System.Data.Common;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Sgol.Web.Infrastructure.Http;

namespace Sgol.Web.Infrastructure.Persistence.Planning;

internal sealed class OperationalLockTelemetry : DbTransactionInterceptor
{
    internal static readonly OperationalLockTelemetry Instance = new();
    private readonly ConditionalWeakTable<DbTransaction, Hold> acquired = new();
    private sealed record Hold(long At, string Operation, Guid TransactionId);
    internal void Acquired(DbTransaction transaction, Guid transactionId, string operation)
    {
        // Drivers can reuse the DbTransaction object after an implicit rollback on Dispose.
        // EF's transaction identity prevents attributing that idle interval to the next lock.
        if (acquired.TryGetValue(transaction, out var existing) && existing.TransactionId == transactionId) return;
        acquired.Remove(transaction);
        acquired.Add(transaction, new(Stopwatch.GetTimestamp(), operation, transactionId));
    }
    private void End(DbTransaction transaction)
    {
        if (!acquired.TryGetValue(transaction, out var hold)) return;
        acquired.Remove(transaction);
        PerformanceWork.Duration.Record(Stopwatch.GetElapsedTime(hold.At).TotalMilliseconds,
            new KeyValuePair<string, object?>("stage", "lock.hold." + hold.Operation));
    }
    public override void TransactionCommitted(DbTransaction transaction, TransactionEndEventData eventData) => End(transaction);
    public override void TransactionRolledBack(DbTransaction transaction, TransactionEndEventData eventData) => End(transaction);
    public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
    { End(transaction); return Task.CompletedTask; }
    public override Task TransactionRolledBackAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
    { End(transaction); return Task.CompletedTask; }
}
