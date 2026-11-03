using Ags.Finance.Modules.Accounts;
using Ags.Finance.Modules.Accounts.Contracts;
using Ags.Finance.Modules.Invoices;
using Ags.Finance.Modules.Preferences;
using Ags.Finance.Modules.Reimbursements;
using Ags.Finance.Modules.Reimbursements.Contracts;
using Ags.Finance.Modules.Transactions;
using Ags.Finance.Modules.Transactions.Contracts;
using Ags.Finance.SharedKernel;
using Ags.Finance.Tests.Support;
using Ags.Finance.Web.Components.Common;
using Ags.Finance.Web.Components.Entries;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;

namespace Ags.Finance.Web.Tests;

/// <summary>Cenário de tela de lançamentos com dados fictícios, banco real e os provedores de janela e tooltip.</summary>
internal sealed class EntryUi : IAsyncDisposable
{
    private readonly PostgresTestDatabase _database;

    private EntryUi(PostgresTestDatabase database, BunitContext context)
    {
        _database = database;
        Context = context;
    }

    public BunitContext Context { get; }

    public IRenderedComponent<MudDialogProvider> Dialogs { get; private set; } = default!;

    public IRenderedComponent<MudPopoverProvider> Popovers { get; private set; } = default!;

    public Guid Account { get; private set; }

    public Guid Card { get; private set; }

    public Guid ExpenseSubcategory { get; private set; }

    public Guid IncomeSubcategory { get; private set; }

    public Guid Merchant { get; private set; }

    public Guid IncomeMerchant { get; private set; }

    public IEntryCatalog Entries => Context.Services.GetRequiredService<IEntryCatalog>();

    public static async Task<EntryUi> CreateAsync()
    {
        var database = await PostgresTestDatabase.CreateAsync();
        var context = new BunitContext().UseAppUi();
        context.Services.AddSingleton<TimeProvider>(new FixedClock())
            .AddAccountsModule(database.ConnectionString)
            .AddTransactionsModule(database.ConnectionString)
            .AddReimbursementsModule(database.ConnectionString)
            .AddInvoicesModule(database.ConnectionString)
            .AddPreferencesModule(database.ConnectionString);
        await context.Services.MigrateAccountsModuleAsync();
        await context.Services.MigrateTransactionsModuleAsync();
        await context.Services.MigrateReimbursementsModuleAsync();
        await context.Services.MigrateInvoicesModuleAsync();
        await context.Services.MigratePreferencesModuleAsync();

        var ui = new EntryUi(database, context);
        await ui.SeedAsync();
        ui.Popovers = context.Render<MudPopoverProvider>();
        ui.Dialogs = context.Render<MudDialogProvider>();
        return ui;
    }

    public Task OpenNewAsync(EntryDialogType type, Guid? account = null) =>
        OpenAsync(windows => windows.ShowNewAsync(type, account));

    public Task OpenEditAsync(EntryDetails entry) =>
        OpenAsync(windows => windows.ShowEditAsync(entry));

    public async Task OpenTransferAsync(Guid? source = null)
    {
        await Dialogs.InvokeAsync(() => Context.Services.GetRequiredService<EntryDialogs>().ShowTransferAsync(source));
        Dialogs.WaitForAssertion(() => Assert.Contains("Corrente Exemplo", Dialogs.Find("#transfer-source").TextContent, StringComparison.Ordinal));
    }

    public void SetAmount(string value, string field = "#entry-amount")
    {
        Dialogs.Find(field).Input(value);
        Dialogs.WaitForAssertion(() => Assert.Equal(decimal.Parse(value, System.Globalization.CultureInfo.GetCultureInfo("pt-BR")), Dialogs.FindComponent<MoneyField>().Instance.Value));
    }

    public async Task SetMerchantAsync(string name = "Loja Exemplo") => await Dialogs.Find("#entry-merchant").InputAsync(name);

    public async Task FillExpenseAsync(string amount = "150")
    {
        SetAmount(amount);
        await SetMerchantAsync();
        await Dialogs.Find("#entry-account").ChangeAsync(Account.ToString());
    }

    public Task SubmitAsync() => Dialogs.Find("#entry-form").SubmitAsync(EventArgs.Empty);

    public async ValueTask DisposeAsync()
    {
        await Context.DisposeAsync();
        await _database.DisposeAsync();
    }

    private async Task OpenAsync(Func<EntryDialogs, Task<IDialogReference>> open)
    {
        await Dialogs.InvokeAsync(() => open(Context.Services.GetRequiredService<EntryDialogs>()));
        Dialogs.WaitForAssertion(() => Assert.Equal("true", Dialogs.Find("#entry-form").GetAttribute("data-ready")));
    }

    private async Task SeedAsync()
    {
        var accounts = Context.Services.GetRequiredService<IAccountCatalog>();
        var bank = await accounts.CreateBankAsync("Banco Exemplo");
        Account = (await accounts.SaveAccountAsync(new(null, bank.ExternalId!.Value, "Corrente Exemplo", AccountKind.Checking, "green"))).ExternalId!.Value;
        Card = (await accounts.SaveCreditCardAsync(new(null, Account, "Cartão Exemplo", CardBrand.Visa, new Money(10000), 3, 10, true, "green"))).ExternalId!.Value;
        var classifications = Context.Services.GetRequiredService<IClassificationCatalog>();
        var expense = await classifications.SaveCategoryAsync(null, "Mercado", CategoryKind.Expense);
        ExpenseSubcategory = (await classifications.SaveSubcategoryAsync(null, expense.ExternalId!.Value, "Feira")).ExternalId!.Value;
        var income = await classifications.SaveCategoryAsync(null, "Renda", CategoryKind.Income);
        IncomeSubcategory = (await classifications.SaveSubcategoryAsync(null, income.ExternalId!.Value, "Salário")).ExternalId!.Value;
        var merchants = Context.Services.GetRequiredService<IMerchantCatalog>();
        Merchant = (await merchants.SaveAsync(null, "Loja Exemplo", ExpenseSubcategory)).ExternalId!.Value;
        IncomeMerchant = (await merchants.SaveAsync(null, "Empresa Exemplo", IncomeSubcategory)).ExternalId!.Value;
    }

    public async Task<Guid> AddPersonAsync(decimal percent = 50m) =>
        (await Context.Services.GetRequiredService<IPersonCatalog>().SaveAsync(null, "Pessoa Exemplo", percent)).ExternalId!.Value;

    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);
    }
}
