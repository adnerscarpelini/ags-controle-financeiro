using Ags.Finance.Modules.Accounts.Contracts;
using Ags.Finance.Modules.Invoices.Contracts;
using Ags.Finance.Modules.Transactions.Contracts;
using Ags.Finance.SharedKernel;
using Microsoft.Extensions.DependencyInjection;

namespace Ags.Finance.Modules.Invoices.Tests;

public sealed partial class InvoiceTests
{
    [Fact]
    public async Task ObligationsUseRemainingBalancesPreserveArchivedCardsAndShowPaidMonths()
    {
        var month = new AccountingMonth(2026, 11);
        var entries = Provider.GetRequiredService<IEntryCatalog>();
        await entries.CreateInstallmentsAsync(Purchase(1000, 11), 10);
        var accounts = Provider.GetRequiredService<IAccountCatalog>();
        var other = await accounts.SaveCreditCardAsync(new(null, _accountId, "Outro Cartão", CardBrand.Mastercard, new Money(1000), 3, 10, true, "green"));
        await entries.CreateAsync(Purchase(500, 11) with { CardExternalId = other.ExternalId });
        var result = await Catalog.GetObligationsAsync(month);
        Assert.Equal(new Money(600), result.Months.Single(x => x.Month == month).RemainingBalance);
        Assert.Equal(new Money(1000), result.Cards.Single(x => x.CardExternalId == _cardId).RemainingBalance);
        await Payments.ConfirmPaymentAsync(Payment(20) with { Month = month });
        await accounts.DeactivateCreditCardAsync(_cardId);
        result = await Catalog.GetObligationsAsync(month);
        Assert.Equal(new Money(580), result.Months.Single(x => x.Month == month).RemainingBalance);
        Assert.Equal(new Money(980), result.Cards.Single(x => x.CardExternalId == _cardId).RemainingBalance);
        await Lifecycle.CloseAsync(_cardId, month);
        Assert.True((await Payments.ConfirmPaymentAsync(Payment(80, true) with { Month = month })).Succeeded);
        await Lifecycle.CloseAsync(other.ExternalId!.Value, month);
        Assert.True((await Payments.ConfirmPaymentAsync(Payment(500, true) with { CardExternalId = other.ExternalId.Value, Month = month })).Succeeded);
        result = await Catalog.GetObligationsAsync(month);
        Assert.True(result.Months.Single(x => x.Month == month).FullyPaid);
        Assert.Equal(Money.Zero, result.Months.Single(x => x.Month == month).RemainingBalance);
        Assert.Equal(new Money(900), result.Cards.Single(x => x.CardExternalId == _cardId).RemainingBalance);
    }

    [Fact]
    public async Task ForwardTotalIncludesSelectedMonthAndOnlyLaterInvoicesWithInstallments()
    {
        var entries = Provider.GetRequiredService<IEntryCatalog>();
        await entries.CreateAsync(Purchase(100, 10));
        await entries.CreateAsync(Purchase(500, 11));
        var result = await Catalog.GetObligationsAsync(new AccountingMonth(2026, 10));
        Assert.Equal(new Money(100), Assert.Single(result.Cards).RemainingBalance);
        Assert.Equal(2, result.Months.Count);
        Assert.NotNull((await Catalog.GetObligationsAsync(default)).Error);
    }

    [Fact]
    public async Task CreditRemainsVisibleAfterExplicitSettlement()
    {
        await Provider.GetRequiredService<IEntryCatalog>().CreateAsync(Purchase(100));
        await Lifecycle.CloseAsync(_cardId, new AccountingMonth(2026, 10));
        await Payments.ConfirmPaymentAsync(Payment(120, true));
        var result = await Catalog.GetObligationsAsync(new AccountingMonth(2026, 10));
        Assert.Equal(new Money(-20), Assert.Single(result.Months).RemainingBalance);
        Assert.False(Assert.Single(result.Months).FullyPaid);
        Assert.Equal(new Money(-20), Assert.Single(result.Cards).RemainingBalance);
    }
}
