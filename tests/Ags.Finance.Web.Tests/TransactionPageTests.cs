using Ags.Finance.Modules.Invoices.Contracts;
using Ags.Finance.Modules.Transactions.Contracts;
using Ags.Finance.SharedKernel;
using Ags.Finance.Web.Components.Pages;
using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace Ags.Finance.Web.Tests;

/// <summary>Consulta e ações por linha de Lançamentos. Os cenários de formulário estão em EntryDialogTests.</summary>
public sealed class TransactionPageTests
{
    [Fact]
    public async Task EditingARowOpensTheWindowAndSavingUpdatesTheRow()
    {
        await using var ui = await EntryUi.CreateAsync();
        await ui.Entries.CreateAsync(new(new DateOnly(2026, 9, 1), ui.Merchant, ui.ExpenseSubcategory, ui.Account, new Money(50)));
        var page = ui.Context.Render<Transactions>();
        page.WaitForAssertion(() => Assert.Contains("-R$ 50,00", page.Find(".catalog-list").TextContent, StringComparison.Ordinal));

        await page.FindAll(".catalog-list button").Single(x => x.TextContent == "Editar").ClickAsync(new MouseEventArgs());
        ui.Dialogs.WaitForAssertion(() => Assert.Equal("Loja Exemplo", ui.Dialogs.Find("#entry-merchant").GetAttribute("value")));
        ui.SetAmount("60");
        await ui.SubmitAsync();

        page.WaitForAssertion(() => Assert.Contains("-R$ 60,00", page.Find(".catalog-list").TextContent, StringComparison.Ordinal));
    }

    [Fact]
    public async Task InstallmentsCanBeAnticipatedCancelledAndDeletedByScope()
    {
        await using var ui = await EntryUi.CreateAsync();
        await ui.Entries.CreateInstallmentsAsync(new(new DateOnly(2026, 10, 2), ui.Merchant, ui.ExpenseSubcategory, ui.Account, new Money(1000), string.Empty, new ReferenceMonth(2026, 12), ui.Card, UseSelectedInvoice: true), 3);
        var page = ui.Context.Render<Transactions>();
        page.WaitForAssertion(() => Assert.Contains("Parcela 1/3", page.Markup, StringComparison.Ordinal));

        var anticipating = page.FindAll(".catalog-list li").Single(x => x.TextContent.Contains("Parcela 1/3", StringComparison.Ordinal));
        await anticipating.QuerySelectorAll("button").Single(x => x.TextContent == "Antecipar parcelas").ClickAsync(new MouseEventArgs());
        page.WaitForAssertion(() => Assert.False(page.Find("#anticipation-form button").HasAttribute("disabled")));
        await page.FindAll("[aria-label='Antecipar parcelas'] button").Single(x => x.TextContent == "Cancelar").ClickAsync(new MouseEventArgs());
        anticipating = page.FindAll(".catalog-list li").Single(x => x.TextContent.Contains("Parcela 1/3", StringComparison.Ordinal));
        await anticipating.QuerySelectorAll("button").Single(x => x.TextContent == "Antecipar parcelas").ClickAsync(new MouseEventArgs());
        page.WaitForAssertion(() => Assert.False(page.Find("#anticipation-form button").HasAttribute("disabled")));
        await page.Find("#anticipation-form").SubmitAsync(EventArgs.Empty);
        await page.FindAll("[aria-label='Antecipar parcelas'] button").Single(x => x.TextContent == "Confirmar antecipação").ClickAsync(new MouseEventArgs());
        Assert.Equal(new ReferenceMonth(2026, 9), (await ui.Entries.GetAsync()).Single(x => x.InstallmentNumber == 3).Month);

        var second = page.FindAll(".catalog-list li").Single(x => x.TextContent.Contains("Parcela 2/3", StringComparison.Ordinal));
        await second.QuerySelectorAll("button").Single(x => x.TextContent == "Excluir").ClickAsync(new MouseEventArgs());
        await page.FindAll("[role=alertdialog] button")[1].ClickAsync(new MouseEventArgs());
        Assert.Equal(3, (await ui.Entries.GetAsync()).Count(x => x.InstallmentNumber is not null));
        second = page.FindAll(".catalog-list li").Single(x => x.TextContent.Contains("Parcela 2/3", StringComparison.Ordinal));
        await second.QuerySelectorAll("button").Single(x => x.TextContent == "Excluir").ClickAsync(new MouseEventArgs());
        page.Find("#delete-scope").Change(EntryChangeScope.ThisAndFollowing.ToString());
        await page.Find("[role=alertdialog] button").ClickAsync(new MouseEventArgs());
        Assert.Single(await ui.Entries.GetAsync(), x => x.InstallmentNumber is not null);
    }

