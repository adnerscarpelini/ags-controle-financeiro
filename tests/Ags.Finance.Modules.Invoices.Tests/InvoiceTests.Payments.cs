using Ags.Finance.Modules.Accounts.Contracts;
using Ags.Finance.Modules.Invoices.Contracts;
using Ags.Finance.Modules.Transactions.Contracts;
using Ags.Finance.SharedKernel;
using Microsoft.Extensions.DependencyInjection;

namespace Ags.Finance.Modules.Invoices.Tests;

public sealed partial class InvoiceTests
{
    private ConfirmInvoicePaymentRequest Payment(decimal amount, bool settle = false)
        => new(Guid.CreateVersion7(), _cardId, new AccountingMonth(2026, 10), _accountId, new DateOnly(2026, 9, 1), new Money(amount), settle);

    [Fact]
    public async Task PartialAndTotalPaymentsDebitOnlyChosenAccountAndExplicitlySettle()
    {
        var entries = Provider.GetRequiredService<IEntryCatalog>();
        await entries.CreateAsync(Purchase(1000));
        await Lifecycle.CloseAsync(_cardId, new AccountingMonth(2026, 10));
        var accounts = Provider.GetRequiredService<IAccountCatalog>();
        var bank = Assert.Single(await accounts.GetBanksAsync());
        var other = await accounts.SaveAccountAsync(new(null, bank.ExternalId, "Outra Conta", AccountKind.Checking, "green", new Money(3000), new DateOnly(2026, 9, 1)));
        var first = Payment(500) with { AccountExternalId = other.ExternalId!.Value };
        var concurrent = await Task.WhenAll(Payments.ConfirmPaymentAsync(first), Payments.ConfirmPaymentAsync(first));
        Assert.All(concurrent, x => Assert.True(x.Succeeded, x.Error));
        var invoice = Assert.Single(await Catalog.GetAsync());
        Assert.Equal(new Money(500), invoice.RemainingBalance);
        Assert.Equal(InvoiceStatus.Closed, invoice.Status);
        Assert.False((await Lifecycle.ReopenAsync(_cardId, invoice.Month)).Succeeded);
        Assert.Equal(new Money(2500), (await entries.GetBalanceAsync(other.ExternalId.Value, first.Date))?.EffectiveBalance);
        Assert.Equal(Money.Zero, (await entries.GetBalanceAsync(_accountId, first.Date))?.EffectiveBalance);
        var final = Payment(500, true);
        Assert.True((await Payments.ConfirmPaymentAsync(final)).Succeeded);
        Assert.True((await Payments.ConfirmPaymentAsync(final)).Succeeded);
        invoice = Assert.Single(await Catalog.GetAsync());
        Assert.Equal(InvoiceStatus.Paid, invoice.Status);
        Assert.Equal(_clock.Now, invoice.PaidAt);
        Assert.Equal(Money.Zero, invoice.RemainingBalance);
        Assert.Equal(new Money(-500), (await entries.GetBalanceAsync(_accountId, first.Date))?.EffectiveBalance);
        Assert.Equal(2, invoice.PaymentHistory.Count);
        Assert.Equal(2, (await Provider.GetRequiredService<IAccountMovementCatalog>().GetAsync()).Count);
        Assert.Equal(new Money(-1000), (await entries.QueryAsync(new(Month: invoice.Month))).Expenses);
        var payment = invoice.PaymentHistory[0];
        Assert.False((await Payments.DeleteAllowanceAsync(payment.ExternalId)).Succeeded);
    }

