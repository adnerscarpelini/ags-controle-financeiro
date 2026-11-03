using Ags.Finance.Modules.Accounts.Contracts;
using Ags.Finance.Modules.Transactions.Contracts;
using Ags.Finance.SharedKernel;
using Microsoft.Extensions.DependencyInjection;

namespace Ags.Finance.Modules.Transactions.Tests;

public sealed partial class EntryTests
{
    [Fact]
    public async Task PendingQueryCombinesFiltersOrdersByExpectedDateAndTotalsTheWholeResult()
    {
        var income = await IncomeSubcategoryAsync();
        await Entries.CreateRecurringAsync(new(Request() with { IsPaid = false, Date = new DateOnly(2026, 11, 1), Amount = new Money(50) }));
        await Entries.CreateRecurringAsync(new(Request() with { IsPaid = false, Date = new DateOnly(2026, 11, 2), Amount = new Money(100), SubcategoryExternalId = income }));
        await Entries.CreateRecurringAsync(new(Request() with { IsPaid = false, Date = new DateOnly(2026, 11, 3), Amount = new Money(200) }));
        await Entries.CreateAsync(Request() with { Date = new DateOnly(2026, 11, 1) });
        await Entries.CreateRecurringAsync(new(Request() with { IsPaid = false, Date = new DateOnly(2026, 11, 1), CardExternalId = await CreateCardAsync() }));
        var filter = new EntryFilter(Month: new AccountingMonth(2026, 11), PageSize: 1);
        var all = await Entries.QueryPendingAsync(filter);
        Assert.Equal(3, all.TotalCount);
        Assert.Equal(new DateOnly(2026, 11, 1), Assert.Single(all.Entries).Date);
        Assert.Equal(new Money(100), all.Income);
        Assert.Equal(new Money(-250), all.Expenses);
        var second = await Entries.QueryPendingAsync(filter with { Page = 2 });
        Assert.Equal(new DateOnly(2026, 11, 2), Assert.Single(second.Entries).Date);
        var combined = await Entries.QueryPendingAsync(filter with { AccountExternalId = _accountId, Kind = CategoryKind.Expense, FromDate = new DateOnly(2026, 11, 1), ToDate = new DateOnly(2026, 11, 2) });
        Assert.Equal(1, combined.TotalCount);
        Assert.Equal(new Money(-50), combined.Expenses);
        var reusable = await Entries.QueryAsync(filter with { PendingRecurringOnly = true });
        Assert.Equal(all.Entries, reusable.Entries);
        Assert.Equal(all.Balance, reusable.Balance);
    }

    [Fact]
    public async Task ArchivedPendingAccountRemainsVisibleAndConfirmationUsesThatSameAccount()
    {
        await Entries.CreateRecurringAsync(new(Request() with { IsPaid = false, Date = new DateOnly(2026, 11, 1), Amount = new Money(100) }));
        await Provider.GetRequiredService<IAccountCatalog>().DeactivateAccountAsync(_accountId);
        var filter = new EntryFilter(FromDate: new DateOnly(2026, 11, 1), ToDate: new DateOnly(2026, 12, 31), AccountExternalId: _accountId);
        var pending = await Entries.QueryPendingAsync(filter);
        Assert.Equal(2, pending.TotalCount);
        var first = pending.Entries[0];
        Assert.True((await Entries.ConfirmEffectiveAsync(first.ExternalId, new DateOnly(2026, 11, 2))).Succeeded);
        var remaining = await Entries.QueryPendingAsync(filter);
        Assert.Equal(1, remaining.TotalCount);
        var confirmed = (await Entries.GetAsync()).Single(x => x.ExternalId == first.ExternalId);
        Assert.True(confirmed.IsEffective);
        Assert.Equal(_accountId, confirmed.AccountExternalId);
        Assert.Equal(new Money(900), (await Entries.GetBalanceAsync(_accountId, new DateOnly(2026, 11, 2)))?.EffectiveBalance);
    }
}
