using Ags.Finance.Modules.Reports.Contracts;
using Ags.Finance.Modules.Transactions.Contracts;
using Ags.Finance.SharedKernel;
using Microsoft.Extensions.DependencyInjection;

namespace Ags.Finance.Modules.Reports.Tests;

public sealed class HeatMapTests : ReportFixture
{
    private static readonly ReferenceMonth s_october = new(2026, 10);
    private IHeatMapReports HeatMaps => Provider.GetRequiredService<IHeatMapReports>();

    [Fact]
    public async Task CategoryRowShowsTheTenMonthsAndTheBiggestMonthIsTheMostIntense()
    {
        for (var month = 1; month <= 10; month++) { await AddAsync(new ReferenceMonth(2026, month), month == 6 ? 900m : 100m); }
        var map = await HeatMaps.GetAsync(s_october);
        var row = Assert.Single(map.Rows, x => x is { Name: "Mercado", Level: 0 });
        Assert.Equal(12, row.Cells.Count);
        Assert.Equal(10, row.Cells.Count(x => x.Amount.Amount > 0));
        Assert.Equal(1m, row.Cells[5].Intensity);
        Assert.All(row.Cells.Where((_, index) => index != 5 && index < 10), x => Assert.True(x.Intensity is > 0 and < 1));
        Assert.Equal(0m, row.Cells[11].Intensity);
        Assert.Equal(new Money(1800m), row.Total);
    }

    [Fact]
    public async Task EachMonthColumnOfTheCategoriesAddsUpToTheMonthlyExpenses()
    {
        var transport = await SubcategoryAsync("Transporte", "Combustível");
        await AddAsync(s_october, 120m);
        await AddAsync(s_october, 80m, transport);
        await AddAsync(new ReferenceMonth(2026, 9), 30m, transport);
        await AddAsync(s_october, 1000m, income: true);
        var map = await HeatMaps.GetAsync(s_october);
        for (var index = 0; index < 12; index++)
        {
            Assert.Equal(map.Expenses[index], new Money(map.Rows.Where(x => x.Level == 0).Sum(x => x.Cells[index].Amount.Amount)));
        }
        Assert.Equal((new Money(200m), new Money(1000m), new Money(800m)), (map.Expenses[9], map.Income[9], map.Balance[9]));
        Assert.Equal(["Mercado", "Feira", "Transporte", "Combustível"], map.Rows.Select(x => x.Name));
    }

    [Fact]
    public async Task SubcategoriesAreNestedUnderTheirCategoryAndOrderedByAmount()
    {
        var dairy = await SubcategoryAsync("Mercado", "Laticínios");
        await AddAsync(s_october, 50m);
        await AddAsync(s_october, 300m, dairy);
        var rows = (await HeatMaps.GetAsync(s_october)).Rows;
        Assert.Equal([("Mercado", 0), ("Laticínios", 1), ("Feira", 1)], rows.Select(x => (x.Name, x.Level)));
    }

    [Fact]
    public async Task InvoiceAdjustmentIsItsOwnRowCountedOnce()
    {
        var card = await CardAsync("Cartão A");
        await PurchaseAsync(card, new DateOnly(2026, 10, 1), 100m);
        Assert.True((await Entries.AdjustInvoiceAsync(new(Guid.CreateVersion7(), card, s_october, new Money(140m)))).Succeeded);
        var map = await HeatMaps.GetAsync(s_october);
        Assert.Equal(new Money(40m), Assert.Single(map.Rows, x => x.IsInvoiceAdjustment).Cells[9].Amount);
        Assert.Equal(new Money(140m), map.Expenses[9]);
    }

    [Fact]
    public async Task ChangingTheYearShowsTheMonthsOfTheNewYearWithoutManualSetup()
    {
        await AddAsync(new ReferenceMonth(2025, 12), 10m);
        await AddAsync(new ReferenceMonth(2026, 1), 20m);
        var map = await HeatMaps.GetAsync(new ReferenceMonth(2025, 12));
        Assert.Equal(2025, map.Year);
        Assert.Equal(new Money(10m), map.Expenses[11]);
        Assert.Equal(Money.Zero, map.Expenses[0]);
        Assert.Equal(new Money(20m), (await HeatMaps.GetAsync(new ReferenceMonth(2026, 1))).Expenses[0]);
    }
}
