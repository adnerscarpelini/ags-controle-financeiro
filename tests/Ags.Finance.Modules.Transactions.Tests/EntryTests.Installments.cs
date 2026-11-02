using Ags.Finance.SharedKernel;

namespace Ags.Finance.Modules.Transactions.Tests;

public sealed partial class EntryTests
{
    [Fact]
    public async Task InstallmentsPreserveExactTotalAndConsecutiveMonthsWithExplicitRelationship()
    {
        var card = await CreateCardAsync();
        var result = await Entries.CreateInstallmentsAsync(Request() with { CardExternalId = card, Amount = new Money(1000), Month = new AccountingMonth(2026, 10) }, 3);
        Assert.True(result.Succeeded, result.Error);
        var installments = (await Entries.GetAsync()).OrderBy(x => x.InstallmentNumber).ToList();
        Assert.Equal(3, installments.Count);
        Assert.Equal([new Money(-333.34m), new Money(-333.33m), new Money(-333.33m)], installments.Select(x => x.Amount));
        Assert.Equal([new AccountingMonth(2026, 10), new AccountingMonth(2026, 11), new AccountingMonth(2026, 12)], installments.Select(x => x.Month));
        Assert.All(installments, entry => { Assert.Equal(result.ExternalId, entry.InstallmentPurchaseExternalId); Assert.Equal(3, entry.InstallmentCount); Assert.Equal(Request().Date, entry.Date); });
        Assert.Equal(-1000m, installments.Sum(x => x.Amount.Amount));
        Assert.Equal(new Money(1000), (await Entries.GetBalanceAsync(_accountId, new DateOnly(2027, 12, 31)))?.EffectiveBalance);
    }

    [Fact]
    public async Task TenInstallmentsStartingInJuneEndInMarchAndTinyValuesKeepSplitResult()
    {
        var card = await CreateCardAsync();
        var result = await Entries.CreateInstallmentsAsync(Request() with { CardExternalId = card, Month = new AccountingMonth(2026, 6), Amount = new Money(1.09m) }, 10);
        Assert.True(result.Succeeded, result.Error);
        var installments = (await Entries.GetAsync()).OrderBy(x => x.InstallmentNumber).ToList();
        Assert.Equal(new AccountingMonth(2027, 3), installments[^1].Month);
        Assert.Equal(new Money(-0.10m), installments[0].Amount);
        Assert.Equal(-1.09m, installments.Sum(x => x.Amount.Amount));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(49)]
    public async Task InvalidInstallmentCountDoesNotSaveAnyEntries(int count)
    {
        Assert.False((await Entries.CreateInstallmentsAsync(Request() with { CardExternalId = await CreateCardAsync() }, count)).Succeeded);
        Assert.Empty(await Entries.GetAsync());
    }

    [Fact]
    public async Task CashInstallmentsAndCalendarOverflowAreRejected()
    {
        Assert.False((await Entries.CreateInstallmentsAsync(Request(), 3)).Succeeded);
        Assert.False((await Entries.CreateInstallmentsAsync(Request() with { CardExternalId = await CreateCardAsync(), Month = new AccountingMonth(9999, 12) }, 2)).Succeeded);
        Assert.Empty(await Entries.GetAsync());
    }
}
