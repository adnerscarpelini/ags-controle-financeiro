using Ags.Finance.Modules.Accounts.Contracts;
using Ags.Finance.Modules.Attachments.Contracts;
using Ags.Finance.Modules.Invoices.Contracts;
using Ags.Finance.Modules.Preferences.Contracts;
using Ags.Finance.Modules.Transactions.Contracts;
using Ags.Finance.SharedKernel;
using Ags.Finance.Web.Components;
using Ags.Finance.Web.Components.Common;
using Ags.Finance.Web.Components.Pages;
using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;

namespace Ags.Finance.Web.Tests;

/// <summary>Carteira de cartões e detalhe da fatura (#100). O relógio do cenário está em 01/09/2026 e a fatura atual é 09/2026.</summary>
public sealed class CardsPageTests
{
    private static readonly ReferenceMonth s_september = new(2026, 9);

    private static async Task BuyAsync(EntryUi ui, decimal amount, ReferenceMonth? month = null) =>
        Assert.True((await ui.Entries.CreateAsync(new(new DateOnly(2026, 8, 30), ui.Merchant, ui.ExpenseSubcategory, ui.Account, new Money(amount), string.Empty, month ?? s_september, ui.Card, UseSelectedInvoice: true))).Succeeded);

    private static async Task CloseAsync(EntryUi ui, ReferenceMonth? month = null) =>
        Assert.True((await ui.Context.Services.GetRequiredService<IInvoiceLifecycle>().CloseAsync(ui.Card, month ?? s_september)).Succeeded);

    private static async Task PayAsync(EntryUi ui, decimal amount, bool settle = true) =>
        Assert.True((await ui.Context.Services.GetRequiredService<IInvoicePaymentCatalog>().ConfirmPaymentAsync(new(Guid.CreateVersion7(), ui.Card, s_september, ui.Account, new DateOnly(2026, 9, 1), new Money(amount), settle))).Succeeded);

    private static IRenderedComponent<Cards> OpenCards(EntryUi ui)
    {
        var page = ui.Context.Render<Cards>();
        page.WaitForAssertion(() => Assert.NotEmpty(page.FindAll("[data-card]")));
        return page;
    }

    private static IRenderedComponent<InvoiceDetail> OpenDetail(EntryUi ui, ReferenceMonth? month = null)
    {
        var value = month ?? s_september;
        ui.Context.Services.GetRequiredService<NavigationManager>().NavigateTo($"/cartoes/{ui.Card}/faturas?mes={value.Year:0000}-{value.Month:00}");
        var page = ui.Context.Render<InvoiceDetail>(parameters => parameters.Add(x => x.CardExternalId, ui.Card));
        page.WaitForAssertion(() => Assert.Contains("Cartão Exemplo", page.Find("h1").TextContent, StringComparison.Ordinal));
        return page;
    }

    private static async Task ChooseAsync(EntryUi ui, IElement activator, string item)
    {
        await activator.ClickAsync(new MouseEventArgs());
        ui.Popovers.WaitForAssertion(() => Assert.Contains(ui.Popovers.FindAll(".mud-popover-open .mud-menu-item"), x => x.TextContent.Trim() == item));
        _ = ui.Popovers.FindAll(".mud-popover-open .mud-menu-item").Single(x => x.TextContent.Trim() == item).ClickAsync(new MouseEventArgs());
    }

    private static string Stat<TPage>(IRenderedComponent<TPage> page, string name)
        where TPage : IComponent => page.Find($"[data-stat='{name}'] .stat-value").TextContent;

