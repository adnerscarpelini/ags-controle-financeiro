using Ags.Finance.Modules.Invoices.Contracts;
using Ags.Finance.Modules.Transactions.Contracts;
using Ags.Finance.SharedKernel;
using Microsoft.Extensions.DependencyInjection;

namespace Ags.Finance.Modules.Invoices.Tests;

public sealed partial class InvoiceTests
{
    [Theory]
    [InlineData(RecurringEditScope.OnlyThis)]
    [InlineData(RecurringEditScope.AllPending)]
    [InlineData(RecurringEditScope.All)]
    public async Task ProtectedOccurrenceRejectsEveryScopeWithoutChangingSeries(RecurringEditScope scope)
    {
        var entries = Provider.GetRequiredService<IEntryCatalog>();
        var series = await entries.CreateRecurringAsync(new(Purchase(50)));
        var november = Assert.Single(await Catalog.GetMonthAsync(new ReferenceMonth(2026, 11)));
        await Lifecycle.CloseAsync(_cardId, november.Month);
        var selected = Assert.Single(november.Entries);
        Assert.False((await entries.EditRecurringAsync(selected.ExternalId, new(Change(selected) with { Amount = new Money(60) }, scope))).Succeeded);
        Assert.False((await entries.DeleteRecurringAsync(selected.ExternalId, (RecurringChangeScope)(int)scope)).Succeeded);
        Assert.Equal(new Money(50), (await entries.GetRecurringAsync(series.ExternalId!.Value))?.Template.Amount);
        Assert.Equal(new Money(50), Assert.Single(await Catalog.GetMonthAsync(new ReferenceMonth(2026, 12))).Total);
    }

    [Fact]
    public async Task AllFailsAsOneOperationWhenALaterInvoiceIsClosed()
    {
        var entries = Provider.GetRequiredService<IEntryCatalog>();
        await entries.CreateRecurringAsync(new(Purchase(50)));
        var october = Assert.Single(await Catalog.GetMonthAsync(new ReferenceMonth(2026, 10)));
        await Catalog.GetMonthAsync(new ReferenceMonth(2026, 11));
        await Lifecycle.CloseAsync(_cardId, new ReferenceMonth(2026, 11));
        var selected = Assert.Single(october.Entries);
        Assert.False((await entries.EditRecurringAsync(selected.ExternalId, new(Change(selected) with { Amount = new Money(60) }, RecurringEditScope.All))).Succeeded);
        Assert.Equal(new Money(50), Assert.Single(await Catalog.GetMonthAsync(october.Month)).Total);
        Assert.Equal(new Money(50), Assert.Single(await Catalog.GetMonthAsync(new ReferenceMonth(2026, 12))).Total);
    }

    [Fact]
    public async Task AllChangesPastConfirmedCardOccurrencesAndKeepsTheirIdentity()
    {
        var entries = Provider.GetRequiredService<IEntryCatalog>();
        await entries.CreateRecurringAsync(new(Purchase(50)));
        var october = Assert.Single(await Catalog.GetMonthAsync(new ReferenceMonth(2026, 10)));
        var november = Assert.Single(await Catalog.GetMonthAsync(new ReferenceMonth(2026, 11)));
        _clock.Now = new DateTimeOffset(2026, 9, 20, 12, 0, 0, TimeSpan.Zero);
        var future = Assert.Single(november.Entries);
        Assert.True((await entries.EditRecurringAsync(future.ExternalId, new(Change(future) with { Amount = new Money(60) }, RecurringEditScope.All))).Succeeded);
        var past = Assert.Single(Assert.Single(await Catalog.GetMonthAsync(october.Month)).Entries);
        Assert.Equal(Assert.Single(october.Entries).ExternalId, past.ExternalId);
        Assert.Equal(new Money(-60), past.Amount);
        Assert.Equal(new Money(60), Assert.Single(await Catalog.GetMonthAsync(november.Month)).Total);
        Assert.True((await entries.DeleteRecurringAsync(future.ExternalId, RecurringChangeScope.All)).Succeeded);
        Assert.Equal(new Money(60), Assert.Single(await Catalog.GetMonthAsync(october.Month)).Total);
        Assert.Empty(Assert.Single(await Catalog.GetMonthAsync(november.Month)).Entries);
    }
    [Fact]
    public async Task CardRecurrenceUsesPurchaseCalendarAndAppearsInFutureInvoiceComposition()
    {
        var entries = Provider.GetRequiredService<IEntryCatalog>();
        var series = await entries.CreateRecurringAsync(new(Purchase(142.90m) with { Date = new DateOnly(2026, 9, 27) }));
        Assert.True(series.Succeeded, series.Error);
        var invoice = Assert.Single(await Catalog.GetMonthAsync(new ReferenceMonth(2026, 11)));
        var occurrence = Assert.Single(invoice.Entries);
        Assert.Equal(new DateOnly(2026, 10, 27), occurrence.Date);
        Assert.Equal(new ReferenceMonth(2026, 11), occurrence.Month);
        Assert.Equal(series.ExternalId, occurrence.RecurringEntryExternalId);
        Assert.Equal(new Money(142.90m), invoice.Total);
    }

    [Fact]
    public async Task ProtectedInvoiceNeverReceivesMissingRecurringOccurrence()
    {
        var november = new ReferenceMonth(2026, 11);
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
        Assert.Equal(new Money(100), Assert.Single(await Catalog.GetMonthAsync(new ReferenceMonth(2026, 12))).Total);
    }

    [Fact]
    public async Task ReturningAfterCutoffMaterializesEarlierPlanBeforeAutomaticClose()
    {
        var entries = Provider.GetRequiredService<IEntryCatalog>();
        await entries.CreateRecurringAsync(new(Purchase(100) with { Date = new DateOnly(2026, 9, 27) }));
        _clock.Now = new DateTimeOffset(2026, 11, 20, 12, 0, 0, TimeSpan.Zero);
        Assert.Equal(2, await Lifecycle.CloseDueAsync());
        var november = Assert.Single(await Catalog.GetMonthAsync(new ReferenceMonth(2026, 11)));
        Assert.Equal(InvoiceStatus.Closed, november.Status);
        Assert.Equal(new Money(100), november.Total);
        Assert.Single(november.Entries);
        Assert.False((await entries.CreateAsync(Purchase(50, 11))).Succeeded);
    }
}