    [Fact]
    public async Task FiltersByMonthAndTextShowTheTotalsAndCanBeCleared()
    {
        await using var ui = await EntryUi.CreateAsync();
        await ui.Entries.CreateAsync(new(new DateOnly(2026, 10, 2), ui.Merchant, ui.ExpenseSubcategory, ui.Account, new Money(400)));
        await ui.Entries.CreateAsync(new(new DateOnly(2026, 11, 2), ui.Merchant, ui.ExpenseSubcategory, ui.Account, new Money(100)));
        var page = ui.Context.Render<Transactions>();
        page.WaitForAssertion(() => Assert.Equal(2, page.FindAll(".catalog-list li").Count));

        page.Find("#filter-month").Change("2026-10");
        await page.Find("#entry-filters").SubmitAsync(EventArgs.Empty);
        Assert.Single(page.FindAll(".catalog-list li"));
        Assert.Contains("-R$ 400,00", page.Find("[aria-label='Totais do filtro']").TextContent, StringComparison.Ordinal);
        Assert.Empty(page.FindAll(".entry-month-future"));

        page.Find("#entry-search").Change("sem correspondencia");
        await page.Find("#entry-filters").SubmitAsync(EventArgs.Empty);
        Assert.Empty(page.FindAll(".catalog-list li"));

        await page.FindAll("#entry-filters button").Single(x => x.TextContent == "Limpar filtros").ClickAsync(new MouseEventArgs());
        Assert.Equal(2, page.FindAll(".catalog-list li").Count);
    }

    [Fact]
    public async Task RecurringRowsAreDeletedByScopeAfterChoosingIt()
    {
        await using var ui = await EntryUi.CreateAsync();
        await ui.Entries.CreateRecurringAsync(new(new(new DateOnly(2026, 9, 10), ui.Merchant, ui.ExpenseSubcategory, ui.Account, new Money(50), IsPaid: false), new ReferenceMonth(2026, 12)));
        await ui.Entries.EnsureRecurringMonthAsync(new ReferenceMonth(2026, 11));
        var page = ui.Context.Render<Transactions>();
        page.WaitForAssertion(() => Assert.Contains("Recorrente", page.Markup, StringComparison.Ordinal));
        page.Find("#filter-month").Change("2026-10");
        page.Find("#filter-effective").Change("false");
        await page.Find("#entry-filters").SubmitAsync(EventArgs.Empty);
        Assert.Single(page.FindAll(".catalog-list li"));
        Assert.Contains("Pendente", page.Find(".catalog-list").TextContent, StringComparison.Ordinal);

        await page.FindAll(".catalog-list button").Single(x => x.TextContent == "Excluir").ClickAsync(new MouseEventArgs());
        Assert.Equal(4, page.FindAll("#delete-recurring-scope option").Count);
        await page.FindAll("[role=alertdialog] button")[1].ClickAsync(new MouseEventArgs());
        Assert.Single(page.FindAll(".catalog-list li"));
        await page.FindAll(".catalog-list button").Single(x => x.TextContent == "Excluir").ClickAsync(new MouseEventArgs());
        await page.Find("[role=alertdialog] button").ClickAsync(new MouseEventArgs());
        Assert.Contains("Escolha o alcance", page.Find("[role=status]").TextContent, StringComparison.Ordinal);
        page.Find("#delete-recurring-scope").Change(RecurringChangeScope.OnlyThis.ToString());
        await page.Find("[role=alertdialog] button").ClickAsync(new MouseEventArgs());
        Assert.Empty(page.FindAll(".catalog-list li"));
    }

