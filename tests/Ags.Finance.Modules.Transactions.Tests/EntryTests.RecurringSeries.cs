using Ags.Finance.Modules.Transactions.Contracts;
using Ags.Finance.SharedKernel;

namespace Ags.Finance.Modules.Transactions.Tests;

public sealed partial class EntryTests
{
    private async Task<Guid> PendingSeriesAsync(decimal amount = 100, ReferenceMonth? end = null, DateOnly? date = null)
    {
        var series = await Entries.CreateRecurringAsync(new(Request() with { Date = date ?? new DateOnly(2026, 10, 5), Amount = new Money(amount), IsPaid = false }, end));
        Assert.True(series.Succeeded, series.Error);
        return series.ExternalId!.Value;
    }

    [Fact]
    public async Task ActiveSeriesAreListedWithNamesAndEndedOrDeletedOnesLeave()
    {
        var active = await PendingSeriesAsync();
        await PendingSeriesAsync(end: new ReferenceMonth(2026, 10));
        var deleted = await PendingSeriesAsync();
        Assert.True((await Entries.DeleteRecurringSeriesAsync(deleted)).Succeeded);

        var series = Assert.Single(await Entries.GetRecurringSeriesAsync());

        Assert.Equal((active, 5, new Money(100), CategoryKind.Expense), (series.ExternalId, series.Day, series.Amount, series.Kind));
        Assert.False(string.IsNullOrEmpty(series.MerchantName));
        Assert.Contains(">", series.Classification, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EditingAPendingSeriesKeepsEarlierPendingOnesAndChangesFromTheCurrentMonth()
    {
        var series = await PendingSeriesAsync();
        var october = await OccurrenceAsync(2026, 10);
        var november = await OccurrenceAsync(2026, 11);

        var result = await Entries.EditRecurringSeriesAsync(series, new(new Money(200), 20));

        Assert.True(result.Succeeded, result.Error);
        Assert.Equal((new Money(-100), october.Date), ((await OccurrenceAsync(2026, 10)).Amount, (await OccurrenceAsync(2026, 10)).Date));
        Assert.Equal((new Money(-200), new DateOnly(2026, 11, 20)), ((await OccurrenceAsync(2026, 11)).Amount, (await OccurrenceAsync(2026, 11)).Date));
        Assert.Equal(november.ExternalId, (await OccurrenceAsync(2026, 11)).ExternalId);
        Assert.Equal((new Money(-200), new DateOnly(2027, 2, 20)), ((await OccurrenceAsync(2027, 2)).Amount, (await OccurrenceAsync(2027, 2)).Date));
        Assert.Equal((new Money(200), 20), ((await Entries.GetRecurringSeriesAsync()).Single().Amount, (await Entries.GetRecurringSeriesAsync()).Single().Day));
    }

    [Fact]
    public async Task EditingKeepsThePaidCurrentMonthAndStartsAtTheNextOpenOne()
    {
        var series = await PendingSeriesAsync(date: new DateOnly(2026, 11, 5));
        var november = await OccurrenceAsync(2026, 11);
        Assert.True((await Entries.ConfirmEffectiveAsync(november.ExternalId, november.Date)).Succeeded);

        Assert.True((await Entries.EditRecurringSeriesAsync(series, new(new Money(200), 31))).Succeeded);

        Assert.Equal((new Money(-100), new DateOnly(2026, 11, 5)), ((await OccurrenceAsync(2026, 11)).Amount, (await OccurrenceAsync(2026, 11)).Date));
        Assert.Equal((new Money(-200), new DateOnly(2026, 12, 31)), ((await OccurrenceAsync(2026, 12)).Amount, (await OccurrenceAsync(2026, 12)).Date));
        Assert.Equal(new DateOnly(2027, 2, 28), (await OccurrenceAsync(2027, 2)).Date);
        Assert.Equal(new Money(900), await BalanceAsync(_accountId, new DateOnly(2027, 2, 28)));
    }

    [Fact]
    public async Task ChangingTheAccountMovesOnlyOpenAndFutureOccurrences()
    {
        var destination = await SecondCheckingAsync();
        var series = await PendingSeriesAsync(date: new DateOnly(2026, 11, 5));
        var november = await OccurrenceAsync(2026, 11);
        Assert.True((await Entries.ConfirmEffectiveAsync(november.ExternalId, november.Date)).Succeeded);

        Assert.True((await Entries.EditRecurringSeriesAsync(series, new(new Money(100), 5, destination))).Succeeded);

        Assert.Equal(_accountId, (await OccurrenceAsync(2026, 11)).AccountExternalId);
        Assert.Equal(destination, (await OccurrenceAsync(2026, 12)).AccountExternalId);
        Assert.Equal(destination, (await OccurrenceAsync(2027, 3)).AccountExternalId);
    }

    [Fact]
    public async Task InvalidValuesAreRefusedWithoutSaving()
    {
        var series = await PendingSeriesAsync();

        Assert.False((await Entries.EditRecurringSeriesAsync(series, new(new Money(200), 32))).Succeeded);
        Assert.False((await Entries.EditRecurringSeriesAsync(series, new(new Money(200), 0))).Succeeded);
        Assert.False((await Entries.EditRecurringSeriesAsync(series, new(Money.Zero, 5))).Succeeded);
        Assert.False((await Entries.EditRecurringSeriesAsync(Guid.CreateVersion7(), new(new Money(200), 5))).Succeeded);

        Assert.Equal((new Money(100), 5), ((await Entries.GetRecurringSeriesAsync()).Single().Amount, (await Entries.GetRecurringSeriesAsync()).Single().Day));
    }

    [Fact]
    public async Task DeletingRemovesOpenAndFutureAndKeepsWhatAlreadyPassed()
    {
        var series = await PendingSeriesAsync(date: new DateOnly(2026, 10, 5));
        var october = await OccurrenceAsync(2026, 10);
        var november = await OccurrenceAsync(2026, 11);
        var december = await OccurrenceAsync(2026, 12);
        Assert.True((await Entries.ConfirmEffectiveAsync(november.ExternalId, november.Date)).Succeeded);

        Assert.True((await Entries.DeleteRecurringSeriesAsync(series)).Succeeded);

        var remaining = (await Entries.GetAsync()).Select(x => x.ExternalId).ToList();
        Assert.Contains(october.ExternalId, remaining);
        Assert.Contains(november.ExternalId, remaining);
        Assert.DoesNotContain(december.ExternalId, remaining);
        Assert.Empty((await Entries.QueryAsync(new(Month: new ReferenceMonth(2027, 1)))).Entries);
        Assert.Empty(await Entries.GetRecurringSeriesAsync());
    }
}
