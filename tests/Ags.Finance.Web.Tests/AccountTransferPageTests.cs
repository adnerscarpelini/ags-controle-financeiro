using Ags.Finance.Modules.Accounts;
using Ags.Finance.Modules.Accounts.Contracts;
using Ags.Finance.Modules.Transactions;
using Ags.Finance.Modules.Transactions.Contracts;
using Ags.Finance.SharedKernel;
using Ags.Finance.Tests.Support;
using Ags.Finance.Web.Components.Entries;
using Ags.Finance.Web.Components.Pages;
using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;

namespace Ags.Finance.Web.Tests;

public sealed class AccountTransferPageTests
{
    private sealed record Scenario(BunitContext Context, PostgresTestDatabase Database, IRenderedComponent<MudDialogProvider> Dialogs, Guid Source, Guid Destination) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await Database.DisposeAsync();
        }

        public async Task OpenAsync() => await Dialogs.InvokeAsync(() => Context.Services.GetRequiredService<EntryDialogs>().ShowTransferAsync());

        public IAccountMovementCatalog Movements => Context.Services.GetRequiredService<IAccountMovementCatalog>();

        public async Task FillAsync(string amount = "300")
        {
            Dialogs.WaitForAssertion(() => Assert.Contains("Corrente Exemplo", Dialogs.Find("#transfer-source").TextContent, StringComparison.Ordinal));
            Dialogs.Find("#transfer-amount").Input(amount);
            await Dialogs.Find("#transfer-source").ChangeAsync(Source.ToString());
            await Dialogs.Find("#transfer-destination").ChangeAsync(Destination.ToString());
        }
    }

    private static async Task<Scenario> CreateAsync()
    {
        var database = await PostgresTestDatabase.CreateAsync();
        var context = new BunitContext().UseAppUi();
        context.Services.AddAccountsModule(database.ConnectionString).AddTransactionsModule(database.ConnectionString);
        await context.Services.MigrateAccountsModuleAsync();
        await context.Services.MigrateTransactionsModuleAsync();
        var accounts = context.Services.GetRequiredService<IAccountCatalog>();
        var bank = await accounts.CreateBankAsync("Banco Exemplo");
        var today = DateOnly.FromDateTime(context.Services.GetRequiredService<TimeProvider>().GetLocalNow().DateTime);
        var source = await accounts.SaveAccountAsync(new(null, bank.ExternalId!.Value, "Corrente Exemplo", AccountKind.Checking, "green", new Money(1000), today));
        var destination = await accounts.SaveAccountAsync(new(null, bank.ExternalId.Value, "Reserva Exemplo", AccountKind.Investment, "green"));
        context.Render<MudPopoverProvider>();
        return new(context, database, context.Render<MudDialogProvider>(), source.ExternalId!.Value, destination.ExternalId!.Value);
    }

    [Fact]
    public async Task TransferRequiresConfirmationAndDisplaysBothLegsAndBalances()
    {
        await using var scenario = await CreateAsync();
        var page = scenario.Context.Render<AccountTransfers>();
        page.WaitForAssertion(() => Assert.Contains("R$ 1.000,00", page.Markup, StringComparison.Ordinal));
        await page.Find("[data-new-transfer]").ClickAsync(new MouseEventArgs());
        await scenario.FillAsync();

        _ = scenario.Dialogs.Find("#transfer-form").SubmitAsync(EventArgs.Empty);
        scenario.Dialogs.WaitForAssertion(() => Assert.Contains("R$ 300,00", scenario.Dialogs.Find(".confirm-message").TextContent, StringComparison.Ordinal));
        await scenario.Dialogs.Find("[data-confirm-cancel]").ClickAsync(new MouseEventArgs());
        scenario.Dialogs.WaitForAssertion(() => Assert.Empty(scenario.Dialogs.FindAll(".confirm-message")));
        Assert.Empty(await scenario.Movements.GetAsync());

        scenario.Dialogs.WaitForAssertion(() => Assert.False(scenario.Dialogs.Find("[data-save]").HasAttribute("disabled")));
        _ = scenario.Dialogs.Find("#transfer-form").SubmitAsync(EventArgs.Empty);
        scenario.Dialogs.WaitForAssertion(() => Assert.NotEmpty(scenario.Dialogs.FindAll("[data-confirm-ok]")));
        await scenario.Dialogs.Find("[data-confirm-ok]").ClickAsync(new MouseEventArgs());

        scenario.Dialogs.WaitForAssertion(() => Assert.Empty(scenario.Dialogs.FindAll("#transfer-form")));
        page.WaitForAssertion(() => Assert.Equal(2, page.FindAll("[aria-label='Movimentações de contas'] li").Count));
        Assert.Contains("R$ 700,00", page.Markup, StringComparison.Ordinal);
        Assert.Equal(2, (await scenario.Movements.GetAsync()).Count);
    }

    [Fact]
    public async Task BalanceAdjustmentShowsTheDifferenceAndCanBeCancelledBeforeConfirming()
    {
        await using var scenario = await CreateAsync();
        var page = scenario.Context.Render<AccountTransfers>();
        page.WaitForAssertion(() => Assert.Contains("R$ 1.000,00", page.Markup, StringComparison.Ordinal));
        page.Find("#adjust-account").Change(scenario.Source.ToString());
        page.Find("#adjust-balance").Change("1150");

        await page.Find("#balance-adjustment").SubmitAsync(EventArgs.Empty);
        Assert.Contains("R$ 1.000,00", page.Find("[aria-label='Confirmar ajuste de saldo']").TextContent, StringComparison.Ordinal);
        Assert.Contains("R$ 150,00", page.Find("[aria-label='Confirmar ajuste de saldo']").TextContent, StringComparison.Ordinal);
        page.FindAll("[aria-label='Confirmar ajuste de saldo'] button")[1].Click();
        Assert.Empty(await scenario.Movements.GetAsync());
        await page.Find("#balance-adjustment").SubmitAsync(EventArgs.Empty);
        await page.Find("[aria-label='Confirmar ajuste de saldo'] button").ClickAsync(new MouseEventArgs());

        Assert.Single(await scenario.Movements.GetAsync());
        Assert.Contains("Ajuste de saldo", page.Find("[aria-label='Movimentações de contas']").TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheWindowExplainsWhatIsMissingAndKeepsTheDataOnARuleError()
    {
        await using var scenario = await CreateAsync();
        await scenario.OpenAsync();
        scenario.Dialogs.WaitForAssertion(() => Assert.Contains("Corrente Exemplo", scenario.Dialogs.Find("#transfer-source").TextContent, StringComparison.Ordinal));

        Assert.True(scenario.Dialogs.Find("[data-save]").HasAttribute("disabled"));
        Assert.Equal("Informe um valor maior que zero.", scenario.Dialogs.Find("[data-entry-missing]").TextContent);
        scenario.Dialogs.Find("#transfer-amount").Input("10");
        Assert.Equal("Escolha a conta de origem.", scenario.Dialogs.Find("[data-entry-missing]").TextContent);
        await scenario.Dialogs.Find("#transfer-source").ChangeAsync(scenario.Source.ToString());
        Assert.Equal("Escolha a conta de destino.", scenario.Dialogs.Find("[data-entry-missing]").TextContent);
        Assert.DoesNotContain("Reserva Exemplo", scenario.Dialogs.Find("#transfer-source").TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SaveAndCreateNewKeepsTheWindowOpenWithAnEmptyAmount()
    {
        await using var scenario = await CreateAsync();
        await scenario.OpenAsync();
        await scenario.FillAsync("50");

        _ = scenario.Dialogs.Find("[data-save-new]").ClickAsync(new MouseEventArgs());
        scenario.Dialogs.WaitForAssertion(() => Assert.NotEmpty(scenario.Dialogs.FindAll("[data-confirm-ok]")));
        await scenario.Dialogs.Find("[data-confirm-ok]").ClickAsync(new MouseEventArgs());

        scenario.Dialogs.WaitForAssertion(() => Assert.Contains("Transferência registrada", scenario.Dialogs.Find("[data-entry-success]").TextContent, StringComparison.Ordinal));
        Assert.True(scenario.Dialogs.Find("[data-save]").HasAttribute("disabled"));
        Assert.Equal(2, (await scenario.Movements.GetAsync()).Count);
    }

    [Fact]
    public async Task ClosingWithFilledDataAsksForConfirmation()
    {
        await using var scenario = await CreateAsync();
        await scenario.OpenAsync();
        await scenario.FillAsync();

        _ = scenario.Dialogs.Find("[data-dialog-close]").ClickAsync(new MouseEventArgs());

        scenario.Dialogs.WaitForAssertion(() => Assert.Contains("Descartar alterações?", scenario.Dialogs.Markup, StringComparison.Ordinal));
    }
}
