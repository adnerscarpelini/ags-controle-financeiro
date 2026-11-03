using Ags.Finance.Modules.Accounts.Infrastructure;
using Ags.Finance.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Ags.Finance.Modules.Accounts.Tests;

public sealed class MigrationTests : IAsyncLifetime
{
    private const string Schema = "accounts";
    private const string HistoryTable = "__ef_migrations";

    private PostgresTestDatabase? _database;

    private PostgresTestDatabase Database => _database ?? throw new InvalidOperationException("Fixture not initialized.");

    public async Task InitializeAsync()
    {
        _database = await PostgresTestDatabase.CreateAsync();
    }

    public async Task DisposeAsync()
    {
        if (_database is not null)
        {
            await _database.DisposeAsync();
        }
    }

    [Fact]
    public async Task MigratingAnEmptyDatabaseCreatesTheModuleSchemaAndItsOwnHistoryTable()
    {
        await using var provider = new ServiceCollection().AddAccountsModule(Database.ConnectionString).BuildServiceProvider();

        await provider.MigrateAccountsModuleAsync();

        Assert.Equal([Schema], await Database.QueryAsync($"SELECT schema_name FROM information_schema.schemata WHERE schema_name = '{Schema}'"));
        Assert.Equal(3, int.Parse(await Database.ScalarAsync($"SELECT COUNT(*) FROM {Schema}.{HistoryTable}"), System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public async Task MigratingAnExistingDatabaseAppliesOnlyPendingMigrations()
    {
        await using var provider = new ServiceCollection().AddAccountsModule(Database.ConnectionString).BuildServiceProvider();

        await provider.MigrateAccountsModuleAsync();
        await provider.MigrateAccountsModuleAsync();

        Assert.Equal(3, int.Parse(await Database.ScalarAsync($"SELECT COUNT(*) FROM {Schema}.{HistoryTable}"), System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public async Task EveryTableOfTheModuleLivesInTheModuleSchema()
    {
        await using var provider = new ServiceCollection().AddAccountsModule(Database.ConnectionString).BuildServiceProvider();

        await provider.MigrateAccountsModuleAsync();

        var tables = await Database.QueryAsync(
            "SELECT table_schema FROM information_schema.tables WHERE table_schema NOT IN ('pg_catalog', 'information_schema')");
        Assert.NotEmpty(tables);
        Assert.All(tables, schema => Assert.Equal(Schema, schema));
    }

    /// <summary>O snapshot e a migration escritos à mão precisam descrever exatamente o modelo atual.</summary>
    [Fact]
    public void TheMigrationsMatchTheCurrentModel()
    {
        using var provider = new ServiceCollection().AddAccountsModule(Database.ConnectionString).BuildServiceProvider();
        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AccountsDbContext>();

        Assert.False(context.Database.HasPendingModelChanges());
    }

    [Fact]
    public async Task MigratingCreatesTheCatalogTablesWithBigintMoneyAndUuidKeys()
    {
        await using var provider = new ServiceCollection().AddAccountsModule(Database.ConnectionString).BuildServiceProvider();

        await provider.MigrateAccountsModuleAsync();

        Assert.Equal(
            ["accounts", "banks", "card_last_digits", "credit_cards"],
            await Database.QueryAsync($"SELECT table_name FROM information_schema.tables WHERE table_schema = '{Schema}' AND table_name <> '{HistoryTable}' ORDER BY table_name"));
        Assert.Equal("bigint", await Database.ScalarAsync($"SELECT data_type FROM information_schema.columns WHERE table_schema = '{Schema}' AND table_name = 'credit_cards' AND column_name = 'CreditLimit'"));
        Assert.Equal("uuid", await Database.ScalarAsync($"SELECT data_type FROM information_schema.columns WHERE table_schema = '{Schema}' AND table_name = 'banks' AND column_name = 'ExternalId'"));
    }
}
