using System.Transactions;
using Ags.Finance.Modules.Accounts.Contracts;
using Npgsql;

namespace Ags.Finance.Modules.Accounts.Infrastructure;

internal sealed class FinancialOperation(NpgsqlDataSource dataSource) : IFinancialOperation
{
    public async Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> action, Func<T, bool> succeeded, CancellationToken cancellationToken = default)
    {
        using var scope = new TransactionScope(TransactionScopeOption.Required,
            new TransactionOptions { IsolationLevel = IsolationLevel.ReadCommitted, Timeout = TimeSpan.FromSeconds(60) }, TransactionScopeAsyncFlowOption.Enabled);
        await using (var connection = await dataSource.OpenConnectionAsync(cancellationToken))
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT pg_advisory_xact_lock(724764654901)";
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        var result = await action(cancellationToken);
        if (succeeded(result)) { scope.Complete(); }
        return result;
    }
}