    [Fact]
    public async Task APendingRowCanBeCancelledAndThenPaidWithTheChosenDate()
    {
        await using var ui = await EntryUi.CreateAsync();
        await ui.Entries.CreateAsync(new(new DateOnly(2026, 11, 10), ui.Merchant, ui.ExpenseSubcategory, ui.Account, new Money(50), IsPaid: false));
        var page = ui.Context.Render<Transactions>();
        page.WaitForAssertion(() => Assert.Single(page.FindAll(".catalog-list li")));
        page.Find("#filter-effective").Change("false");
        await page.Find("#entry-filters").SubmitAsync(EventArgs.Empty);

        await page.FindAll(".catalog-list button").Single(x => x.TextContent == "Pagar").ClickAsync(new MouseEventArgs());
        await page.FindAll("[aria-label='Registrar baixa manual'] button")[1].ClickAsync(new MouseEventArgs());
        Assert.Single(page.FindAll(".catalog-list li"));
        await page.FindAll(".catalog-list button").Single(x => x.TextContent == "Pagar").ClickAsync(new MouseEventArgs());
        await page.WaitForElement("#effective-date").ChangeAsync("2026-11-12");
        await page.Find("[aria-label='Registrar baixa manual'] button").ClickAsync(new MouseEventArgs());

        Assert.Empty(page.FindAll(".catalog-list li"));
        Assert.Equal(new DateOnly(2026, 11, 12), Assert.Single(await ui.Entries.GetAsync()).EffectiveDate);
    }

