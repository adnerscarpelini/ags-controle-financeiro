using Ags.Finance.Modules.Accounts.Contracts;
using Ags.Finance.Modules.Transactions.Contracts;
using Ags.Finance.SharedKernel;
using Microsoft.Extensions.DependencyInjection;

namespace Ags.Finance.Modules.Transactions.Tests;

public sealed partial class EntryTests
{
    [Fact]
    public async Task MonthlyCashOccursAutomaticallyAndRemainsPendingAfterTheExpectedDate()
    {
        var result = await Entries.CreateRecurringAsync(new(Request() with { Date = new DateOnly(2026, 10, 10), Amount = new Money(142.90m) }));
        Assert.True(result.Succeeded, result.Error);
        var november = Assert.Single((await Entries.QueryAsync(new(Month: new AccountingMonth(2026, 11)))).Entries);
        Assert.Equal(new DateOnly(2026, 11, 10), november.Date);
        Assert.Equal(result.ExternalId, november.RecurringEntryExternalId);
        Assert.Equal(new DateOnly(2026, 11, 1), november.OccurrenceMonth);
        Assert.False(november.IsEffective);
        Assert.Null(november.EffectiveDate);
        Assert.Null(november.EffectiveAt);
        _clock.Now = new DateTimeOffset(2026, 12, 20, 12, 0, 0, TimeSpan.Zero);
        var overdue = Assert.Single((await Entries.QueryAsync(new(Month: new AccountingMonth(2026, 11)))).Entries);
        Assert.Equal(november.ExternalId, overdue.ExternalId);
        Assert.False(overdue.IsEffective);
        Assert.Equal(new Money(1000), (await Entries.GetBalanceAsync(_accountId, new DateOnly(2026, 12, 20)))?.EffectiveBalance);
    }

    [Theory]
    [InlineData(2027, 2, 28)]
    [InlineData(2028, 2, 29)]
    [InlineData(2027, 4, 30)]
    [InlineData(2027, 3, 31)]
    public async Task Day31UsesLastDayWithoutChangingTheSeries(int year, int month, int day)
    {
        var series = await Entries.CreateRecurringAsync(new(Request() with { Date = new DateOnly(2027, 1, 31) }));
        var occurrence = Assert.Single((await Entries.QueryAsync(new(Month: new AccountingMonth(year, month)))).Entries);
        Assert.Equal(new DateOnly(year, month, day), occurrence.Date);
        Assert.Equal(31, (await Entries.GetRecurringAsync(series.ExternalId!.Value))?.Day);
    }

    [Fact]
    public async Task EndMonthStopsGenerationAndIncomeForecastDoesNotIncreaseEffectiveBalance()
    {
        var series = await Entries.CreateRecurringAsync(new(Request() with
        {
            Date = new DateOnly(2026, 10, 10),
            SubcategoryExternalId = await IncomeSubcategoryAsync(),
            Amount = new Money(500)
        }, new AccountingMonth(2026, 12)));
        Assert.True(series.Succeeded, series.Error);
        var december = await Entries.QueryAsync(new(Month: new AccountingMonth(2026, 12)));
        Assert.Equal(new Money(500), december.Income);
        Assert.False(Assert.Single(december.Entries).IsEffective);
        Assert.Empty((await Entries.QueryAsync(new(Month: new AccountingMonth(2027, 1)))).Entries);
        Assert.Equal(new Money(1000), (await Entries.GetBalanceAsync(_accountId, new DateOnly(2027, 1, 31)))?.EffectiveBalance);
    }

    [Fact]
    public async Task ConcurrentMonthQueriesKeepOneOccurrenceWithStableIdentity()
    {
        await Entries.CreateRecurringAsync(new(Request() with { Date = new DateOnly(2026, 10, 10) }));
        var results = await Task.WhenAll(Entries.QueryAsync(new(Month: new AccountingMonth(2026, 12))), Entries.QueryAsync(new(Month: new AccountingMonth(2026, 12))));
        Assert.Equal(Assert.Single(results[0].Entries).ExternalId, Assert.Single(results[1].Entries).ExternalId);
        var all = await Entries.GetAsync();
        Assert.Equal(all.Count, all.Select(x => (x.RecurringEntryExternalId, x.OccurrenceMonth)).Distinct().Count());
    }

    [Fact]
    public async Task ExistingInactiveReferencesContinueTheirPlanAndMergeUpdatesTheSeries()
    {
        var created = await Entries.CreateRecurringAsync(new(Request() with { Date = new DateOnly(2026, 10, 10) }));
        var merchants = Provider.GetRequiredService<IMerchantCatalog>();
        var target = await merchants.SaveAsync(null, "Loja Mantida", null);
        Assert.True((await merchants.MergeAsync(_merchantId, target.ExternalId!.Value)).Succeeded);
        await merchants.DeactivateAsync(target.ExternalId.Value);
        await Provider.GetRequiredService<IClassificationCatalog>().DeactivateAsync(_subcategoryId, true);
        await Provider.GetRequiredService<IAccountCatalog>().DeactivateAccountAsync(_accountId);
        var december = Assert.Single((await Entries.QueryAsync(new(Month: new AccountingMonth(2026, 12)))).Entries);
        Assert.Equal(target.ExternalId, december.MerchantExternalId);
        Assert.False(december.IsEffective);
        Assert.Equal(target.ExternalId, (await Entries.GetRecurringAsync(created.ExternalId!.Value))?.Template.MerchantExternalId);
    }

    [Fact]
    public async Task GenericChangesCannotBypassRecurringScopeAndInvalidEndDoesNotPersist()
    {
        Assert.False((await Entries.CreateRecurringAsync(new(Request(), new AccountingMonth(2026, 9)))).Succeeded);
        Assert.Empty(await Entries.GetAsync());
        await Entries.CreateRecurringAsync(new(Request()));
        var entry = Assert.Single((await Entries.QueryAsync(new(Month: new AccountingMonth(2026, 10)))).Entries);
        Assert.False((await Entries.EditAsync(entry.ExternalId, Change(entry), EntryChangeScope.OnlyThis)).Succeeded);
        Assert.False((await Entries.DeleteAsync(entry.ExternalId, EntryChangeScope.OnlyThis)).Succeeded);
    }
}
