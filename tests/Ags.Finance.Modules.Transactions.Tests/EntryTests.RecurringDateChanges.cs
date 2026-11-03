using Ags.Finance.Modules.Transactions.Contracts;
using Ags.Finance.SharedKernel;

namespace Ags.Finance.Modules.Transactions.Tests;

public sealed partial class EntryTests
{
    [Fact]
    public async Task CashRecurringEditsIgnoreManualMonthForEveryScope()
    {
        await Entries.CreateRecurringAsync(new(Request() with { IsPaid = false, Month = new AccountingMonth(2027, 1) }));
        foreach (var scope in Enum.GetValues<RecurringChangeScope>())
        {
            var first = (await Entries.GetAsync()).Single(x => x.OccurrenceMonth == new DateOnly(2026, 10, 1));
            Assert.True((await Entries.EditRecurringAsync(first.ExternalId, new(Change(first) with { Month = new AccountingMonth(2027, 1) }, scope))).Succeeded);
            Assert.All(await Entries.GetAsync(), entry => Assert.Equal(AccountingMonth.FromDate(entry.Date), entry.Month));
        }
    }
    [Theory]
    [InlineData(RecurringChangeScope.ThisAndFollowing, false)]
    [InlineData(RecurringChangeScope.ThisAndFollowing, true)]
    [InlineData(RecurringChangeScope.All, false)]
    [InlineData(RecurringChangeScope.All, true)]
    public async Task MovingDateAcrossMonthsKeepsMaterializedAndFutureOccurrencesConsistent(RecurringChangeScope scope, bool materialized)
    {
        await Entries.CreateRecurringAsync(new(Request() with { IsPaid = false, Date = new DateOnly(2026, 10, 10), Amount = new Money(50) }, new AccountingMonth(2026, 12)));
        var october = await OccurrenceAsync(2026, 10);
        var november = await OccurrenceAsync(2026, 11);
        EntryDetails? december = materialized ? await OccurrenceAsync(2026, 12) : null;
        var result = await Entries.EditRecurringAsync(november.ExternalId, new(Change(november) with { Date = new DateOnly(2026, 12, 15), Month = new AccountingMonth(2026, 12) }, scope));
        Assert.True(result.Succeeded, result.Error);
        var moved = await OccurrenceAsync(2026, 12);
        Assert.Equal(november.ExternalId, moved.ExternalId);
        Assert.Equal(new DateOnly(2026, 12, 15), moved.Date);
        var january = await OccurrenceAsync(2027, 1);
        Assert.Equal(new DateOnly(2027, 1, 15), january.Date);
        Assert.Equal(new Money(-50), january.Amount);
        if (december is not null) { Assert.Equal(december.ExternalId, january.ExternalId); }
        var range = await Entries.QueryPendingAsync(new(FromDate: new DateOnly(2026, 12, 1), ToDate: new DateOnly(2027, 1, 31)));
        Assert.Equal(2, range.TotalCount);
        Assert.Equal(new DateOnly(2026, 12, 15), range.Entries[0].Date);
        Assert.Equal(new DateOnly(2027, 1, 15), range.Entries[1].Date);
        Assert.Empty((await Entries.QueryAsync(new(Month: new AccountingMonth(2027, 2)))).Entries);
        var series = await Entries.GetRecurringAsync(moved.RecurringEntryExternalId!.Value);
        Assert.Equal(new AccountingMonth(2027, 1), series?.EndMonth);
        if (scope == RecurringChangeScope.ThisAndFollowing)
        {
            Assert.Equal(october.ExternalId, (await OccurrenceAsync(2026, 10)).ExternalId);
            Assert.Empty((await Entries.QueryAsync(new(Month: new AccountingMonth(2026, 11)))).Entries);
        }
        else
        {
            var shiftedOctober = await OccurrenceAsync(2026, 11);
            Assert.Equal(october.ExternalId, shiftedOctober.ExternalId);
            Assert.Equal(new DateOnly(2026, 11, 15), shiftedOctober.Date);
        }
    }
}
