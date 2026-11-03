using Ags.Finance.Modules.Accounts;
using Ags.Finance.Modules.Accounts.Contracts;
using Ags.Finance.Modules.Attachments;
using Ags.Finance.Modules.Transactions.Contracts;
using Ags.Finance.SharedKernel;
using Ags.Finance.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace Ags.Finance.Modules.Transactions.Tests;

public sealed partial class EntryTests : IAsyncLifetime
{
    private readonly string _attachmentsDirectory = Path.Combine(Path.GetTempPath(), "ags-entry-attachments-" + Guid.NewGuid().ToString("N"));
    private PostgresTestDatabase? _database;
    private ServiceProvider? _provider;
    private ServiceProvider Provider => _provider ?? throw new InvalidOperationException();
    private IEntryCatalog Entries => Provider.GetRequiredService<IEntryCatalog>();
    private Guid _accountId;
    private Guid _subcategoryId;
    private Guid _merchantId;
    private readonly ManualTimeProvider _clock = new() { Now = new DateTimeOffset(2026, 11, 2, 12, 0, 0, TimeSpan.Zero) };

    public async Task InitializeAsync()
    {
        _database = await PostgresTestDatabase.CreateAsync();
        Directory.CreateDirectory(_attachmentsDirectory);
        _provider = new ServiceCollection().AddSingleton<TimeProvider>(_clock).AddAttachmentsModule(_database.ConnectionString, new(_attachmentsDirectory)).AddAccountsModule(_database.ConnectionString).AddTransactionsModule(_database.ConnectionString).BuildServiceProvider();
        await Provider.MigrateAttachmentsModuleAsync();
        await Provider.MigrateAccountsModuleAsync();
        await Provider.MigrateTransactionsModuleAsync();
        var accounts = Provider.GetRequiredService<IAccountCatalog>();
        var bank = await accounts.CreateBankAsync("Banco Exemplo");
        var account = await accounts.SaveAccountAsync(new(null, bank.ExternalId!.Value, "Conta Exemplo", AccountKind.Checking, "green", new Money(1000), new DateOnly(2026, 10, 1)));
        _accountId = account.ExternalId!.Value;
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

    private CreateEntryRequest Request() => new(new DateOnly(2026, 10, 5), _merchantId, _subcategoryId, _accountId, new Money(150));

    [Fact]
    public async Task ExpenseStartsEffectiveAndReducesBalanceWithoutDuplicatingOpeningAsIncome()
    {
        var result = await Entries.CreateAsync(Request());
        Assert.True(result.Succeeded, result.Error);
        var entry = Assert.Single(await Entries.GetAsync());
        Assert.Equal(new Money(-150), entry.Amount);
        Assert.Equal(new AccountingMonth(2026, 10), entry.Month);
        Assert.True(entry.IsEffective);
        Assert.Equal(entry.Date, entry.EffectiveDate);
        Assert.NotNull(entry.EffectiveAt);
        Assert.Equal(new Money(850), (await Entries.GetBalanceAsync(_accountId, new DateOnly(2026, 10, 5)))?.EffectiveBalance);
    }

    [Fact]
    public async Task IncomeIsPositiveAndManualMonthIsPreserved()
    {
        var classifications = Provider.GetRequiredService<IClassificationCatalog>();
        var category = await classifications.SaveCategoryAsync(null, "Receitas", CategoryKind.Income);
        var subcategory = await classifications.SaveSubcategoryAsync(null, category.ExternalId!.Value, "Recebimentos");
        var result = await Entries.CreateAsync(Request() with { SubcategoryExternalId = subcategory.ExternalId!.Value, Month = new AccountingMonth(2026, 11) });
        Assert.True(result.Succeeded, result.Error);
        var entry = Assert.Single(await Entries.GetAsync());
        Assert.Equal(new Money(150), entry.Amount);
        Assert.Equal(new AccountingMonth(2026, 11), entry.Month);
        Assert.Equal(new DateOnly(2026, 10, 5), entry.Date);
        Assert.Equal(new Money(1150), (await Entries.GetBalanceAsync(_accountId, entry.Date))?.EffectiveBalance);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task NonPositiveAmountIsRejectedWithoutPersistence(int amount)
    {
        Assert.False((await Entries.CreateAsync(Request() with { Amount = new Money(amount) })).Succeeded);
        Assert.Empty(await Entries.GetAsync());
    }

    [Fact]
    public async Task UnknownAndInactiveReferencesAreRejected()
    {
        Assert.False((await Entries.CreateAsync(Request() with { MerchantExternalId = Guid.NewGuid() })).Succeeded);
        Assert.False((await Entries.CreateAsync(Request() with { SubcategoryExternalId = Guid.NewGuid() })).Succeeded);
        Assert.False((await Entries.CreateAsync(Request() with { AccountExternalId = Guid.NewGuid() })).Succeeded);
        await Provider.GetRequiredService<IAccountCatalog>().DeactivateAccountAsync(_accountId);
        Assert.False((await Entries.CreateAsync(Request())).Succeeded);
        Assert.Empty(await Entries.GetAsync());
    }

    [Fact]
    public async Task OpeningReferenceExcludesEarlierMovementsFromCashButPreservesTheirHistory()
    {
        await Entries.CreateAsync(Request() with { Date = new DateOnly(2026, 9, 30) });
        await Entries.CreateAsync(Request());
        Assert.Equal(2, (await Entries.GetAsync()).Count);
        Assert.Equal(Money.Zero, (await Entries.GetBalanceAsync(_accountId, new DateOnly(2026, 9, 30)))?.EffectiveBalance);
        Assert.Equal(new Money(1000), (await Entries.GetBalanceAsync(_accountId, new DateOnly(2026, 10, 1)))?.EffectiveBalance);
        Assert.Equal(new Money(850), (await Entries.GetBalanceAsync(_accountId, new DateOnly(2026, 10, 5)))?.EffectiveBalance);
    }

    [Fact]
    public async Task MerchantDefaultIsEditableAndMergeTransfersExistingReferencesAtomically()
    {
        Assert.True((await Entries.CreateAsync(Request() with { SubcategoryExternalId = Guid.Empty })).Succeeded);
        var merchants = Provider.GetRequiredService<IMerchantCatalog>();
        var target = await merchants.SaveAsync(null, "Loja Mantida", null);
        Assert.True((await merchants.MergeAsync(_merchantId, target.ExternalId!.Value)).Succeeded);
        var entry = Assert.Single(await Entries.GetAsync());
        Assert.Equal(target.ExternalId, entry.MerchantExternalId);
        Assert.Equal("Loja Mantida", entry.MerchantName);
        Assert.Equal(_subcategoryId, entry.SubcategoryExternalId);
    }

    [Fact]
    public async Task InactiveMerchantAndClassificationRemainInHistory()
    {
        await Entries.CreateAsync(Request());
        await Provider.GetRequiredService<IMerchantCatalog>().DeactivateAsync(_merchantId);
        await Provider.GetRequiredService<IClassificationCatalog>().DeactivateAsync(_subcategoryId, true);
        Assert.False((await Entries.CreateAsync(Request())).Succeeded);
        Assert.Single(await Entries.GetAsync());
    }
}
