using Ags.Finance.Modules.Transactions.Domain;
using Ags.Finance.Modules.Transactions.Infrastructure;
using Ags.Finance.SharedKernel;
using Ags.Finance.Tests.Support;
using Microsoft.EntityFrameworkCore;

namespace Ags.Finance.Modules.Transactions.Tests;

/// <summary>Prova as convenções de persistência com PostgreSQL real, usando uma entidade só de teste.</summary>
public sealed class PersistenceConventionsTests : IAsyncLifetime
{
    private readonly ManualTimeProvider _clock = new() { Now = new DateTimeOffset(2027, 1, 15, 10, 0, 0, TimeSpan.Zero) };
    private PostgresTestDatabase? _database;

    private PostgresTestDatabase Database => _database ?? throw new InvalidOperationException("Fixture not initialized.");

    public async Task InitializeAsync()
    {
        _database = await PostgresTestDatabase.CreateAsync();
        await using var context = CreateContext();
        await context.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        if (_database is not null)
        {
            await _database.DisposeAsync();
        }
    }

    [Theory]
    [InlineData(1234.56, 123456)]
    [InlineData(-10.05, -1005)]
    [InlineData(0, 0)]
    public async Task MoneyIsStoredAsCentsInABigintColumn(double amount, long expectedCents)
    {
        var entry = new TestEntry(new Money((decimal)amount), new AccountingMonth(2027, 1));
        await SaveAsync(entry);

        Assert.Equal(expectedCents.ToString(System.Globalization.CultureInfo.InvariantCulture), await Database.ScalarAsync("SELECT \"Amount\" FROM transactions.test_entries"));
        Assert.Equal("bigint", await ColumnTypeAsync("Amount"));

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

        Assert.Equal(expectedValue.ToString(System.Globalization.CultureInfo.InvariantCulture), await Database.ScalarAsync("SELECT \"Month\" FROM transactions.test_entries"));
        Assert.Equal("integer", await ColumnTypeAsync("Month"));

        await using var context = CreateContext();
        var loaded = await context.Entries.SingleAsync();
        Assert.Equal(new AccountingMonth(year, month), loaded.Month);
    }

    [Fact]
    public async Task TimestampsComeFromTheTimeProviderOnInsertAndUpdateAndAreStoredInUtc()
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
        Assert.Equal("timestamp with time zone", await ColumnTypeAsync("CreatedAt"));
    }

    [Fact]
    public async Task ExternalIdIsAVersion7GuidWithAUniqueIndex()
    {
        var entry = new TestEntry(new Money(1m), new AccountingMonth(2027, 1));
        await SaveAsync(entry);

        Assert.Equal(7, entry.ExternalId.Version);
        Assert.Equal("uuid", await ColumnTypeAsync("ExternalId"));
        Assert.Equal(
            "1",
            await Database.ScalarAsync(
                "SELECT COUNT(*) FROM pg_indexes WHERE schemaname = 'transactions' AND tablename = 'test_entries' "
                + "AND indexdef LIKE 'CREATE UNIQUE INDEX%' AND indexdef LIKE '%ExternalId%'"));
    }

    [Fact]
    public async Task IdIsAnIdentityColumn()
    {
        Assert.Equal("YES", await Database.ScalarAsync("SELECT is_identity FROM information_schema.columns WHERE table_schema = 'transactions' AND table_name = 'test_entries' AND column_name = 'Id'"));
    }

    [Fact]
    public async Task TablesLiveInTheModuleSchema()
    {
        Assert.Equal("1", await Database.ScalarAsync("SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = 'transactions' AND table_name = 'test_entries'"));
    }

    private Task<string> ColumnTypeAsync(string column)
    {
        return Database.ScalarAsync($"SELECT data_type FROM information_schema.columns WHERE table_schema = 'transactions' AND table_name = 'test_entries' AND column_name = '{column}'");
    }

    private ConventionsDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ConventionsDbContext>()
            .UseNpgsql(Database.ConnectionString)
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
            modelBuilder.HasDefaultSchema(TransactionsDbContext.Schema);
            PersistenceConventions.IgnoreEntityBase(modelBuilder);
            modelBuilder.Entity<TestEntry>().ToTable("test_entries");
            PersistenceConventions.ApplyEntityRules(modelBuilder);
        }
    }
}
