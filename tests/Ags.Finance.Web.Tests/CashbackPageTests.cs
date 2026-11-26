using Ags.Finance.Modules.Accounts.Contracts;
using Ags.Finance.Modules.Invoices.Contracts;
using Ags.Finance.Modules.Transactions.Contracts;
using Ags.Finance.SharedKernel;
using Ags.Finance.Web.Components.Pages;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace Ags.Finance.Web.Tests;

/// <summary>Cashback dos cartões (#153): cadastro, coluna na fatura e painel. O relógio do cenário está em 01/09/2026 e a fatura atual é 09/2026.</summary>
public sealed class CashbackPageTests
{
    private static readonly ReferenceMonth s_september = new(2026, 9);

    private static async Task EnableCashbackAsync(EntryUi ui, decimal percent = 1m) =>
        Assert.True((await ui.Context.Services.GetRequiredService<IAccountCatalog>().SaveCreditCardAsync(new(ui.Card, ui.Account, "Cartão Exemplo", CardBrand.Visa, new Money(10000), 3, 10, true, "green",
            Cashback: new CardCashback(percent)))).Succeeded);

    private static async Task BuyAsync(EntryUi ui, decimal amount) =>
        Assert.True((await ui.Entries.CreateAsync(new(new DateOnly(2026, 8, 30), ui.Merchant, ui.ExpenseSubcategory, ui.Account, new Money(amount), string.Empty, s_september, ui.Card, UseSelectedInvoice: true))).Succeeded);

    private static async Task PaySeptemberAsync(EntryUi ui, decimal amount)
    {
        Assert.True((await ui.Context.Services.GetRequiredService<IInvoiceLifecycle>().CloseAsync(ui.Card, s_september)).Succeeded);
        Assert.True((await ui.Context.Services.GetRequiredService<IInvoicePaymentCatalog>().ConfirmPaymentAsync(new(Guid.CreateVersion7(), ui.Card, s_september, ui.Account, new DateOnly(2026, 9, 1), new Money(amount), true))).Succeeded);
    }

    private static IRenderedComponent<Cashback> OpenPanel(EntryUi ui)
    {
        var page = ui.Context.Render<Cashback>();
        page.WaitForAssertion(() => Assert.Contains("Cashback", page.Find("h1").TextContent, StringComparison.Ordinal));
        return page;
    }

    private static string Stat(IRenderedComponent<Cashback> page, string name) => page.Find($"[data-stat='{name}'] .stat-value").TextContent;

    [Fact]
    public async Task TheCardWindowTurnsCashbackOnAndTheWalletShowsThePercent()
    {
        await using var ui = await EntryUi.CreateAsync();
        var page = ui.Context.Render<Cards>();
        page.WaitForAssertion(() => Assert.NotEmpty(page.FindAll("[data-card-menu] button")));
        await page.Find("[data-card-menu] button").ClickAsync(new MouseEventArgs());
        ui.Popovers.WaitForAssertion(() => Assert.Contains(ui.Popovers.FindAll(".mud-popover-open .mud-menu-item"), x => x.TextContent.Trim() == "Editar"));
        _ = ui.Popovers.FindAll(".mud-popover-open .mud-menu-item").Single(x => x.TextContent.Trim() == "Editar").ClickAsync(new MouseEventArgs());
        ui.Dialogs.WaitForElement("#card-uses-cashback");
        Assert.Empty(ui.Dialogs.FindAll("#card-cashback-percent"));

        await ui.Dialogs.Find("#card-uses-cashback").ChangeAsync(true);
        await ui.Dialogs.WaitForElement("#card-cashback-percent").ChangeAsync("1.5");
        await ui.Dialogs.Find("#card-form").SubmitAsync(EventArgs.Empty);

        page.WaitForAssertion(() => Assert.Contains("Cashback 1,5%", page.Find("[data-card-cashback]").TextContent, StringComparison.Ordinal));
        Assert.Equal(1.5m, Assert.Single(await ui.Context.Services.GetRequiredService<IAccountCatalog>().GetCreditCardsAsync()).CashbackPercent);
    }

