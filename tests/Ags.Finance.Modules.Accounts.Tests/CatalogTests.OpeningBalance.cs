using Ags.Finance.Modules.Accounts.Contracts;
using Ags.Finance.Modules.Accounts.Infrastructure;
using Ags.Finance.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Ags.Finance.Modules.Accounts.Tests;

public sealed partial class CatalogTests
{
    [Theory]
    [InlineData(125050)]
    [InlineData(0)]
    [InlineData(-125050)]
    public async Task OpeningBalanceAndReferencePersistExactlyWithoutCreatingIncome(long cents)
    {
        var bank = await Catalog.CreateBankAsync("Banco Exemplo");
        var date = new DateOnly(2026, 11, 1);
        var saved = await Catalog.SaveAccountAsync(new(null, bank.ExternalId!.Value, "Conta de abertura", AccountKind.Checking, "green", Money.FromCents(cents), date));
        Assert.True(saved.Succeeded, saved.Error);
        var account = Assert.Single(await Catalog.GetAccountsAsync());
        Assert.Equal(Money.FromCents(cents), account.OpeningBalance);
        Assert.Equal(date, account.OpeningDate);
        using var scope = Provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AccountsDbContext>();
        await context.Database.OpenConnectionAsync();
        await using var command = context.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT \"OpeningBalance\", pg_typeof(\"OpeningBalance\")::text FROM accounts.accounts";
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal(cents, reader.GetInt64(0));
        Assert.Equal("bigint", reader.GetString(1));
    }

    [Fact]
    public async Task MissingReferenceOrInvalidAccountDoesNotSaveOpeningBalance()
    {
        var bank = await Catalog.CreateBankAsync("Banco Exemplo");
        var request = new SaveAccountRequest(null, bank.ExternalId!.Value, "Conta Exemplo", AccountKind.Checking, "green", new Money(100));
        Assert.False((await Catalog.SaveAccountAsync(request)).Succeeded);
        Assert.False((await Catalog.SaveAccountAsync(request with { OpeningDate = new DateOnly(2026, 11, 1), Color = "invalid" })).Succeeded);
        Assert.Empty(await Catalog.GetAccountsAsync(false));
    }

    [Fact]
    public async Task EditingAccountKeepsOpeningAndRejectsReplacingIt()
    {
        var bank = await Catalog.CreateBankAsync("Banco Exemplo");
        var request = new SaveAccountRequest(null, bank.ExternalId!.Value, "Conta Exemplo", AccountKind.Checking, "green", new Money(100), new DateOnly(2026, 11, 1));
        var saved = await Catalog.SaveAccountAsync(request);
        Assert.True((await Catalog.SaveAccountAsync(request with { ExternalId = saved.ExternalId, Name = "Conta editada", OpeningBalance = null, OpeningDate = null })).Succeeded);
        Assert.False((await Catalog.SaveAccountAsync(request with { ExternalId = saved.ExternalId, OpeningBalance = new Money(200) })).Succeeded);
        var account = Assert.Single(await Catalog.GetAccountsAsync());
        Assert.Equal("Conta editada", account.Name);
        Assert.Equal(new Money(100), account.OpeningBalance);
        Assert.Equal(request.OpeningDate, account.OpeningDate);
    }

    [Fact]
    public async Task DefaultReferenceUsesTheBusinessClockInsteadOfUtcDate()
    {
        var connection = (_database ?? throw new InvalidOperationException()).ConnectionString;
        await using var provider = new ServiceCollection().AddSingleton<TimeProvider>(new OpeningClock()).AddAccountsModule(connection).BuildServiceProvider();
        var catalog = provider.GetRequiredService<IAccountCatalog>();
        var bank = await catalog.CreateBankAsync("Banco Exemplo");
        var saved = await catalog.SaveAccountAsync(new(null, bank.ExternalId!.Value, "Conta Exemplo", AccountKind.Checking, "green"));
        Assert.True(saved.Succeeded, saved.Error);
        var account = Assert.Single(await catalog.GetAccountsAsync());
        Assert.Equal(Money.Zero, account.OpeningBalance);
        Assert.Equal(new DateOnly(2026, 11, 1), account.OpeningDate);
    }

    private sealed class OpeningClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 11, 2, 1, 0, 0, TimeSpan.Zero);
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.CreateCustomTimeZone("test-brt", TimeSpan.FromHours(-3), "BRT", "BRT");
    }
}
