using Ags.Finance.Modules.Accounts;
using Ags.Finance.Modules.Accounts.Contracts;
using Ags.Finance.Modules.Imports.Application.Csv;
using Ags.Finance.Modules.Imports.Contracts;
using Ags.Finance.Modules.Reimbursements;
using Ags.Finance.Modules.Transactions;
using Ags.Finance.Modules.Transactions.Contracts;
using Ags.Finance.SharedKernel;
using Ags.Finance.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace Ags.Finance.Modules.Imports.Tests;

/// <summary>Banco PostgreSQL isolado com os módulos cujos cadastros o CSV cobre, já migrados.</summary>
public abstract class ImportsFixture : IAsyncLifetime
{
    private PostgresTestDatabase? _database;
    private ServiceProvider? _provider;

    protected PostgresTestDatabase Database => _database ?? throw new InvalidOperationException();
    protected ServiceProvider Provider => _provider ?? throw new InvalidOperationException();

    public virtual async Task InitializeAsync()
    {
        _database = await PostgresTestDatabase.CreateAsync();
        _provider = Build(Database.ConnectionString).BuildServiceProvider();
        await Provider.MigrateAccountsModuleAsync();
        await Provider.MigrateTransactionsModuleAsync();
        await Provider.MigrateReimbursementsModuleAsync();
    }

    public async Task DisposeAsync()
    {
        if (_provider is not null) { await _provider.DisposeAsync(); }
        if (_database is not null) { await _database.DisposeAsync(); }
    }

    protected static IServiceCollection Build(string connectionString)
        => new ServiceCollection().AddAccountsModule(connectionString).AddTransactionsModule(connectionString).AddReimbursementsModule(connectionString).AddImportsModule();

    protected T Get<T>() where T : notnull => Provider.GetRequiredService<T>();

    internal static CsvTable Parse(CsvFile file, RegistryKind kind)
    {
        var layout = Application.Registries.RegistryLayout.For(kind);
        var result = CsvReader.Parse(file.Content, layout.Type, layout.Version, layout.Columns);
        Assert.True(result.Succeeded, result.Error);
        return result.Table!;
    }

    /// <summary>Cria um conjunto fictício com um cadastro de cada tipo, um arquivado e textos difíceis.</summary>
    protected async Task<Seed> SeedAsync()
    {
        var accounts = Get<IAccountCatalog>();
        var bank = (await accounts.CreateBankAsync("Banco \"São João\", Filial")).ExternalId!.Value;
        var account = (await accounts.SaveAccountAsync(new(null, bank, "=Conta\nPrincipal", AccountKind.Checking, "green", new Money(1234.56m), new DateOnly(2026, 1, 1)))).ExternalId!.Value;
        var archivedAccount = (await accounts.SaveAccountAsync(new(null, bank, "Conta Antiga", AccountKind.Savings, "slate", Money.Zero, new DateOnly(2025, 1, 1)))).ExternalId!.Value;
        var card = (await accounts.SaveCreditCardAsync(new(null, account, "Cartão Exemplo", CardBrand.Mastercard, new Money(5000.01m), 3, 10, true, "violet"))).ExternalId!.Value;
        var final = (await Get<ICardLastDigitsCatalog>().SaveAsync(new(null, card, "1234", CardUsage.Shared, "@Pessoa, \"apelido\""))).ExternalId!.Value;
        var classifications = Get<IClassificationCatalog>();
        var category = (await classifications.SaveCategoryAsync(null, "Mercado", Ags.Finance.Modules.Transactions.Contracts.CategoryKind.Expense)).ExternalId!.Value;
        var subcategory = (await classifications.SaveSubcategoryAsync(null, category, "Feira")).ExternalId!.Value;
        var merchant = (await Get<IMerchantCatalog>().SaveAsync(null, "Padaria Ação", subcategory)).ExternalId!.Value;
        var person = (await Get<Ags.Finance.Modules.Reimbursements.Contracts.IPersonCatalog>().SaveAsync(null, "Maria Exemplo", "Maria, pagadora")).ExternalId!.Value;
        Assert.True((await accounts.DeactivateAccountAsync(archivedAccount)).Succeeded);
        return new(bank, account, archivedAccount, card, final, category, subcategory, merchant, person);
    }

    protected sealed record Seed(Guid Bank, Guid Account, Guid ArchivedAccount, Guid Card, Guid Final, Guid Category, Guid Subcategory, Guid Merchant, Guid Person);
}
