using Ags.Finance.Modules.Accounts.Contracts;
using Ags.Finance.Modules.Accounts.Infrastructure;
using Ags.Finance.SharedKernel;
using Ags.Finance.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Ags.Finance.Modules.Accounts.Tests;

public sealed partial class CatalogTests : IAsyncLifetime
{
    private PostgresTestDatabase? _database;
    private ServiceProvider? _provider;
    private IAccountCatalog Catalog => Provider.GetRequiredService<IAccountCatalog>();
    private ServiceProvider Provider => _provider ?? throw new InvalidOperationException("Fixture not initialized.");

    public async Task InitializeAsync()
    {
        _database = await PostgresTestDatabase.CreateAsync();
        _provider = new ServiceCollection().AddAccountsModule(_database.ConnectionString).BuildServiceProvider();
        await Provider.MigrateAccountsModuleAsync();
    }
    public async Task DisposeAsync()
    {
        if (_provider is not null)
        {
            await _provider.DisposeAsync();
        }
        if (_database is not null)
        {
            await _database.DisposeAsync();
        }
    }

    [Fact]
    public async Task CreatesMastercardWithLimitColorAndCheckingAccount()
    {
        var accountId = await CreateAccountAsync();
        var result = await Catalog.SaveCreditCardAsync(Card(accountId));
        Assert.True(result.Succeeded, result.Error);
        var saved = Assert.Single(await Catalog.GetCreditCardsAsync());
        Assert.Equal(CardBrand.Mastercard, saved.Brand);
        Assert.Equal(new Money(10000), saved.CreditLimit);
        Assert.Equal("violet", saved.Color);
        Assert.Equal(accountId, saved.AccountExternalId);
        Assert.True(saved.IncludesClosingDay);
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(32, 10)]
    [InlineData(3, 0)]
    [InlineData(3, 32)]
    public async Task RejectsInvalidCalendarDays(int closingDay, int dueDay)
    {
        var result = await Catalog.SaveCreditCardAsync(Card(await CreateAccountAsync()) with
        {
            ClosingDay = closingDay,
            DueDay = dueDay
        });
        Assert.False(result.Succeeded);
        Assert.Contains("entre 1 e 31", result.Error, StringComparison.Ordinal);
        Assert.Empty(await Catalog.GetCreditCardsAsync(false));
    }

    [Fact]
    public async Task RejectsNegativeLimitAndMissingCheckingAccount()
    {
        Assert.False((await Catalog.SaveCreditCardAsync(Card(Guid.NewGuid()))).Succeeded);
        var account = await CreateAccountAsync();
        Assert.False((await Catalog.SaveCreditCardAsync(Card(account) with
        {
            CreditLimit = new Money(-1)
        })).Succeeded);
        var investment = await CreateAccountAsync(AccountKind.Investment);
        Assert.False((await Catalog.SaveCreditCardAsync(Card(investment))).Succeeded);
        await Catalog.DeactivateAccountAsync(account);
        Assert.False((await Catalog.SaveCreditCardAsync(Card(account))).Succeeded);
    }

    [Fact]
    public async Task AccountRequiresAnExistingBankAndPaletteColor()
    {
        var request = new SaveAccountRequest(null, Guid.NewGuid(), "Conta exemplo", AccountKind.Checking, "green");
        Assert.False((await Catalog.SaveAccountAsync(request)).Succeeded);
        var bank = await Catalog.CreateBankAsync("Banco Exemplo");
        Assert.False((await Catalog.SaveAccountAsync(request with
        {
            BankExternalId = bank.ExternalId!.Value,
            Color = ""
        })).Succeeded);
        Assert.False((await Catalog.SaveAccountAsync(request with
        {
            BankExternalId = bank.ExternalId.Value,
            Color = "arbitrary"
        })).Succeeded);
        Assert.Empty(await Catalog.GetAccountsAsync(false));
    }

    [Fact]
    public async Task AccountNamesAreUniqueWithinBankIncludingWhitespaceAndCase()
    {
        var accountId = await CreateAccountAsync();
        var account = Assert.Single(await Catalog.GetAccountsAsync());
        var duplicate = new SaveAccountRequest(null, account.BankExternalId, "  CONTA EXEMPLO  ", account.Kind, account.Color);
        Assert.False((await Catalog.SaveAccountAsync(duplicate)).Succeeded);
        Assert.True((await Catalog.SaveAccountAsync(duplicate with
        {
            ExternalId = accountId
        })).Succeeded);
        var otherBank = await Catalog.CreateBankAsync("Outro Banco");
        Assert.True((await Catalog.SaveAccountAsync(duplicate with
        {
            BankExternalId = otherBank.ExternalId!.Value
        })).Succeeded);
    }

