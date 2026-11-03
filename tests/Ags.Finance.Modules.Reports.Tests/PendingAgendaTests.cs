using Ags.Finance.Modules.Preferences.Contracts;
using Ags.Finance.Modules.Reports.Contracts;
using Ags.Finance.SharedKernel;
using Microsoft.Extensions.DependencyInjection;

namespace Ags.Finance.Modules.Reports.Tests;

public sealed class PendingAgendaTests : ReportFixture
{
    private IPendingAgenda Agenda => Provider.GetRequiredService<IPendingAgenda>();

    private void Today(int year, int month, int day) => Clock.Now = new DateTimeOffset(year, month, day, 12, 0, 0, TimeSpan.Zero);

    private async Task<Guid> PendingAsync(DateOnly date, decimal amount, bool income = false, ReferenceMonth? month = null)
    {
        var request = new Ags.Finance.Modules.Transactions.Contracts.CreateEntryRequest(date, MerchantId, income ? IncomeSubcategoryId : ExpenseSubcategoryId, AccountId, new Money(amount), Month: month);
        var result = await Entries.CreateRecurringAsync(new(request with { IsPaid = false }));
        Assert.True(result.Succeeded, result.Error);
        return (await Entries.QueryPendingAsync(new(FromDate: date, ToDate: date, PageSize: 200))).Entries.Single(x => x.RecurringEntryExternalId == result.ExternalId).ExternalId;
    }

    [Theory]
    [InlineData(7, 8)]
    [InlineData(15, 16)]
    [InlineData(30, 1)]
    public async Task WindowFollowsThePreferenceAsAClosedIntervalOfNDaysFromToday(int days, int lastDay)
    {
        Today(2026, 11, 2);
        var saved = await Provider.GetRequiredService<IPreferences>().SaveAsync(AppPreferences.Default with { PendingWindowDays = days });
        Assert.True(saved.Succeeded, saved.Error);
        var end = days == 30 ? new DateOnly(2026, 12, lastDay) : new DateOnly(2026, 11, lastDay);
        await PendingAsync(end, 40m);
        await PendingAsync(end.AddDays(1), 999m);

        var agenda = await Agenda.GetAsync();

        Assert.Equal((new DateOnly(2026, 11, 2), end, days), (agenda.Today, agenda.WindowEnd, agenda.WindowDays));
        Assert.Equal([end], agenda.Upcoming.Items.Select(x => x.Date));
        Assert.Equal(new Money(40m), agenda.Upcoming.ToPay);
    }

    [Fact]
    public async Task WindowIsTodayThroughTodayPlusSixAndOverdueIsSeparate()
    {
        Today(2026, 11, 2);
        await PendingAsync(new DateOnly(2026, 11, 1), 30m);
        await PendingAsync(new DateOnly(2026, 11, 2), 1000m, income: true);
        await PendingAsync(new DateOnly(2026, 11, 8), 200m);
        await PendingAsync(new DateOnly(2026, 11, 9), 999m);
        var balance = (await Entries.GetBalanceAsync(AccountId, new DateOnly(2026, 12, 31)))!.EffectiveBalance;
        var agenda = await Agenda.GetAsync();
        Assert.Equal((new DateOnly(2026, 11, 2), new DateOnly(2026, 11, 8)), (agenda.Today, agenda.WindowEnd));
        Assert.Equal([new DateOnly(2026, 11, 2), new DateOnly(2026, 11, 8)], agenda.Upcoming.Items.Select(x => x.Date));
        Assert.Equal((new Money(1000m), new Money(200m)), (agenda.Upcoming.ToReceive, agenda.Upcoming.ToPay));
        var late = Assert.Single(agenda.Overdue.Items);
        Assert.Equal((new DateOnly(2026, 11, 1), new Money(30m), "Conta Exemplo", "Loja Exemplo"), (late.Date, late.Amount, late.AccountName, late.Description));
        Assert.Equal(new Money(30m), agenda.Overdue.ToPay);
        Assert.Equal(balance, (await Entries.GetBalanceAsync(AccountId, new DateOnly(2026, 12, 31)))!.EffectiveBalance);
    }

    [Fact]
    public async Task WindowCrossesTheYearEnd()
    {
        Today(2026, 12, 28);
        await PendingAsync(new DateOnly(2027, 1, 3), 10m);
        await PendingAsync(new DateOnly(2027, 1, 4), 20m);
        var agenda = await Agenda.GetAsync();
        Assert.Equal(new DateOnly(2027, 1, 3), agenda.WindowEnd);
        Assert.Equal(new DateOnly(2027, 1, 3), Assert.Single(agenda.Upcoming.Items).Date);
    }

    [Fact]
    public async Task WindowIncludesTheLeapDay()
    {
        Today(2028, 2, 26);
        await PendingAsync(new DateOnly(2028, 2, 29), 15m);
        await PendingAsync(new DateOnly(2028, 3, 3), 25m);
        await PendingAsync(new DateOnly(2028, 3, 4), 35m);
        var agenda = await Agenda.GetAsync();
        Assert.Equal(new DateOnly(2028, 3, 3), agenda.WindowEnd);
        Assert.Equal([new DateOnly(2028, 2, 29), new DateOnly(2028, 3, 3)], agenda.Upcoming.Items.Select(x => x.Date));
    }

    [Fact]
    public async Task EffectivatingAnItemRemovesItFromListAndTotal()
    {
        Today(2026, 11, 2);
        var id = await PendingAsync(new DateOnly(2026, 11, 3), 80m);
        await PendingAsync(new DateOnly(2026, 11, 4), 20m);
        Assert.Equal(new Money(100m), (await Agenda.GetAsync()).Upcoming.ToPay);
        Assert.True((await Entries.ConfirmEffectiveAsync(id, new DateOnly(2026, 11, 3))).Succeeded);
        var agenda = await Agenda.GetAsync();
        Assert.Equal(new Money(20m), agenda.Upcoming.ToPay);
        Assert.Single(agenda.Upcoming.Items);
    }

    [Fact]
    public async Task InclusionFollowsTheExpectedDateEvenWhenTheReferenceMonthDiffers()
    {
        Today(2026, 11, 2);
        await PendingAsync(new DateOnly(2026, 11, 5), 60m, month: new ReferenceMonth(2026, 12));
        var item = Assert.Single((await Agenda.GetAsync()).Upcoming.Items);
        Assert.Equal(new DateOnly(2026, 11, 5), item.Date);
    }

    [Fact]
    public async Task EmptyWindowHasZeroTotals()
    {
        Today(2026, 11, 2);
        var agenda = await Agenda.GetAsync();
        Assert.Empty(agenda.Upcoming.Items);
        Assert.Empty(agenda.Overdue.Items);
        Assert.Equal((Money.Zero, Money.Zero), (agenda.Upcoming.ToReceive, agenda.Upcoming.ToPay));
    }

    [Fact]
    public async Task TodayFollowsTheBusinessTimeZoneInsteadOfTheUtcDate()
    {
        Clock.Zone = TimeZoneInfo.CreateCustomTimeZone("business", TimeSpan.FromHours(-3), "business", "business");
        Clock.Now = new DateTimeOffset(2026, 11, 3, 1, 0, 0, TimeSpan.Zero);
        var agenda = await Agenda.GetAsync();
        Assert.Equal((new DateOnly(2026, 11, 2), new DateOnly(2026, 11, 8)), (agenda.Today, agenda.WindowEnd));
    }
}
