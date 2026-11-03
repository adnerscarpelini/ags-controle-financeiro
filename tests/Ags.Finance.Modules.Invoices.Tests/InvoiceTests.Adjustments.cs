using Ags.Finance.Modules.Invoices.Contracts;
using Ags.Finance.Modules.Transactions.Contracts;
using Ags.Finance.SharedKernel;
using Microsoft.Extensions.DependencyInjection;

namespace Ags.Finance.Modules.Invoices.Tests;

public sealed partial class InvoiceTests
{
    private IInvoiceAdjustmentCatalog Adjustments => Provider.GetRequiredService<IInvoiceAdjustmentCatalog>();
    private AdjustInvoiceTotalRequest Adjustment(decimal total, decimal? expected = null)
        => new(Guid.CreateVersion7(), _cardId, new ReferenceMonth(2026, 10), new Money(total), expected is { } amount ? new Money(amount) : null);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AdjustmentChangesGrossAndExpensesWithoutChangingPurchasesOrCash(bool closed)
    {
        var entries = Provider.GetRequiredService<IEntryCatalog>();
        await entries.CreateAsync(Purchase(100));
        if (closed) { await Lifecycle.CloseAsync(_cardId, new ReferenceMonth(2026, 10)); }
        var request = Adjustment(100.01m, 100);
        var results = await Task.WhenAll(Adjustments.AdjustTotalAsync(request), Adjustments.AdjustTotalAsync(request));
        Assert.All(results, x => Assert.True(x.Succeeded, x.Error));
        var invoice = Assert.Single(await Catalog.GetAsync());
        Assert.Equal(new Money(100.01m), invoice.Total);
        Assert.Equal(closed ? InvoiceStatus.Closed : InvoiceStatus.Open, invoice.Status);
        Assert.Equal(new Money(-100), invoice.Entries.Single(x => !x.IsInvoiceAdjustment).Amount);
        var adjustment = Assert.Single(invoice.Entries, x => x.IsInvoiceAdjustment);
        Assert.Equal(new DateOnly(2026, 9, 1), adjustment.Date);
        Assert.Equal(new ReferenceMonth(2026, 10), adjustment.Month);
        Assert.Equal("Ajuste de fatura", adjustment.Classification);
        Assert.Equal(Guid.Empty, adjustment.MerchantExternalId);
        Assert.Equal(new Money(-0.01m), adjustment.Amount);
        Assert.Equal(new Money(-100.01m), (await entries.QueryAsync(new(Month: invoice.Month))).Expenses);
        Assert.Equal(Money.Zero, (await entries.GetBalanceAsync(_accountId, new DateOnly(2026, 10, 31)))?.EffectiveBalance);
        Assert.True((await Adjustments.AdjustTotalAsync(Adjustment(100.01m))).Succeeded);
        Assert.Equal(2, (await entries.GetAsync()).Count);
        Assert.False((await entries.EditAsync(adjustment.ExternalId, Purchase(1), EntryChangeScope.OnlyThis)).Succeeded);
        Assert.False((await entries.DeleteAsync(adjustment.ExternalId, EntryChangeScope.OnlyThis)).Succeeded);
        Assert.True((await Adjustments.AdjustTotalAsync(Adjustment(100))).Succeeded);
        Assert.Equal(3, (await entries.GetAsync()).Count);
        Assert.Equal(new Money(100), Assert.Single(await Catalog.GetAsync()).Total);
        if (closed) { Assert.False((await entries.CreateAsync(Purchase(1))).Succeeded); }
    }

    [Fact]
    public async Task PartialPaymentAndNegativeTotalSurviveAdjustmentWithoutAutomaticSettlement()
    {
        var entries = Provider.GetRequiredService<IEntryCatalog>();
        await entries.CreateAsync(Purchase(1000));
        await Lifecycle.CloseAsync(_cardId, new ReferenceMonth(2026, 10));
        await Payments.ConfirmPaymentAsync(Payment(200));
        Assert.True((await Adjustments.AdjustTotalAsync(Adjustment(1000.01m))).Succeeded);
        Assert.Equal(new Money(800.01m), Assert.Single(await Catalog.GetAsync()).RemainingBalance);
        Assert.True((await Adjustments.AdjustTotalAsync(Adjustment(-5))).Succeeded);
        var invoice = Assert.Single(await Catalog.GetAsync());
        Assert.Equal(new Money(-5), invoice.Total);
        Assert.Equal(new Money(-205), invoice.RemainingBalance);
        Assert.Equal(InvoiceStatus.Closed, invoice.Status);
        Assert.False((await Lifecycle.ReopenAsync(_cardId, invoice.Month)).Succeeded);
        Assert.Equal(new Money(200), Assert.Single(invoice.PaymentHistory).Amount);
        Assert.Equal(new Money(-200), (await entries.GetBalanceAsync(_accountId, new DateOnly(2026, 10, 31)))?.EffectiveBalance);
        Assert.Equal(new Money(5), (await entries.QueryAsync(new(Month: invoice.Month))).Expenses);
        Assert.True((await Payments.ConfirmSettlementAsync(_cardId, invoice.Month)).Succeeded);
        Assert.False((await Adjustments.AdjustTotalAsync(Adjustment(0))).Succeeded);
        Assert.Equal(3, (await entries.GetAsync()).Count);
    }

    [Fact]
    public async Task StalePreviewAndFailedPersistenceLeaveNoAdjustment()
    {
        await Provider.GetRequiredService<IEntryCatalog>().CreateAsync(Purchase(100));
        Assert.False((await Adjustments.AdjustTotalAsync(Adjustment(101, 99))).Succeeded);
        await (_database ?? throw new InvalidOperationException()).ExecuteAsync("ALTER TABLE transactions.invoice_adjustments ADD CONSTRAINT test_reject_adjustment CHECK (\"ExpenseDifference\" = 0)");
        await Assert.ThrowsAsync<Microsoft.EntityFrameworkCore.DbUpdateException>(() => Adjustments.AdjustTotalAsync(Adjustment(101)));
        Assert.Equal(new Money(100), Assert.Single(await Catalog.GetAsync()).Total);
        Assert.Single(await Provider.GetRequiredService<IEntryCatalog>().GetAsync());
    }
}
