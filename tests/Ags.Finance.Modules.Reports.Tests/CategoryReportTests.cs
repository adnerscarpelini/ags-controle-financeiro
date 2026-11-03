using Ags.Finance.Modules.Accounts.Contracts;
using Ags.Finance.Modules.Reports.Contracts;
using Ags.Finance.Modules.Transactions.Contracts;
using Ags.Finance.SharedKernel;
using Microsoft.Extensions.DependencyInjection;

namespace Ags.Finance.Modules.Reports.Tests;

public sealed class CategoryReportTests : ReportFixture
{
    private static readonly AccountingMonth s_october = new(2026, 10);
    private ICategoryReports Reports => Provider.GetRequiredService<ICategoryReports>();

    [Fact]
    public async Task CategoryAboveItsYearAverageShowsThePositiveDifference()
    {
        await AddAsync(new AccountingMonth(2026, 8), 800m);
        await AddAsync(new AccountingMonth(2026, 9), 1000m);
        await AddAsync(s_october, 1200m);
        var report = await Reports.GetAsync(s_october);
        var line = Assert.Single(report.Expenses);
        Assert.Equal(("Mercado", new Money(1200m), new Money(1000m), new Money(1000m), new Money(200m)), (line.Name, line.Amount, line.PreviousMonth, line.YearAverage, line.DifferenceToAverage));
        Assert.True(line.AboveAverage);
        Assert.Equal(100m, line.PercentOfMonth);
    }

    [Fact]
    public async Task CategoriesAreOrderedByAmountAndOmitWhenThereWasNoSpendingInTheMonth()
    {
        var transport = await SubcategoryAsync("Transporte", "Combustível");
        var health = await SubcategoryAsync("Saúde", "Farmácia");
        await AddAsync(s_october, 300m);
        await AddAsync(s_october, 700m, transport);
        await AddAsync(new AccountingMonth(2026, 9), 50m, health);
        var report = await Reports.GetAsync(s_october);
        Assert.Equal(["Transporte", "Mercado"], report.Expenses.Select(x => x.Name));
        Assert.Equal([70m, 30m], report.Expenses.Select(x => x.PercentOfMonth));
        Assert.DoesNotContain(report.Expenses, x => x.Name == "Saúde");
    }

    [Fact]
    public async Task CategoryTotalsMatchTheMonthlySummaryIncludingInvoiceAdjustmentsOnce()
    {
        var card = (await Provider.GetRequiredService<IAccountCatalog>().SaveCreditCardAsync(new(null, AccountId, "Cartão Exemplo", CardBrand.Visa, new Money(10000), 3, 10, true, "green"))).ExternalId!.Value;
        await AddAsync(s_october, 250m);
        Assert.True((await Entries.CreateAsync(new(new DateOnly(2026, 10, 1), MerchantId, ExpenseSubcategoryId, AccountId, new Money(100m), CardExternalId: card))).Succeeded);
        Assert.True((await Entries.AdjustInvoiceAsync(new(Guid.CreateVersion7(), card, s_october, new Money(130m)))).Succeeded);
        var report = await Reports.GetAsync(s_october);
        var summary = await Provider.GetRequiredService<IMonthlySummaries>().GetAsync(s_october);
        Assert.Equal(summary.Expenses, report.TotalExpenses);
        Assert.Equal(report.TotalExpenses, new Money(report.Expenses.Sum(x => x.Amount.Amount)));
        var adjustment = Assert.Single(report.Expenses, x => x.IsInvoiceAdjustment);
        Assert.Equal(("Ajuste de fatura", new Money(30m)), (adjustment.Name, adjustment.Amount));
    }

    [Fact]
    public async Task IncomeIsListedByCategoryAndSubcategoryWithTheSameColumns()
    {
        var bonus = await SubcategoryAsync("Trabalho", "Bônus", CategoryKind.Income);
        await AddAsync(new AccountingMonth(2026, 9), 5000m, income: true);
        await AddAsync(s_october, 6000m, income: true);
        await AddAsync(s_october, 2000m, bonus);
        var report = await Reports.GetAsync(s_october);
        Assert.Equal(new Money(8000m), report.TotalIncome);
        Assert.Equal(["Trabalho > Salário", "Trabalho > Bônus"], report.Income.Select(x => x.Name));
        var salary = report.Income[0];
        Assert.Equal((new Money(5000m), new Money(5500m), new Money(500m)), (salary.PreviousMonth, salary.YearAverage, salary.DifferenceToAverage));
    }
}
