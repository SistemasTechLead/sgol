using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;
using Sgol.Planning.Contracts;

namespace Sgol.Web.Infrastructure.Persistence.Planning;

// Stamp economic visibility at the transaction's commit boundary, not request start or SaveChanges.
// Uses the same transaction; failure or rollback cannot leave a partial economic history.
internal sealed class TaskBonusCommitInterceptor(Func<DateTimeOffset> now) : DbTransactionInterceptor
{
    private DbCommand? Prepare(DbTransaction transaction, TransactionEventData data)
    {
        if (data.Context is not SgolDbContext db) return null;
        var at = now();
        if (db.PendingBonusExclusions.Values.Any(week => at >= TaskBonusCalculator.Period(week).ReviewCutoffAt))
            throw new DirectionPlanException("BONUS_CLOSED", 409);
        if (db.PendingBonusFacts.Count == 0 && db.PendingBonusAvailability.Count == 0) return null;
        var command = transaction.Connection!.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            UPDATE task_bonus_fact SET "RecordedAt" = @at WHERE "Id" = ANY(@facts);
            UPDATE task_bonus_availability_fact SET "RecordedAt" = @at WHERE "Id" = ANY(@availability);
            """;
        command.Parameters.Add(new NpgsqlParameter("at", at));
        command.Parameters.Add(new NpgsqlParameter("facts", db.PendingBonusFacts.ToArray()));
        command.Parameters.Add(new NpgsqlParameter("availability", db.PendingBonusAvailability.ToArray()));
        return command;
    }
    public override InterceptionResult TransactionCommitting(DbTransaction transaction, TransactionEventData eventData, InterceptionResult result)
    {
        using var command = Prepare(transaction, eventData); command?.ExecuteNonQuery(); return result;
    }
    public override async ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction transaction, TransactionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default)
    {
        await using var command = Prepare(transaction, eventData);
        if (command != null) await command.ExecuteNonQueryAsync(cancellationToken);
        return result;
    }
    private static void Clear(TransactionEndEventData data)
    {
        if (data.Context is not SgolDbContext db) return;
        db.PendingBonusFacts.Clear(); db.PendingBonusAvailability.Clear(); db.PendingBonusExclusions.Clear();
    }
    public override void TransactionCommitted(DbTransaction transaction, TransactionEndEventData eventData) => Clear(eventData);
    public override void TransactionRolledBack(DbTransaction transaction, TransactionEndEventData eventData) => Clear(eventData);
    public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
    { Clear(eventData); return Task.CompletedTask; }
    public override Task TransactionRolledBackAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
    { Clear(eventData); return Task.CompletedTask; }
}
