using Ags.Finance.Modules.Accounts;
using Ags.Finance.Modules.Identity;
using Ags.Finance.Modules.Invoices;
using Ags.Finance.Modules.Reimbursements;
using Ags.Finance.Modules.Transactions;
using Microsoft.Data.Sqlite;

namespace Ags.Finance.Web.Persistence;

internal static class DatabaseStartup
{
    public const string DatabaseFileName = "finance.db";

    /// <summary>Cria a pasta de dados, se necessário, e devolve a connection string do banco único.</summary>
    public static string PrepareConnectionString(string dataDirectory)
    {
        Directory.CreateDirectory(dataDirectory);

        return new SqliteConnectionStringBuilder
        {
            DataSource = Path.Combine(dataDirectory, DatabaseFileName),
        }.ToString();
    }

    /// <summary>Ativa o modo WAL e aplica as migrations pendentes de cada módulo com persistência.</summary>
    public static async Task InitializeAsync(IServiceProvider services, string connectionString, CancellationToken cancellationToken = default)
    {
        await EnableWalAsync(connectionString, cancellationToken);

        await services.MigrateIdentityModuleAsync(cancellationToken);
        await services.MigrateAccountsModuleAsync(cancellationToken);
        await services.MigrateTransactionsModuleAsync(cancellationToken);
        await services.MigrateInvoicesModuleAsync(cancellationToken);
        await services.MigrateReimbursementsModuleAsync(cancellationToken);
    }

    private static async Task EnableWalAsync(string connectionString, CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA journal_mode=WAL;";
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
