using Ags.Finance.Modules.Transactions.Contracts;
using Ags.Finance.Modules.Transactions.Domain;
using Ags.Finance.Modules.Transactions.Infrastructure;
using Ags.Finance.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RepeatPeriod = Ags.Finance.Modules.Transactions.Contracts.RepeatPeriod;

namespace Ags.Finance.Modules.Transactions.Tests;

public sealed partial class EntryTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RepetitionCreatesFullValuesAndOnlyTheFirstOccurrenceFollowsTheSwitch(bool paid)
    {
        var result = await Entries.CreateRepeatedAsync(Request() with { Date = new DateOnly(2027, 2, 10), Amount = new Money(200), IsPaid = paid }, 8, RepeatPeriod.Months);
        Assert.True(result.Succeeded, result.Error);
        var entries = (await Entries.GetAsync()).OrderBy(x => x.RepetitionNumber).ToList();
        Assert.Equal(8, entries.Count);
        Assert.Equal(new DateOnly(2027, 9, 10), entries[^1].Date);
        Assert.Equal(Enumerable.Range(1, 8), entries.Select(x => x.RepetitionNumber!.Value));
        Assert.All(entries, x => { Assert.Equal(8, x.RepetitionCount); Assert.Equal(result.ExternalId, x.RepetitionExternalId); Assert.Equal(new Money(-200), x.Amount); Assert.Equal(ReferenceMonth.FromDate(x.Date), x.Month); Assert.Equal("Loja Exemplo", x.MerchantName); });
        Assert.Equal(paid, entries[0].IsEffective);
        Assert.All(entries.Skip(1), x => Assert.False(x.IsEffective));
        Assert.Equal(new Money(paid ? 800 : 1000), (await Entries.GetBalanceAsync(_accountId, new DateOnly(2027, 9, 30)))?.EffectiveBalance);
    }

    [Theory]
    [InlineData(RepeatPeriod.Months, 2027, 1, 31, 2, 2027, 2, 28)]
    [InlineData(RepeatPeriod.Months, 2027, 1, 31, 3, 2027, 3, 31)]
    [InlineData(RepeatPeriod.Years, 2028, 2, 29, 2, 2029, 2, 28)]
    [InlineData(RepeatPeriod.Years, 2028, 2, 29, 5, 2032, 2, 29)]
    [InlineData(RepeatPeriod.Weeks, 2026, 12, 28, 2, 2027, 1, 4)]
    [InlineData(RepeatPeriod.Days, 2026, 12, 31, 2, 2027, 1, 1)]
    public async Task RepetitionDatesUseTheOriginalAnchorAndCalendarLimits(RepeatPeriod period, int year, int month, int day, int count, int endYear, int endMonth, int endDay)
    {
        Assert.True((await Entries.CreateRepeatedAsync(Request() with { Date = new DateOnly(year, month, day) }, count, period)).Succeeded);
        var last = (await Entries.GetAsync()).MaxBy(x => x.RepetitionNumber)!;
        Assert.Equal(new DateOnly(endYear, endMonth, endDay), last.Date);
        Assert.Equal(ReferenceMonth.FromDate(last.Date), last.Month);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(121)]
    public async Task InvalidRepeatCountDoesNotPersistAnything(int count)
    {
        Assert.False((await Entries.CreateRepeatedAsync(Request(), count, RepeatPeriod.Months)).Succeeded);
        Assert.Empty(await Entries.GetAsync());
    }

    [Fact]
    public async Task RepetitionRejectsCardInvalidPeriodAndCalendarOverflowAndAccepts120Occurrences()
    {
        Assert.False((await Entries.CreateRepeatedAsync(Request() with { CardExternalId = await CreateCardAsync() }, 2, RepeatPeriod.Months)).Succeeded);
        Assert.False((await Entries.CreateRepeatedAsync(Request(), 2, (RepeatPeriod)999)).Succeeded);
        Assert.False((await Entries.CreateRepeatedAsync(Request() with { Date = DateOnly.MaxValue }, 2, RepeatPeriod.Days)).Succeeded);
        Assert.Empty(await Entries.GetAsync());
        Assert.True((await Entries.CreateRepeatedAsync(Request(), 120, RepeatPeriod.Days)).Succeeded);
        Assert.Equal(120, (await Entries.GetAsync()).Count);
    }

    [Fact]
    public async Task RepetitionScopesPreservePaidOccurrencesAndNumbersAndShiftDatesByTheSameDayDelta()
    {
        await Entries.CreateRepeatedAsync(Request() with { Date = new DateOnly(2027, 1, 31) }, 8, RepeatPeriod.Months);
        var entries = (await Entries.GetAsync()).OrderBy(x => x.RepetitionNumber).ToList();
        await Entries.ConfirmEffectiveAsync(entries[4].ExternalId, entries[4].Date);
        Assert.False((await Entries.EditAsync(entries[0].ExternalId, Change(entries[0]), EntryChangeScope.OnlyThis)).Succeeded);
        var selected = entries[2];
        Assert.True((await Entries.EditAsync(selected.ExternalId, Change(selected) with { Amount = new Money(200), Date = selected.Date.AddDays(5) }, EntryChangeScope.ThisAndFollowing)).Succeeded);
        var changed = (await Entries.GetAsync()).OrderBy(x => x.RepetitionNumber).ToList();
        Assert.Equal(entries[0].Date, changed[0].Date);
        Assert.Equal(entries[4].Date, changed[4].Date);
        Assert.Equal(entries[4].Amount, changed[4].Amount);
        Assert.Equal(entries[7].Date.AddDays(5), changed[7].Date);
        Assert.Equal(new Money(-200), changed[7].Amount);
        Assert.True((await Entries.DeleteAsync(selected.ExternalId, EntryChangeScope.OnlyThis)).Succeeded);
        Assert.Equal([1, 2, 4, 5, 6, 7, 8], (await Entries.GetAsync()).OrderBy(x => x.RepetitionNumber).Select(x => x.RepetitionNumber!.Value));
        var first = (await Entries.GetAsync()).Single(x => x.RepetitionNumber == 1);
        Assert.True((await Entries.EditAsync(first.ExternalId, Change(first) with { Amount = new Money(250) }, EntryChangeScope.All)).Succeeded);
        Assert.All(await Entries.GetAsync(), x => Assert.Equal(new Money(-250), x.Amount));
        Assert.True((await Entries.DeleteAsync(first.ExternalId, EntryChangeScope.All)).Succeeded);
        Assert.Empty(await Entries.GetAsync());
    }

    [Fact]
    public async Task FailureInTheMiddleOfRepetitionRollsBackEveryOccurrenceAndItsGroup()
    {
        await using var scope = Provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<TransactionsDbContext>();
        await context.Database.ExecuteSqlRawAsync("""
            CREATE FUNCTION transactions.reject_repetition() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN IF NEW."RepetitionNumber" = 3 THEN RAISE EXCEPTION 'Falha simulada'; END IF; RETURN NEW; END $$;
            CREATE TRIGGER reject_repetition BEFORE INSERT ON transactions.entries FOR EACH ROW EXECUTE FUNCTION transactions.reject_repetition();
            """);
        await Assert.ThrowsAsync<DbUpdateException>(() => Entries.CreateRepeatedAsync(Request(), 8, RepeatPeriod.Months));
        Assert.Empty(await Entries.GetAsync());
        Assert.Empty(await context.Set<EntryRepetition>().AsNoTracking().ToListAsync());
    }
}
