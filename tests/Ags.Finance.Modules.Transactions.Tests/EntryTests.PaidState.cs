using Ags.Finance.Modules.Accounts.Contracts;
using Ags.Finance.Modules.Transactions.Contracts;
using Ags.Finance.SharedKernel;
using Microsoft.Extensions.DependencyInjection;

namespace Ags.Finance.Modules.Transactions.Tests;

public sealed partial class EntryTests
{
    [Theory]
    [InlineData(true, false, 900)]
    [InlineData(true, true, 1100)]
    [InlineData(false, false, 1000)]
    [InlineData(false, true, 1000)]
    public async Task NewCashEntryUsesPaidSwitchAndPendingQueryIncludesSingles(bool paid, bool income, int balance)
    {
        var result = await Entries.CreateAsync(Request() with { Amount = new Money(100), IsPaid = paid, SubcategoryExternalId = income ? await IncomeSubcategoryAsync() : _subcategoryId });
        Assert.True(result.Succeeded, result.Error);
        var entry = Assert.Single(await Entries.GetAsync());
        Assert.Equal(paid, entry.IsEffective);
        Assert.Equal(paid ? 0 : 1, (await Entries.QueryPendingAsync(new())).TotalCount);
        Assert.Equal(new Money(balance), (await Entries.GetBalanceAsync(_accountId, entry.Date))?.EffectiveBalance);
        if (!paid)
        {
            var date = entry.Date.AddDays(1);
            Assert.True((await Entries.ConfirmEffectiveAsync(entry.ExternalId, new ConfirmEffectiveRequest(date, new Money(100), _accountId))).Succeeded);
            Assert.True((await Entries.ConfirmEffectiveAsync(entry.ExternalId, new ConfirmEffectiveRequest(date, new Money(200), _accountId))).Succeeded);
            Assert.Equal(new Money(income ? 1100 : 900), (await Entries.GetBalanceAsync(_accountId, date))?.EffectiveBalance);
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task FirstRecurringOccurrenceFollowsSwitchAndFollowingOccurrencesStayPending(bool paid)
    {
        Assert.True((await Entries.CreateRecurringAsync(new(Request() with { IsPaid = paid }))).Succeeded);
        Assert.Equal(paid, (await OccurrenceAsync(2026, 10)).IsEffective);
        Assert.False((await OccurrenceAsync(2026, 11)).IsEffective);
        Assert.False((await OccurrenceAsync(2026, 12)).IsEffective);
    }

    [Fact]
    public async Task ManualConfirmationChangesOnlySelectedOccurrenceAndUndoRestoresEachAccount()
    {
        await Entries.CreateRecurringAsync(new(Request() with { IsPaid = false }));
        var entry = await OccurrenceAsync(2026, 11);
        var account = await Provider.GetRequiredService<IAccountCatalog>().SaveAccountAsync(new(null, (await Provider.GetRequiredService<IAccountCatalog>().CreateBankAsync("Banco Alternativo")).ExternalId!.Value, "Outra conta", AccountKind.Checking, "green", new Money(500), new DateOnly(2026, 10, 1)));
        var date = new DateOnly(2026, 11, 9);
        var request = new ConfirmEffectiveRequest(date, new Money(200), account.ExternalId!.Value);
        Assert.True((await Entries.ConfirmEffectiveAsync(entry.ExternalId, request)).Succeeded);
        var changed = await OccurrenceAsync(2026, 11);
        Assert.Equal(date, changed.Date);
        Assert.Equal(new Money(-200), changed.Amount);
        Assert.Equal(account.ExternalId, changed.AccountExternalId);
        Assert.Equal(new Money(300), (await Entries.GetBalanceAsync(account.ExternalId.Value, date))?.EffectiveBalance);
        Assert.Equal(new Money(-150), (await OccurrenceAsync(2026, 12)).Amount);
        Assert.True((await Entries.UndoEffectiveAsync(entry.ExternalId)).Succeeded);
        Assert.True((await Entries.UndoEffectiveAsync(entry.ExternalId)).Succeeded);
        Assert.Equal(new Money(500), (await Entries.GetBalanceAsync(account.ExternalId.Value, date))?.EffectiveBalance);
        Assert.False((await OccurrenceAsync(2026, 11)).IsEffective);
    }

    [Fact]
    public async Task InvalidCashConfirmationAndCardUndoHaveNoEffectAndEditWithoutSwitchPreservesState()
    {
        await Entries.CreateAsync(Request() with { IsPaid = false });
        var entry = Assert.Single(await Entries.GetAsync());
        Assert.False((await Entries.ConfirmEffectiveAsync(entry.ExternalId, new ConfirmEffectiveRequest(entry.Date, new Money(0), _accountId))).Succeeded);
        Assert.False((await Entries.ConfirmEffectiveAsync(entry.ExternalId, new ConfirmEffectiveRequest(entry.Date, new Money(100), Guid.NewGuid()))).Succeeded);
        Assert.True((await Entries.EditAsync(entry.ExternalId, Change(entry) with { Details = "Detalhes" }, EntryChangeScope.OnlyThis)).Succeeded);
        Assert.False(Assert.Single(await Entries.GetAsync()).IsEffective);
        Assert.True((await Entries.EditAsync(entry.ExternalId, Change(entry) with { IsPaid = true }, EntryChangeScope.OnlyThis)).Succeeded);
        Assert.True((await Entries.EditAsync(entry.ExternalId, Change(entry) with { IsPaid = false }, EntryChangeScope.OnlyThis)).Succeeded);
        Assert.Equal(new Money(1000), (await Entries.GetBalanceAsync(_accountId, entry.Date))?.EffectiveBalance);
        await Entries.CreateAsync(Request() with { CardExternalId = await CreateCardAsync(), IsPaid = false });
        var card = (await Entries.GetAsync()).Single(x => x.CardExternalId is not null);
        Assert.True(card.IsEffective);
        Assert.False((await Entries.UndoEffectiveAsync(card.ExternalId)).Succeeded);
    }
}
