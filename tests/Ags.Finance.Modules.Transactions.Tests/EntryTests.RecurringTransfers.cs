using Ags.Finance.Modules.Accounts.Contracts;
using Ags.Finance.Modules.Transactions.Contracts;
using Ags.Finance.SharedKernel;
using Microsoft.Extensions.DependencyInjection;

namespace Ags.Finance.Modules.Transactions.Tests;

public sealed partial class EntryTests
{
    private static readonly ReferenceMonth s_recurringFirst = new(2026, 11);
    private static readonly ReferenceMonth s_recurringSecond = new(2026, 12);

    private IRecurringTransferCatalog Transfers => Provider.GetRequiredService<IRecurringTransferCatalog>();

    private async Task<Guid> SecondCheckingAsync() => await DestinationAsync(AccountKind.Checking, 0);

    private async Task<Money?> BalanceAsync(Guid account, DateOnly date) => (await Entries.GetBalanceAsync(account, date))?.EffectiveBalance;

    private async Task<(Guid Destination, RecurringTransferOccurrence First, RecurringTransferOccurrence Second)> SeriesAsync(bool confirmed = false, ReferenceMonth? end = null)
    {
        var destination = await SecondCheckingAsync();
        var series = await Transfers.CreateAsync(new(_accountId, destination, new DateOnly(2026, 11, 5), new Money(100), end, confirmed));
        Assert.True(series.Succeeded, series.Error);
        return (destination, Assert.Single(await Transfers.GetMonthAsync(s_recurringFirst)), Assert.Single(await Transfers.GetMonthAsync(s_recurringSecond)));
    }

    [Fact]
    public async Task TransferBetweenCheckingAccountsMovesBothBalances()
    {
        var destination = await SecondCheckingAsync();

        var result = await Movements.TransferAsync(new(Guid.CreateVersion7(), _accountId, destination, new DateOnly(2026, 11, 1), new Money(100)));

        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(new Money(900), await BalanceAsync(_accountId, new DateOnly(2026, 11, 1)));
        Assert.Equal(new Money(100), await BalanceAsync(destination, new DateOnly(2026, 11, 1)));
    }

    [Fact]
    public async Task RecurringRedemptionFromInvestmentMovesBalancesOnlyWhenConfirmed()
    {
        var reserve = await DestinationAsync(AccountKind.Investment, 500);
        var series = await Transfers.CreateAsync(new(reserve, _accountId, new DateOnly(2026, 11, 5), new Money(100), null, false));
        Assert.True(series.Succeeded, series.Error);
        var occurrence = Assert.Single(await Transfers.GetMonthAsync(s_recurringFirst));
        Assert.Equal(new Money(500), await BalanceAsync(reserve, occurrence.Date));

        var confirmed = await Transfers.ConfirmAsync(occurrence.ExternalId, occurrence.Date, occurrence.Amount);

        Assert.True(confirmed.Succeeded, confirmed.Error);
        Assert.Equal(new Money(400), await BalanceAsync(reserve, occurrence.Date));
        Assert.Equal(new Money(1100), await BalanceAsync(_accountId, occurrence.Date));
    }

    [Fact]
    public async Task SameOrArchivedAccountsAreRejectedWithoutMovingBalances()
    {
        var destination = await SecondCheckingAsync();
        Assert.False((await Transfers.CreateAsync(new(_accountId, _accountId, new DateOnly(2026, 11, 5), new Money(100)))).Succeeded);
        Assert.Null((await Provider.GetRequiredService<IAccountCatalog>().DeactivateAccountAsync(destination)).Error);

        Assert.False((await Transfers.CreateAsync(new(_accountId, destination, new DateOnly(2026, 11, 5), new Money(100)))).Succeeded);
        Assert.False((await Movements.TransferAsync(new(Guid.CreateVersion7(), _accountId, destination, new DateOnly(2026, 11, 5), new Money(100)))).Succeeded);

        Assert.False(await Transfers.HasSeriesAsync());
        Assert.Empty(await Movements.GetAsync());
    }

    [Fact]
    public async Task TheFirstOccurrenceFollowsTheSwitchAndTheNextOnesArePending()
    {
        var (destination, first, second) = await SeriesAsync(confirmed: true);

        Assert.True(first.IsConfirmed);
        Assert.False(second.IsConfirmed);
        Assert.Equal(new DateOnly(2026, 12, 5), second.Date);
        Assert.Equal(new Money(900), await BalanceAsync(_accountId, new DateOnly(2026, 12, 31)));
        Assert.Equal(new Money(100), await BalanceAsync(destination, new DateOnly(2026, 12, 31)));
    }

