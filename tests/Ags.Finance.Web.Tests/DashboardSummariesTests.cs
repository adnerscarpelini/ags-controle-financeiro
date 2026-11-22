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
    public async Task ACardWithoutPurchasesShowsAZeroInvoice()
    {
        await using var ui = await EntryUi.CreateAsync();

        var section = ui.Context.Render<CardInvoicesSummary>(parameters => parameters.Add(x => x.Month, s_september));

        section.WaitForAssertion(() => Assert.NotEmpty(section.FindAll("[data-card-invoice]")));
        Assert.Equal("Fatura zerada", section.Find("[data-invoice-due-text]").TextContent);
        Assert.Equal("R$ 0,00", section.Find("[data-invoices-total]").TextContent);
        Assert.Contains("value-neutral", section.Find("[data-invoices-total]").ClassList);
        Assert.Empty(section.FindAll("[data-invoice-status]"));
        Assert.Empty(section.FindAll("[data-invoice-deductions]"));
    }

    [Fact]
    public async Task AnInvoiceShowsItsDueDateTotalAndUsedLimit()
    {
        await using var ui = await EntryUi.CreateAsync();
        Assert.True((await ui.Entries.CreateAsync(new(new DateOnly(2026, 8, 30), ui.Merchant, ui.ExpenseSubcategory, ui.Account, new Money(100), string.Empty, s_september, ui.Card))).Succeeded);

        var section = ui.Context.Render<CardInvoicesSummary>(parameters => parameters.Add(x => x.Month, s_september));

        section.WaitForAssertion(() => Assert.Contains("Vence em", section.Find("[data-invoice-due-text]").TextContent, StringComparison.Ordinal));
        Assert.Equal("R$ 100,00", section.Find("[data-invoice-total]").TextContent);
        Assert.Equal("R$ 100,00", section.Find("[data-invoice-remaining]").TextContent);
        Assert.Equal("R$ 0,00", section.Find("[data-invoice-deductions]").TextContent);
        Assert.Contains("value-negative", section.Find("[data-invoice-total]").ClassList);
        Assert.Equal("Aberta", section.Find("[data-invoice-status]").TextContent);
        Assert.Empty(section.FindAll("[data-limit-available]"));
    }

    [Fact]
    public async Task AnAnticipatedPaymentKeepsTheFullSpendingAndReducesWhatIsLeftToPay()
    {
        await using var ui = await EntryUi.CreateAsync();
        Assert.True((await ui.Entries.CreateAsync(new(new DateOnly(2026, 8, 30), ui.Merchant, ui.ExpenseSubcategory, ui.Account, new Money(100), string.Empty, s_september, ui.Card))).Succeeded);
        var payments = ui.Context.Services.GetRequiredService<IInvoicePaymentCatalog>();
        Assert.True((await payments.ConfirmPaymentAsync(new(Guid.CreateVersion7(), ui.Card, s_september, ui.Account, new DateOnly(2026, 9, 1), new Money(30), false))).Succeeded);

        var section = ui.Context.Render<CardInvoicesSummary>(parameters => parameters.Add(x => x.Month, s_september));

        section.WaitForAssertion(() => Assert.Equal("R$ 70,00", section.Find("[data-invoice-remaining]").TextContent));
        Assert.Equal("R$ 100,00", section.Find("[data-invoice-total]").TextContent);
        Assert.Equal("R$ 30,00", section.Find("[data-invoice-deductions]").TextContent);
        Assert.Contains("value-positive", section.Find("[data-invoice-deductions]").ClassList);
        Assert.Equal("R$ 70,00", section.Find("[data-invoices-total]").TextContent);
        Assert.Equal("Aberta", section.Find("[data-invoice-status]").TextContent);
    }

    [Fact]
    public async Task AnArchivedCardStaysWhileItHasAnInvoiceInTheMonth()
    {
        await using var ui = await EntryUi.CreateAsync();
        Assert.True((await ui.Entries.CreateAsync(new(new DateOnly(2026, 8, 30), ui.Merchant, ui.ExpenseSubcategory, ui.Account, new Money(100), string.Empty, s_september, ui.Card))).Succeeded);
        var accounts = ui.Context.Services.GetRequiredService<IAccountCatalog>();
        Assert.Null((await accounts.DeactivateCreditCardAsync(ui.Card)).Error);

        var withInvoice = ui.Context.Render<CardInvoicesSummary>(parameters => parameters.Add(x => x.Month, s_september));
        withInvoice.WaitForAssertion(() => Assert.Contains("(arquivado)", withInvoice.Find("[data-card-invoice]").TextContent, StringComparison.Ordinal));

        var withoutInvoice = ui.Context.Render<CardInvoicesSummary>(parameters => parameters.Add(x => x.Month, new ReferenceMonth(2026, 3)));
        withoutInvoice.WaitForAssertion(() => Assert.Contains("Nenhum cartão", withoutInvoice.Markup, StringComparison.Ordinal));
    }
}
