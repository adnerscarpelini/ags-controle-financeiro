using Ags.Finance.Modules.Accounts.Contracts;
using Ags.Finance.Modules.Invoices.Contracts;
using Ags.Finance.Modules.Transactions.Contracts;
using Ags.Finance.SharedKernel;
using Microsoft.Extensions.DependencyInjection;

namespace Ags.Finance.Modules.Invoices.Tests;

public sealed partial class InvoiceTests
{
    private IInvoicePaymentCatalog Payments => Provider.GetRequiredService<IInvoicePaymentCatalog>();
    private AllowanceRequest Allowance(decimal amount = 8, InvoicePaymentKind kind = InvoicePaymentKind.Cashback)
        => new(Guid.CreateVersion7(), _cardId, new ReferenceMonth(2026, 10), new DateOnly(2026, 9, 1), new Money(amount), kind);

    [Fact]
    public async Task CashbackAndDiscountReduceRemainingBalanceWithoutChangingExpensesOrCash()
    {
        var entries = Provider.GetRequiredService<IEntryCatalog>();
        await entries.CreateAsync(Purchase(100));
        var request = Allowance();
        var results = await Task.WhenAll(Payments.SaveAllowanceAsync(null, request), Payments.SaveAllowanceAsync(null, request));
        Assert.All(results, x => Assert.True(x.Succeeded, x.Error));
        Assert.True((await Payments.SaveAllowanceAsync(null, Allowance(2, InvoicePaymentKind.Discount))).Succeeded);
        var invoice = Assert.Single(await Catalog.GetAsync());
        Assert.Equal(new Money(100), invoice.Total);
        Assert.Equal(new Money(8), invoice.CashbackTotal);
        Assert.Equal(new Money(2), invoice.DiscountsTotal);
        Assert.Equal(new Money(90), invoice.RemainingBalance);
        Assert.Equal(2, invoice.PaymentHistory.Count);
        Assert.Single(await entries.GetAsync());
        Assert.Equal(new Money(-100), (await entries.QueryAsync(new(Month: invoice.Month))).Expenses);
        Assert.Equal(Money.Zero, (await entries.GetBalanceAsync(_accountId, request.Date))?.EffectiveBalance);
    }

    [Fact]
    public async Task ClosedInvoiceAllowsEditingAndDeletionButPaidBlocksSourceAndDestination()
    {
        var request = Allowance();
        var created = await Payments.SaveAllowanceAsync(null, request);
        await Lifecycle.CloseAsync(_cardId, request.Month);
        Assert.True((await Payments.SaveAllowanceAsync(created.ExternalId, request with { Amount = new Money(10) })).Succeeded);
        Assert.Equal(new Money(-10), Assert.Single(await Catalog.GetMonthAsync(request.Month)).RemainingBalance);
        Assert.Equal(InvoiceStatus.Closed, Assert.Single(await Catalog.GetMonthAsync(request.Month)).Status);
        Assert.True((await Payments.DeleteAllowanceAsync(created.ExternalId!.Value)).Succeeded);
        created = await Payments.SaveAllowanceAsync(null, request with { ConfirmationId = Guid.CreateVersion7() });
        var database = _database ?? throw new InvalidOperationException();
        await database.ExecuteAsync("UPDATE invoices.invoices SET \"Status\" = 2");
        Assert.False((await Payments.SaveAllowanceAsync(created.ExternalId, request with { Amount = new Money(20) })).Succeeded);
        Assert.False((await Payments.DeleteAllowanceAsync(created.ExternalId!.Value)).Succeeded);
        Assert.False((await Payments.SaveAllowanceAsync(null, Allowance())).Succeeded);
        var other = Allowance() with { Month = new ReferenceMonth(2026, 11) };
        var movable = await Payments.SaveAllowanceAsync(null, other);
        Assert.False((await Payments.SaveAllowanceAsync(movable.ExternalId, other with { Month = request.Month })).Succeeded);
        Assert.Equal(2, (await Payments.GetHistoryAsync()).Count);
    }

    [Fact]
    public async Task YearFilterUsesReceiptDateAndCashbackCreditDoesNotMarkInvoicePaid()
    {
        await Payments.SaveAllowanceAsync(null, Allowance(8) with { Date = new DateOnly(2025, 12, 31) });
        await Payments.SaveAllowanceAsync(null, Allowance(12) with { Date = new DateOnly(2026, 1, 1) });
        await Payments.SaveAllowanceAsync(null, Allowance(3, InvoicePaymentKind.Discount));
        var annual = await Payments.GetHistoryAsync(_cardId, 2026, InvoicePaymentKind.Cashback);
        Assert.Equal(new Money(12), Assert.Single(annual).Amount);
        var invoice = Assert.Single(await Catalog.GetAsync());
        Assert.Equal(new Money(-23), invoice.RemainingBalance);
        Assert.Equal(InvoiceStatus.Open, invoice.Status);
        Assert.Empty(await Payments.GetHistoryAsync(Guid.NewGuid(), 2026));
    }

    [Theory]
    [InlineData(0, InvoicePaymentKind.Cashback)]
    [InlineData(-1, InvoicePaymentKind.Discount)]
    [InlineData(10, InvoicePaymentKind.Payment)]
    public async Task InvalidAllowanceCannotCreatePaymentOrEmptyInvoice(decimal amount, InvoicePaymentKind kind)
    {
        Assert.False((await Payments.SaveAllowanceAsync(null, Allowance(amount, kind))).Succeeded);
        Assert.Empty(await Payments.GetHistoryAsync());
        Assert.Empty(await Catalog.GetAsync());
    }

    [Fact]
    public async Task ConfirmedPaymentCannotUseAllowanceEditingOrDeletion()
    {
        var request = Allowance();
        var created = await Payments.SaveAllowanceAsync(null, request);
        await (_database ?? throw new InvalidOperationException()).ExecuteAsync("UPDATE invoices.invoice_payments SET \"Kind\" = 0");
        Assert.False((await Payments.SaveAllowanceAsync(created.ExternalId, request)).Succeeded);
        Assert.False((await Payments.DeleteAllowanceAsync(created.ExternalId!.Value)).Succeeded);
        Assert.Equal(InvoicePaymentKind.Payment, Assert.Single(await Payments.GetHistoryAsync()).Kind);
    }

    [Fact]
    public async Task FailedOperationRollsBackAllowanceAndItsInvoiceRegistration()
    {
        var succeeded = await Provider.GetRequiredService<IFinancialOperation>().ExecuteAsync(async token =>
        {
            Assert.True((await Payments.SaveAllowanceAsync(null, Allowance(), token)).Succeeded);
            return false;
        }, x => x);
        Assert.False(succeeded);
        Assert.Empty(await Payments.GetHistoryAsync());
        Assert.Empty(await Catalog.GetAsync());
    }
}
