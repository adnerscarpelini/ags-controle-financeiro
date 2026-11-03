using Ags.Finance.Modules.Accounts;
using Ags.Finance.Modules.Accounts.Contracts;
using Ags.Finance.Modules.Attachments;
using Ags.Finance.Modules.Invoices.Contracts;
using Ags.Finance.Modules.Preferences;
using Ags.Finance.Modules.Preferences.Contracts;
using Ags.Finance.Modules.Transactions;
using Ags.Finance.Modules.Transactions.Contracts;
using Ags.Finance.SharedKernel;
using Ags.Finance.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace Ags.Finance.Modules.Invoices.Tests;

public sealed partial class InvoiceTests : IAsyncLifetime
{
    private readonly string _attachmentsDirectory = Path.Combine(Path.GetTempPath(), "ags-invoice-attachments-" + Guid.NewGuid().ToString("N"));
    private PostgresTestDatabase? _database;
    private ServiceProvider? _provider;
    private ServiceProvider Provider => _provider ?? throw new InvalidOperationException();
    private IInvoiceCatalog Catalog => Provider.GetRequiredService<IInvoiceCatalog>();
    private Guid _accountId;
    private Guid _cardId;
    private Guid _merchantId;
    private Guid _subcategoryId;
    private readonly InvoiceClock _clock = new();

    public async Task InitializeAsync()
    {
        _database = await PostgresTestDatabase.CreateAsync();
        Directory.CreateDirectory(_attachmentsDirectory);
        _provider = new ServiceCollection().AddSingleton<TimeProvider>(_clock).AddAttachmentsModule(_database.ConnectionString, new(_attachmentsDirectory)).AddAccountsModule(_database.ConnectionString).AddTransactionsModule(_database.ConnectionString).AddInvoicesModule(_database.ConnectionString).AddPreferencesModule(_database.ConnectionString).BuildServiceProvider();
        await Provider.MigrateAttachmentsModuleAsync();
        await Provider.MigrateAccountsModuleAsync();
        await Provider.MigrateTransactionsModuleAsync();
        await Provider.MigrateInvoicesModuleAsync();
        await Provider.MigratePreferencesModuleAsync();
        // Os testes herdados verificam séries, edição e proteção de fatura somando as ocorrências futuras desde já;
        // o modo "somar só na data" tem testes próprios em InvoiceTests.RecurringForecast.cs.
        await Provider.GetRequiredService<IPreferences>().SaveAsync(AppPreferences.Default with { RecurringInvoices = RecurringInvoiceMode.SumUpfront });
        var accounts = Provider.GetRequiredService<IAccountCatalog>();
        var bank = await accounts.CreateBankAsync("Banco Exemplo");
        var account = await accounts.SaveAccountAsync(new(null, bank.ExternalId!.Value, "Conta Exemplo", AccountKind.Checking, "green"));
        _accountId = account.ExternalId!.Value;
        var card = await accounts.SaveCreditCardAsync(new(null, _accountId, "Cartão Exemplo", CardBrand.Visa, new Money(10000), 3, 10, true, "green"));
        _cardId = card.ExternalId!.Value;
        var classifications = Provider.GetRequiredService<IClassificationCatalog>();
        var category = await classifications.SaveCategoryAsync(null, "Mercado", CategoryKind.Expense);
        var subcategory = await classifications.SaveSubcategoryAsync(null, category.ExternalId!.Value, "Feira");
        _subcategoryId = subcategory.ExternalId!.Value;
        var merchant = await Provider.GetRequiredService<IMerchantCatalog>().SaveAsync(null, "Loja Exemplo", _subcategoryId);
        _merchantId = merchant.ExternalId!.Value;
    }

    public async Task DisposeAsync()
    {
        if (_provider is not null) { await _provider.DisposeAsync(); }
        if (_database is not null) { await _database.DisposeAsync(); }
        if (Directory.Exists(_attachmentsDirectory)) { Directory.Delete(_attachmentsDirectory, true); }
    }

    private CreateEntryRequest Purchase(decimal amount, int month = 10) => new(new DateOnly(2026, 9, 14), _merchantId, _subcategoryId, _accountId, new Money(amount), Month: new AccountingMonth(2026, month), CardExternalId: _cardId);

    private sealed class InvoiceClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.CreateCustomTimeZone("test-brt", TimeSpan.FromHours(-3), "BRT", "BRT");
    }

    [Fact]
    public async Task GroupsOnlyCardEntriesUsingSavedMonthAndExactComposition()
    {
        var entries = Provider.GetRequiredService<IEntryCatalog>();
        Assert.Empty(await Catalog.GetAsync());
        Assert.True((await entries.CreateAsync(Purchase(100.01m))).Succeeded);
        Assert.True((await entries.CreateAsync(Purchase(149.99m))).Succeeded);
        Assert.True((await entries.CreateAsync(Purchase(50, 11))).Succeeded);
        Assert.True((await entries.CreateAsync(Purchase(999) with { CardExternalId = null })).Succeeded);
        var invoices = await Catalog.GetAsync();
        Assert.Equal(2, invoices.Count);
        var october = invoices.Single(x => x.Month == new AccountingMonth(2026, 10));
        Assert.Equal(new Money(250), october.Total);
        Assert.Equal(2, october.Entries.Count);
        Assert.Equal(october.Total.Amount, -october.Entries.Sum(x => x.Amount.Amount));
        Assert.Equal(new Money(50), invoices.Single(x => x.Month == new AccountingMonth(2026, 11)).Total);
        Assert.Equal(4, (await entries.GetAsync()).Count);
    }

    [Fact]
    public async Task CardsAreIndependentAndArchivedHistoryRemainsVisible()
    {
        var accounts = Provider.GetRequiredService<IAccountCatalog>();
        var otherCard = await accounts.SaveCreditCardAsync(new(null, _accountId, "Outro Cartão", CardBrand.Elo, new Money(1000), 3, 10, true, "green"));
        var entries = Provider.GetRequiredService<IEntryCatalog>();
        await entries.CreateAsync(Purchase(100));
        await entries.CreateAsync(Purchase(80) with { CardExternalId = otherCard.ExternalId });
        await accounts.DeactivateCreditCardAsync(_cardId);
        await accounts.DeactivateAccountAsync(_accountId);
        var invoices = await Catalog.GetAsync();
        Assert.Equal(2, invoices.Count);
        Assert.Equal(new Money(100), invoices.Single(x => x.CardExternalId == _cardId).Total);
        Assert.Equal(new Money(80), invoices.Single(x => x.CardExternalId == otherCard.ExternalId).Total);
    }
}
