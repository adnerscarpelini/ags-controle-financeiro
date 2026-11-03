using Ags.Finance.Modules.Invoices.Contracts;
using Ags.Finance.Modules.Transactions.Contracts;
using Ags.Finance.SharedKernel;
using Microsoft.Extensions.DependencyInjection;

namespace Ags.Finance.Modules.Invoices.Tests;

public sealed partial class InvoiceTests
{
    private async Task<EntryDetails> AnticipationPurchaseAsync()
    {
        _clock.Now = new DateTimeOffset(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);
        var entries = Provider.GetRequiredService<IEntryCatalog>();
        Assert.True((await entries.CreateInstallmentsAsync(Purchase(1000, 6) with { Date = new DateOnly(2026, 5, 14) }, 10)).Succeeded);
        _clock.Now = new DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);
        return (await entries.GetAsync()).Single(x => x.InstallmentNumber == 4);
    }

    [Fact]
    public async Task LastThreeInstallmentsMoveTogetherAndDiscountIsSeparateAndIdempotent()
    {
        var selected = await AnticipationPurchaseAsync();
        var request = new AnticipateInstallmentsRequest(Guid.CreateVersion7(), selected.ExternalId, 3, new Money(15), new AccountingMonth(2026, 9));
        var catalog = Provider.GetRequiredService<IInvoiceAnticipationCatalog>();
        var results = await Task.WhenAll(catalog.AnticipateAsync(request), catalog.AnticipateAsync(request));
        Assert.All(results, x => Assert.True(x.Succeeded, x.Error));
        var entries = await Provider.GetRequiredService<IEntryCatalog>().GetAsync();
        Assert.Equal(10, entries.Count);
        Assert.Equal(-1000m, entries.Sum(x => x.Amount.Amount));
        Assert.All(entries.Where(x => x.InstallmentNumber >= 8), x => Assert.Equal(new AccountingMonth(2026, 9), x.Month));
        Assert.All(entries, x => Assert.Equal(new DateOnly(2026, 5, 14), x.Date));
        var invoice = Assert.Single(await Catalog.GetMonthAsync(new AccountingMonth(2026, 9)));
        Assert.Equal(new Money(400), invoice.Total);
        Assert.Equal(new Money(385), invoice.RemainingBalance);
        Assert.Equal(InvoicePaymentKind.Discount, Assert.Single(invoice.PaymentHistory).Kind);
        Assert.Contains(selected.MerchantName, Assert.Single(invoice.PaymentHistory).Details, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(7, 0)]
    [InlineData(3, 300)]
    [InlineData(3, -1)]
    public async Task InvalidAnticipationChangesNothing(int count, int discount)
    {
        var selected = await AnticipationPurchaseAsync();
        Assert.False((await Provider.GetRequiredService<IInvoiceAnticipationCatalog>().AnticipateAsync(new(Guid.CreateVersion7(), selected.ExternalId, count, new Money(discount)))).Succeeded);
        Assert.Equal(1, (await Provider.GetRequiredService<IEntryCatalog>().GetAsync()).Count(x => x.Month == selected.Month));
        Assert.Empty(await Payments.GetHistoryAsync());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ProtectedSourceOrTargetRollsBackEveryMoveAndDiscount(bool source)
    {
        var selected = await AnticipationPurchaseAsync();
        await Lifecycle.CloseAsync(_cardId, source ? new AccountingMonth(2027, 2) : new AccountingMonth(2026, 9));
        var result = await Provider.GetRequiredService<IInvoiceAnticipationCatalog>().AnticipateAsync(new(Guid.CreateVersion7(), selected.ExternalId, 3, new Money(15)));
        Assert.False(result.Succeeded);
        Assert.Equal(1, (await Provider.GetRequiredService<IEntryCatalog>().GetAsync()).Count(x => x.Month == selected.Month));
        Assert.Empty(await Payments.GetHistoryAsync());
    }

    [Fact]
    public async Task FailureAfterMovesAndDiscountRevertsBothModules()
    {
        var selected = await AnticipationPurchaseAsync();
        await (_database ?? throw new InvalidOperationException()).ExecuteAsync("ALTER TABLE invoices.installment_anticipations ADD CONSTRAINT test_reject_anticipation CHECK (\"Count\" = 0)");
        await Assert.ThrowsAsync<Microsoft.EntityFrameworkCore.DbUpdateException>(() => Provider.GetRequiredService<IInvoiceAnticipationCatalog>().AnticipateAsync(new(Guid.CreateVersion7(), selected.ExternalId, 3, new Money(15))));
        Assert.Empty(await Payments.GetHistoryAsync());
        Assert.Equal(1, (await Provider.GetRequiredService<IEntryCatalog>().GetAsync()).Count(x => x.Month == selected.Month));
    }
}