    [Theory]
    [InlineData(AccountKind.Investment)]
    [InlineData(AccountKind.Savings)]
    public async Task SupportsInvestmentAndSavingsAccounts(AccountKind kind)
    {
        var id = await CreateAccountAsync(kind);
        Assert.Equal(kind, Assert.Single(await Catalog.GetAccountsAsync()).Kind);
        Assert.NotEqual(Guid.Empty, id);
    }

    [Fact]
    public async Task EditingAndDeactivatingPreservesIdentityAndHistoricalData()
    {
        var accountId = await CreateAccountAsync();
        var created = await Catalog.SaveCreditCardAsync(Card(accountId));
        Assert.True((await Catalog.SaveCreditCardAsync(Card(accountId) with
        {
            ExternalId = created.ExternalId,
            Name = "Cartão editado",
            DueDay = 12
        })).Succeeded);
        await Catalog.DeactivateCreditCardAsync(created.ExternalId!.Value);
        Assert.Empty(await Catalog.GetCreditCardsAsync());
        var card = Assert.Single(await Catalog.GetCreditCardsAsync(false));
        Assert.Equal("Cartão editado", card.Name);
        Assert.Equal(12, card.DueDay);
        Assert.Equal(created.ExternalId, card.ExternalId);
        Assert.False(card.IsActive);
        await Catalog.DeactivateAccountAsync(accountId);
        Assert.Empty(await Catalog.GetAccountsAsync());
        Assert.Single(await Catalog.GetAccountsAsync(false));
    }

    [Fact]
    public async Task DeactivatedAccountRemovesItsCardsFromSelectionsWithoutDeletingThem()
    {
        var accountId = await CreateAccountAsync();
        await Catalog.SaveCreditCardAsync(Card(accountId));
        await Catalog.DeactivateAccountAsync(accountId);
        Assert.Empty(await Catalog.GetCreditCardsAsync());
        Assert.Single(await Catalog.GetCreditCardsAsync(false));
    }

    [Fact]
    public async Task CannotTurnACardPaymentAccountIntoAnInvestment()
    {
        var accountId = await CreateAccountAsync();
        await Catalog.SaveCreditCardAsync(Card(accountId));
        var account = Assert.Single(await Catalog.GetAccountsAsync());
        Assert.False((await Catalog.SaveAccountAsync(new(accountId, account.BankExternalId, account.Name, AccountKind.Investment, account.Color))).Succeeded);
        Assert.Equal(AccountKind.Checking, Assert.Single(await Catalog.GetAccountsAsync()).Kind);
    }

    [Fact]
    public async Task ExpectedMissingRecordsAndInvalidEnumsReturnFailures()
    {
        Assert.False((await Catalog.CreateBankAsync(" ")).Succeeded);
        Assert.False((await Catalog.DeactivateAccountAsync(Guid.NewGuid())).Succeeded);
        Assert.False((await Catalog.DeactivateCreditCardAsync(Guid.NewGuid())).Succeeded);
        var accountId = await CreateAccountAsync();
        Assert.False((await Catalog.SaveCreditCardAsync(Card(accountId) with
        {
            Brand = (CardBrand)999
        })).Succeeded);
        Assert.False((await Catalog.SaveCreditCardAsync(Card(accountId) with
        {
            ExternalId = Guid.NewGuid()
        })).Succeeded);
    }

    [Fact]
    public async Task MigrationSnapshotMatchesRuntimeModelAndLimitIsStoredAsCents()
    {
        using var scope = Provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AccountsDbContext>();
        Assert.False(context.Database.HasPendingModelChanges());
        await Catalog.SaveCreditCardAsync(Card(await CreateAccountAsync()));
        await context.Database.OpenConnectionAsync();
        await using var command = context.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT \"CreditLimit\", pg_typeof(\"CreditLimit\")::text, \"CreatedAt\", \"UpdatedAt\" FROM accounts.credit_cards";
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal(1000000, reader.GetInt64(0));
        Assert.Equal("bigint", reader.GetString(1));
        Assert.Equal(TimeSpan.Zero, reader.GetFieldValue<DateTimeOffset>(2).Offset);
        Assert.Equal(TimeSpan.Zero, reader.GetFieldValue<DateTimeOffset>(3).Offset);
    }

    private async Task<Guid> CreateAccountAsync(AccountKind kind = AccountKind.Checking)
    {
        var bank = await Catalog.CreateBankAsync("Banco Exemplo");
        var result = await Catalog.SaveAccountAsync(new(null, bank.ExternalId!.Value, "Conta exemplo", kind, "green"));
        Assert.True(result.Succeeded, result.Error);
        return result.ExternalId!.Value;
    }

    private static SaveCreditCardRequest Card(Guid accountId)
        => new(null, accountId, "Cartão exemplo", CardBrand.Mastercard, new Money(10000), 3, 10, true, "violet");
}
