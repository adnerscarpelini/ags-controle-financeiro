using Ags.Finance.Modules.Transactions.Contracts;
using Ags.Finance.SharedKernel;

namespace Ags.Finance.Modules.Transactions.Tests;

public sealed partial class EntryTests
{
    [Fact]
    public async Task AllPendingPreservesPaidFutureAndPastWhileAllChangesThemAndUpdatesTheSeries()
    {
        var series = await Entries.CreateRecurringAsync(new(Request() with { Date = new DateOnly(2026, 11, 5), Amount = new Money(100) }));
        var november = await OccurrenceAsync(2026, 11);
        var december = await OccurrenceAsync(2026, 12);
        var january = await OccurrenceAsync(2027, 1);
        Assert.True((await Entries.ConfirmEffectiveAsync(january.ExternalId, january.Date)).Succeeded);
        Assert.True((await Entries.EditRecurringAsync(december.ExternalId, new(Change(december) with { Amount = new Money(200) }, RecurringEditScope.AllPending))).Succeeded);
        Assert.Equal(new Money(-100), (await OccurrenceAsync(2026, 11)).Amount);
        Assert.Equal(new Money(-100), (await OccurrenceAsync(2027, 1)).Amount);
        Assert.Equal(new Money(-200), (await OccurrenceAsync(2027, 2)).Amount);
        Assert.Equal(new Money(800), (await Entries.GetBalanceAsync(_accountId, new DateOnly(2027, 2, 28)))?.EffectiveBalance);
        Assert.Equal(series.ExternalId, (await OccurrenceAsync(2027, 2)).RecurringEntryExternalId);
        var request = new EditRecurringEntryRequest(Change(december) with { Amount = new Money(300) }, RecurringEditScope.All);
        Assert.True((await Entries.EditRecurringAsync(december.ExternalId, request)).Succeeded);
        Assert.True((await Entries.EditRecurringAsync(december.ExternalId, request)).Succeeded);
        Assert.Equal(new Money(-300), (await OccurrenceAsync(2026, 11)).Amount);
        Assert.Equal(new Money(-300), (await OccurrenceAsync(2027, 1)).Amount);
        Assert.Equal(new Money(-300), (await OccurrenceAsync(2027, 3)).Amount);
        Assert.Equal(new Money(400), (await Entries.GetBalanceAsync(_accountId, new DateOnly(2027, 3, 31)))?.EffectiveBalance);
        Assert.Equal(november.ExternalId, (await OccurrenceAsync(2026, 11)).ExternalId);
    }

    [Fact]
    public async Task OnlyThisDoesNotChangeTheSeriesOrFollowingOccurrences()
    {
        var series = await Entries.CreateRecurringAsync(new(Request() with { Amount = new Money(100) }));
        var december = await OccurrenceAsync(2026, 12);
        Assert.True((await Entries.EditRecurringAsync(december.ExternalId, new(Change(december) with { Amount = new Money(200) }, RecurringEditScope.OnlyThis))).Succeeded);
        Assert.Equal(new Money(-200), (await OccurrenceAsync(2026, 12)).Amount);
        Assert.Equal(new Money(-100), (await OccurrenceAsync(2027, 1)).Amount);
        Assert.Equal(new Money(100), (await Entries.GetRecurringAsync(series.ExternalId!.Value))?.Template.Amount);
    }
}
