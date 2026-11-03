using Ags.Finance.Modules.Invoices.Contracts;
using Ags.Finance.Modules.Transactions.Contracts;
using Ags.Finance.SharedKernel;
using Microsoft.Extensions.DependencyInjection;

namespace Ags.Finance.Modules.Invoices.Tests;

public sealed partial class InvoiceTests
{
    [Fact]
    public async Task TheLimitUsedSumsTheRemainingBalanceOfUnpaidInvoicesIncludingFutureOnes()
    {
        Assert.True((await Provider.GetRequiredService<IEntryCatalog>().CreateAsync(Purchase(100))).Succeeded);
        Assert.True((await Provider.GetRequiredService<IEntryCatalog>().CreateAsync(Purchase(50, 11))).Succeeded);

        var limit = Assert.Single(await Catalog.GetCardLimitsAsync());

        Assert.Equal(_cardId, limit.CardExternalId);
        Assert.Equal(new Money(150), limit.Used);
        Assert.Equal(limit.Limit - new Money(150), limit.Available);
    }

    [Fact]
    public async Task APaidInvoiceFreesTheLimitAndACreditDoesNotReduceAnotherInvoice()
    {
        var entries = Provider.GetRequiredService<IEntryCatalog>();
        var month = new ReferenceMonth(2026, 10);
        Assert.True((await entries.CreateAsync(Purchase(100))).Succeeded);
        Assert.True((await entries.CreateAsync(Purchase(50, 11))).Succeeded);
        var payments = Provider.GetRequiredService<IInvoicePaymentCatalog>();
        Assert.True((await Provider.GetRequiredService<IInvoiceLifecycle>().CloseAsync(_cardId, month)).Succeeded);
        Assert.True((await payments.ConfirmPaymentAsync(new(Guid.CreateVersion7(), _cardId, month, _accountId, new DateOnly(2026, 9, 20), new Money(100), true))).Succeeded);

        Assert.Equal(new Money(50), Assert.Single(await Catalog.GetCardLimitsAsync()).Used);

        var credit = await payments.SaveAllowanceAsync(null, new(Guid.CreateVersion7(), _cardId, new ReferenceMonth(2026, 11), new DateOnly(2026, 9, 20), new Money(80), InvoicePaymentKind.Cashback));
        Assert.True(credit.Succeeded, credit.Error);
        Assert.Equal(Money.Zero, Assert.Single(await Catalog.GetCardLimitsAsync()).Used);
    }

    [Fact]
    public void TheUsedPercentIsLimitedToAHundredAndZeroWithoutLimit()
    {
        Assert.Equal(100, new CardLimit(Guid.NewGuid(), new Money(100), new Money(250)).UsedPercent);
        Assert.Equal(25, new CardLimit(Guid.NewGuid(), new Money(100), new Money(25)).UsedPercent);
        Assert.Equal(0, new CardLimit(Guid.NewGuid(), Money.Zero, new Money(25)).UsedPercent);
    }

    [Fact]
    public async Task TheDatesFollowTheCardCalendarAndAnUnknownCardReturnsNull()
    {
        var dates = await Catalog.GetDatesAsync(_cardId, new ReferenceMonth(2026, 10));

        Assert.NotNull(dates);
        Assert.Equal(10, dates.DueDate.Month);
        Assert.True(dates.ClosingDate <= dates.DueDate);
        Assert.Null(await Catalog.GetDatesAsync(Guid.NewGuid(), new ReferenceMonth(2026, 10)));
    }
}