    [Fact]
    public async Task WithTheSwitchOffTheFirstOccurrenceIsPendingAndNothingMoves()
    {
        var (destination, first, _) = await SeriesAsync();

        Assert.False(first.IsConfirmed);
        Assert.Empty(await Movements.GetAsync());
        Assert.Equal(new Money(1000), await BalanceAsync(_accountId, new DateOnly(2026, 12, 31)));
        Assert.Equal(Money.Zero, await BalanceAsync(destination, new DateOnly(2026, 12, 31)));
    }

    [Fact]
    public async Task DayThirtyOneFallsOnTheLastDayWithoutChangingTheDayOfTheSeries()
    {
        var destination = await SecondCheckingAsync();
        Assert.True((await Transfers.CreateAsync(new(_accountId, destination, new DateOnly(2026, 12, 31), new Money(100), null, false))).Succeeded);

        Assert.Equal(new DateOnly(2027, 2, 28), Assert.Single(await Transfers.GetMonthAsync(new ReferenceMonth(2027, 2))).Date);
        Assert.Equal(new DateOnly(2027, 3, 31), Assert.Single(await Transfers.GetMonthAsync(new ReferenceMonth(2027, 3))).Date);
    }

    [Fact]
    public async Task TheEndMonthIsTheLastOccurrence()
    {
        await SeriesAsync(end: new ReferenceMonth(2027, 1));

        Assert.Single(await Transfers.GetMonthAsync(new ReferenceMonth(2027, 1)));
        Assert.Empty(await Transfers.GetMonthAsync(new ReferenceMonth(2027, 2)));
    }

    [Fact]
    public async Task ConfirmingUsesTheInformedValuesOnceAndUndoingRestoresTheBalances()
    {
        var (destination, first, _) = await SeriesAsync();
        var date = new DateOnly(2026, 11, 6);

        var confirmations = await Task.WhenAll(Transfers.ConfirmAsync(first.ExternalId, date, new Money(120)), Transfers.ConfirmAsync(first.ExternalId, date, new Money(120)));

        Assert.All(confirmations, x => Assert.True(x.Succeeded, x.Error));
        Assert.Equal(2, (await Movements.GetAsync()).Count);
        Assert.Equal(new Money(880), await BalanceAsync(_accountId, date));
        Assert.Equal(new Money(120), await BalanceAsync(destination, date));
        Assert.Equal(new Money(100), Assert.Single(await Transfers.GetMonthAsync(s_recurringSecond)).Amount);

        Assert.True((await Transfers.UndoAsync(first.ExternalId)).Succeeded);

        Assert.False(Assert.Single(await Transfers.GetMonthAsync(s_recurringFirst)).IsConfirmed);
        Assert.Empty(await Movements.GetAsync());
        Assert.Equal(new Money(1000), await BalanceAsync(_accountId, date));
    }

    [Fact]
    public async Task ThisAndFutureOpenChangesFollowingPendingOnesAndTheSeriesOnly()
    {
        var (destination, first, second) = await SeriesAsync();
        var january = Assert.Single(await Transfers.GetMonthAsync(new ReferenceMonth(2027, 1)));
        Assert.True((await Transfers.ConfirmAsync(january.ExternalId, january.Date, january.Amount)).Succeeded);

        var result = await Transfers.EditAsync(second.ExternalId, new(_accountId, destination, second.Date, new Money(200), RecurringEditScope.ThisAndFutureOpen));

        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(new Money(100), Assert.Single(await Transfers.GetMonthAsync(s_recurringFirst)).Amount);
        Assert.Equal(new Money(200), Assert.Single(await Transfers.GetMonthAsync(s_recurringSecond)).Amount);
        Assert.Equal(new Money(100), Assert.Single(await Transfers.GetMonthAsync(new ReferenceMonth(2027, 1))).Amount);
        Assert.Equal(new Money(200), Assert.Single(await Transfers.GetMonthAsync(new ReferenceMonth(2027, 2))).Amount);
        Assert.Equal(first.ExternalId, Assert.Single(await Transfers.GetMonthAsync(s_recurringFirst)).ExternalId);
    }