    [Fact]
    public async Task TheInvoiceScreenShowsThePurchaseClosesAdjustsAndPays()
    {
        await using var ui = await EntryUi.CreateAsync();
        await ui.Entries.CreateAsync(new(new DateOnly(2026, 10, 2), ui.Merchant, ui.ExpenseSubcategory, ui.Account, new Money(100), string.Empty, new ReferenceMonth(2026, 11), ui.Card, UseSelectedInvoice: true));
        var invoices = ui.Context.Render<Ags.Finance.Web.Components.Pages.Invoices>();
        invoices.WaitForAssertion(() => Assert.Single(invoices.FindAll(".catalog-list li")));
        await invoices.Find(".catalog-list button").ClickAsync(new MouseEventArgs());
        Assert.Contains("Loja Exemplo", invoices.Find("[aria-label='Composição da fatura']").TextContent, StringComparison.Ordinal);
        Assert.Single(invoices.FindAll("[aria-label='Composição da fatura'] li"));

        await invoices.FindAll(".catalog-list button").Single(x => x.TextContent == "Fechar").ClickAsync(new MouseEventArgs());
        await invoices.FindAll("[role=alertdialog] button")[1].ClickAsync(new MouseEventArgs());
        Assert.Empty(invoices.FindAll("[role=alertdialog]"));
        await invoices.FindAll(".catalog-list button").Single(x => x.TextContent == "Fechar").ClickAsync(new MouseEventArgs());
        await invoices.Find("[role=alertdialog] button").ClickAsync(new MouseEventArgs());
        Assert.Contains("Fechada", invoices.Find("[aria-label='Composição da fatura']").TextContent, StringComparison.Ordinal);

        var closedRow = invoices.FindAll(".catalog-list li").Single(x => x.TextContent.Contains("Fechada", StringComparison.Ordinal));
        await closedRow.QuerySelectorAll("button").Single(x => x.TextContent == "Ajustar valor").ClickAsync(new MouseEventArgs());
        invoices.Find("#invoice-correct-total").Change("100.01");
        await invoices.FindAll("[aria-label='Ajustar total da fatura'] button").Single(x => x.TextContent == "Cancelar").ClickAsync(new MouseEventArgs());
        closedRow = invoices.FindAll(".catalog-list li").Single(x => x.TextContent.Contains("Fechada", StringComparison.Ordinal));
        await closedRow.QuerySelectorAll("button").Single(x => x.TextContent == "Ajustar valor").ClickAsync(new MouseEventArgs());
        invoices.Find("#invoice-correct-total").Change("100.01");
        await invoices.Find("#invoice-adjustment-form").SubmitAsync(EventArgs.Empty);
        Assert.Contains("R$ 0,01", invoices.Find("[aria-label='Ajustar total da fatura']").TextContent, StringComparison.Ordinal);
        await invoices.FindAll("[aria-label='Ajustar total da fatura'] button").Single(x => x.TextContent == "Confirmar ajuste de fatura").ClickAsync(new MouseEventArgs());

        closedRow = invoices.FindAll(".catalog-list li").Single(x => x.TextContent.Contains("Fechada", StringComparison.Ordinal));
        await closedRow.QuerySelectorAll("button").Single(x => x.TextContent == "Pagar").ClickAsync(new MouseEventArgs());
        invoices.WaitForAssertion(() => Assert.False(invoices.Find("#invoice-payment-form button").HasAttribute("disabled")));
        Assert.Equal(ui.Account.ToString(), invoices.Find("#payment-account").GetAttribute("value"));
        await invoices.FindAll("[aria-label='Registrar pagamento de fatura'] button").Single(x => x.TextContent == "Cancelar").ClickAsync(new MouseEventArgs());
        var payments = ui.Context.Services.GetRequiredService<IInvoicePaymentCatalog>();
        Assert.Empty(await payments.GetHistoryAsync());
        closedRow = invoices.FindAll(".catalog-list li").Single(x => x.TextContent.Contains("Fechada", StringComparison.Ordinal));
        await closedRow.QuerySelectorAll("button").Single(x => x.TextContent == "Pagar").ClickAsync(new MouseEventArgs());
        invoices.WaitForAssertion(() => Assert.False(invoices.Find("#invoice-payment-form button").HasAttribute("disabled")));
        invoices.Find("#payment-settle").Change(true);
        await invoices.Find("#invoice-payment-form").SubmitAsync(EventArgs.Empty);
        Assert.Contains("definitivo", invoices.Find("[aria-label='Registrar pagamento de fatura']").TextContent, StringComparison.Ordinal);
        await invoices.FindAll("[aria-label='Registrar pagamento de fatura'] button").Single(x => x.TextContent == "Confirmar pagamento").ClickAsync(new MouseEventArgs());

        Assert.Contains("Paga", invoices.FindAll(".catalog-list li").Single(x => x.TextContent.Contains("11/2026", StringComparison.Ordinal)).TextContent, StringComparison.Ordinal);
        Assert.Single(await payments.GetHistoryAsync());
    }

    [Fact]
    public async Task TheRowShowsTheReimbursementPersonAndPercent()
    {
        await using var ui = await EntryUi.CreateAsync();
        var person = await ui.AddPersonAsync();
        await ui.Entries.CreateAsync(new(new DateOnly(2026, 9, 1), ui.Merchant, ui.ExpenseSubcategory, ui.Account, new Money(100), string.Empty, Reimbursement: new(person, 30m)));

        var page = ui.Context.Render<Transactions>();

        page.WaitForAssertion(() => Assert.Contains("Reembolso: Pessoa Exemplo · 30%", page.Find(".catalog-list li").TextContent, StringComparison.Ordinal));
    }
}
