using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace Ags.Finance.Modules.Invoices.Tests;

public sealed class MigrationTests : IDisposable
{
    private const string HistoryTable = "__ef_migrations_invoices";
    private const string EfLockTable = "__EFMigrationsLock";
    private const string TablePrefix = "invoices_";

    private readonly string _directory = Directory.CreateTempSubdirectory("ags-finance-tests-").FullName;

    [Fact]
    public async Task MigratingCreatesTheDatabaseFileAndTheModuleHistoryTable()
    {
        var connectionString = ConnectionString();
        await using var provider = new ServiceCollection().AddInvoicesModule(connectionString).BuildServiceProvider();

        await provider.MigrateInvoicesModuleAsync();

        Assert.True(File.Exists(Path.Combine(_directory, "finance.db")));
        Assert.Contains(HistoryTable, await QueryAsync(connectionString, "SELECT name FROM sqlite_master WHERE type = 'table'"));
        Assert.Single(await QueryAsync(connectionString, $"SELECT MigrationId FROM {HistoryTable}"));
    }

    [Fact]
    public async Task MigratingAnExistingDatabaseAppliesOnlyPendingMigrations()
    {
        var connectionString = ConnectionString();
        await using var provider = new ServiceCollection().AddInvoicesModule(connectionString).BuildServiceProvider();

        await provider.MigrateInvoicesModuleAsync();
        await provider.MigrateInvoicesModuleAsync();

        Assert.Single(await QueryAsync(connectionString, $"SELECT MigrationId FROM {HistoryTable}"));
    }

    /// <summary>A tabela de lock é interna do EF Core e compartilhada por todos os módulos; as demais seguem o prefixo.</summary>
    [Fact]
    public async Task EveryTableOfTheModuleUsesTheModulePrefix()
    {
        var connectionString = ConnectionString();
        await using var provider = new ServiceCollection().AddInvoicesModule(connectionString).BuildServiceProvider();

        await provider.MigrateInvoicesModuleAsync();

        var tables = await QueryAsync(
            connectionString,
            "SELECT name FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite%'");
        Assert.All(tables, table => Assert.True(
            table is HistoryTable or EfLockTable || table.StartsWith(TablePrefix, StringComparison.Ordinal),
            $"Tabela fora do padrão do módulo: {table}"));
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        Directory.Delete(_directory, recursive: true);
    }

    private static async Task<List<string>> QueryAsync(string connectionString, string sql)
    {
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await using var reader = await command.ExecuteReaderAsync();

        var values = new List<string>();
        while (await reader.ReadAsync())
        {
            values.Add(reader.GetString(0));
        }

        return values;
    }

    private string ConnectionString()
    {
        return new SqliteConnectionStringBuilder { DataSource = Path.Combine(_directory, "finance.db") }.ToString();
    }
}
