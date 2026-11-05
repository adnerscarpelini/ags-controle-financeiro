using Ags.Finance.Modules.Accounts.Contracts;
using Ags.Finance.Modules.Invoices.Contracts;
using Ags.Finance.SharedKernel;
using Ags.Finance.Web.Components.Dashboard;
using Ags.Finance.Web.Components.Entries;
using Ags.Finance.Web.Components.Pages;
using Bunit;
using Microsoft.Extensions.DependencyInjection;

namespace Ags.Finance.Web.Tests;

/// <summary>Faturas do mês e contas correntes no Dashboard (#112). O relógio do cenário está em 01/09/2026.</summary>
public sealed class DashboardSummariesTests
{
    private static readonly ReferenceMonth s_september = new(2026, 9);

    [Fact]
    public async Task SavingAnEntryRefreshesTheOpenDashboardAndKeepsTheChosenMonth()
    {
        await using var ui = await EntryUi.CreateAsync();
        var page = ui.Context.Render<Dashboard>();
        page.WaitForAssertion(() => Assert.Equal("R$ 0,00", page.Find("[data-stat=behavior-spent] .stat-value").TextContent));
        page.Find("[data-month-next]").Click();
        page.Find("[data-month-previous]").Click();
        page.WaitForAssertion(() => Assert.Contains("Setembro de 2026", page.Find("#behavior-title").TextContent, StringComparison.Ordinal));
        Assert.True((await ui.Entries.CreateAsync(new(new DateOnly(2026, 9, 1), ui.Merchant, ui.ExpenseSubcategory, ui.Account, new Money(100)))).Succeeded);

        await page.InvokeAsync(() => ui.Context.Services.GetRequiredService<EntryChangeNotifier>().Notify());

        page.WaitForAssertion(() => Assert.Equal("-R$ 100,00", page.Find("[data-stat=behavior-spent] .stat-value").TextContent));
        page.WaitForAssertion(() => Assert.Equal("-R$ 100,00", page.Find("[data-stat=expenses] .stat-value").TextContent));
        Assert.Contains("Setembro de 2026", page.Find("#behavior-title").TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OnlyCheckingAccountsAppearAndEnterTheTotal()
    {
        await using var ui = await EntryUi.CreateAsync();
        var accounts = ui.Context.Services.GetRequiredService<IAccountCatalog>();
        var banks = await accounts.GetBanksAsync();
        Assert.True((await accounts.SaveAccountAsync(new(null, banks[0].ExternalId, "Reserva", AccountKind.Savings, "green"))).ExternalId.HasValue);
        Assert.True((await accounts.SaveAccountAsync(new(null, banks[0].ExternalId, "Aplicações", AccountKind.Investment, "green"))).ExternalId.HasValue);

        var section = ui.Context.Render<CheckingAccountsSummary>(parameters => parameters.Add(x => x.Month, s_september));

        section.WaitForAssertion(() => Assert.NotEmpty(section.FindAll("[data-checking-account]")));
        Assert.DoesNotContain("Reserva", section.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("Aplicações", section.Markup, StringComparison.Ordinal);
        Assert.Equal(section.Find("[data-checking-account] [data-current-balance]").TextContent, section.Find("[data-current-total]").TextContent);
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
        Assert.NotEmpty(section.Find("[data-limit-available]").TextContent);
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
