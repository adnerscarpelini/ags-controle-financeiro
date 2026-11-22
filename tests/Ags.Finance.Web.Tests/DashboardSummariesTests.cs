using Ags.Finance.Modules.Accounts.Contracts;
using Ags.Finance.Modules.Invoices.Contracts;
using Ags.Finance.SharedKernel;
using Ags.Finance.Web.Components.Dashboard;
using Ags.Finance.Web.Components.Entries;
using Ags.Finance.Web.Components.Pages;
using Bunit;
using Microsoft.Extensions.DependencyInjection;

namespace Ags.Finance.Web.Tests;

/// <summary>Faixas Hoje e Mês do Dashboard (#143), com faturas e contas correntes (#112). O relógio do cenário está em 01/09/2026.</summary>
public sealed class DashboardSummariesTests
{
    private static readonly ReferenceMonth s_september = new(2026, 9);

    [Fact]
    public async Task SavingAnEntryRefreshesTheOpenDashboardAndKeepsTheChosenMonth()
    {
        await using var ui = await EntryUi.CreateAsync();
        var page = ui.Context.Render<Dashboard>();
        page.WaitForAssertion(() => Assert.Equal("R$ 0,00", page.Find("[data-stat=spent] .stat-value").TextContent));
        page.Find("[data-month-next]").Click();
        page.Find("[data-month-previous]").Click();
        page.WaitForAssertion(() => Assert.Equal("Setembro de 2026", page.Find("[data-month-label]").TextContent));
        Assert.True((await ui.Entries.CreateAsync(new(new DateOnly(2026, 9, 1), ui.Merchant, ui.ExpenseSubcategory, ui.Account, new Money(100)))).Succeeded);

        await page.InvokeAsync(() => ui.Context.Services.GetRequiredService<EntryChangeNotifier>().Notify());

        page.WaitForAssertion(() => Assert.Equal("-R$ 100,00", page.Find("[data-stat=spent] .stat-value").TextContent));
        page.WaitForAssertion(() => Assert.Equal("-R$ 100,00", page.Find("[data-stat=expenses] .stat-value").TextContent));
        Assert.Equal("Setembro de 2026", page.Find("[data-month-label]").TextContent);
    }

    [Fact]
    public async Task ChangingTheMonthMovesOnlyTheMonthBandAndKeepsTodaysPosition()
    {
        await using var ui = await EntryUi.CreateAsync();
        Assert.True((await ui.Entries.CreateAsync(new(new DateOnly(2026, 9, 1), ui.Merchant, ui.ExpenseSubcategory, ui.Account, new Money(100)))).Succeeded);
        var page = ui.Context.Render<Dashboard>();
        page.WaitForAssertion(() =>
        {
            Assert.Equal("-R$ 100,00", page.Find("[data-stat=spent] .stat-value").TextContent);
            Assert.NotEmpty(page.FindAll("[data-invoices-total]"));
            Assert.NotEmpty(page.FindAll("[data-pending-title], #pending-title"));
        });
        var accounts = page.Find("[data-current-total]").TextContent;
        var invoices = page.Find("[data-invoices-total]").TextContent;
        Assert.NotNull(page.Find("[data-band=today] #today-title"));
        Assert.Contains("01/09/2026", page.Find("[data-band=today] .band-note").TextContent, StringComparison.Ordinal);

        page.Find("[data-month-next]").Click();

        page.WaitForAssertion(() => Assert.Equal("R$ 0,00", page.Find("[data-stat=spent] .stat-value").TextContent));
        Assert.Equal("Outubro de 2026", page.Find("[data-band=month] [data-month-label]").TextContent);
        Assert.Equal(accounts, page.Find("[data-current-total]").TextContent);
        Assert.Equal(invoices, page.Find("[data-invoices-total]").TextContent);
        Assert.Empty(page.FindAll("[data-band=today] [data-month-label]"));
    }

