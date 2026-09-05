using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Sgol.Cv02Demo;

internal sealed class DemoReadOnlyCommandInterceptor : DbCommandInterceptor
{
    public override InterceptionResult<int> NonQueryExecuting(
        DbCommand command, CommandEventData eventData, InterceptionResult<int> result)
    {
        EnsureAllowed(command, allowTransactionDeclaration: true);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        EnsureAllowed(command, allowTransactionDeclaration: true);
        return ValueTask.FromResult(result);
    }

    public override InterceptionResult<DbDataReader> ReaderExecuting(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
    {
        EnsureAllowed(command, allowTransactionDeclaration: false);
        return result;
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        EnsureAllowed(command, allowTransactionDeclaration: false);
        return ValueTask.FromResult(result);
    }

    public override InterceptionResult<object> ScalarExecuting(
        DbCommand command, CommandEventData eventData, InterceptionResult<object> result)
    {
        EnsureAllowed(command, allowTransactionDeclaration: false);
        return result;
    }

    public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<object> result,
        CancellationToken cancellationToken = default)
    {
        EnsureAllowed(command, allowTransactionDeclaration: false);
        return ValueTask.FromResult(result);
    }

    private static void EnsureAllowed(DbCommand command, bool allowTransactionDeclaration)
    {
        var sql = command.CommandText.TrimStart();
        var read = sql.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase) ||
            sql.StartsWith("WITH", StringComparison.OrdinalIgnoreCase);
        var transactionDeclaration = allowTransactionDeclaration &&
            string.Equals(sql, "SET TRANSACTION READ ONLY", StringComparison.OrdinalIgnoreCase);
        if (!read && !transactionDeclaration)
            throw new DemoSafetyException("CV02_SCENARIO_FAILED");
    }
}
