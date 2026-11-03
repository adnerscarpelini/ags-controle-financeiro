using Ags.Finance.Modules.Accounts;
using Ags.Finance.Modules.Accounts.Contracts;
using Ags.Finance.Modules.Invoices;
using Ags.Finance.Modules.Preferences;
using Ags.Finance.Modules.Reimbursements;
using Ags.Finance.Modules.Transactions;
using Ags.Finance.Modules.Transactions.Contracts;
using Ags.Finance.SharedKernel;
using Ags.Finance.Tests.Support;
using Ags.Finance.Web.Components.Pages;
using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;

namespace Ags.Finance.Web.Tests;

/// <summary>Tela de Contas em cards (#99), com transferências e ajustes de saldo em janela.</summary>
public sealed class AccountsPageTests
{
    private sealed record Scenario(PostgresTestDatabase Database, BunitContext Context, IRenderedComponent<MudDialogProvider> Dialogs, IRenderedComponent<MudPopoverProvider> Popovers, Guid Bank) : IAsyncDisposable
    {
        public IAccountCatalog Accounts => Context.Services.GetRequiredService<IAccountCatalog>();

        public IAccountMovementCatalog Movements => Context.Services.GetRequiredService<IAccountMovementCatalog>();

        public IEntryCatalog Entries => Context.Services.GetRequiredService<IEntryCatalog>();

        public async Task<Guid> AddAccountAsync(string name, decimal opening = 0, AccountKind kind = AccountKind.Checking) =>
            (await Accounts.SaveAccountAsync(new(null, Bank, name, kind, "green", kind == AccountKind.Checking ? new Money(opening) : null, kind == AccountKind.Checking ? new DateOnly(2026, 1, 1) : null))).ExternalId!.Value;

        public IRenderedComponent<Accounts> Open(int cards)
        {
            var page = Context.Render<Accounts>();
            page.WaitForAssertion(() => Assert.Equal(cards, page.FindAll("[data-account-card]").Count));
            return page;
        }

        public async Task ChooseAsync(IRenderedComponent<Accounts> page, string account, string action)
        {
            await page.Find($"[data-account-card='{account}'] [data-account-menu] button").ClickAsync(new MouseEventArgs());
            Popovers.WaitForAssertion(() => Assert.Contains(Popovers.FindAll(".mud-popover-open .mud-menu-item"), x => x.TextContent.Trim() == action));
            await Popovers.FindAll(".mud-popover-open .mud-menu-item").Single(x => x.TextContent.Trim() == action).ClickAsync(new MouseEventArgs());
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await Database.DisposeAsync();
        }
    }

