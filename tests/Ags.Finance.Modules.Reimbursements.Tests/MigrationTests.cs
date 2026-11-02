using Ags.Finance.Modules.Reimbursements.Infrastructure;
using Ags.Finance.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Ags.Finance.Modules.Reimbursements.Tests;

public sealed class MigrationTests : IAsyncLifetime
{
    private const string Schema = "reimbursements";
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
        await using var provider = new ServiceCollection().AddReimbursementsModule(Database.ConnectionString).BuildServiceProvider();

        await provider.MigrateReimbursementsModuleAsync();

        Assert.Equal([Schema], await Database.QueryAsync($"SELECT schema_name FROM information_schema.schemata WHERE schema_name = '{Schema}'"));
        Assert.Equal(1, int.Parse(await Database.ScalarAsync($"SELECT COUNT(*) FROM {Schema}.{HistoryTable}"), System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public async Task MigratingAnExistingDatabaseAppliesOnlyPendingMigrations()
    {
        await using var provider = new ServiceCollection().AddReimbursementsModule(Database.ConnectionString).BuildServiceProvider();

        await provider.MigrateReimbursementsModuleAsync();
        await provider.MigrateReimbursementsModuleAsync();

        Assert.Equal(1, int.Parse(await Database.ScalarAsync($"SELECT COUNT(*) FROM {Schema}.{HistoryTable}"), System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public async Task EveryTableOfTheModuleLivesInTheModuleSchema()
    {
        await using var provider = new ServiceCollection().AddReimbursementsModule(Database.ConnectionString).BuildServiceProvider();

        await provider.MigrateReimbursementsModuleAsync();

        var tables = await Database.QueryAsync(
            "SELECT table_schema FROM information_schema.tables WHERE table_schema NOT IN ('pg_catalog', 'information_schema')");
        Assert.NotEmpty(tables);
        Assert.All(tables, schema => Assert.Equal(Schema, schema));
    }

    /// <summary>O snapshot e a migration escritos à mão precisam descrever exatamente o modelo atual.</summary>
    [Fact]
    public void TheMigrationsMatchTheCurrentModel()
    {
        using var provider = new ServiceCollection().AddReimbursementsModule(Database.ConnectionString).BuildServiceProvider();
        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ReimbursementsDbContext>();

        Assert.False(context.Database.HasPendingModelChanges());
    }
}
