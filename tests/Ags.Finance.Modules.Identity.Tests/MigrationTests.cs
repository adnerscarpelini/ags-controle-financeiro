using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Ags.Finance.Modules.Identity.Tests;

public sealed class MigrationTests : IDisposable
{
    private const string HistoryTable = "__ef_migrations_identity";
    private const string EfLockTable = "__EFMigrationsLock";
    private const string TablePrefix = "identity_";

    private readonly string _directory = Directory.CreateTempSubdirectory("ags-finance-tests-").FullName;

    [Fact]
    public async Task MigratingCreatesTheDatabaseFileAndTheModuleHistoryTable()
    {
        var connectionString = ConnectionString();
        await using var provider = new ServiceCollection().AddIdentityModule(connectionString).BuildServiceProvider();

        await provider.MigrateIdentityModuleAsync();

        Assert.True(File.Exists(Path.Combine(_directory, "finance.db")));
        Assert.Contains(HistoryTable, await QueryAsync(connectionString, "SELECT name FROM sqlite_master WHERE type = 'table'"));
        Assert.Equal(2, (await QueryAsync(connectionString, $"SELECT MigrationId FROM {HistoryTable}")).Count);
    }

    [Fact]
    public async Task MigratingAnExistingDatabaseAppliesOnlyPendingMigrations()
    {
        var connectionString = ConnectionString();
        await using var provider = new ServiceCollection().AddIdentityModule(connectionString).BuildServiceProvider();

        await provider.MigrateIdentityModuleAsync();
        await provider.MigrateIdentityModuleAsync();

        Assert.Equal(2, (await QueryAsync(connectionString, $"SELECT MigrationId FROM {HistoryTable}")).Count);
    }

    /// <summary>A tabela de lock é interna do EF Core e compartilhada por todos os módulos; as demais seguem o prefixo.</summary>
    [Fact]
    public async Task EveryTableOfTheModuleUsesTheModulePrefix()
    {
        var connectionString = ConnectionString();
        await using var provider = new ServiceCollection().AddIdentityModule(connectionString).BuildServiceProvider();

        await provider.MigrateIdentityModuleAsync();

        var tables = await QueryAsync(
            connectionString,
            "SELECT name FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite%'");
        Assert.All(tables, table => Assert.True(
            table is HistoryTable or EfLockTable || table.StartsWith(TablePrefix, StringComparison.Ordinal),
            $"Tabela fora do padrão do módulo: {table}"));
    }

    [Fact]
    public async Task MigratingCreatesTheUsersTableWithAUniqueExternalId()
    {
        var connectionString = ConnectionString();
        await using var provider = new ServiceCollection().AddIdentityModule(connectionString).BuildServiceProvider();

        await provider.MigrateIdentityModuleAsync();

        Assert.Contains("identity_users", await QueryAsync(connectionString, "SELECT name FROM sqlite_master WHERE type = 'table'"));
        Assert.Contains("IX_identity_users_ExternalId", await QueryAsync(connectionString, "SELECT name FROM sqlite_master WHERE type = 'index'"));
    }

    /// <summary>O snapshot e a migration escritos à mão precisam descrever exatamente o modelo atual.</summary>
    [Fact]
    public void TheMigrationsMatchTheCurrentModel()
    {
        var connectionString = ConnectionString();
        using var provider = new ServiceCollection().AddIdentityModule(connectionString).BuildServiceProvider();
        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<Infrastructure.IdentityDbContext>();

        Assert.False(context.Database.HasPendingModelChanges());
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
