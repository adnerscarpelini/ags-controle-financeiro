using Ags.Finance.Modules.Transactions.Contracts;
using Ags.Finance.SharedKernel;

namespace Ags.Finance.Modules.Transactions.Tests;

public sealed partial class EntryTests
{
    [Fact]
    public async Task SingleExceptionKeepsItsSlotWhenDateMovesAndDeletedSlotNeverReturns()
    {
        await Entries.CreateRecurringAsync(new(Request() with { IsPaid = false, Date = new DateOnly(2026, 10, 10), Amount = new Money(50) }));
        var november = await OccurrenceAsync(2026, 11);
        var edited = await Entries.EditRecurringAsync(november.ExternalId, new(Change(november) with { Date = new DateOnly(2026, 12, 15), Amount = new Money(60) }, RecurringChangeScope.OnlyThis));
        Assert.True(edited.Succeeded, edited.Error);
        var same = await OccurrenceAsync(2026, 11);
        Assert.Equal(november.ExternalId, same.ExternalId);
        Assert.Equal(new DateOnly(2026, 12, 15), same.Date);
        Assert.Equal(new Money(-60), same.Amount);
        Assert.Equal(new Money(-50), (await OccurrenceAsync(2026, 12)).Amount);
        Assert.True((await Entries.DeleteRecurringAsync(same.ExternalId, RecurringChangeScope.OnlyThis)).Succeeded);
        Assert.Empty((await Entries.QueryAsync(new(Month: new AccountingMonth(2026, 11)))).Entries);
        Assert.Empty((await Entries.QueryAsync(new(Month: new AccountingMonth(2026, 11)))).Entries);
        Assert.Equal(10, (await Entries.GetRecurringAsync(november.RecurringEntryExternalId!.Value))?.Day);
    }

    [Fact]
    public async Task FollowingSplitsSeriesAndCarriesDay31ThroughFebruary()
    {
        await Entries.CreateRecurringAsync(new(Request() with { IsPaid = false, Date = new DateOnly(2026, 10, 10), Amount = new Money(50) }));
        var december = await OccurrenceAsync(2026, 12);
        var november = await OccurrenceAsync(2026, 11);
        var result = await Entries.EditRecurringAsync(november.ExternalId, new(Change(november) with { Amount = new Money(60) }, RecurringChangeScope.ThisAndFollowing, 31));
        Assert.True(result.Succeeded, result.Error);
        var changed = await OccurrenceAsync(2026, 11);
        Assert.NotEqual(november.RecurringEntryExternalId, changed.RecurringEntryExternalId);
        Assert.Equal(november.ExternalId, changed.ExternalId);
        Assert.Equal(new DateOnly(2026, 11, 30), changed.Date);
        Assert.False(changed.IsEffective);
        Assert.Equal(new Money(-50), (await OccurrenceAsync(2026, 10)).Amount);
        Assert.Equal(december.ExternalId, (await OccurrenceAsync(2026, 12)).ExternalId);
        var february = await OccurrenceAsync(2027, 2);
        Assert.Equal(new DateOnly(2027, 2, 28), february.Date);
        Assert.Equal(new Money(-60), february.Amount);
        Assert.Equal(new DateOnly(2027, 3, 31), (await OccurrenceAsync(2027, 3)).Date);
        Assert.True((await Entries.DeleteRecurringAsync(changed.ExternalId, RecurringChangeScope.ThisAndFollowing)).Succeeded);
        Assert.Empty((await Entries.QueryAsync(new(Month: new AccountingMonth(2026, 11)))).Entries);
        Assert.Empty((await Entries.QueryAsync(new(Month: new AccountingMonth(2027, 2)))).Entries);
        Assert.Single((await Entries.QueryAsync(new(Month: new AccountingMonth(2026, 10)))).Entries);
    }

    [Fact]
    public async Task AllUpdatesPendingPastAndFutureWithoutEffectivenessAndRejectsInvalidScopeAtomically()
    {
        await Entries.CreateRecurringAsync(new(Request() with { IsPaid = false, Date = new DateOnly(2026, 10, 10), Amount = new Money(50) }));
        var november = await OccurrenceAsync(2026, 11);
        Assert.False((await Entries.EditRecurringAsync(november.ExternalId, new(Change(november), (RecurringChangeScope)999))).Succeeded);
        Assert.True((await Entries.EditRecurringAsync(november.ExternalId, new(Change(november) with { Amount = new Money(60) }, RecurringChangeScope.All))).Succeeded);
        Assert.Equal(new Money(-60), (await OccurrenceAsync(2026, 10)).Amount);
        Assert.Equal(new Money(-60), (await OccurrenceAsync(2027, 2)).Amount);
        Assert.All(await Entries.GetAsync(), x => Assert.False(x.IsEffective));
        Assert.True((await Entries.DeleteRecurringAsync(november.ExternalId, RecurringChangeScope.All)).Succeeded);
        Assert.Empty(await Entries.GetAsync());
        Assert.Empty((await Entries.QueryAsync(new(Month: new AccountingMonth(2028, 11)))).Entries);
    }

    private async Task<EntryDetails> OccurrenceAsync(int year, int month)
        => Assert.Single((await Entries.QueryAsync(new(Month: new AccountingMonth(year, month)))).Entries);
}
