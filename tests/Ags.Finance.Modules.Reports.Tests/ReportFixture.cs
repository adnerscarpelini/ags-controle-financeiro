using Ags.Finance.Modules.Accounts;
using Ags.Finance.Modules.Accounts.Contracts;
using Ags.Finance.Modules.Invoices;
using Ags.Finance.Modules.Preferences;
using Ags.Finance.Modules.Transactions;
using Ags.Finance.Modules.Transactions.Contracts;
using Ags.Finance.SharedKernel;
using Ags.Finance.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace Ags.Finance.Modules.Reports.Tests;

/// <summary>Banco PostgreSQL isolado com Accounts, Transactions e Reports, e um relógio fixo em 02/10/2026 (faturas de outubro ainda abertas).</summary>
public abstract class ReportFixture : IAsyncLifetime
{
    private PostgresTestDatabase? _database;
    private ServiceProvider? _provider;

    protected PostgresTestDatabase Database => _database ?? throw new InvalidOperationException();
    protected ServiceProvider Provider => _provider ?? throw new InvalidOperationException();
    protected FixedClock Clock { get; } = new(new DateTimeOffset(2026, 10, 2, 12, 0, 0, TimeSpan.Zero));
    protected Guid AccountId { get; private set; }
    protected Guid MerchantId { get; private set; }
    protected Guid ExpenseSubcategoryId { get; private set; }
    protected Guid IncomeSubcategoryId { get; private set; }
    protected IEntryCatalog Entries => Provider.GetRequiredService<IEntryCatalog>();

    public virtual async Task InitializeAsync()
    {
        _database = await PostgresTestDatabase.CreateAsync();
        _provider = ConfigureServices(new ServiceCollection()).BuildServiceProvider();
        await Provider.MigrateAccountsModuleAsync();
        await Provider.MigrateTransactionsModuleAsync();
        await Provider.MigrateInvoicesModuleAsync();
        await Provider.MigratePreferencesModuleAsync();
        var accounts = Provider.GetRequiredService<IAccountCatalog>();
        var bank = await accounts.CreateBankAsync("Banco Exemplo");
        AccountId = (await accounts.SaveAccountAsync(new(null, bank.ExternalId!.Value, "Conta Exemplo", AccountKind.Checking, "green", new Money(1000), new DateOnly(2026, 1, 1)))).ExternalId!.Value;
        var classifications = Provider.GetRequiredService<IClassificationCatalog>();
        var expenseCategory = await classifications.SaveCategoryAsync(null, "Mercado", CategoryKind.Expense);
        ExpenseSubcategoryId = (await classifications.SaveSubcategoryAsync(null, expenseCategory.ExternalId!.Value, "Feira")).ExternalId!.Value;
        var incomeCategory = await classifications.SaveCategoryAsync(null, "Trabalho", CategoryKind.Income);
        IncomeSubcategoryId = (await classifications.SaveSubcategoryAsync(null, incomeCategory.ExternalId!.Value, "Salário")).ExternalId!.Value;
        MerchantId = (await Provider.GetRequiredService<IMerchantCatalog>().SaveAsync(null, "Loja Exemplo", ExpenseSubcategoryId)).ExternalId!.Value;
    }

    public async Task DisposeAsync()
    {
        if (_provider is not null) { await _provider.DisposeAsync(); }
        if (_database is not null) { await _database.DisposeAsync(); }
    }

    protected virtual IServiceCollection ConfigureServices(IServiceCollection services)
        => services.AddSingleton<TimeProvider>(Clock).AddAccountsModule(Database.ConnectionString).AddTransactionsModule(Database.ConnectionString).AddInvoicesModule(Database.ConnectionString).AddPreferencesModule(Database.ConnectionString).AddReportsModule();

    protected async Task<Guid> CardAsync(string name, string color = "green")
    {
        var result = await Provider.GetRequiredService<IAccountCatalog>().SaveCreditCardAsync(new(null, AccountId, name, CardBrand.Visa, new Money(10000), 3, 10, true, color));
        Assert.True(result.Succeeded, result.Error);
        return result.ExternalId!.Value;
    }

    protected async Task PurchaseAsync(Guid card, DateOnly date, decimal amount, int installments = 1)
    {
        var request = new CreateEntryRequest(date, MerchantId, ExpenseSubcategoryId, AccountId, new Money(amount), CardExternalId: card);
        var result = installments == 1 ? await Entries.CreateAsync(request) : await Entries.CreateInstallmentsAsync(request, installments);
        Assert.True(result.Succeeded, result.Error);
    }

    protected async Task<Guid> SubcategoryAsync(string category, string name, CategoryKind kind = CategoryKind.Expense)
    {
        var classifications = Provider.GetRequiredService<IClassificationCatalog>();
        var existing = (await classifications.GetCategoriesAsync()).FirstOrDefault(x => x.Name == category && x.Kind == kind);
        var categoryId = existing?.ExternalId ?? (await classifications.SaveCategoryAsync(null, category, kind)).ExternalId!.Value;
        return (await classifications.SaveSubcategoryAsync(null, categoryId, name)).ExternalId!.Value;
    }

    protected async Task<Guid> AddAsync(ReferenceMonth month, decimal amount, Guid? subcategory = null, bool income = false, int day = 5)
    {
        var result = await Entries.CreateAsync(new(new DateOnly(month.Year, month.Month, day), MerchantId, subcategory ?? (income ? IncomeSubcategoryId : ExpenseSubcategoryId), AccountId, new Money(amount), Month: month));
        Assert.True(result.Succeeded, result.Error);
        return result.ExternalId!.Value;
    }

    protected sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now;
        public TimeZoneInfo Zone { get; set; } = TimeZoneInfo.Utc;
        public override TimeZoneInfo LocalTimeZone => Zone;
    }
}
