using Ags.Finance.Modules.Accounts.Contracts;
using Ags.Finance.Modules.Invoices.Contracts;
using Ags.Finance.Modules.Transactions.Contracts;
using Ags.Finance.SharedKernel;
using Microsoft.Extensions.DependencyInjection;

namespace Ags.Finance.Modules.Invoices.Tests;

public sealed partial class InvoiceTests
{
    [Fact]
    public async Task ReopeningPaidInvoiceRestoresCentsExactlyOnceAndOldConfirmationCannotRepay()
    {
        var entries = Provider.GetRequiredService<IEntryCatalog>();
        await entries.CreateAsync(Purchase(66.66m));
        var month = new ReferenceMonth(2026, 10);
        await Lifecycle.CloseAsync(_cardId, month);
        var request = Payment(66.66m, true);
        Assert.True((await Payments.ConfirmPaymentAsync(request)).Succeeded);
        var entry = Assert.Single(await entries.GetAsync());
        Assert.False((await entries.EditAsync(entry.ExternalId, Change(entry) with { Details = "Detalhe" }, EntryChangeScope.OnlyThis)).Succeeded);
        Assert.Equal(new Money(-66.66m), (await entries.GetBalanceAsync(_accountId, request.Date))?.EffectiveBalance);
        var results = await Task.WhenAll(Payments.ReopenUndoingPaymentsAsync(_cardId, month), Payments.ReopenUndoingPaymentsAsync(_cardId, month));
        Assert.All(results, x => Assert.True(x.Succeeded, x.Error));
        var invoice = Assert.Single(await Catalog.GetAsync());
        Assert.Equal(InvoiceStatus.Open, invoice.Status);
        Assert.False(invoice.HasConfirmedPayment);
        Assert.Null(invoice.PaidAt);
        Assert.NotNull(Assert.Single(invoice.PaymentHistory).UndoneAt);
        Assert.Equal(Money.Zero, invoice.PaymentsTotal);
        Assert.Equal(new Money(66.66m), invoice.RemainingBalance);
        Assert.Equal(Money.Zero, (await entries.GetBalanceAsync(_accountId, request.Date))?.EffectiveBalance);
        Assert.True((await entries.EditAsync(entry.ExternalId, Change(entry) with { Details = "Detalhe" }, EntryChangeScope.OnlyThis)).Succeeded);
        Assert.True((await Payments.ConfirmPaymentAsync(request)).Succeeded);
        Assert.Empty(await Provider.GetRequiredService<IAccountMovementCatalog>().GetAsync());
        await Lifecycle.CloseAsync(_cardId, month);
        Assert.False((await Payments.ConfirmPaymentAsync(Payment(30, true))).Succeeded);
        Assert.True((await Payments.ConfirmPaymentAsync(Payment(66.66m, true))).Succeeded);
        Assert.Equal(new Money(66.66m), Assert.Single(await Catalog.GetAsync()).PaymentsTotal);
    }

    [Theory]
    [InlineData(InvoicePaymentKind.Discount)]
    [InlineData(InvoicePaymentKind.Cashback)]
    public async Task OpenInvoiceUndoRestoresTwoAccountsKeepsAllowancesAndSuppressesAutomaticClosing(InvoicePaymentKind allowanceKind)
    {
        var entries = Provider.GetRequiredService<IEntryCatalog>();
        await entries.CreateAsync(Purchase(100));
        var accounts = Provider.GetRequiredService<IAccountCatalog>();
        var bank = Assert.Single(await accounts.GetBanksAsync());
        var other = (await accounts.SaveAccountAsync(new(null, bank.ExternalId, "Outra Conta", AccountKind.Checking, "green", new Money(100), new DateOnly(2026, 9, 1)))).ExternalId!.Value;
        Assert.True((await Payments.ConfirmPaymentAsync(Payment(30))).Succeeded);
        Assert.True((await Payments.ConfirmPaymentAsync(Payment(20) with { AccountExternalId = other })).Succeeded);
        Assert.True((await Payments.SaveAllowanceAsync(null, Allowance(5) with { Kind = allowanceKind })).Succeeded);
        await accounts.DeactivateAccountAsync(_accountId);
        _clock.Now = new DateTimeOffset(2026, 11, 5, 12, 0, 0, TimeSpan.Zero);
        Assert.True((await Payments.ReopenUndoingPaymentsAsync(_cardId, new ReferenceMonth(2026, 10))).Succeeded);
        Assert.Equal(0, await Lifecycle.CloseDueAsync());
        var invoice = Assert.Single(await Catalog.GetAsync());
        Assert.Equal(InvoiceStatus.Open, invoice.Status);
        Assert.Equal(new Money(5), invoice.DiscountsTotal + invoice.CashbackTotal);
        Assert.Equal(new Money(95), invoice.RemainingBalance);
        Assert.All(invoice.PaymentHistory.Where(x => x.Kind == InvoicePaymentKind.Payment), x => Assert.NotNull(x.UndoneAt));
        Assert.Null(invoice.PaymentHistory.Single(x => x.Kind == allowanceKind).UndoneAt);
        Assert.Equal(Money.Zero, (await entries.GetBalanceAsync(_accountId, new DateOnly(2026, 11, 5)))?.EffectiveBalance);
        Assert.Equal(new Money(100), (await entries.GetBalanceAsync(other, new DateOnly(2026, 11, 5)))?.EffectiveBalance);
        Assert.True((await Lifecycle.CloseAsync(_cardId, invoice.Month)).Succeeded);
        Assert.Equal(InvoiceStatus.Closed, Assert.Single(await Catalog.GetAsync()).Status);
    }

    [Fact]
    public async Task FailedReopeningRollsBackPaymentsCashAndInvoiceStateTogether()
    {
        var entries = Provider.GetRequiredService<IEntryCatalog>();
        await entries.CreateAsync(Purchase(66.66m));
        var month = new ReferenceMonth(2026, 10);
        await Lifecycle.CloseAsync(_cardId, month);
        Assert.True((await Payments.ConfirmPaymentAsync(Payment(66.66m, true))).Succeeded);
        await (_database ?? throw new InvalidOperationException()).ExecuteAsync("ALTER TABLE invoices.invoices ADD CONSTRAINT test_reject_reopening CHECK (\"Status\" = 2)");
        await Assert.ThrowsAsync<Microsoft.EntityFrameworkCore.DbUpdateException>(() => Payments.ReopenUndoingPaymentsAsync(_cardId, month));
        var invoice = Assert.Single(await Catalog.GetAsync());
        Assert.Equal(InvoiceStatus.Paid, invoice.Status);
        Assert.True(invoice.HasConfirmedPayment);
        Assert.Null(Assert.Single(invoice.PaymentHistory).UndoneAt);
        Assert.Equal(new Money(-66.66m), (await entries.GetBalanceAsync(_accountId, new DateOnly(2026, 9, 1)))?.EffectiveBalance);
    }
}