    [Fact]
    public async Task ACardWithAClosedInvoiceShowsPayInvoiceWhenTheScreenOpens()
    {
        await using var ui = await EntryUi.CreateAsync();
        await BuyAsync(ui, 100);
        await CloseAsync(ui);

        var page = OpenCards(ui);

        Assert.NotEmpty(page.FindAll("[data-pay-invoice]"));
        Assert.Contains("09/2026", page.Find("[data-pending-invoice]").TextContent, StringComparison.Ordinal);
        Assert.Contains("R$ 100,00", page.Find("[data-pending-invoice]").TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheOpenTabShowsTheOpenInvoicesAndTheClosedTabTheClosedOnes()
    {
        await using var ui = await EntryUi.CreateAsync();
        await BuyAsync(ui, 100);
        await BuyAsync(ui, 40, new ReferenceMonth(2026, 10));
        await CloseAsync(ui);
        var page = OpenCards(ui);

        Assert.Equal("Fatura aberta", page.Find("[data-invoice-status]").TextContent);
        Assert.Equal("R$ 40,00", page.Find("[data-invoice-total]").TextContent);
        Assert.Equal("true", page.Find("[data-tab='open']").GetAttribute("aria-pressed"));

        await page.Find("[data-tab='closed']").ClickAsync(new MouseEventArgs());

        Assert.Equal("Fatura fechada", page.Find("[data-invoice-status]").TextContent);
        Assert.Equal("R$ 100,00", page.Find("[data-invoice-total]").TextContent);
        Assert.Equal("10/09/2026", page.Find("[data-invoice-due]").TextContent);
    }

    [Fact]
    public async Task APaidInvoiceShowsItsStateInsteadOfPayInvoice()
    {
        await using var ui = await EntryUi.CreateAsync();
        await BuyAsync(ui, 100);
        await CloseAsync(ui);
        await PayAsync(ui, 100);
        var page = OpenCards(ui);

        await page.Find("[data-tab='closed']").ClickAsync(new MouseEventArgs());

        Assert.Equal("Fatura paga", page.Find("[data-invoice-paid]").TextContent);
        Assert.Empty(page.FindAll("[data-pay-invoice]"));
    }

    [Fact]
    public async Task ClickingTheCardOpensTheCorrespondingInvoice()
    {
        await using var ui = await EntryUi.CreateAsync();
        await BuyAsync(ui, 100);
        var page = OpenCards(ui);

        await page.Find("[data-card]").ClickAsync(new MouseEventArgs());

        Assert.EndsWith($"/cartoes/{ui.Card}/faturas?mes=2026-09", ui.Context.Services.GetRequiredService<NavigationManager>().Uri, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheCardShowsTheLimitAndTheSummaryTheTotals()
    {
        await using var ui = await EntryUi.CreateAsync();
        await BuyAsync(ui, 2500);

        var page = OpenCards(ui);

        Assert.Contains("Limite disponível: R$ 7.500,00 de R$ 10.000,00", page.Find("[data-card-limit]").TextContent, StringComparison.Ordinal);
        Assert.Equal("25", page.Find("[data-card-limit] [role=progressbar]").GetAttribute("aria-valuenow"));
        Assert.Equal("10/09/2026", Stat(page, "next-due"));
        Assert.Equal("R$ 7.500,00", Stat(page, "available"));
        Assert.Equal("R$ 2.500,00", Stat(page, "invoices-total"));
    }

    [Fact]
    public async Task NoCardGuidesToRegisterTheFirst()
    {
        await using var ui = await EntryUi.CreateAsync();
        await ui.Context.Services.GetRequiredService<IAccountCatalog>().DeactivateCreditCardAsync(ui.Card);

        var page = ui.Context.Render<Cards>();

        page.WaitForAssertion(() => Assert.Contains("Cadastre o primeiro", page.Find(".empty-state").TextContent, StringComparison.Ordinal));
    }

    [Fact]
    public async Task ANewCardIsCreatedInTheWindowWithHelpOnClosingAndDue()
    {
        await using var ui = await EntryUi.CreateAsync();
        var page = OpenCards(ui);

        await page.Find("[data-new-card]").ClickAsync(new MouseEventArgs());
        await ui.Dialogs.WaitForElement("#card-name").InputAsync("Cartão Novo");
        Assert.Contains(ui.Dialogs.FindAll(".help-tip-button"), x => x.GetAttribute("aria-label")!.Contains("entram na fatura seguinte", StringComparison.Ordinal));
        Assert.Contains(ui.Dialogs.FindAll(".help-tip-button"), x => x.GetAttribute("aria-label")!.Contains("dia em que a fatura vence", StringComparison.Ordinal));
        await ui.Dialogs.Find("#card-account").ChangeAsync(ui.Account.ToString());
        await ui.Dialogs.Find("#card-brand").ChangeAsync(CardBrand.Mastercard.ToString());
        ui.Dialogs.Find("#card-limit").Input("10000");
        await ui.Dialogs.Find("#card-color").ChangeAsync("violet");
        ui.Dialogs.WaitForAssertion(() => Assert.False(ui.Dialogs.Find("[data-save]").HasAttribute("disabled")));
        await ui.Dialogs.Find("#card-form").SubmitAsync(EventArgs.Empty);

        page.WaitForAssertion(() => Assert.Equal(2, page.FindAll("[data-card]").Count));
        Assert.Contains("Mastercard", page.Find("[data-card='Cartão Novo']").TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnErrorWhenSavingACardStaysInTheWindowAndKeepsTheData()
    {
        await using var ui = await EntryUi.CreateAsync();
        var page = OpenCards(ui);
        await page.Find("[data-new-card]").ClickAsync(new MouseEventArgs());
        await ui.Dialogs.WaitForElement("#card-name").InputAsync("Cartão sem conta");

        await ui.Dialogs.Find("#card-form").SubmitAsync(EventArgs.Empty);

        ui.Dialogs.WaitForAssertion(() => Assert.NotEmpty(ui.Dialogs.Find("[data-entry-error]").TextContent));
        Assert.Equal("Cartão sem conta", ui.Dialogs.Find("#card-name").GetAttribute("value"));
    }

    [Fact]
    public async Task TheCardMenuEditsAndRefreshesTheWallet()
    {
        await using var ui = await EntryUi.CreateAsync();
        var page = OpenCards(ui);

        await ChooseAsync(ui, page.Find("[data-card-menu] button"), "Editar");
        await ui.Dialogs.WaitForElement("#card-name").InputAsync("Cartão Atualizado");
        await ui.Dialogs.Find("#card-form").SubmitAsync(EventArgs.Empty);

        page.WaitForAssertion(() => Assert.NotEmpty(page.FindAll("[data-card='Cartão Atualizado']")));
    }

    [Fact]
    public async Task TheCardMenuManagesTheFinalsAndTheWalletShowsThem()
    {
        await using var ui = await EntryUi.CreateAsync();
        var page = OpenCards(ui);
        Assert.Contains("Sem final cadastrado", page.Find(".wallet-card-identification").TextContent, StringComparison.Ordinal);

        await ChooseAsync(ui, page.Find("[data-card-menu] button"), "Finais do cartão");
        await ui.Dialogs.WaitForElement("#card-digits").ChangeAsync("5678");
        await ui.Dialogs.Find("#digits-label").ChangeAsync("Assinaturas");
        await ui.Dialogs.Find("form").SubmitAsync(EventArgs.Empty);

        page.WaitForAssertion(() => Assert.Contains("Final 5678 · Assinaturas", page.Find(".wallet-card-identification").TextContent, StringComparison.Ordinal));
        Assert.Empty(page.FindAll(".wallet-card img"));
    }

    [Fact]
    public async Task TheCardMenuListsTheInvoiceHistoryAndOpensAnInvoice()
    {
        await using var ui = await EntryUi.CreateAsync();
        await BuyAsync(ui, 100);
        var page = OpenCards(ui);

        await ChooseAsync(ui, page.Find("[data-card-menu] button"), "Histórico de faturas");
        ui.Dialogs.WaitForAssertion(() => Assert.Contains("09/2026", ui.Dialogs.Find("[aria-label='Histórico de faturas']").TextContent, StringComparison.Ordinal));
        await ui.Dialogs.Find("[data-open-invoice]").ClickAsync(new MouseEventArgs());

        Assert.EndsWith("faturas?mes=2026-09", ui.Context.Services.GetRequiredService<NavigationManager>().Uri, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ArchivingAsksForConfirmationAndArchivedCardsCanBeRestored()
    {
        await using var ui = await EntryUi.CreateAsync();
        var catalog = ui.Context.Services.GetRequiredService<IAccountCatalog>();
        var page = OpenCards(ui);

        await ChooseAsync(ui, page.Find("[data-card-menu] button"), "Arquivar");
        ui.Dialogs.WaitForAssertion(() => Assert.NotEmpty(ui.Dialogs.FindAll("[data-confirm-cancel]")));
        await ui.Dialogs.Find("[data-confirm-cancel]").ClickAsync(new MouseEventArgs());
        Assert.Single(await catalog.GetCreditCardsAsync());

        await ChooseAsync(ui, page.Find("[data-card-menu] button"), "Arquivar");
        ui.Dialogs.WaitForAssertion(() => Assert.NotEmpty(ui.Dialogs.FindAll("[data-confirm-ok]")));
        await ui.Dialogs.Find("[data-confirm-ok]").ClickAsync(new MouseEventArgs());
        page.WaitForAssertion(() => Assert.NotEmpty(page.FindAll(".empty-state")));
        Assert.Empty(await catalog.GetCreditCardsAsync());
        Assert.Single(await catalog.GetCreditCardsAsync(false));
    }

    [Fact]
    public async Task APaidInvoiceDoesNotOfferEditingAndTheMenuOffersReopen()
    {
        await using var ui = await EntryUi.CreateAsync();
        await BuyAsync(ui, 100);
        await CloseAsync(ui);
        await PayAsync(ui, 100);

        var page = OpenDetail(ui);

        Assert.Equal("Paga", Stat(page, "invoice-status"));
        Assert.Empty(page.FindAll("[data-action]"));
        Assert.Contains("Reabra a fatura", page.Find("[data-purchase-locked] .help-tip-button").GetAttribute("aria-label"), StringComparison.Ordinal);
        Assert.NotEmpty(page.FindAll("[data-invoice-action='reopen']"));
        Assert.Empty(page.FindAll("[data-invoice-action='pay']"));
        Assert.Empty(page.FindAll("[data-invoice-action='close']"));
        Assert.Empty(page.FindAll("[data-invoice-action='adjust']"));
    }

    [Fact]
    public async Task AnOpenInvoiceOffersPurchaseActionsAndTheStateActions()
    {
        await using var ui = await EntryUi.CreateAsync();
        await BuyAsync(ui, 100);

        var page = OpenDetail(ui);

        Assert.Equal("Aberta", Stat(page, "invoice-status"));
        Assert.Equal(["edit", "delete"], page.FindAll("[data-action]").Select(x => x.GetAttribute("data-action")));
        Assert.Equal(["close", "adjust", "pay"], page.FindAll("[data-invoice-action]").Select(x => x.GetAttribute("data-invoice-action")));
        Assert.Contains("Mercado › Feira", page.Find(".data-table tbody tr").TextContent, StringComparison.Ordinal);
        Assert.Equal("R$ 100,00", Stat(page, "invoice-total"));
        Assert.Equal("10/09/2026", Stat(page, "invoice-due"));
        Assert.Equal("03/09/2026", Stat(page, "invoice-closing"));
    }

    [Fact]
    public async Task AClosedInvoiceBlocksPurchasesAndOffersPayAdjustAndReopen()
    {
        await using var ui = await EntryUi.CreateAsync();
        await BuyAsync(ui, 100);
        await CloseAsync(ui);

        var page = OpenDetail(ui);

        Assert.Empty(page.FindAll("[data-action]"));
        Assert.Equal(["reopen", "adjust", "pay"], page.FindAll("[data-invoice-action]").Select(x => x.GetAttribute("data-invoice-action")));
    }

    [Theory]
    [InlineData("Histórico", "Cashback:")]
    [InlineData("Cashback e descontos", "Novo cashback ou desconto")]
    [InlineData("Compromissos", "Em aberto por cartão")]
    [InlineData("Anexos", "Documentos anexados")]
    public async Task EveryInvoiceFunctionIsReachableFromTheDetailMenu(string item, string expected)
    {
        await using var ui = await EntryUi.CreateAsync();
        await BuyAsync(ui, 100);
        var page = OpenDetail(ui);

        await ChooseAsync(ui, page.Find("[data-invoice-menu] button"), item);

        ui.Dialogs.WaitForAssertion(() => Assert.Contains(expected, ui.Dialogs.Markup, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("Pagar antecipado", false)]
    [InlineData("Pagar parcial", true)]
    public async Task TheMenuOffersThePaymentThatFitsTheState(string item, bool closed)
    {
        await using var ui = await EntryUi.CreateAsync();
        await BuyAsync(ui, 100);
        if (closed) { await CloseAsync(ui); }
        var page = OpenDetail(ui);

        await ChooseAsync(ui, page.Find("[data-invoice-menu] button"), item);

        ui.Dialogs.WaitForAssertion(() => Assert.Equal(ui.Account.ToString(), ui.Dialogs.Find("#payment-account").GetAttribute("value")));
    }

    [Fact]
    public async Task TheInvoiceActionsAndValuesOnlyAppearAfterTheInvoiceLoads()
    {
        await using var ui = await EntryUi.CreateAsync();
        await BuyAsync(ui, 100);
        await CloseAsync(ui);
        await PayAsync(ui, 100);
        ui.Context.Services.GetRequiredService<NavigationManager>().NavigateTo($"/cartoes/{ui.Card}/faturas?mes=2026-09");

        var hold = await ui.LockSchemaAsync("invoices");
        var page = ui.Context.Render<InvoiceDetail>(parameters => parameters.Add(x => x.CardExternalId, ui.Card));
        await ui.WaitForBlockedQueryAsync();

        Assert.Empty(page.FindAll("[data-invoice-action]"));
        Assert.Empty(page.FindAll("[data-invoice-menu]"));
        Assert.Empty(page.FindAll("[data-stat='invoice-status']"));
        Assert.DoesNotContain("Cartão Exemplo", page.Find("h1").TextContent, StringComparison.Ordinal);

        await hold.DisposeAsync();
        page.WaitForAssertion(() => Assert.Equal("Paga", Stat(page, "invoice-status")));
        Assert.Contains("Cartão Exemplo", page.Find("h1").TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheWalletOnlyShowsACardAfterEverythingAboutItLoads()
    {
        await using var ui = await EntryUi.CreateAsync();
        await BuyAsync(ui, 2500);

        var hold = await ui.LockSchemaAsync("invoices");
        var page = ui.Context.Render<Cards>();
        await ui.WaitForBlockedQueryAsync();
        page.Render();

        Assert.Empty(page.FindAll("[data-card]"));

        await hold.DisposeAsync();
        page.WaitForAssertion(() => Assert.NotEmpty(page.FindAll("[data-card]")));
        Assert.Contains("Limite disponível: R$ 7.500,00 de R$ 10.000,00", page.Find("[data-card-limit]").TextContent, StringComparison.Ordinal);
        Assert.Equal("Fatura aberta", page.Find("[data-invoice-status]").TextContent);
    }

    [Fact]
    public async Task TheMonthSelectorMovesBetweenInvoices()
    {
        await using var ui = await EntryUi.CreateAsync();
        await BuyAsync(ui, 100);
        var page = OpenDetail(ui);
        Assert.Equal("Fatura anterior", page.Find("[data-month-previous]").GetAttribute("aria-label"));

        await page.Find("[data-month-next]").ClickAsync(new MouseEventArgs());

        Assert.EndsWith("faturas?mes=2026-10", ui.Context.Services.GetRequiredService<NavigationManager>().Uri, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ClosingAdjustingAndPayingFollowTheConfirmationsAndKeepTheRules()
    {
        await using var ui = await EntryUi.CreateAsync();
        await BuyAsync(ui, 100);
        var page = OpenDetail(ui);
        var payments = ui.Context.Services.GetRequiredService<IInvoicePaymentCatalog>();

        _ = page.Find("[data-invoice-action='close']").ClickAsync(new MouseEventArgs());
        ui.Dialogs.WaitForAssertion(() => Assert.NotEmpty(ui.Dialogs.FindAll("[data-confirm-cancel]")));
        await ui.Dialogs.Find("[data-confirm-cancel]").ClickAsync(new MouseEventArgs());
        page.WaitForAssertion(() => Assert.Equal("Aberta", Stat(page, "invoice-status")));

        _ = page.Find("[data-invoice-action='close']").ClickAsync(new MouseEventArgs());
        ui.Dialogs.WaitForAssertion(() => Assert.NotEmpty(ui.Dialogs.FindAll("[data-confirm-ok]")));
        await ui.Dialogs.Find("[data-confirm-ok]").ClickAsync(new MouseEventArgs());
        page.WaitForAssertion(() => Assert.Equal("Fechada", Stat(page, "invoice-status")));

        await page.Find("[data-invoice-action='adjust']").ClickAsync(new MouseEventArgs());
        ui.Dialogs.WaitForElement("#invoice-adjustment-form");
        await ui.Dialogs.Find("[data-dialog-cancel]").ClickAsync(new MouseEventArgs());
        await page.Find("[data-invoice-action='adjust']").ClickAsync(new MouseEventArgs());
        ui.Dialogs.WaitForElement("#invoice-correct-total").Input("100,01");
        ui.Dialogs.WaitForAssertion(() => Assert.Equal(100.01m, ui.Dialogs.FindComponent<MoneyField>().Instance.Value));
        await ui.Dialogs.Find("#invoice-adjustment-form").SubmitAsync(EventArgs.Empty);
        Assert.Contains("R$ 0,01", ui.Dialogs.Find("[data-adjustment-review]").TextContent, StringComparison.Ordinal);
        await ui.Dialogs.Find("[data-confirm-ok]").ClickAsync(new MouseEventArgs());
        page.WaitForAssertion(() => Assert.Equal("R$ 100,01", Stat(page, "invoice-total")));

        await page.Find("[data-invoice-action='pay']").ClickAsync(new MouseEventArgs());
        ui.Dialogs.WaitForAssertion(() => Assert.False(ui.Dialogs.Find("[data-review]").HasAttribute("disabled")));
        Assert.Equal(ui.Account.ToString(), ui.Dialogs.Find("#payment-account").GetAttribute("value"));
        await ui.Dialogs.Find("[data-dialog-cancel]").ClickAsync(new MouseEventArgs());
        Assert.Empty(await payments.GetHistoryAsync());

        await page.Find("[data-invoice-action='pay']").ClickAsync(new MouseEventArgs());
        ui.Dialogs.WaitForAssertion(() => Assert.False(ui.Dialogs.Find("[data-review]").HasAttribute("disabled")));
        await ui.Dialogs.Find("#payment-settle").ChangeAsync(true);
        await ui.Dialogs.Find("#invoice-payment-form").SubmitAsync(EventArgs.Empty);
        Assert.Contains("só pode ser desfeito reabrindo a fatura", ui.Dialogs.Markup, StringComparison.Ordinal);
        await ui.Dialogs.Find("[data-confirm-ok]").ClickAsync(new MouseEventArgs());

        page.WaitForAssertion(() => Assert.Equal("Paga", Stat(page, "invoice-status")));
        Assert.Single(await payments.GetHistoryAsync());
    }

    [Fact]
    public async Task ThePaymentAccountCanBeChangedAndOnlyTheChosenOneIsDebited()
    {
        await using var ui = await EntryUi.CreateAsync();
        var accounts = ui.Context.Services.GetRequiredService<IAccountCatalog>();
        var bank = Assert.Single(await accounts.GetBanksAsync());
        var other = (await accounts.SaveAccountAsync(new(null, bank.ExternalId, "Outra Corrente", AccountKind.Checking, "green"))).ExternalId!.Value;
        await BuyAsync(ui, 100);
        await CloseAsync(ui);
        var page = OpenCards(ui);

        await page.Find("[data-pay-invoice]").ClickAsync(new MouseEventArgs());
        ui.Dialogs.WaitForAssertion(() => Assert.Equal(ui.Account.ToString(), ui.Dialogs.Find("#payment-account").GetAttribute("value")));
        await ui.Dialogs.Find("#payment-account").ChangeAsync(other.ToString());
        await ui.Dialogs.Find("#invoice-payment-form").SubmitAsync(EventArgs.Empty);
        await ui.Dialogs.Find("[data-confirm-ok]").ClickAsync(new MouseEventArgs());

        var movement = Assert.Single(await ui.Context.Services.GetRequiredService<IAccountMovementCatalog>().GetAsync());
        Assert.Equal(other, movement.AccountExternalId);
        Assert.DoesNotContain(await ui.Entries.GetAsync(), x => x.Amount == new Money(-200));
    }

    [Theory]
    [InlineData(RecurringInvoiceMode.SumOnDate, "R$ 100,00", "Previsto: + R$ 50,00 (total previsto: R$ 150,00)")]
    [InlineData(RecurringInvoiceMode.SumUpfront, "R$ 150,00", "Inclui R$ 50,00 em recorrentes previstas.")]
    public async Task TheDetailShowsTheForecastAccordingToThePreference(RecurringInvoiceMode mode, string total, string note)
    {
        await using var ui = await EntryUi.CreateAsync(new DateTimeOffset(2026, 9, 20, 12, 0, 0, TimeSpan.Zero));
        Assert.True((await ui.Context.Services.GetRequiredService<IPreferences>().SaveAsync(AppPreferences.Default with { RecurringInvoices = mode })).Succeeded);
        CreateEntryRequest Purchase(decimal amount, DateOnly date) => new(date, ui.Merchant, ui.ExpenseSubcategory, ui.Account, new Money(amount), CardExternalId: ui.Card);
        Assert.True((await ui.Entries.CreateAsync(Purchase(100, new DateOnly(2026, 9, 14)))).Succeeded);
        Assert.True((await ui.Entries.CreateRecurringAsync(new(Purchase(50, new DateOnly(2026, 9, 27))))).Succeeded);

        var page = OpenDetail(ui, new ReferenceMonth(2026, 10));

        page.WaitForAssertion(() => Assert.Equal(total, Stat(page, "invoice-total")));
        Assert.Contains(note, page.Find("[data-stat='invoice-total'] .stat-note").TextContent, StringComparison.Ordinal);
        var forecast = page.FindAll(".data-table tbody tr").Single(x => x.TextContent.Contains("Recorrente · prevista", StringComparison.Ordinal));
        Assert.Contains("27/09/2026", forecast.TextContent, StringComparison.Ordinal);
        Assert.NotNull(forecast.QuerySelector(".invoice-forecast"));
    }

    [Fact]
    public async Task ReopeningListsThePaymentsSupportsCancelAndShowsTheUndoneHistory()
    {
        await using var ui = await EntryUi.CreateAsync();
        await ui.Context.Services.GetRequiredService<IAccountCatalog>().SaveAccountAsync(new(ui.Account, (await ui.Context.Services.GetRequiredService<IAccountCatalog>().GetBanksAsync())[0].ExternalId, "Corrente Exemplo", AccountKind.Checking, "green"));
        await BuyAsync(ui, 66.66m);
        await CloseAsync(ui);
        await PayAsync(ui, 66.66m);
        var before = (await ui.Entries.GetBalanceAsync(ui.Account, new DateOnly(2026, 9, 1)))?.EffectiveBalance;
        var page = OpenDetail(ui);

        _ = page.Find("[data-invoice-action='reopen']").ClickAsync(new MouseEventArgs());
        ui.Dialogs.WaitForAssertion(() => Assert.Contains("Corrente Exemplo", ui.Dialogs.Find("[aria-label='Pagamentos a desfazer']").TextContent, StringComparison.Ordinal));
        Assert.Contains("66,66", ui.Dialogs.Find("[aria-label='Pagamentos a desfazer']").TextContent, StringComparison.Ordinal);
        await ui.Dialogs.Find("[data-confirm-cancel]").ClickAsync(new MouseEventArgs());
        Assert.Equal(before, (await ui.Entries.GetBalanceAsync(ui.Account, new DateOnly(2026, 9, 1)))?.EffectiveBalance);
        page.WaitForAssertion(() => Assert.Equal("Paga", Stat(page, "invoice-status")));

        _ = page.Find("[data-invoice-action='reopen']").ClickAsync(new MouseEventArgs());
        ui.Dialogs.WaitForAssertion(() => Assert.NotEmpty(ui.Dialogs.FindAll("[data-confirm-ok]")));
        await ui.Dialogs.Find("[data-confirm-ok]").ClickAsync(new MouseEventArgs());
        page.WaitForAssertion(() => Assert.Equal("Aberta", Stat(page, "invoice-status")));
        Assert.Equal(before + new Money(66.66m), (await ui.Entries.GetBalanceAsync(ui.Account, new DateOnly(2026, 9, 1)))?.EffectiveBalance);

        await ChooseAsync(ui, page.Find("[data-invoice-menu] button"), "Histórico");
        ui.Dialogs.WaitForAssertion(() => Assert.Contains("Pagamento desfeito", ui.Dialogs.Markup, StringComparison.Ordinal));
        Assert.DoesNotContain("irreversível", ui.Dialogs.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CashbackIsCreatedEditedAndDeletedAfterConfirmationAndTheInvoiceShowsTheCredit()
    {
        await using var ui = await EntryUi.CreateAsync();
        var page = OpenCards(ui);
        await ChooseAsync(ui, page.Find(".page-head-actions .mud-menu button"), "Cashback e descontos");
        ui.Dialogs.WaitForAssertion(() => Assert.Contains("Cartão Exemplo", ui.Dialogs.Find("#allowance-card").TextContent, StringComparison.Ordinal));
        var history = "[aria-label='Histórico de abatimentos'] tbody tr";
        await ui.Dialogs.Find("#allowance-card").ChangeAsync(ui.Card.ToString());
        ui.Dialogs.Find("#allowance-amount").Input("8");
        ui.Dialogs.WaitForAssertion(() => Assert.Equal(8m, ui.Dialogs.FindComponent<MoneyField>().Instance.Value));
        await ui.Dialogs.Find("#allowance-form").SubmitAsync(EventArgs.Empty);
        ui.Dialogs.WaitForAssertion(() => Assert.Contains("R$ 8,00", ui.Dialogs.Find(history).TextContent, StringComparison.Ordinal));

        await ui.Dialogs.Find($"{history} [data-action=edit]").ClickAsync(new MouseEventArgs());
        ui.Dialogs.Find("#allowance-amount").Input("10");
        ui.Dialogs.WaitForAssertion(() => Assert.Equal(10m, ui.Dialogs.FindComponent<MoneyField>().Instance.Value));
        await ui.Dialogs.Find("#allowance-form").SubmitAsync(EventArgs.Empty);
        ui.Dialogs.WaitForAssertion(() => Assert.Contains("R$ 10,00", ui.Dialogs.Find(history).TextContent, StringComparison.Ordinal));
        Assert.Contains("R$ 10,00", ui.Dialogs.Find("[data-cashback-total]").TextContent, StringComparison.Ordinal);

        var invoice = Assert.Single(await ui.Context.Services.GetRequiredService<IInvoiceCatalog>().GetAsync());
        Assert.Equal(InvoiceStatus.Open, invoice.Status);
        Assert.True(invoice.RemainingBalance.IsNegative);

        _ = ui.Dialogs.Find($"{history} [data-action=delete]").ClickAsync(new MouseEventArgs());
        ui.Dialogs.WaitForAssertion(() => Assert.NotEmpty(ui.Dialogs.FindAll("[data-confirm-cancel]")));
        await ui.Dialogs.Find("[data-confirm-cancel]").ClickAsync(new MouseEventArgs());
        ui.Dialogs.WaitForAssertion(() => Assert.Empty(ui.Dialogs.FindAll("[data-confirm-cancel]")));
        Assert.NotEmpty(ui.Dialogs.FindAll(history));

        _ = ui.Dialogs.Find($"{history} [data-action=delete]").ClickAsync(new MouseEventArgs());
        ui.Dialogs.WaitForAssertion(() => Assert.NotEmpty(ui.Dialogs.FindAll("[data-confirm-ok]")));
        await ui.Dialogs.Find("[data-confirm-ok]").ClickAsync(new MouseEventArgs());
        ui.Dialogs.WaitForAssertion(() => Assert.Contains("Nenhum abatimento", ui.Dialogs.Markup, StringComparison.Ordinal));
    }

    [Fact]
    public async Task TheDetailIdentifiesACredit()
    {
        await using var ui = await EntryUi.CreateAsync();
        var saved = await ui.Context.Services.GetRequiredService<IInvoicePaymentCatalog>().SaveAllowanceAsync(null, new(Guid.CreateVersion7(), ui.Card, s_september, new DateOnly(2026, 9, 1), new Money(10), InvoicePaymentKind.Cashback));
        Assert.True(saved.Succeeded, saved.Error);

        var page = OpenDetail(ui);

        page.WaitForAssertion(() => Assert.Contains("(crédito)", Stat(page, "invoice-remaining"), StringComparison.Ordinal));
    }

    [Fact]
    public async Task TheObligationsWindowShowsTheCurrentMonthAndCredits()
    {
        await using var ui = await EntryUi.CreateAsync();
        await BuyAsync(ui, 100);
        var page = OpenCards(ui);

        await ChooseAsync(ui, page.Find(".page-head-actions .mud-menu button"), "Saldos de faturas e parcelas futuras");

        ui.Dialogs.WaitForAssertion(() => Assert.Contains("Mês corrente", ui.Dialogs.Find("[aria-label='Em aberto por mês']").TextContent, StringComparison.Ordinal));
        Assert.Contains("R$ 100,00", ui.Dialogs.Find("[aria-label='Em aberto por cartão']").TextContent, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("cashback", "Novo cashback ou desconto")]
    [InlineData("compromissos", "Em aberto por cartão")]
    public async Task TheOldRoutesOpenTheCorrespondingWindow(string query, string expected)
    {
        await using var ui = await EntryUi.CreateAsync();
        ui.Context.Services.GetRequiredService<NavigationManager>().NavigateTo($"/cartoes?abrir={query}");

        ui.Context.Render<Cards>();

        ui.Dialogs.WaitForAssertion(() => Assert.Contains(expected, ui.Dialogs.Markup, StringComparison.Ordinal));
    }

    [Fact]
    public async Task AFailedDocumentShowsRetryWithoutConfirmingThePaymentAgain()
    {
        await using var ui = await EntryUi.CreateAsync();
        await BuyAsync(ui, 100);
        var page = OpenDetail(ui);
        await page.Find("[data-invoice-action='pay']").ClickAsync(new MouseEventArgs());
        ui.Dialogs.WaitForAssertion(() => Assert.False(ui.Dialogs.Find("[data-review]").HasAttribute("disabled")));
        ui.Dialogs.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromText("%PDF-1.7\n1 0 obj<</Type /Catalog>>endobj\ntrailer<</Root 1 0 R>>\n%%EOF\n", "fictitious.pdf", contentType: "application/pdf"));
        var attachments = ui.AttachmentsDirectory;
        Directory.Delete(attachments, true);
        await ui.Dialogs.Find("#invoice-payment-form").SubmitAsync(EventArgs.Empty);

        await ui.Dialogs.Find("[data-confirm-ok]").ClickAsync(new MouseEventArgs());

        var payments = ui.Context.Services.GetRequiredService<IInvoicePaymentCatalog>();
        ui.Dialogs.WaitForAssertion(() => Assert.Contains("Pagamento confirmado. O anexo falhou", ui.Dialogs.Markup, StringComparison.Ordinal));
        Assert.Empty(ui.Dialogs.FindAll("[data-confirm-ok]"));
        Assert.Single(await payments.GetHistoryAsync());
        Assert.Single(await ui.Context.Services.GetRequiredService<IAccountMovementCatalog>().GetAsync());

        Directory.CreateDirectory(attachments);
        await ui.Dialogs.Find("[data-retry-attachments]").ClickAsync(new MouseEventArgs());

        ui.Dialogs.WaitForAssertion(() => Assert.Empty(ui.Dialogs.FindAll("#invoice-payment-form, [data-retry-attachments]")));
        Assert.Single(await payments.GetHistoryAsync());
        Assert.Single(await ui.Context.Services.GetRequiredService<IAccountMovementCatalog>().GetAsync());
        Assert.Equal(Assert.Single(await payments.GetHistoryAsync()).ExternalId, Assert.Single(await ui.Context.Services.GetRequiredService<IAttachmentCatalog>().ListAsync()).Owner.ExternalId);
    }
}
