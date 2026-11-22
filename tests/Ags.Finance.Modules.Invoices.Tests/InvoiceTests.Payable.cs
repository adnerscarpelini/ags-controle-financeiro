using Ags.Finance.Modules.Accounts.Contracts;
using Ags.Finance.Modules.Invoices.Contracts;
using Ags.Finance.Modules.Transactions.Contracts;
using Ags.Finance.SharedKernel;
using Microsoft.Extensions.DependencyInjection;

namespace Ags.Finance.Modules.Invoices.Tests;

/// <summary>
/// Faturas a pagar na posição de hoje (#144). O cartão do cenário fecha no dia 3 e vence no dia 10:
/// uma compra em 01/09 cai na fatura de setembro, e uma em 05/09 já cai na de outubro.
/// </summary>
public sealed partial class InvoiceTests
{
    private static readonly ReferenceMonth s_payableSeptember = new(2026, 9);
    private static readonly ReferenceMonth s_payableOctober = new(2026, 10);

    private CreateEntryRequest PurchaseOn(DateOnly date, decimal amount, Guid? card = null)
        => new(date, _merchantId, _subcategoryId, _accountId, new Money(amount), CardExternalId: card ?? _cardId);

    [Fact]
    public async Task TheCurrentOpenInvoiceShowsWhatIsLeftAndFutureInstallmentsStayOut()
    {
        var entries = Provider.GetRequiredService<IEntryCatalog>();
        Assert.True((await entries.CreateInstallmentsAsync(PurchaseOn(new DateOnly(2026, 9, 1), 300m), 3)).Succeeded);
        Assert.True((await Provider.GetRequiredService<IInvoicePaymentCatalog>().ConfirmPaymentAsync(new(Guid.CreateVersion7(), _cardId, s_payableSeptember, _accountId, new DateOnly(2026, 9, 1), new Money(40m)))).Succeeded);

        var invoice = Assert.Single(await Catalog.GetPayableAsync());

        Assert.Equal((s_payableSeptember, InvoiceStatus.Open), (invoice.Month, invoice.Status));
        Assert.Equal((new Money(100m), new Money(60m)), (invoice.Total, invoice.RemainingBalance));
    }

    [Fact]
    public async Task AClosedUnpaidInvoiceComesBeforeTheCurrentOpenOneOfTheSameCard()
    {
        var entries = Provider.GetRequiredService<IEntryCatalog>();
        Assert.True((await entries.CreateAsync(PurchaseOn(new DateOnly(2026, 9, 1), 2000m))).Succeeded);
        _clock.Now = new DateTimeOffset(2026, 9, 5, 12, 0, 0, TimeSpan.Zero);
        Assert.True((await Provider.GetRequiredService<IInvoiceLifecycle>().CloseAsync(_cardId, s_payableSeptember)).Succeeded);
        Assert.True((await Provider.GetRequiredService<IInvoicePaymentCatalog>().ConfirmPaymentAsync(new(Guid.CreateVersion7(), _cardId, s_payableSeptember, _accountId, new DateOnly(2026, 9, 5), new Money(500m)))).Succeeded);
        Assert.True((await entries.CreateAsync(PurchaseOn(new DateOnly(2026, 9, 5), 80m))).Succeeded);

        var invoices = await Catalog.GetPayableAsync();

        Assert.Equal([(s_payableSeptember, InvoiceStatus.Closed), (s_payableOctober, InvoiceStatus.Open)], invoices.Select(x => (x.Month, x.Status)));
        Assert.Equal((new Money(2000m), new Money(1500m)), (invoices[0].Total, invoices[0].RemainingBalance));
        Assert.Equal(new Money(80m), invoices[1].RemainingBalance);
    }

    [Fact]
    public async Task PaidInvoicesAndClosedOnesWithoutBalanceAreNotDebt()
    {
        var entries = Provider.GetRequiredService<IEntryCatalog>();
        var payments = Provider.GetRequiredService<IInvoicePaymentCatalog>();
        Assert.True((await entries.CreateAsync(PurchaseOn(new DateOnly(2026, 9, 1), 100m))).Succeeded);
        var second = (await Provider.GetRequiredService<IAccountCatalog>().SaveCreditCardAsync(new(null, _accountId, "Outro Cartão", CardBrand.Visa, new Money(10000), 3, 10, true, "green"))).ExternalId!.Value;
        Assert.True((await entries.CreateAsync(PurchaseOn(new DateOnly(2026, 9, 1), 50m, second))).Succeeded);
        _clock.Now = new DateTimeOffset(2026, 9, 5, 12, 0, 0, TimeSpan.Zero);
        var lifecycle = Provider.GetRequiredService<IInvoiceLifecycle>();
        Assert.True((await lifecycle.CloseAsync(_cardId, s_payableSeptember)).Succeeded);
        Assert.True((await lifecycle.CloseAsync(second, s_payableSeptember)).Succeeded);
        Assert.True((await payments.ConfirmPaymentAsync(new(Guid.CreateVersion7(), _cardId, s_payableSeptember, _accountId, new DateOnly(2026, 9, 5), new Money(100m), ConfirmSettlement: true))).Succeeded);
        Assert.True((await payments.ConfirmPaymentAsync(new(Guid.CreateVersion7(), second, s_payableSeptember, _accountId, new DateOnly(2026, 9, 5), new Money(50m)))).Succeeded);

        Assert.Equal(InvoiceStatus.Paid, (await Catalog.GetAsync()).Single(x => x.CardExternalId == _cardId).Status);
        Assert.Equal(InvoiceStatus.Closed, (await Catalog.GetAsync()).Single(x => x.CardExternalId == second).Status);
        Assert.Empty(await Catalog.GetPayableAsync());
    }

    [Fact]
    public async Task AReopenedEarlierInvoiceIsStillDebtWhileACurrentInvoiceWithoutMovementStaysOut()
    {
        var entries = Provider.GetRequiredService<IEntryCatalog>();
        Assert.True((await entries.CreateAsync(PurchaseOn(new DateOnly(2026, 9, 1), 100m))).Succeeded);
        _clock.Now = new DateTimeOffset(2026, 9, 5, 12, 0, 0, TimeSpan.Zero);
        var lifecycle = Provider.GetRequiredService<IInvoiceLifecycle>();
        Assert.True((await lifecycle.CloseAsync(_cardId, s_payableSeptember)).Succeeded);
        Assert.True((await lifecycle.ReopenAsync(_cardId, s_payableSeptember)).Succeeded);

        var invoice = Assert.Single(await Catalog.GetPayableAsync());

        Assert.Equal((s_payableSeptember, InvoiceStatus.Open, new Money(100m)), (invoice.Month, invoice.Status, invoice.RemainingBalance));
    }

    [Fact]
    public async Task NoCardPurchasesMeansNothingToPay()
    {
        Assert.True((await Provider.GetRequiredService<IEntryCatalog>().CreateAsync(PurchaseOn(new DateOnly(2026, 9, 1), 100m) with { CardExternalId = null })).Succeeded);

        Assert.Empty(await Catalog.GetPayableAsync());
    }
}
