using Ags.Finance.Modules.Invoices.Contracts;
using Ags.Finance.Modules.Transactions.Contracts;
using Ags.Finance.SharedKernel;
using Microsoft.Extensions.DependencyInjection;

namespace Ags.Finance.Modules.Invoices.Tests;

public sealed partial class InvoiceTests
{
    [Fact]
    public async Task CardRecurrenceUsesPurchaseCalendarAndAppearsInFutureInvoiceComposition()
    {
        var entries = Provider.GetRequiredService<IEntryCatalog>();
        var series = await entries.CreateRecurringAsync(new(Purchase(142.90m) with { Date = new DateOnly(2026, 9, 27) }));
        Assert.True(series.Succeeded, series.Error);
        var invoice = Assert.Single(await Catalog.GetMonthAsync(new AccountingMonth(2026, 11)));
        var occurrence = Assert.Single(invoice.Entries);
        Assert.Equal(new DateOnly(2026, 10, 27), occurrence.Date);
        Assert.Equal(new AccountingMonth(2026, 11), occurrence.Month);
        Assert.Equal(series.ExternalId, occurrence.RecurringEntryExternalId);
        Assert.Equal(new Money(142.90m), invoice.Total);
    }

    [Fact]
    public async Task ProtectedInvoiceNeverReceivesMissingRecurringOccurrence()
    {
        var november = new AccountingMonth(2026, 11);
        await Lifecycle.CloseAsync(_cardId, november);
        var entries = Provider.GetRequiredService<IEntryCatalog>();
        await entries.CreateRecurringAsync(new(Purchase(100) with { Date = new DateOnly(2026, 9, 27) }));
        var generated = await entries.EnsureRecurringMonthAsync(november);
        Assert.Equal(0, generated.GeneratedCount);
        Assert.Equal(1, generated.ProtectedCount);
        var frozen = Assert.Single(await Catalog.GetMonthAsync(november));
        Assert.Equal(InvoiceStatus.Closed, frozen.Status);
        Assert.Empty(frozen.Entries);
        Assert.Equal(Money.Zero, frozen.Total);
        Assert.Equal(new Money(100), Assert.Single(await Catalog.GetMonthAsync(new AccountingMonth(2026, 12))).Total);
    }

    [Fact]
    public async Task ReturningAfterCutoffMaterializesEarlierPlanBeforeAutomaticClose()
    {
        var entries = Provider.GetRequiredService<IEntryCatalog>();
        await entries.CreateRecurringAsync(new(Purchase(100) with { Date = new DateOnly(2026, 9, 27) }));
        _clock.Now = new DateTimeOffset(2026, 11, 20, 12, 0, 0, TimeSpan.Zero);
        Assert.Equal(2, await Lifecycle.CloseDueAsync());
        var november = Assert.Single(await Catalog.GetMonthAsync(new AccountingMonth(2026, 11)));
        Assert.Equal(InvoiceStatus.Closed, november.Status);
        Assert.Equal(new Money(100), november.Total);
        Assert.Single(november.Entries);
        Assert.False((await entries.CreateAsync(Purchase(50, 11))).Succeeded);
    }
}
