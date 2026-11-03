using Ags.Finance.Tests.Support;
using Ags.Finance.Web.Components;
using Ags.Finance.Web.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

namespace Ags.Finance.Web.Tests;

/// <summary>Sobe a aplicação inteira sobre um PostgreSQL real e vazio e confere o banco criado.</summary>
public sealed class DatabaseStartupTests : IAsyncLifetime
{
    private static readonly string[] s_modules = ["identity", "accounts", "transactions", "invoices", "reimbursements"];

    private readonly string _directory = Path.Combine(Path.GetTempPath(), "ags-finance-tests-" + Guid.NewGuid().ToString("N"));
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

        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public async Task StartingOnAnEmptyDatabaseCreatesASchemaWithItsOwnHistoryForEveryModule()
    {
        await StartApplicationAsync();

        foreach (var module in s_modules)
        {
            Assert.Equal([module], await Database.QueryAsync($"SELECT schema_name FROM information_schema.schemata WHERE schema_name = '{module}'"));
            Assert.Equal(module == "transactions" ? "13" : module == "invoices" ? "5" : module == "accounts" ? "2" : "1", await Database.ScalarAsync($"SELECT COUNT(*) FROM {module}.__ef_migrations"));
        }
    }

    [Fact]
    public async Task StartingAgainOnAnExistingDatabaseKeepsEachMigrationRecordedOnce()
    {
        await StartApplicationAsync();
        await StartApplicationAsync();

        foreach (var module in s_modules)
        {
            Assert.Equal(module == "transactions" ? "13" : module == "invoices" ? "5" : module == "accounts" ? "2" : "1", await Database.ScalarAsync($"SELECT COUNT(*) FROM {module}.__ef_migrations"));
        }
    }

    [Fact]
    public async Task NoTableCrossesModulesWithAForeignKey()
    {
        await StartApplicationAsync();

        Assert.Equal("0", await Database.ScalarAsync("""
            SELECT COUNT(*) FROM pg_constraint c
            JOIN pg_class source ON source.oid = c.conrelid
            JOIN pg_class target ON target.oid = c.confrelid
            WHERE c.contype = 'f' AND source.relnamespace <> target.relnamespace
            """));
    }

    [Fact]
    public void ConnectionStringComesFromTheConfiguredFinanceEntry()
    {
        var configuration = Configure(Database.ConnectionString);

        Assert.Equal(
            new NpgsqlConnectionStringBuilder(Database.ConnectionString).Database,
            new NpgsqlConnectionStringBuilder(DatabaseStartup.GetConnectionString(configuration)).Database);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void MissingConnectionStringIsReportedClearly(string? value)
    {
        var exception = Assert.Throws<InvalidOperationException>(() => DatabaseStartup.GetConnectionString(Configure(value)));

        Assert.Contains("ConnectionStrings__Finance", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void InvalidConnectionStringIsReportedWithoutEchoingThePassword()
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => DatabaseStartup.GetConnectionString(Configure("Host=db;Password=segredo-123;Opcao-Inexistente=1")));

        Assert.DoesNotContain("segredo-123", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ConnectionStringWithoutDatabaseOrUserIsRejected()
    {
        Assert.Throws<InvalidOperationException>(() => DatabaseStartup.GetConnectionString(Configure("Host=db;Password=x")));
    }

    [Fact]
    public async Task WrongCredentialsFailImmediatelyAndNeverEchoThePassword()
    {
        var wrong = new NpgsqlConnectionStringBuilder(Database.ConnectionString) { Password = "senha-errada-xyz" }.ToString();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => DatabaseStartup.WaitUntilReadyAsync(wrong, TimeSpan.FromSeconds(30), NullLogger.Instance));

        Assert.DoesNotContain("senha-errada-xyz", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnavailableDatabaseGivesUpAfterTheTimeoutWithAClearMessage()
    {
        var unreachable = new NpgsqlConnectionStringBuilder(Database.ConnectionString) { Host = "127.0.0.1", Port = 1, Timeout = 1 }.ToString();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => DatabaseStartup.WaitUntilReadyAsync(unreachable, TimeSpan.FromSeconds(1), NullLogger.Instance));

        Assert.Contains("não ficou disponível", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ReadyTimeoutDefaultsAndRejectsInvalidValues()
    {
        Assert.Equal(TimeSpan.FromSeconds(60), DatabaseStartup.GetReadyTimeout(new ConfigurationBuilder().Build()));
        Assert.Throws<InvalidOperationException>(() => DatabaseStartup.GetReadyTimeout(
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { [DatabaseStartup.ReadyTimeoutKey] = "0" }).Build()));
    }

    private static IConfiguration Configure(string? connectionString)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:Finance"] = connectionString })
            .Build();
    }

    private async Task StartApplicationAsync()
    {
        await using var factory = new WebApplicationFactory<App>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("DataDirectory", _directory);
            builder.UseSetting("ConnectionStrings:Finance", Database.ConnectionString);
        });
        using var client = factory.CreateClient();
        using var response = await client.GetAsync(new Uri("/", UriKind.Relative));
        response.EnsureSuccessStatusCode();
    }
}