    [Fact]
    public async Task TheRemovedIndicatorsNoLongerAppear()
    {
        await using var ui = await EntryUi.CreateAsync();

        var page = ui.Context.Render<Dashboard>();

        page.WaitForAssertion(() => Assert.NotEmpty(page.FindAll("[data-month-totals] [data-stat]")));
        Assert.Equal(["income", "spent", "expenses"], page.FindAll("[data-month-totals] [data-stat]").Select(x => x.GetAttribute("data-stat")));
        page.WaitForAssertion(() => Assert.NotEmpty(page.FindAll("[data-invoices-total]")));
        var labels = page.FindAll(".stat-label").Select(x => x.TextContent.Trim()).ToList();
        foreach (var removed in new[] { "Mês anterior", "Média no ano", "% guardado", "Saldo do orçamento" })
        {
            Assert.DoesNotContain(labels, label => label.StartsWith(removed, StringComparison.Ordinal));
        }
        Assert.DoesNotContain("Saldo previsto", page.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("Limite disponível", page.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OnlyCheckingAccountsAppearAndEnterTheTotal()
    {
        await using var ui = await EntryUi.CreateAsync();
        var accounts = ui.Context.Services.GetRequiredService<IAccountCatalog>();
        var banks = await accounts.GetBanksAsync();
        Assert.True((await accounts.SaveAccountAsync(new(null, banks[0].ExternalId, "Reserva", AccountKind.Savings, "green"))).ExternalId.HasValue);
        Assert.True((await accounts.SaveAccountAsync(new(null, banks[0].ExternalId, "Aplicações", AccountKind.Investment, "green"))).ExternalId.HasValue);

        var section = ui.Context.Render<CheckingAccountsSummary>();

        section.WaitForAssertion(() => Assert.NotEmpty(section.FindAll("[data-checking-account]")));
        Assert.DoesNotContain("Reserva", section.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("Aplicações", section.Markup, StringComparison.Ordinal);
        Assert.Equal(section.Find("[data-checking-account] [data-current-balance]").TextContent, section.Find("[data-current-total]").TextContent);
        Assert.Empty(section.FindAll("[data-forecast-balance]"));
    }

    [Fact]
    public async Task WithoutPayableInvoicesTheCardSaysSoAndTheTotalIsZero()
    {
        await using var ui = await EntryUi.CreateAsync();

        var section = ui.Context.Render<CardInvoicesSummary>();

        section.WaitForAssertion(() => Assert.NotEmpty(section.FindAll("[data-no-payable]")));
        Assert.Equal("Nenhuma fatura a pagar.", section.Find("[data-no-payable]").TextContent);
        Assert.Equal("R$ 0,00", section.Find("[data-invoices-total]").TextContent);
        Assert.Contains("value-neutral", section.Find("[data-invoices-total]").ClassList);
        Assert.Empty(section.FindAll("[data-card-invoice]"));
    }

    [Fact]
    public async Task AnOpenInvoiceHighlightsWhatIsLeftWithTheTotalAndDeductionsBelow()
    {
        await using var ui = await EntryUi.CreateAsync();
        Assert.True((await ui.Entries.CreateAsync(new(new DateOnly(2026, 8, 30), ui.Merchant, ui.ExpenseSubcategory, ui.Account, new Money(2000), string.Empty, s_september, ui.Card))).Succeeded);
        var payments = ui.Context.Services.GetRequiredService<IInvoicePaymentCatalog>();
        Assert.True((await payments.ConfirmPaymentAsync(new(Guid.CreateVersion7(), ui.Card, s_september, ui.Account, new DateOnly(2026, 9, 1), new Money(500), false))).Succeeded);

        var section = ui.Context.Render<CardInvoicesSummary>();

        section.WaitForAssertion(() => Assert.Equal("R$ 1.500,00", section.Find("[data-invoice-remaining]").TextContent));
        Assert.Contains("value-negative", section.Find("[data-invoice-remaining]").ClassList);
        Assert.Equal("R$ 2.000,00", section.Find("[data-invoice-total]").TextContent);
        Assert.Equal("R$ 500,00", section.Find("[data-invoice-deductions]").TextContent);
        Assert.Equal("R$ 1.500,00", section.Find("[data-invoices-total]").TextContent);
        Assert.Equal("Aberta", section.Find("[data-invoice-status]").TextContent);
        Assert.Contains("Fecha em 03/09", section.Find("[data-card-invoice] .row-note").TextContent, StringComparison.Ordinal);
        Assert.Equal("Vence em 10/09/2026", section.Find("[data-invoice-due]").TextContent);
        Assert.Empty(section.FindAll("[data-limit-available]"));
    }

    [Fact]
    public async Task WithoutDeductionsOnlyTheTotalAppearsBelow()
    {
        await using var ui = await EntryUi.CreateAsync();
        Assert.True((await ui.Entries.CreateAsync(new(new DateOnly(2026, 8, 30), ui.Merchant, ui.ExpenseSubcategory, ui.Account, new Money(100), string.Empty, s_september, ui.Card))).Succeeded);

        var section = ui.Context.Render<CardInvoicesSummary>();

        section.WaitForAssertion(() => Assert.Equal("R$ 100,00", section.Find("[data-invoice-total]").TextContent));
        Assert.Empty(section.FindAll("[data-invoice-deductions]"));
        Assert.DoesNotContain("abatido", section.Find("[data-card-invoice]").TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AClosedInvoicePastItsDueDateComesFirstWithTheDueDateInRed()
    {
        await using var ui = await EntryUi.CreateAsync();
        Assert.True((await ui.Entries.CreateAsync(new(new DateOnly(2026, 8, 30), ui.Merchant, ui.ExpenseSubcategory, ui.Account, new Money(100), string.Empty, s_september, ui.Card))).Succeeded);
        ui.SetNow(new DateTimeOffset(2026, 9, 15, 12, 0, 0, TimeSpan.Zero));
        Assert.True((await ui.Context.Services.GetRequiredService<IInvoiceLifecycle>().CloseAsync(ui.Card, s_september)).Succeeded);
        Assert.True((await ui.Entries.CreateAsync(new(new DateOnly(2026, 9, 15), ui.Merchant, ui.ExpenseSubcategory, ui.Account, new Money(40), CardExternalId: ui.Card))).Succeeded);

        var section = ui.Context.Render<CardInvoicesSummary>();

        section.WaitForAssertion(() => Assert.Equal(2, section.FindAll("[data-card-invoice]").Count));
        var closed = section.FindAll("[data-card-invoice]")[0];
        Assert.Equal("Fechada", closed.QuerySelector("[data-invoice-status]")!.TextContent);
        Assert.Equal("Venceu em 10/09/2026", closed.QuerySelector("[data-invoice-due]")!.TextContent);
        Assert.Contains("value-negative", closed.QuerySelector("[data-invoice-due]")!.ClassList);
        Assert.DoesNotContain("Fecha em", closed.TextContent, StringComparison.Ordinal);
        var open = section.FindAll("[data-card-invoice]")[1];
        Assert.Equal("Aberta", open.QuerySelector("[data-invoice-status]")!.TextContent);
        Assert.DoesNotContain("value-negative", open.QuerySelector("[data-invoice-due]")!.ClassList);
        Assert.Equal("R$ 140,00", section.Find("[data-invoices-total]").TextContent);
    }

    [Fact]
    public async Task AnOpenInvoicePastItsCutoffDoesNotAnnounceAClosingDateInThePast()
    {
        await using var ui = await EntryUi.CreateAsync();
        Assert.True((await ui.Entries.CreateAsync(new(new DateOnly(2026, 8, 30), ui.Merchant, ui.ExpenseSubcategory, ui.Account, new Money(100), string.Empty, s_september, ui.Card))).Succeeded);
        ui.SetNow(new DateTimeOffset(2026, 9, 5, 12, 0, 0, TimeSpan.Zero));

        var section = ui.Context.Render<CardInvoicesSummary>();

        section.WaitForAssertion(() => Assert.Equal("R$ 100,00", section.Find("[data-invoice-remaining]").TextContent));
        Assert.Equal("Aberta", section.Find("[data-invoice-status]").TextContent);
        Assert.DoesNotContain("Fecha em", section.Find("[data-card-invoice]").TextContent, StringComparison.Ordinal);
        Assert.Equal("Vence em 10/09/2026", section.Find("[data-invoice-due]").TextContent);
    }

    [Fact]
    public async Task AnArchivedCardKeepsItsPayableInvoice()
    {
        await using var ui = await EntryUi.CreateAsync();
        Assert.True((await ui.Entries.CreateAsync(new(new DateOnly(2026, 8, 30), ui.Merchant, ui.ExpenseSubcategory, ui.Account, new Money(100), string.Empty, s_september, ui.Card))).Succeeded);
        var accounts = ui.Context.Services.GetRequiredService<IAccountCatalog>();
        Assert.Null((await accounts.DeactivateCreditCardAsync(ui.Card)).Error);

        var section = ui.Context.Render<CardInvoicesSummary>();

        section.WaitForAssertion(() => Assert.Contains("(arquivado)", section.Find("[data-card-invoice]").TextContent, StringComparison.Ordinal));
        Assert.Equal("R$ 100,00", section.Find("[data-invoices-total]").TextContent);
    }
}
