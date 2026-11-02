using Ags.Finance.Modules.Transactions.Domain;
using Ags.Finance.Modules.Transactions.Infrastructure;
using Ags.Finance.SharedKernel;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Ags.Finance.Modules.Transactions.Tests;

/// <summary>Prova as convenções de persistência com SQLite real, usando uma entidade só de teste.</summary>
public sealed class PersistenceConventionsTests : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly ManualTimeProvider _clock = new() { Now = new DateTimeOffset(2027, 1, 15, 10, 0, 0, TimeSpan.Zero) };

    public PersistenceConventionsTests()
    {
        _connection.Open();
        using var context = CreateContext();
        context.Database.EnsureCreated();
    }

    [Theory]
    [InlineData(1234.56, 123456)]
    [InlineData(-10.05, -1005)]
    [InlineData(0, 0)]
    public async Task MoneyIsStoredAsCentsInAnIntegerColumn(double amount, long expectedCents)
    {
        var entry = new TestEntry(new Money((decimal)amount), new AccountingMonth(2027, 1));
        await SaveAsync(entry);

        Assert.Equal(expectedCents, await ScalarAsync("SELECT Amount FROM transactions_test_entries"));
        Assert.Equal("integer", await TextAsync("SELECT typeof(Amount) FROM transactions_test_entries"));

        await using var context = CreateContext();
        var loaded = await context.Entries.SingleAsync();
        Assert.Equal(new Money((decimal)amount), loaded.Amount);
    }

    [Theory]
    [InlineData(2027, 1, 202701)]
    [InlineData(2026, 12, 202612)]
    public async Task AccountingMonthIsStoredAsYyyyMm(int year, int month, long expectedValue)
    {
        await SaveAsync(new TestEntry(new Money(1m), new AccountingMonth(year, month)));

        Assert.Equal(expectedValue, await ScalarAsync("SELECT Month FROM transactions_test_entries"));

        await using var context = CreateContext();
        var loaded = await context.Entries.SingleAsync();
        Assert.Equal(new AccountingMonth(year, month), loaded.Month);
    }

    [Fact]
    public async Task TimestampsComeFromTheTimeProviderOnInsertAndUpdate()
    {
        var createdAt = _clock.Now;
        var entry = new TestEntry(new Money(5m), new AccountingMonth(2027, 1));
        await using (var context = CreateContext())
        {
            context.Entries.Add(entry);
            await context.SaveChangesAsync();
        }

        var updatedAt = createdAt.AddDays(3);
        _clock.Now = updatedAt;
        await using (var context = CreateContext())
        {
            var loaded = await context.Entries.SingleAsync();
            loaded.Amount = new Money(7m);
            await context.SaveChangesAsync();
        }

        await using var verification = CreateContext();
        var saved = await verification.Entries.SingleAsync();
        Assert.Equal(createdAt, saved.CreatedAt);
        Assert.Equal(updatedAt, saved.UpdatedAt);
    }

    [Fact]
    public async Task ExternalIdIsAVersion7GuidWithAUniqueIndex()
    {
        var entry = new TestEntry(new Money(1m), new AccountingMonth(2027, 1));
        await SaveAsync(entry);

        Assert.Equal(7, entry.ExternalId.Version);
        Assert.Equal(1, await ScalarAsync("SELECT COUNT(*) FROM pragma_index_list('transactions_test_entries') WHERE \"unique\" = 1 AND origin = 'c'"));
    }

    [Fact]
    public async Task TableNamesReceiveTheModulePrefix()
    {
        Assert.Equal(1, await ScalarAsync("SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'transactions_test_entries'"));
    }

    public void Dispose()
    {
        _connection.Dispose();
    }

    private ConventionsDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ConventionsDbContext>()
            .UseSqlite(_connection)
            .AddInterceptors(new AuditTimestampsInterceptor(_clock))
            .Options;
        return new ConventionsDbContext(options);
    }

    private async Task SaveAsync(TestEntry entry)
    {
        await using var context = CreateContext();
        context.Entries.Add(entry);
        await context.SaveChangesAsync();
    }

    private async Task<long> ScalarAsync(string sql)
    {
        await using var command = _connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToInt64(await command.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);
    }

    private async Task<string> TextAsync(string sql)
    {
        await using var command = _connection.CreateCommand();
        command.CommandText = sql;
        return (string)(await command.ExecuteScalarAsync())!;
    }

    private sealed class TestEntry : Entity
    {
        public TestEntry(Money amount, AccountingMonth month)
        {
            Amount = amount;
            Month = month;
        }

        public Money Amount { get; set; }

        public AccountingMonth Month { get; set; }
    }

    private sealed class ConventionsDbContext : DbContext
    {
        public ConventionsDbContext(DbContextOptions<ConventionsDbContext> options)
            : base(options)
        {
        }

        public DbSet<TestEntry> Entries => Set<TestEntry>();

        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        {
            PersistenceConventions.ConfigureValueConversions(configurationBuilder);
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            PersistenceConventions.IgnoreEntityBase(modelBuilder);
            modelBuilder.Entity<TestEntry>().ToTable("test_entries");
            PersistenceConventions.ApplyEntityRules(modelBuilder, TransactionsDbContext.TablePrefix);
        }
    }
}
