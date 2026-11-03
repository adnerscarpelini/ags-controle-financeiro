using Ags.Finance.Modules.Accounts;
using Ags.Finance.Modules.Accounts.Contracts;
using Ags.Finance.Modules.Transactions;
using Ags.Finance.Modules.Transactions.Contracts;
using Ags.Finance.SharedKernel;
using Ags.Finance.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace Ags.Finance.Modules.Reimbursements.Tests;

/// <summary>Banco PostgreSQL isolado com os módulos que o reembolso consome já migrados.</summary>
public abstract class ReimbursementFixture : IAsyncLifetime
{
    private PostgresTestDatabase? _database;
    private ServiceProvider? _provider;

    protected PostgresTestDatabase Database => _database ?? throw new InvalidOperationException();
    protected ServiceProvider Provider => _provider ?? throw new InvalidOperationException();
    protected Guid SubcategoryId { get; private set; }
    protected Guid AccountId { get; private set; }
    protected Guid MerchantId { get; private set; }

    public virtual async Task InitializeAsync()
    {
        _database = await PostgresTestDatabase.CreateAsync();
        _provider = Build(new ServiceCollection()).BuildServiceProvider();
        await Provider.MigrateAccountsModuleAsync();
        await Provider.MigrateTransactionsModuleAsync();
        await Provider.MigrateReimbursementsModuleAsync();
        var accounts = Provider.GetRequiredService<IAccountCatalog>();
        var bank = await accounts.CreateBankAsync("Banco Exemplo");
        AccountId = (await accounts.SaveAccountAsync(new(null, bank.ExternalId!.Value, "Conta Exemplo", AccountKind.Checking, "green", new Money(1000), new DateOnly(2026, 1, 1)))).ExternalId!.Value;
        var classifications = Provider.GetRequiredService<IClassificationCatalog>();
        var category = await classifications.SaveCategoryAsync(null, "Casa", CategoryKind.Expense);
        SubcategoryId = (await classifications.SaveSubcategoryAsync(null, category.ExternalId!.Value, "Internet")).ExternalId!.Value;
        MerchantId = (await Provider.GetRequiredService<IMerchantCatalog>().SaveAsync(null, "Provedor Exemplo", SubcategoryId)).ExternalId!.Value;
    }

    public async Task DisposeAsync()
    {
        if (_provider is not null) { await _provider.DisposeAsync(); }
        if (_database is not null) { await _database.DisposeAsync(); }
    }

    protected IServiceCollection Build(IServiceCollection services)
        => services.AddAccountsModule(Database.ConnectionString).AddTransactionsModule(Database.ConnectionString).AddReimbursementsModule(Database.ConnectionString);

}