    [Fact]
    public async Task OnlyThisChangesOneOccurrenceAndAConfirmedOneMustBeUndoneFirst()
    {
        var (destination, first, second) = await SeriesAsync();
        Assert.True((await Transfers.EditAsync(second.ExternalId, new(_accountId, destination, new DateOnly(2026, 12, 20), new Money(200), RecurringEditScope.OnlyThis))).Succeeded);

        Assert.Equal(new DateOnly(2026, 12, 20), Assert.Single(await Transfers.GetMonthAsync(s_recurringSecond)).Date);
        Assert.Equal(new Money(100), Assert.Single(await Transfers.GetMonthAsync(new ReferenceMonth(2027, 1))).Amount);
        Assert.True((await Transfers.ConfirmAsync(first.ExternalId, first.Date, first.Amount)).Succeeded);
        Assert.False((await Transfers.EditAsync(first.ExternalId, new(_accountId, destination, first.Date, new Money(5), RecurringEditScope.OnlyThis))).Succeeded);
        Assert.False((await Transfers.DeleteAsync(first.ExternalId, RecurringChangeScope.OnlyThis)).Succeeded);
    }

    [Fact]
    public async Task DeletingOnlyThisDoesNotRecreateTheMonth()
    {
        var (_, _, second) = await SeriesAsync();

        Assert.True((await Transfers.DeleteAsync(second.ExternalId, RecurringChangeScope.OnlyThis)).Succeeded);

        Assert.Empty(await Transfers.GetMonthAsync(s_recurringSecond));
        Assert.Single(await Transfers.GetMonthAsync(new ReferenceMonth(2027, 1)));
    }

    [Fact]
    public async Task DeletingThisAndFollowingKeepsConfirmedOnesAndStopsTheSeries()
    {
        var (_, first, second) = await SeriesAsync(confirmed: true);
        Assert.True(first.IsConfirmed);

        Assert.True((await Transfers.DeleteAsync(second.ExternalId, RecurringChangeScope.ThisAndFollowing)).Succeeded);

        Assert.Single(await Transfers.GetMonthAsync(s_recurringFirst));
        Assert.Empty(await Transfers.GetMonthAsync(s_recurringSecond));
        Assert.Empty(await Transfers.GetMonthAsync(new ReferenceMonth(2027, 1)));
    }

    [Fact]
    public async Task DeletingAllKeepsTheConfirmedHistory()
    {
        var (_, first, second) = await SeriesAsync(confirmed: true);

        Assert.True((await Transfers.DeleteAsync(second.ExternalId, RecurringChangeScope.All)).Succeeded);

        Assert.Equal(first.ExternalId, Assert.Single(await Transfers.GetMonthAsync(s_recurringFirst)).ExternalId);
        Assert.Empty(await Transfers.GetMonthAsync(s_recurringSecond));
        Assert.Equal(2, (await Movements.GetAsync()).Count);
    }

    [Fact]
    public async Task APendingOccurrenceEntersTheForecastOfBothAccountsButNotTheCurrentBalance()
    {
        var (destination, _, _) = await SeriesAsync();
        var today = new DateOnly(2026, 11, 1);

        var source = await Entries.GetBalanceForecastAsync(_accountId, today, s_recurringFirst);
        var target = await Entries.GetBalanceForecastAsync(destination, today, s_recurringFirst);
        var total = await Entries.GetBalanceForecastAsync(null, today, s_recurringFirst);

        Assert.Equal((new Money(1000), new Money(900)), (source?.CurrentBalance, source?.ForecastBalance));
        Assert.Equal((Money.Zero, new Money(100)), (target?.CurrentBalance, target?.ForecastBalance));
        Assert.Equal(total?.CurrentBalance, total?.ForecastBalance);
    }

    [Fact]
    public async Task ARecurringTransferStaysOutOfIncomeExpensesAndBudget()
    {
        await SeriesAsync(confirmed: true);

        var budget = await Entries.QueryAsync(new(Month: s_recurringFirst));

        Assert.Equal((Money.Zero, Money.Zero), (budget.Income, budget.Expenses));
        Assert.Empty(await Entries.GetAsync());
        Assert.Empty(await Provider.GetRequiredService<IEntryTotals>().GetMonthlyAsync(s_recurringFirst, s_recurringSecond));
    }

    [Fact]
    public async Task PendingOnesAreListedByDateIncludingOverdueOnes()
    {
        var (_, first, second) = await SeriesAsync();

        Assert.Equal([first.ExternalId, second.ExternalId], (await Transfers.GetPendingAsync(null, new DateOnly(2026, 12, 31))).Select(x => x.ExternalId));
        Assert.Equal([second.ExternalId], (await Transfers.GetPendingAsync(new DateOnly(2026, 12, 1), new DateOnly(2026, 12, 31))).Select(x => x.ExternalId));
    }
}
