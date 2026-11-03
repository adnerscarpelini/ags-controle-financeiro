using Ags.Finance.Modules.Accounts.Contracts;
using Ags.Finance.Modules.Transactions.Contracts;
using Ags.Finance.SharedKernel;
using Microsoft.Extensions.DependencyInjection;

namespace Ags.Finance.Modules.Invoices.Tests;

public sealed partial class InvoiceTests
{
    [Fact]
    public async Task ArchiveAndRestoreDoNotChangeHistoryPaymentsBalanceOrInvoiceState()
    {
        var entries = Provider.GetRequiredService<IEntryCatalog>();
        var accounts = Provider.GetRequiredService<IAccountCatalog>();
        await entries.CreateAsync(Purchase(100));
        await Payments.ConfirmPaymentAsync(Payment(20));
        await Lifecycle.CloseAsync(_cardId, new AccountingMonth(2026, 10));
        var before = Assert.Single(await Catalog.GetAsync());
        var balance = await entries.GetBalanceAsync(_accountId, new DateOnly(2026, 10, 31));
        await accounts.DeactivateAccountAsync(_accountId);
        await accounts.DeactivateCreditCardAsync(_cardId);
        Assert.Equal(before.Total, Assert.Single(await Catalog.GetAsync()).Total);
        Assert.Equal(before.RemainingBalance, Assert.Single(await Catalog.GetAsync()).RemainingBalance);
        await accounts.ReactivateAccountAsync(_accountId);
        Assert.Empty(await accounts.GetCreditCardsAsync());
        await accounts.ReactivateCreditCardAsync(_cardId);
        var after = Assert.Single(await Catalog.GetAsync());
        Assert.Equal(before.ExternalId, after.ExternalId);
        Assert.Equal(before.Status, after.Status);
        Assert.Equal(before.Entries, after.Entries);
        Assert.Equal(before.PaymentHistory, after.PaymentHistory);
        Assert.Equal(balance, await entries.GetBalanceAsync(_accountId, new DateOnly(2026, 10, 31)));
    }
}