    [Fact]
    public async Task TheInvoiceShowsTheCashbackOfEachPurchaseAndTheExpectedTotal()
    {
        await using var ui = await EntryUi.CreateAsync();
        await EnableCashbackAsync(ui, 1.5m);
        await BuyAsync(ui, 200);
        ui.Context.Services.GetRequiredService<NavigationManager>().NavigateTo($"/cartoes/{ui.Card}/faturas?mes=2026-09");

        var page = ui.Context.Render<InvoiceDetail>(parameters => parameters.Add(x => x.CardExternalId, ui.Card));

        page.WaitForAssertion(() => Assert.Contains("R$ 3,00 (1,5%)", page.Find("[data-entry-cashback]").TextContent, StringComparison.Ordinal));
        Assert.Contains("R$ 3,00", page.Find("[data-stat='invoice-cashback'] .stat-value").TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ACardWithoutCashbackShowsNoCashbackColumnAndThePanelIsEmpty()
    {
        await using var ui = await EntryUi.CreateAsync();
        await BuyAsync(ui, 200);
        ui.Context.Services.GetRequiredService<NavigationManager>().NavigateTo($"/cartoes/{ui.Card}/faturas?mes=2026-09");
        var detail = ui.Context.Render<InvoiceDetail>(parameters => parameters.Add(x => x.CardExternalId, ui.Card));
        detail.WaitForAssertion(() => Assert.NotEmpty(detail.FindAll("[data-stat='invoice-total']")));
        Assert.Empty(detail.FindAll("[data-entry-cashback]"));
        Assert.Empty(detail.FindAll("[data-stat='invoice-cashback']"));

        var panel = OpenPanel(ui);

        panel.WaitForAssertion(() => Assert.Contains("Nenhum cashback neste mês", panel.Markup, StringComparison.Ordinal));
    }

    [Fact]
    public async Task ThePanelReceivesTheCashbackOfAPaidInvoiceInTheAccountAndUndoesIt()
    {
        await using var ui = await EntryUi.CreateAsync();
        await EnableCashbackAsync(ui);
        await BuyAsync(ui, 120);
        var page = OpenPanel(ui);
        page.WaitForAssertion(() => Assert.Equal("Previsto", page.Find("[data-cashback-status]").TextContent));
        Assert.Empty(page.FindAll("[data-receive-cashback]"));
        Assert.Contains("R$ 1,20", Stat(page, "cashback-expected"), StringComparison.Ordinal);

        await PaySeptemberAsync(ui, 120);
        page = OpenPanel(ui);
        page.WaitForAssertion(() => Assert.Equal("A receber", page.Find("[data-cashback-status]").TextContent));
        Assert.Contains("R$ 1,20", Stat(page, "cashback-to-receive"), StringComparison.Ordinal);

        _ = page.Find("[data-receive-cashback]").ClickAsync(new MouseEventArgs());
        ui.Dialogs.WaitForElement("#cashback-received");
        ui.Dialogs.WaitForAssertion(() => Assert.Equal(ui.Account.ToString(), ui.Dialogs.Find("#cashback-account").GetAttribute("value")));
        ui.Dialogs.Find("#cashback-received").Input("1.10");
        await ui.Dialogs.Find("[data-confirm-ok]").ClickAsync(new MouseEventArgs());

        page.WaitForAssertion(() => Assert.Equal("Recebido", page.Find("[data-cashback-status]").TextContent));
        Assert.Contains("R$ 1,10", page.Find("[data-cashback-row-received]").TextContent, StringComparison.Ordinal);
        Assert.Contains("R$ 1,10", page.Find("[data-cashback-year-total]").TextContent, StringComparison.Ordinal);
        var income = Assert.Single(await ui.Entries.GetAsync(), x => x.Kind == CategoryKind.Income);
        Assert.Equal(new Money(1.10m), income.Amount);

        _ = page.Find("[data-undo-cashback]").ClickAsync(new MouseEventArgs());
        ui.Dialogs.WaitForAssertion(() => Assert.NotEmpty(ui.Dialogs.FindAll("[data-confirm-ok]")));
        await ui.Dialogs.Find("[data-confirm-ok]").ClickAsync(new MouseEventArgs());

        page.WaitForAssertion(() => Assert.Equal("A receber", page.Find("[data-cashback-status]").TextContent));
        Assert.DoesNotContain(await ui.Entries.GetAsync(), x => x.Kind == CategoryKind.Income);
    }
}
