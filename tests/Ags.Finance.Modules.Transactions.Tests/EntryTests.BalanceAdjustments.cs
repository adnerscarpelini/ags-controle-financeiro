using Ags.Finance.Modules.Accounts.Contracts;
using Ags.Finance.Modules.Transactions.Contracts;
using Ags.Finance.SharedKernel;
using Microsoft.Extensions.DependencyInjection;

namespace Ags.Finance.Modules.Transactions.Tests;

public sealed partial class EntryTests
{
    [Theory]
    [InlineData(1100, 100)]
    [InlineData(850, -150)]
    [InlineData(-50, -1050)]
    public async Task AdjustmentRecordsDifferenceAndPreservesOpeningAndBudget(int target, int difference)
    {
        var date = new DateOnly(2026, 11, 1);
        var request = new BalanceAdjustmentRequest(Guid.CreateVersion7(), _accountId, date, new Money(target), new Money(1000));
        var results = await Task.WhenAll(Movements.AdjustBalanceAsync(request), Movements.AdjustBalanceAsync(request));
        Assert.All(results, x => Assert.True(x.Succeeded, x.Error));
        var row = Assert.Single(await Movements.GetAsync(_accountId));
        Assert.Equal(new Money(difference), row.Amount);
        Assert.Equal(new Money(target), row.AdjustmentTarget);
        Assert.Equal(AccountMovementKind.BalanceAdjustment, row.Kind);
        Assert.Equal(new Money(target), (await Entries.GetBalanceAsync(_accountId, date))?.EffectiveBalance);
        Assert.Equal(new Money(1000), (await Entries.GetBalanceAsync(_accountId, date.AddDays(-1)))?.EffectiveBalance);
        Assert.Equal(new Money(1000), Assert.Single(await Provider.GetRequiredService<IAccountCatalog>().GetAccountsAsync()).OpeningBalance);
        Assert.Empty(await Entries.GetAsync());
        Assert.Equal(Money.Zero, (await Entries.QueryAsync(new(Month: new ReferenceMonth(2026, 11)))).Expenses);
    }

    [Fact]
    public async Task RetroactiveAdjustmentUsesBalanceOnDateAndCorrectionsAreNewRows()
    {
        await Entries.CreateAsync(Request());
        await Entries.CreateAsync(Request() with { Date = new DateOnly(2026, 11, 10), Amount = new Money(200) });
        var date = new DateOnly(2026, 10, 31);
        Assert.True((await Movements.AdjustBalanceAsync(new(Guid.CreateVersion7(), _accountId, date, new Money(900)))).Succeeded);
        Assert.Equal(new Money(50), Assert.Single(await Movements.GetAsync()).Amount);
        Assert.Equal(new Money(700), (await Entries.GetBalanceAsync(_accountId, new DateOnly(2026, 11, 30)))?.EffectiveBalance);
        Assert.True((await Movements.AdjustBalanceAsync(new(Guid.CreateVersion7(), _accountId, date, new Money(950)))).Succeeded);
        Assert.Equal(2, (await Movements.GetAsync()).Count);
        Assert.Equal(new Money(750), (await Entries.GetBalanceAsync(_accountId, new DateOnly(2026, 11, 30)))?.EffectiveBalance);
    }

    [Fact]
    public async Task ZeroDifferenceInvalidAccountAndStalePreviewNeverCreateAdjustment()
    {
        var date = new DateOnly(2026, 11, 1);
        var request = new BalanceAdjustmentRequest(Guid.CreateVersion7(), _accountId, date, new Money(1000));
        Assert.True((await Movements.AdjustBalanceAsync(request)).Succeeded);
        Assert.False((await Movements.AdjustBalanceAsync(request with { ExpectedBalance = new Money(900) })).Succeeded);
        Assert.False((await Movements.AdjustBalanceAsync(request with { AccountExternalId = Guid.NewGuid() })).Succeeded);
        Assert.False((await Movements.AdjustBalanceAsync(request with { Date = new DateOnly(2026, 9, 30) })).Succeeded);
        await Provider.GetRequiredService<IAccountCatalog>().DeactivateAccountAsync(_accountId);
        Assert.False((await Movements.AdjustBalanceAsync(request)).Succeeded);
        Assert.Empty(await Movements.GetAsync());
    }

    [Fact]
    public async Task FailedAdjustmentLeavesBalanceAndHistoryUnchanged()
    {
        await (_database ?? throw new InvalidOperationException()).ExecuteAsync("ALTER TABLE transactions.account_movements ADD CONSTRAINT test_reject_adjustment CHECK (\"Kind\" = 0)");
        await Assert.ThrowsAsync<Microsoft.EntityFrameworkCore.DbUpdateException>(() => Movements.AdjustBalanceAsync(new(Guid.CreateVersion7(), _accountId, new DateOnly(2026, 11, 1), new Money(1200))));
        Assert.Empty(await Movements.GetAsync());
        Assert.Equal(new Money(1000), (await Entries.GetBalanceAsync(_accountId, new DateOnly(2026, 11, 1)))?.EffectiveBalance);
    }
}
