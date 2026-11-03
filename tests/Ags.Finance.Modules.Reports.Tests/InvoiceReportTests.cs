using Ags.Finance.Modules.Accounts.Contracts;
using Ags.Finance.Modules.Invoices.Contracts;
using Ags.Finance.Modules.Reports.Contracts;
using Ags.Finance.SharedKernel;
using Microsoft.Extensions.DependencyInjection;

namespace Ags.Finance.Modules.Reports.Tests;

public sealed class InvoiceReportTests : ReportFixture
{
    private static readonly AccountingMonth s_october = new(2026, 10);
    private IInvoiceReports Reports => Provider.GetRequiredService<IInvoiceReports>();

    [Fact]
    public async Task TotalRowSumsEveryCardInvoiceOfTheMonth()
    {
        foreach (var (name, amount) in new[] { ("Cartão A", 100m), ("Cartão B", 250m), ("Cartão C", 50.5m) })
        {
            await PurchaseAsync(await CardAsync(name), new DateOnly(2026, 10, 1), amount);
        }
        var report = await Reports.GetAsync(s_october);
        Assert.Equal(["Cartão A", "Cartão B", "Cartão C"], report.Cards.Select(x => x.CardName));
        Assert.Equal((new Money(400.5m), new Money(400.5m)), (report.Total.Invoice, report.Total.Open));
        Assert.All(report.Cards, x => Assert.Equal("Banco Exemplo", x.BankName));
    }

    [Fact]
    public async Task InstallmentsExtendTheChartToTheLastMonthAndAnewCardJoinsTheSeriesAutomatically()
    {
        var first = await CardAsync("Cartão A");
        await PurchaseAsync(first, new DateOnly(2026, 10, 1), 600m, installments: 6);
        var report = await Reports.GetAsync(s_october);
        Assert.Equal(new AccountingMonth(2027, 3), report.Months[^1].Month);
        Assert.Equal(new AccountingMonth(2026, 1), report.Months[0].Month);
        Assert.Equal(new Money(100m), report.Months[^1].Total);
        var second = await CardAsync("Cartão Novo", "violet");
        await PurchaseAsync(second, new DateOnly(2026, 11, 20), 40m);
        var updated = await Reports.GetAsync(s_october);
        Assert.Contains(updated.Series, x => x is { CardName: "Cartão Novo", Color: "violet" });
    }

    [Fact]
    public async Task InvoiceTotalMatchesTheInvoicesScreenAndCountsAnAdjustmentOnce()
    {
        var card = await CardAsync("Cartão A");
        await PurchaseAsync(card, new DateOnly(2026, 10, 1), 100m);
        Assert.True((await Entries.AdjustInvoiceAsync(new(Guid.CreateVersion7(), card, s_october, new Money(125m)))).Succeeded);
        var invoice = Assert.Single(await Provider.GetRequiredService<IInvoiceCatalog>().GetMonthAsync(s_october));
        var line = Assert.Single((await Reports.GetAsync(s_october)).Cards);
        Assert.Equal((invoice.Total, new Money(125m)), (line.Invoice, line.Invoice));
    }

    [Fact]
    public async Task PaymentsAllowancesAndPreviousMonthAndYearAverageAreSeparatedFromTheInvoiceTotal()
    {
        var november = new AccountingMonth(2026, 11);
        var card = await CardAsync("Cartão A");
        await PurchaseAsync(card, new DateOnly(2026, 10, 1), 200m);
        await PurchaseAsync(card, new DateOnly(2026, 10, 20), 400m);
        var payments = Provider.GetRequiredService<IInvoicePaymentCatalog>();
        var paid = await payments.ConfirmPaymentAsync(new(Guid.CreateVersion7(), card, november, AccountId, new DateOnly(2026, 10, 2), new Money(100m)));
        Assert.True(paid.Succeeded, paid.Error);
        var allowance = await payments.SaveAllowanceAsync(null, new(Guid.CreateVersion7(), card, november, new DateOnly(2026, 10, 2), new Money(10m), InvoicePaymentKind.Cashback));
        Assert.True(allowance.Succeeded, allowance.Error);
        var line = Assert.Single((await Reports.GetAsync(november)).Cards);
        Assert.Equal((new Money(400m), new Money(10m), new Money(100m), new Money(290m), new Money(200m), new Money(300m)),
            (line.Invoice, line.Allowances, line.Paid, line.Open, line.PreviousMonth, line.YearAverage));
    }

    [Fact]
    public async Task ArchivedCardWithPendingInvoiceStaysInTheReportAndTheTotal()
    {
        var active = await CardAsync("Cartão A");
        var archived = await CardAsync("Cartão Antigo");
        await PurchaseAsync(active, new DateOnly(2026, 10, 1), 100m);
        await PurchaseAsync(archived, new DateOnly(2026, 10, 1), 70m);
        Assert.True((await Provider.GetRequiredService<IAccountCatalog>().DeactivateCreditCardAsync(archived)).Succeeded);
        var report = await Reports.GetAsync(s_october);
        Assert.Equal(new Money(170m), report.Total.Invoice);
        Assert.True(Assert.Single(report.Cards, x => x.CardName == "Cartão Antigo").IsArchived);
    }
}
