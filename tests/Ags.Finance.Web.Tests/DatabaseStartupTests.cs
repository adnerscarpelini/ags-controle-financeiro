using Ags.Finance.Web.Components;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;

namespace Ags.Finance.Web.Tests;

/// <summary>Sobe a aplicação inteira com uma pasta de dados temporária e confere o banco criado.</summary>
public sealed class DatabaseStartupTests : IDisposable
{
    private static readonly string[] s_modules = ["identity", "accounts", "transactions", "invoices", "reimbursements"];

    private readonly string _directory = Path.Combine(Path.GetTempPath(), "ags-finance-tests-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task StartingWithoutADatabaseCreatesItInTheConfiguredDirectoryAndMigratesEveryModule()
    {
        await StartApplicationAsync();

        var databasePath = Path.Combine(_directory, "finance.db");
        Assert.True(File.Exists(databasePath));

        var tables = await QueryAsync(databasePath, "SELECT name FROM sqlite_master WHERE type = 'table'");
        foreach (var module in s_modules)
        {
            Assert.Contains($"__ef_migrations_{module}", tables);
        }
    }

    [Fact]
    public async Task StartingAgainOnAnExistingDatabaseKeepsEachMigrationRecordedOnce()
    {
        await StartApplicationAsync();
        await StartApplicationAsync();

        var databasePath = Path.Combine(_directory, "finance.db");
        foreach (var module in s_modules)
        {
            var applied = await QueryAsync(databasePath, $"SELECT MigrationId FROM __ef_migrations_{module}");
            Assert.Single(applied);
        }
    }

    [Fact]
    public async Task TheDatabaseRunsInWalMode()
    {
        await StartApplicationAsync();

        var mode = await QueryAsync(Path.Combine(_directory, "finance.db"), "PRAGMA journal_mode");

        Assert.Equal(["wal"], mode);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private async Task StartApplicationAsync()
    {
        await using var factory = new WebApplicationFactory<App>().WithWebHostBuilder(builder => builder.UseSetting("DataDirectory", _directory));
        using var client = factory.CreateClient();
        using var response = await client.GetAsync(new Uri("/", UriKind.Relative));
        response.EnsureSuccessStatusCode();
    }

    private static async Task<List<string>> QueryAsync(string databasePath, string sql)
    {
        await using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = databasePath, Mode = SqliteOpenMode.ReadOnly }.ToString());
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
}
