using Ags.Finance.Modules.Invoices.Contracts;
using Ags.Finance.Modules.Reports.Contracts;
using Ags.Finance.Modules.Transactions.Contracts;
using Ags.Finance.SharedKernel;
using Microsoft.Extensions.DependencyInjection;

namespace Ags.Finance.Modules.Reports.Tests;

public sealed class MonthlyPlanReportTests : ReportFixture
{
    private IMonthlyPlanReports Reports => Provider.GetRequiredService<IMonthlyPlanReports>();
    private IMonthlyPlanCatalog Plans => Provider.GetRequiredService<IMonthlyPlanCatalog>();
    private static readonly ReferenceMonth s_october = new(2026, 10);
    private CreateEntryRequest Expense(decimal amount, bool paid = true) => new(new DateOnly(2026, 10, 1), MerchantId, ExpenseSubcategoryId, AccountId, new Money(amount), IsPaid: paid);

    [Fact]
    public async Task MonthlyComparisonSeparatesPaidAndPendingAndUsesOnlyReceivedIncome()
    {
        var category = (await Provider.GetRequiredService<IClassificationCatalog>().GetSubcategoriesAsync()).Single(x => x.ExternalId == ExpenseSubcategoryId).CategoryExternalId;
        Assert.True((await Plans.CreateAsync(new(s_october, new Money(1000), Categories: [new(category, new Money(200))]))).Succeeded);
        await Entries.CreateAsync(Expense(296.66m));
        await Entries.CreateAsync(Expense(50m, false));
        await Entries.CreateAsync(Expense(200) with { SubcategoryExternalId = IncomeSubcategoryId });
        await Entries.CreateAsync(Expense(900, false) with { SubcategoryExternalId = IncomeSubcategoryId });
        var report = await Reports.GetAsync(s_october);
        var line = Assert.Single(report.Categories);
        Assert.Equal((new Money(200), new Money(296.66m), new Money(50), new Money(346.66m), new Money(-146.66m)), (line.Target, line.Paid, line.Forecast, line.Total, line.Remaining));
        Assert.True(line.Exceeded);
        Assert.Equal(new Money(600), report.RemainingCategories!.Target);
        Assert.Equal(new Money(200), report.ReceivedIncome);
        Assert.Equal(new Money(800), report.PlannedExpenses);
        Assert.Equal(new Money(-600), report.PlannedBalance);
        Assert.Equal(-300m, report.PlannedSavingsPercent);
    }

    [Fact]
    public async Task BothCardViewsUseThePurchaseOrInvoiceMonthWithoutCountingPaymentsTwice()
    {
        var november = new ReferenceMonth(2026, 11);
        var december = new ReferenceMonth(2026, 12);
        var card = await CardAsync("Cartão Exemplo");
        await PurchaseAsync(card, new DateOnly(2026, 11, 20), 100);
        await Plans.CreateAsync(new(november, new Money(1000)));
        await Plans.CreateAsync(new(december, new Money(1000)));
        Assert.Equal(Money.Zero, (await Reports.GetAsync(november)).Total);
        Assert.Equal(new Money(100), (await Reports.GetAsync(december)).Total);
        await Provider.GetRequiredService<IInvoiceLifecycle>().CloseAsync(card, december);
        Assert.True((await Provider.GetRequiredService<IInvoicePaymentCatalog>().ConfirmPaymentAsync(new(Guid.CreateVersion7(), card, december, AccountId, new DateOnly(2026, 10, 2), new Money(100), true))).Succeeded);
        Assert.Equal(new Money(100), (await Reports.GetAsync(december)).Total);
        await Plans.EditAsync(new(november, new Money(1000), CardView: MonthlyPlanCardView.PurchaseDate));
        await Plans.EditAsync(new(december, new Money(1000), CardView: MonthlyPlanCardView.PurchaseDate));
        Assert.Equal(new Money(100), (await Reports.GetAsync(november)).Paid);
        Assert.Equal(Money.Zero, (await Reports.GetAsync(december)).Total);
    }

    [Fact]
    public async Task PurchaseDateViewGeneratesTheRequestedRecurringDateEvenWhenItsInvoiceIsNextMonth()
    {
        var card = await CardAsync("Cartão Exemplo");
        Assert.True((await Entries.CreateRecurringAsync(new(new(new DateOnly(2026, 11, 20), MerchantId, ExpenseSubcategoryId, AccountId, new Money(100), CardExternalId: card)))).Succeeded);
        var december = new ReferenceMonth(2026, 12);
        await Plans.CreateAsync(new(december, new Money(1000), CardView: MonthlyPlanCardView.PurchaseDate));
        var report = await Reports.GetAsync(december);
        Assert.Equal(new Money(100), report.Total);
        Assert.Equal(new Money(100), report.Paid);
    }

    [Fact]
    public async Task UnplannedCategoriesAndInvoiceAdjustmentsCountOnceAndZeroIncomeHasZeroSavingsPercent()
    {
        var card = await CardAsync("Cartão Exemplo");
        await PurchaseAsync(card, new DateOnly(2026, 10, 1), 100);
        Assert.True((await Entries.AdjustInvoiceAsync(new(Guid.CreateVersion7(), card, s_october, new Money(125)))).Succeeded);
        await Plans.CreateAsync(new(s_october, new Money(1000)));
        var report = await Reports.GetAsync(s_october);
        Assert.Empty(report.Categories);
        Assert.Equal(new Money(125), report.Total);
        Assert.Equal(new Money(125), report.RemainingCategories!.Paid);
        Assert.Equal(0m, report.PlannedSavingsPercent);
        Assert.Null((await Reports.GetAsync(s_october.AddMonths(1))).Plan);
    }
}
