using Ags.Finance.Modules.Accounts.Contracts;
using Ags.Finance.Modules.Transactions.Contracts;
using Ags.Finance.SharedKernel;
using Microsoft.Extensions.DependencyInjection;

namespace Ags.Finance.Modules.Transactions.Tests;

public sealed partial class EntryTests
{
    [Fact]
    public async Task FailedFinancialOperationRollsBackStateAndDerivedBalanceTogether()
    {
        await Entries.CreateRecurringAsync(new(Request()));
        var occurrence = await OccurrenceAsync(2026, 10);
        var result = await Provider.GetRequiredService<IFinancialOperation>().ExecuteAsync(async token =>
        {
            Assert.True((await Entries.ConfirmEffectiveAsync(occurrence.ExternalId, occurrence.Date, token)).Succeeded);
            return false;
        }, x => x);
        Assert.False(result);
        Assert.False((await OccurrenceAsync(2026, 10)).IsEffective);
        Assert.Equal(new Money(1000), (await Entries.GetBalanceAsync(_accountId, new DateOnly(2026, 11, 30)))?.EffectiveBalance);
    }
    [Theory]
    [InlineData(false, 900)]
    [InlineData(true, 1100)]
    public async Task ManualConfirmationIsPerOccurrenceUsesRecordedFieldsAndIsConcurrentIdempotent(bool income, int expectedBalance)
    {
        await Entries.CreateRecurringAsync(new(Request() with { Amount = new Money(100), SubcategoryExternalId = income ? await IncomeSubcategoryAsync() : _subcategoryId }));
        var november = await OccurrenceAsync(2026, 11);
        var date = new DateOnly(2026, 11, 8);
        var results = await Task.WhenAll(Entries.ConfirmEffectiveAsync(november.ExternalId, date), Entries.ConfirmEffectiveAsync(november.ExternalId, date));
        Assert.All(results, x => Assert.True(x.Succeeded, x.Error));
        var confirmed = await OccurrenceAsync(2026, 11);
        Assert.Equal(november.ExternalId, confirmed.ExternalId);
        Assert.True(confirmed.IsEffective);
        Assert.Equal(date, confirmed.EffectiveDate);
        Assert.Equal(_clock.Now, confirmed.EffectiveAt);
        Assert.Equal(november.Month, confirmed.Month);
        Assert.Equal(november.AccountExternalId, confirmed.AccountExternalId);
        Assert.False((await OccurrenceAsync(2026, 12)).IsEffective);
        Assert.Equal(new Money(1000), (await Entries.GetBalanceAsync(_accountId, date.AddDays(-1)))?.EffectiveBalance);
        Assert.Equal(new Money(expectedBalance), (await Entries.GetBalanceAsync(_accountId, date))?.EffectiveBalance);
        Assert.True((await Entries.ConfirmEffectiveAsync(november.ExternalId, date.AddDays(1))).Succeeded);
        Assert.Equal(date, (await OccurrenceAsync(2026, 11)).EffectiveDate);
    }

    [Fact]
    public async Task EditingAndDeletingEffectiveOccurrenceRecalculatesBalanceAndAllPreservesConfirmedPast()
    {
        await Entries.CreateRecurringAsync(new(Request() with { Amount = new Money(100) }));
        var october = await OccurrenceAsync(2026, 10);
        await Entries.ConfirmEffectiveAsync(october.ExternalId, october.Date);
        var november = await OccurrenceAsync(2026, 11);
        await Entries.EditRecurringAsync(november.ExternalId, new(Change(november) with { Amount = new Money(200) }, RecurringChangeScope.All));
        Assert.Equal(new Money(-100), (await OccurrenceAsync(2026, 10)).Amount);
        Assert.Equal(new Money(900), (await Entries.GetBalanceAsync(_accountId, new DateOnly(2026, 11, 30)))?.EffectiveBalance);
        await Entries.EditRecurringAsync(october.ExternalId, new(Change(october) with { Amount = new Money(150) }, RecurringChangeScope.OnlyThis));
        Assert.Equal(new Money(850), (await Entries.GetBalanceAsync(_accountId, new DateOnly(2026, 11, 30)))?.EffectiveBalance);
        await Entries.DeleteRecurringAsync(october.ExternalId, RecurringChangeScope.OnlyThis);
        Assert.Equal(new Money(1000), (await Entries.GetBalanceAsync(_accountId, new DateOnly(2026, 11, 30)))?.EffectiveBalance);
    }

    [Fact]
    public async Task InvalidConfirmationLeavesCashPendingAndCardConfirmationCannotDebitAccount()
    {
        await Entries.CreateRecurringAsync(new(Request()));
        var occurrence = await OccurrenceAsync(2026, 10);
        Assert.False((await Entries.ConfirmEffectiveAsync(occurrence.ExternalId, DateOnly.MinValue)).Succeeded);
        Assert.False((await Entries.ConfirmEffectiveAsync(Guid.NewGuid(), occurrence.Date)).Succeeded);
        Assert.False((await OccurrenceAsync(2026, 10)).IsEffective);
        await Entries.CreateAsync(Request() with { CardExternalId = await CreateCardAsync(), Month = new AccountingMonth(2026, 12) });
        var card = (await Entries.GetAsync()).Single(x => x.CardExternalId is not null);
        Assert.False((await Entries.ConfirmEffectiveAsync(card.ExternalId, card.Date)).Succeeded);
        Assert.Equal(new Money(1000), (await Entries.GetBalanceAsync(_accountId, new DateOnly(2026, 12, 30)))?.EffectiveBalance);
    }
}
