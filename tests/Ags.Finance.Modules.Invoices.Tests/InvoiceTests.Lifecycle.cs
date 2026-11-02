using System.Transactions;
using Ags.Finance.Modules.Accounts.Contracts;
using Ags.Finance.Modules.Invoices.Contracts;
using Ags.Finance.Modules.Invoices.Infrastructure;
using Ags.Finance.Modules.Transactions.Contracts;
using Ags.Finance.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Ags.Finance.Modules.Invoices.Tests;

public sealed partial class InvoiceTests
{
    private IInvoiceLifecycle Lifecycle => Provider.GetRequiredService<IInvoiceLifecycle>();
    private static readonly AccountingMonth s_october = new(2026, 10);

    [Fact]
    public async Task ManualCloseProtectsNewPurchasesAndReopeningRequiresAnotherManualClose()
    {
        var entries = Provider.GetRequiredService<IEntryCatalog>();
        Assert.True((await entries.CreateAsync(Purchase(100))).Succeeded);
        Assert.True((await Lifecycle.CloseAsync(_cardId, s_october)).Succeeded);
        Assert.Equal(InvoiceStatus.Closed, Assert.Single(await Catalog.GetAsync()).Status);
        Assert.False((await entries.CreateAsync(Purchase(50))).Succeeded);
        Assert.Single(await entries.GetAsync());
        Assert.True((await Lifecycle.ReopenAsync(_cardId, s_october)).Succeeded);
        _clock.Now = new DateTimeOffset(2026, 10, 20, 12, 0, 0, TimeSpan.Zero);
        Assert.Equal(0, await Lifecycle.CloseDueAsync());
        Assert.True((await entries.CreateAsync(Purchase(50))).Succeeded);
        Assert.True((await Lifecycle.CloseAsync(_cardId, s_october)).Succeeded);
        Assert.Equal(new Money(150), Assert.Single(await Catalog.GetAsync()).Total);
    }

    [Fact]
    public async Task CutoffProtectsWritesEvenBeforeSchedulerAndClosesOnlyWhenDue()
    {
        var entries = Provider.GetRequiredService<IEntryCatalog>();
        await entries.CreateAsync(Purchase(100));
        _clock.Now = new DateTimeOffset(2026, 10, 4, 2, 59, 59, TimeSpan.Zero);
        Assert.Equal(0, await Lifecycle.CloseDueAsync());
        _clock.Now = _clock.Now.AddSeconds(1);
        Assert.False((await entries.CreateAsync(Purchase(50))).Succeeded);
        Assert.Equal(1, await Lifecycle.CloseDueAsync());
        Assert.Equal(InvoiceStatus.Closed, Assert.Single(await Catalog.GetAsync()).Status);
        Assert.Equal(0, await Lifecycle.CloseDueAsync());
    }

    [Fact]
    public async Task ZeroInvoiceRemainsClosedAndPaidOrConfirmedPaymentPreventsReopening()
    {
        Assert.True((await Lifecycle.CloseAsync(_cardId, s_october)).Succeeded);
        var invoice = Assert.Single(await Catalog.GetAsync());
        Assert.Equal(Money.Zero, invoice.Total);
        Assert.Equal(InvoiceStatus.Closed, invoice.Status);
        using var scope = Provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<InvoicesDbContext>();
        await context.Database.ExecuteSqlInterpolatedAsync($"UPDATE invoices.invoices SET \"HasConfirmedPayment\" = TRUE WHERE \"ExternalId\" = {invoice.ExternalId}");
        Assert.False((await Lifecycle.ReopenAsync(_cardId, s_october)).Succeeded);
        await context.Database.ExecuteSqlInterpolatedAsync($"UPDATE invoices.invoices SET \"Status\" = 2 WHERE \"ExternalId\" = {invoice.ExternalId}");
        Assert.False((await Lifecycle.ReopenAsync(_cardId, s_october)).Succeeded);
        Assert.False((await Provider.GetRequiredService<IEntryCatalog>().CreateAsync(Purchase(50))).Succeeded);
        Assert.Equal(InvoiceStatus.Paid, Assert.Single(await Catalog.GetAsync()).Status);
    }

    [Fact]
    public async Task MergeReachingProtectedInvoiceDoesNotChangeAnyReferences()
    {
        var entries = Provider.GetRequiredService<IEntryCatalog>();
        await entries.CreateAsync(Purchase(100));
        await entries.CreateAsync(Purchase(80, 11));
        await Lifecycle.CloseAsync(_cardId, s_october);
        var merchants = Provider.GetRequiredService<IMerchantCatalog>();
        var target = await merchants.SaveAsync(null, "Loja Mantida", null);
        Assert.False((await merchants.MergeAsync(_merchantId, target.ExternalId!.Value)).Succeeded);
        Assert.All(await entries.GetAsync(), entry => Assert.Equal(_merchantId, entry.MerchantExternalId));
        Assert.Equal(2, (await merchants.FindAsync()).Count);
    }

    [Fact]
    public async Task FailureRollsBackTransactionsAndInvoicesWithoutDistributedPromotion()
    {
        var operations = Provider.GetRequiredService<IFinancialOperation>();
        var result = await operations.ExecuteAsync(async token =>
        {
            Assert.True((await Provider.GetRequiredService<IEntryCatalog>().CreateAsync(Purchase(100), token)).Succeeded);
            Assert.True((await Lifecycle.CloseAsync(_cardId, s_october, token)).Succeeded);
            Assert.Equal(Guid.Empty, Transaction.Current?.TransactionInformation.DistributedIdentifier);
            return new InvoiceResult("Falha simulada antes de confirmar.");
        }, x => x.Succeeded);
        Assert.False(result.Succeeded);
        Assert.Empty(await Provider.GetRequiredService<IEntryCatalog>().GetAsync());
        Assert.Empty(await Catalog.GetAsync());
    }

    [Fact]
    public async Task ConcurrentPurchaseAndCloseProduceAConsistentProtectedInvoice()
    {
        var entries = Provider.GetRequiredService<IEntryCatalog>();
        await entries.CreateAsync(Purchase(100));
        var closing = Lifecycle.CloseAsync(_cardId, s_october);
        var adding = entries.CreateAsync(Purchase(50));
        await Task.WhenAll((Task)closing, adding);
        Assert.True((await closing).Succeeded);
        var added = (await adding).Succeeded;
        var invoice = Assert.Single(await Catalog.GetAsync());
        Assert.Equal(InvoiceStatus.Closed, invoice.Status);
        Assert.Equal(new Money(added ? 150 : 100), invoice.Total);
        Assert.Equal(added ? 2 : 1, invoice.Entries.Count);
        Assert.Equal(typeof(InvoiceTests).Assembly.GetName().Name!.Replace(".Tests", string.Empty, StringComparison.Ordinal), Provider.GetRequiredService<IInvoiceWriteGuard>().GetType().Assembly.GetName().Name);
    }
}