    private static async Task<Scenario> CreateAsync()
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
        await context.Services.MigratePreferencesModuleAsync();
        await context.Services.MigrateTransactionsModuleAsync();
        await context.Services.MigrateReimbursementsModuleAsync();
        await context.Services.MigrateInvoicesModuleAsync();
        var bank = await context.Services.GetRequiredService<IAccountCatalog>().CreateBankAsync("Banco Exemplo");
        var popovers = context.Render<MudPopoverProvider>();
        return new(database, context, context.Render<MudDialogProvider>(), popovers, bank.ExternalId!.Value);
    }

    private static string Text(IRenderedComponent<Accounts> page, string account, string selector) =>
        page.Find($"[data-account-card='{account}'] {selector}").TextContent;

    [Fact]
    public async Task ThreeAccountsAppearInCardsWithBalancesAndTheSummaryShowsTheSum()
    {
        await using var scenario = await CreateAsync();
        var first = await scenario.AddAccountAsync("Conta Um", 100);
        await scenario.AddAccountAsync("Conta Dois", 200);
        await scenario.AddAccountAsync("Conta Três", 300);
        var classifications = scenario.Context.Services.GetRequiredService<IClassificationCatalog>();
        var category = await classifications.SaveCategoryAsync(null, "Casa", CategoryKind.Expense);
        var subcategory = await classifications.SaveSubcategoryAsync(null, category.ExternalId!.Value, "Contas");
        var merchant = await scenario.Context.Services.GetRequiredService<IMerchantCatalog>().SaveAsync(null, "Descrição Exemplo", subcategory.ExternalId);
        await scenario.Entries.CreateAsync(new(new DateOnly(2026, 9, 20), merchant.ExternalId!.Value, subcategory.ExternalId!.Value, first, new Money(30), IsPaid: false));

        var page = scenario.Open(3);

        Assert.Equal("R$ 100,00", Text(page, "Conta Um", "[data-current-balance]"));
        Assert.Equal("R$ 70,00", Text(page, "Conta Um", "[data-forecast-balance]"));
        Assert.Equal("R$ 300,00", Text(page, "Conta Três", "[data-current-balance]"));
        Assert.Equal("R$ 600,00", page.Find("[data-stat='current-total'] .stat-value").TextContent);
        Assert.Equal("R$ 570,00", page.Find("[data-stat='forecast-total'] .stat-value").TextContent);
        Assert.Contains("Ajuda: Saldo atual com as pendências do mês desta conta.", page.Find("[data-account-card='Conta Um'] .help-tip-button").GetAttribute("aria-label"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ANegativeBalanceShowsTheMinusSignAsWellAsTheColor()
    {
        await using var scenario = await CreateAsync();
        await scenario.AddAccountAsync("Conta Negativa", -50);

        var page = scenario.Open(1);

        var value = page.Find("[data-account-card='Conta Negativa'] [data-current-balance]");
        Assert.Equal("-R$ 50,00", value.TextContent);
        Assert.Contains("entry-expense", value.ClassList);
    }

    [Fact]
    public async Task AddExpenseOpensTheWindowWithTheAccountSelected()
    {
        await using var scenario = await CreateAsync();
        var account = await scenario.AddAccountAsync("Conta Um", 100);
        var page = scenario.Open(1);

        await page.Find("[data-add-expense]").ClickAsync(new MouseEventArgs());

        scenario.Dialogs.WaitForAssertion(() => Assert.Equal(account.ToString(), scenario.Dialogs.Find("#entry-account").GetAttribute("value")));
    }

    [Fact]
    public async Task ArchivedAccountsAppearIdentifiedOnRequestAndCanBeRestored()
    {
        await using var scenario = await CreateAsync();
        var account = await scenario.AddAccountAsync("Conta Antiga", 100);
        await scenario.AddAccountAsync("Conta Atual", 100);
        await scenario.Accounts.DeactivateAccountAsync(account);
        var page = scenario.Open(1);

        await page.Find("[data-toggle-archived]").ClickAsync(new MouseEventArgs());
        page.WaitForAssertion(() => Assert.Equal(2, page.FindAll("[data-account-card]").Count));
        Assert.Equal("Arquivada", Text(page, "Conta Antiga", "[data-archived-tag]"));
        Assert.Empty(page.FindAll("[data-account-card='Conta Antiga'] [data-add-expense]"));

        _ = scenario.ChooseAsync(page, "Conta Antiga", "Restaurar");
        scenario.Dialogs.WaitForAssertion(() => Assert.NotEmpty(scenario.Dialogs.FindAll("[data-confirm-ok]")));
        await scenario.Dialogs.Find("[data-confirm-ok]").ClickAsync(new MouseEventArgs());

        page.WaitForAssertion(() => Assert.Empty(page.FindAll("[data-archived-tag]")));
        Assert.Equal(2, (await scenario.Accounts.GetAccountsAsync()).Count);
    }

    [Fact]
    public async Task ArchivingAsksForConfirmationAndKeepsTheAccountInTheHistory()
    {
        await using var scenario = await CreateAsync();
        var account = await scenario.AddAccountAsync("Conta Um", 100);
        await scenario.AddAccountAsync("Conta Dois", 100);
        var page = scenario.Open(2);

        _ = scenario.ChooseAsync(page, "Conta Um", "Arquivar");
        scenario.Dialogs.WaitForAssertion(() => Assert.Contains("deixará de aparecer nas seleções", scenario.Dialogs.Find(".confirm-message").TextContent, StringComparison.Ordinal));
        await scenario.Dialogs.Find("[data-confirm-cancel]").ClickAsync(new MouseEventArgs());
        Assert.Equal(2, (await scenario.Accounts.GetAccountsAsync()).Count);

        _ = scenario.ChooseAsync(page, "Conta Um", "Arquivar");
        scenario.Dialogs.WaitForAssertion(() => Assert.NotEmpty(scenario.Dialogs.FindAll("[data-confirm-ok]")));
        await scenario.Dialogs.Find("[data-confirm-ok]").ClickAsync(new MouseEventArgs());

        page.WaitForAssertion(() => Assert.Single(page.FindAll("[data-account-card]")));
        Assert.Contains(await scenario.Accounts.GetAccountsAsync(false), x => x.ExternalId == account && !x.IsActive);
    }

    [Fact]
    public async Task ANewAccountIsCreatedInTheWindowAndAppearsAsACard()
    {
        await using var scenario = await CreateAsync();
        var page = scenario.Context.Render<Accounts>();
        page.WaitForElement(".empty-state");

        await page.Find(".empty-state button").ClickAsync(new MouseEventArgs());
        await scenario.Dialogs.WaitForElement("#account-bank").ChangeAsync(scenario.Bank.ToString());
        await scenario.Dialogs.Find("#account-name").InputAsync("Conta Nova");
        await scenario.Dialogs.Find("#account-color").ChangeAsync("green");
        scenario.Dialogs.Find("#opening-balance").Input("1250,50");
        await scenario.Dialogs.Find("#opening-date").ChangeAsync("2026-09-01");
        scenario.Dialogs.WaitForAssertion(() => Assert.False(scenario.Dialogs.Find("[data-save]").HasAttribute("disabled")));
        await scenario.Dialogs.Find("#account-form").SubmitAsync(EventArgs.Empty);

        page.WaitForAssertion(() => Assert.Equal("R$ 1.250,50", Text(page, "Conta Nova", "[data-current-balance]")));
        Assert.Empty(scenario.Dialogs.FindAll("#account-form"));
    }

    [Fact]
    public async Task AnErrorWhenSavingAppearsInTheWindowWithoutLosingTheData()
    {
        await using var scenario = await CreateAsync();
        await scenario.AddAccountAsync("Conta Um");
        var page = scenario.Open(1);
        await page.Find("[data-new-account]").ClickAsync(new MouseEventArgs());
        await scenario.Dialogs.WaitForElement("#account-name").InputAsync("Conta sem banco");

        await scenario.Dialogs.Find("#account-form").SubmitAsync(EventArgs.Empty);

        scenario.Dialogs.WaitForAssertion(() => Assert.Contains("banco cadastrado", scenario.Dialogs.Find("[data-entry-error]").TextContent, StringComparison.Ordinal));
        Assert.Equal("Conta sem banco", scenario.Dialogs.Find("#account-name").GetAttribute("value"));
    }

    [Fact]
    public async Task EditingKeepsTheRecordedOpeningLocked()
    {
        await using var scenario = await CreateAsync();
        await scenario.AddAccountAsync("Conta Um", 100);
        var page = scenario.Open(1);

        _ = scenario.ChooseAsync(page, "Conta Um", "Editar");

        scenario.Dialogs.WaitForAssertion(() => Assert.Equal("Conta Um", scenario.Dialogs.Find("#account-name").GetAttribute("value")));
        Assert.True(scenario.Dialogs.Find("#opening-balance").HasAttribute("disabled"));
        Assert.True(scenario.Dialogs.Find("#opening-date").HasAttribute("disabled"));
    }

    [Fact]
    public async Task NoAccountGuidesToRegisterTheFirst()
    {
        await using var scenario = await CreateAsync();

        var page = scenario.Context.Render<Accounts>();

        page.WaitForAssertion(() => Assert.Contains("Cadastre a primeira", page.Find(".empty-state").TextContent, StringComparison.Ordinal));
    }

    [Fact]
    public async Task TransferRequiresConfirmationAndUpdatesTheBalancesAndTheHistory()
    {
        await using var scenario = await CreateAsync();
        var source = await scenario.AddAccountAsync("Corrente Exemplo", 1000);
        var destination = await scenario.AddAccountAsync("Reserva Exemplo", kind: AccountKind.Investment);
        var page = scenario.Open(2);

        await page.Find("[data-new-transfer]").ClickAsync(new MouseEventArgs());
        scenario.Dialogs.WaitForAssertion(() => Assert.Contains("Corrente Exemplo", scenario.Dialogs.Find("#transfer-source").TextContent, StringComparison.Ordinal));
        scenario.Dialogs.Find("#transfer-amount").Input("300");
        await scenario.Dialogs.Find("#transfer-source").ChangeAsync(source.ToString());
        await scenario.Dialogs.Find("#transfer-destination").ChangeAsync(destination.ToString());

        _ = scenario.Dialogs.Find("#transfer-form").SubmitAsync(EventArgs.Empty);
        scenario.Dialogs.WaitForAssertion(() => Assert.Contains("R$ 300,00", scenario.Dialogs.Find(".confirm-message").TextContent, StringComparison.Ordinal));
        await scenario.Dialogs.Find("[data-confirm-cancel]").ClickAsync(new MouseEventArgs());
        scenario.Dialogs.WaitForAssertion(() => Assert.Empty(scenario.Dialogs.FindAll(".confirm-message")));
        Assert.Empty(await scenario.Movements.GetAsync());

        scenario.Dialogs.WaitForAssertion(() => Assert.False(scenario.Dialogs.Find("[data-save]").HasAttribute("disabled")));
        _ = scenario.Dialogs.Find("#transfer-form").SubmitAsync(EventArgs.Empty);
        scenario.Dialogs.WaitForAssertion(() => Assert.NotEmpty(scenario.Dialogs.FindAll("[data-confirm-ok]")));
        await scenario.Dialogs.Find("[data-confirm-ok]").ClickAsync(new MouseEventArgs());

        page.WaitForAssertion(() => Assert.Equal("R$ 700,00", Text(page, "Corrente Exemplo", "[data-current-balance]")));
        Assert.Equal(2, (await scenario.Movements.GetAsync()).Count);

        _ = scenario.ChooseAsync(page, "Corrente Exemplo", "Transferências");
        scenario.Dialogs.WaitForAssertion(() => Assert.Contains("Transferência", scenario.Dialogs.Find("[aria-label='Movimentações de contas']").TextContent, StringComparison.Ordinal));
        Assert.Contains("-R$ 300,00", scenario.Dialogs.Find("[aria-label='Movimentações de contas']").TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheMovementsWindowOffersATransferFromThatAccount()
    {
        await using var scenario = await CreateAsync();
        var source = await scenario.AddAccountAsync("Corrente Exemplo", 1000);
        var page = scenario.Open(1);

        _ = scenario.ChooseAsync(page, "Corrente Exemplo", "Transferências");
        scenario.Dialogs.WaitForElement("[data-dialog-done]");
        await scenario.Dialogs.Find("[data-new-transfer]").ClickAsync(new MouseEventArgs());

        scenario.Dialogs.WaitForAssertion(() => Assert.Equal(source.ToString(), scenario.Dialogs.Find("#transfer-source").GetAttribute("value")));
    }

    [Fact]
    public async Task BalanceAdjustmentShowsTheDifferenceAndCanBeCancelledBeforeConfirming()
    {
        await using var scenario = await CreateAsync();
        await scenario.AddAccountAsync("Corrente Exemplo", 1000);
        var page = scenario.Open(1);

        _ = scenario.ChooseAsync(page, "Corrente Exemplo", "Ajustar saldo");
        scenario.Dialogs.WaitForElement("#adjust-balance");
        scenario.Dialogs.Find("#adjust-balance").Input("1150");
        scenario.Dialogs.WaitForAssertion(() => Assert.Equal("1150", scenario.Dialogs.Find("#adjust-balance").GetAttribute("value")?.Replace(".", string.Empty, StringComparison.Ordinal).Split(',')[0]));
        await scenario.Dialogs.Find("#balance-adjustment").SubmitAsync(EventArgs.Empty);
        var review = scenario.Dialogs.Find("[data-adjustment-review]").TextContent;
        Assert.Contains("R$ 1.000,00", review, StringComparison.Ordinal);
        Assert.Contains("R$ 150,00", review, StringComparison.Ordinal);

        await scenario.Dialogs.Find("[data-dialog-cancel]").ClickAsync(new MouseEventArgs());
        Assert.Empty(await scenario.Movements.GetAsync());

        _ = scenario.ChooseAsync(page, "Corrente Exemplo", "Ajustar saldo");
        scenario.Dialogs.WaitForElement("#adjust-balance");
        scenario.Dialogs.Find("#adjust-balance").Input("1150");
        scenario.Dialogs.WaitForAssertion(() => Assert.Equal("1150", scenario.Dialogs.Find("#adjust-balance").GetAttribute("value")?.Replace(".", string.Empty, StringComparison.Ordinal).Split(',')[0]));
        await scenario.Dialogs.Find("#balance-adjustment").SubmitAsync(EventArgs.Empty);
        await scenario.Dialogs.Find("[data-confirm-ok]").ClickAsync(new MouseEventArgs());

        Assert.Equal(AccountMovementKind.BalanceAdjustment, Assert.Single(await scenario.Movements.GetAsync()).Kind);
        page.WaitForAssertion(() => Assert.Equal("R$ 1.150,00", Text(page, "Corrente Exemplo", "[data-current-balance]")));
    }

    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);
    }
}
