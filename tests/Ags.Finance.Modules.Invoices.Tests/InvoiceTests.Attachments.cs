using System.Text;
using Ags.Finance.Modules.Attachments.Contracts;
using Ags.Finance.Modules.Invoices.Contracts;
using Ags.Finance.Modules.Transactions.Contracts;
using Ags.Finance.SharedKernel;
using Microsoft.Extensions.DependencyInjection;

namespace Ags.Finance.Modules.Invoices.Tests;

public sealed partial class InvoiceTests
{
    private IInvoiceAttachmentCatalog Documents => Provider.GetRequiredService<IInvoiceAttachmentCatalog>();
    private static MemoryStream Document() => new(Encoding.ASCII.GetBytes("%PDF-1.7\n1 0 obj<</Type /Catalog>>endobj\ntrailer<</Root 1 0 R>>\n%%EOF\n"));
    private async Task<AttachmentResult> AttachInvoiceAsync(AccountingMonth month)
    {
        using var content = Document();
        return await Documents.UploadInvoiceAsync(_cardId, month, AttachmentKind.Invoice, "fictitious.pdf", content);
    }
    [Fact]
    public async Task UploadCreatesAnUnregisteredInvoiceOpenWithoutChangingItsTotal()
    {
        var month = new AccountingMonth(2026, 10);
        Assert.Null(await Documents.FindInvoiceOwnerAsync(_cardId, month));
        Assert.True((await AttachInvoiceAsync(month)).Succeeded);
        Assert.NotNull(await Documents.FindInvoiceOwnerAsync(_cardId, month));
        var invoice = Assert.Single(await Catalog.GetAsync());
        Assert.Equal(InvoiceStatus.Open, invoice.Status);
        Assert.Equal(Money.Zero, invoice.Total);
        Assert.Empty(invoice.PaymentHistory);
    }
    [Fact]
    public async Task CreatingAnOldInvoiceForADocumentDoesNotSuppressAutomaticClosing()
    {
        var month = new AccountingMonth(2026, 6);
        Assert.True((await AttachInvoiceAsync(month)).Succeeded);
        var owner = await Documents.FindInvoiceOwnerAsync(_cardId, month);
        var before = Assert.Single(await Catalog.GetAsync());
        Assert.Equal(InvoiceStatus.Open, before.Status);
        Assert.Equal(1, await Lifecycle.CloseDueAsync());
        var after = Assert.Single(await Catalog.GetAsync());
        Assert.Equal(InvoiceStatus.Closed, after.Status);
        Assert.Equal(before.Total, after.Total);
        Assert.Equal(owner, await Documents.FindInvoiceOwnerAsync(_cardId, month));
        Assert.Empty(after.PaymentHistory);
    }
    [Fact]
    public async Task PaidInvoiceAndItsPurchaseAcceptDocumentsAndRemovalWithoutAnyFinancialChange()
    {
        var entries = Provider.GetRequiredService<IEntryCatalog>();
        var entry = (await entries.CreateAsync(Purchase(100))).ExternalId!.Value;
        var month = new AccountingMonth(2026, 10);
        await Lifecycle.CloseAsync(_cardId, month);
        await Payments.ConfirmPaymentAsync(Payment(100, true));
        var before = Assert.Single(await Catalog.GetAsync());
        var uploaded = await AttachInvoiceAsync(month);
        Assert.True(uploaded.Succeeded, uploaded.Error);
        var owner = await Documents.FindInvoiceOwnerAsync(_cardId, month);
        Assert.NotNull(owner);
        Assert.True((await Documents.TrashAsync(owner, uploaded.ExternalId!.Value)).Succeeded);
        using var content = Document();
        var purchaseDocuments = Provider.GetRequiredService<IEntryAttachmentCatalog>();
        Assert.True((await purchaseDocuments.UploadAsync(entry, AttachmentKind.Receipt, "fictitious.pdf", content)).Succeeded);
        var after = Assert.Single(await Catalog.GetAsync());
        Assert.Equal(InvoiceStatus.Paid, after.Status);
        Assert.Equal(before.Total, after.Total);
        Assert.Equal(before.RemainingBalance, after.RemainingBalance);
        Assert.Equal(before.PaidAt, after.PaidAt);
        Assert.Equal(before.PaymentHistory.Select(x => x.ExternalId), after.PaymentHistory.Select(x => x.ExternalId));
        Assert.Single(await Provider.GetRequiredService<IAccountMovementCatalog>().GetAsync());
    }
    [Fact]
    public async Task PaymentSurvivesUploadFailureAndRepeatedConfirmationAndUploadDoNotDuplicateAnything()
    {
        await Provider.GetRequiredService<IEntryCatalog>().CreateAsync(Purchase(100));
        var request = Payment(50);
        var payment = await Payments.ConfirmPaymentAsync(request);
        Directory.Delete(_attachmentsDirectory);
        using (var failed = Document())
        { Assert.False((await Documents.UploadPaymentAsync(payment.ExternalId!.Value, AttachmentKind.Receipt, "fictitious.pdf", failed)).Succeeded); }
        Assert.Single(await Payments.GetHistoryAsync());
        Assert.Equal(new Money(50), Assert.Single(await Catalog.GetAsync()).RemainingBalance);
        var repeated = await Payments.ConfirmPaymentAsync(request);
        Assert.Equal(payment.ExternalId, repeated.ExternalId);
        Directory.CreateDirectory(_attachmentsDirectory);
        using var first = Document();
        using var second = Document();
        var results = await Task.WhenAll(Documents.UploadPaymentAsync(payment.ExternalId!.Value, AttachmentKind.Receipt, "fictitious.pdf", first),
            Documents.UploadPaymentAsync(payment.ExternalId.Value, AttachmentKind.Receipt, "again.pdf", second));
        Assert.Single(results, x => x.Succeeded);
        Assert.Single(await Payments.GetHistoryAsync());
        Assert.Single(await Provider.GetRequiredService<IAccountMovementCatalog>().GetAsync());
        Assert.Single(await Provider.GetRequiredService<IAttachmentCatalog>().ListAsync());
        Assert.Single(Directory.GetFiles(_attachmentsDirectory, "*", SearchOption.AllDirectories));
    }
    [Fact]
    public async Task ReopeningKeepsInvoiceDocumentsAndAnotherOwnerCannotRestoreThem()
    {
        var month = new AccountingMonth(2026, 10);
        var uploaded = await AttachInvoiceAsync(month);
        var owner = await Documents.FindInvoiceOwnerAsync(_cardId, month);
        Assert.NotNull(owner);
        await Lifecycle.CloseAsync(_cardId, month);
        Assert.True((await Lifecycle.ReopenAsync(_cardId, month)).Succeeded);
        Assert.Equal(owner, await Documents.FindInvoiceOwnerAsync(_cardId, month));
        Assert.True((await Documents.TrashAsync(owner, uploaded.ExternalId!.Value)).Succeeded);
        Assert.False((await Documents.RestoreAsync(new(AttachmentOwnerType.Invoice, Guid.NewGuid()), uploaded.ExternalId.Value)).Succeeded);
        Assert.True((await Documents.RestoreAsync(owner, uploaded.ExternalId.Value)).Succeeded);
    }
    [Fact]
    public async Task AnticipatingInstallmentsKeepsThePurchaseOwnerAndItsSingleFile()
    {
        var selected = await AnticipationPurchaseAsync();
        var documents = Provider.GetRequiredService<IEntryAttachmentCatalog>();
        using var content = Document();
        Assert.True((await documents.UploadAsync(selected.ExternalId, AttachmentKind.Receipt, "fictitious.pdf", content)).Succeeded);
        var owner = await documents.FindOwnerAsync(selected.ExternalId);
        Assert.True((await Provider.GetRequiredService<IInvoiceAnticipationCatalog>().AnticipateAsync(new(Guid.CreateVersion7(), selected.ExternalId, 3, new Money(15)))).Succeeded);
        foreach (var entry in await Provider.GetRequiredService<IEntryCatalog>().GetAsync())
        { Assert.Equal(owner, await documents.FindOwnerAsync(entry.ExternalId)); }
        Assert.Single(Directory.GetFiles(_attachmentsDirectory, "*", SearchOption.AllDirectories));
    }
}