    [Fact]
    public async Task AdvanceAndExcessPaymentsKeepOpenUntilClosingAndExplicitSettlement()
    {
        var entries = Provider.GetRequiredService<IEntryCatalog>();
        await entries.CreateAsync(Purchase(100));
        Assert.True((await Payments.ConfirmPaymentAsync(Payment(100))).Succeeded);
        var invoice = Assert.Single(await Catalog.GetAsync());
        Assert.Equal(InvoiceStatus.Open, invoice.Status);
        Assert.Equal(Money.Zero, invoice.RemainingBalance);
        await entries.CreateAsync(Purchase(50));
        Assert.Equal(new Money(50), Assert.Single(await Catalog.GetAsync()).RemainingBalance);
        Assert.True((await Payments.ConfirmPaymentAsync(Payment(70))).Succeeded);
        Assert.Equal(new Money(-20), Assert.Single(await Catalog.GetAsync()).RemainingBalance);
        Assert.False((await Payments.ConfirmSettlementAsync(_cardId, invoice.Month)).Succeeded);
        await Lifecycle.CloseAsync(_cardId, invoice.Month);
        Assert.Equal(InvoiceStatus.Closed, Assert.Single(await Catalog.GetAsync()).Status);
        Assert.True((await Payments.ConfirmSettlementAsync(_cardId, invoice.Month)).Succeeded);
        Assert.Equal(InvoiceStatus.Paid, Assert.Single(await Catalog.GetAsync()).Status);
        Assert.Equal(2, (await Payments.GetHistoryAsync()).Count);
        Assert.False((await Lifecycle.ReopenAsync(_cardId, invoice.Month)).Succeeded);
    }

    [Fact]
    public async Task CashbackOnlySettlementCreatesNoPaymentOrDebit()
    {
        await Provider.GetRequiredService<IEntryCatalog>().CreateAsync(Purchase(100));
        await Payments.SaveAllowanceAsync(null, Allowance(100));
        await Lifecycle.CloseAsync(_cardId, new AccountingMonth(2026, 10));
        Assert.True((await Payments.ConfirmSettlementAsync(_cardId, new AccountingMonth(2026, 10))).Succeeded);
        Assert.True((await Payments.ConfirmSettlementAsync(_cardId, new AccountingMonth(2026, 10))).Succeeded);
        Assert.Single(await Payments.GetHistoryAsync());
        Assert.Empty(await Provider.GetRequiredService<IAccountMovementCatalog>().GetAsync());
    }

    [Fact]
    public async Task InvalidPaymentsOrUnfundedSettlementCannotLeaveRegistrationOrDebit()
    {
        var entries = Provider.GetRequiredService<IEntryCatalog>();
        await entries.CreateAsync(Purchase(100));
        await Lifecycle.CloseAsync(_cardId, new AccountingMonth(2026, 10));
        foreach (var invalid in new[] { Payment(0), Payment(-1), Payment(50, true), Payment(100) with { AccountExternalId = Guid.NewGuid() } })
        {
            Assert.False((await Payments.ConfirmPaymentAsync(invalid)).Succeeded);
        }
        await Provider.GetRequiredService<IAccountCatalog>().DeactivateAccountAsync(_accountId);
        Assert.False((await Payments.ConfirmPaymentAsync(Payment(100))).Succeeded);
        Assert.Empty(await Payments.GetHistoryAsync());
        Assert.Empty(await Provider.GetRequiredService<IAccountMovementCatalog>().GetAsync());
        Assert.False(Assert.Single(await Catalog.GetAsync()).HasConfirmedPayment);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task FailureInEitherModuleRollsBackPaymentDebitAndStatus(bool failPayment)
    {
        await Provider.GetRequiredService<IEntryCatalog>().CreateAsync(Purchase(100));
        await Lifecycle.CloseAsync(_cardId, new AccountingMonth(2026, 10));
        var table = failPayment ? "invoices.invoice_payments" : "transactions.account_movements";
        var restriction = failPayment ? "\"Kind\" <> 0" : "\"Kind\" <> 2";
        await (_database ?? throw new InvalidOperationException()).ExecuteAsync($"ALTER TABLE {table} ADD CONSTRAINT test_reject_payment CHECK ({restriction})");
        await Assert.ThrowsAsync<Microsoft.EntityFrameworkCore.DbUpdateException>(() => Payments.ConfirmPaymentAsync(Payment(100, true)));
        Assert.Empty(await Payments.GetHistoryAsync());
        Assert.Empty(await Provider.GetRequiredService<IAccountMovementCatalog>().GetAsync());
        var invoice = Assert.Single(await Catalog.GetAsync());
        Assert.Equal(InvoiceStatus.Closed, invoice.Status);
        Assert.False(invoice.HasConfirmedPayment);
        Assert.Equal(new Money(100), invoice.RemainingBalance);
    }
}
