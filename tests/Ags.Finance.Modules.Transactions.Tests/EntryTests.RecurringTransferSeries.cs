using Ags.Finance.Modules.Transactions.Contracts;
using Ags.Finance.SharedKernel;

namespace Ags.Finance.Modules.Transactions.Tests;

/// <summary>O relógio do cenário está em 02/11/2026: outubro é mês anterior e novembro é o atual.</summary>
public sealed partial class EntryTests
{
    private async Task<(Guid Series, Guid Destination)> TransferSeriesFromOctoberAsync()
    {
        var destination = await SecondCheckingAsync();
        var series = await Transfers.CreateAsync(new(_accountId, destination, new DateOnly(2026, 10, 5), new Money(100), null, false));
        Assert.True(series.Succeeded, series.Error);
        return (series.ExternalId!.Value, destination);
    }

    [Fact]
    public async Task EditingATransferSeriesChangesOnlyPendingMonthsFromTheCurrentOneOn()
    {
        var (series, destination) = await TransferSeriesFromOctoberAsync();
        var december = Assert.Single(await Transfers.GetMonthAsync(s_recurringSecond));
        Assert.True((await Transfers.ConfirmAsync(december.ExternalId, december.Date, december.Amount)).Succeeded);

        var result = await Transfers.EditSeriesAsync(series, new(_accountId, destination, new Money(200), 31));

        Assert.True(result.Succeeded, result.Error);
        var october = Assert.Single(await Transfers.GetMonthAsync(new ReferenceMonth(2026, 10)));
        Assert.Equal((new Money(100), new DateOnly(2026, 10, 5)), (october.Amount, october.Date));
        var november = Assert.Single(await Transfers.GetMonthAsync(s_recurringFirst));
        Assert.Equal((new Money(200), new DateOnly(2026, 11, 30)), (november.Amount, november.Date));
        var confirmed = Assert.Single(await Transfers.GetMonthAsync(s_recurringSecond));
        Assert.Equal((new Money(100), new DateOnly(2026, 12, 5), true), (confirmed.Amount, confirmed.Date, confirmed.IsConfirmed));
        var february = Assert.Single(await Transfers.GetMonthAsync(new ReferenceMonth(2027, 2)));
        Assert.Equal((new Money(200), new DateOnly(2027, 2, 28)), (february.Amount, february.Date));
        Assert.Equal((new Money(200), 31), ((await Transfers.GetSeriesAsync()).Single().Amount, (await Transfers.GetSeriesAsync()).Single().Day));
    }

    [Fact]
    public async Task EditingATransferSeriesCanMoveItToAnotherAccountAndRefusesInvalidValues()
    {
        var (series, destination) = await TransferSeriesFromOctoberAsync();

        Assert.False((await Transfers.EditSeriesAsync(series, new(_accountId, destination, new Money(100), 32))).Succeeded);
        Assert.False((await Transfers.EditSeriesAsync(series, new(_accountId, destination, Money.Zero, 5))).Succeeded);
        Assert.False((await Transfers.EditSeriesAsync(series, new(_accountId, _accountId, new Money(100), 5))).Succeeded);
        Assert.False((await Transfers.EditSeriesAsync(Guid.CreateVersion7(), new(_accountId, destination, new Money(100), 5))).Succeeded);
        Assert.Equal(new Money(100), (await Transfers.GetSeriesAsync()).Single().Amount);

        Assert.True((await Transfers.EditSeriesAsync(series, new(destination, _accountId, new Money(100), 5))).Succeeded);
        Assert.Equal(destination, Assert.Single(await Transfers.GetMonthAsync(s_recurringFirst)).SourceAccountExternalId);
        Assert.Equal(_accountId, Assert.Single(await Transfers.GetMonthAsync(new ReferenceMonth(2026, 10))).SourceAccountExternalId);
    }

    [Fact]
    public async Task DeletingATransferSeriesKeepsConfirmedAndPreviousMonths()
    {
        var (series, _) = await TransferSeriesFromOctoberAsync();
        var december = Assert.Single(await Transfers.GetMonthAsync(s_recurringSecond));
        Assert.True((await Transfers.ConfirmAsync(december.ExternalId, december.Date, december.Amount)).Succeeded);
        Assert.Single(await Transfers.GetMonthAsync(new ReferenceMonth(2027, 1)));

        Assert.True((await Transfers.DeleteSeriesAsync(series)).Succeeded);

        Assert.Single(await Transfers.GetMonthAsync(new ReferenceMonth(2026, 10)));
        Assert.Empty(await Transfers.GetMonthAsync(s_recurringFirst));
        Assert.Equal(december.ExternalId, Assert.Single(await Transfers.GetMonthAsync(s_recurringSecond)).ExternalId);
        Assert.Empty(await Transfers.GetMonthAsync(new ReferenceMonth(2027, 1)));
        Assert.Empty(await Transfers.GetSeriesAsync());
    }
}
