using Ags.Finance.Modules.Accounts.Contracts;
using Ags.Finance.Modules.Attachments.Contracts;
using Ags.Finance.Modules.Invoices.Contracts;
using Ags.Finance.Modules.Invoices.Domain;
using Ags.Finance.SharedKernel;

namespace Ags.Finance.Modules.Invoices.Application.ManageAttachments;

internal sealed class InvoiceAttachmentCatalog(IInvoiceStore store, IAccountCatalog accounts, IFinancialOperation operations, IAttachmentCatalog? attachments = null) : IInvoiceAttachmentCatalog
{
    private IAttachmentCatalog Catalog => attachments ?? throw new InvalidOperationException("Attachments module is not registered.");
    public async Task<AttachmentOwner?> FindInvoiceOwnerAsync(Guid cardExternalId, AccountingMonth month, CancellationToken cancellationToken = default)
        => await store.FindAsync(cardExternalId, month, cancellationToken) is { } invoice ? new(AttachmentOwnerType.Invoice, invoice.ExternalId) : null;

    public Task<AttachmentResult> UploadInvoiceAsync(Guid cardExternalId, AccountingMonth month, AttachmentKind kind, string fileName, Stream content, CancellationToken cancellationToken = default)
        => operations.ExecuteAsync(async token =>
        {
            if (month.Year == 0 || !(await accounts.GetCreditCardsAsync(false, token)).Any(x => x.ExternalId == cardExternalId))
            { return new AttachmentResult(null, "Selecione uma fatura válida."); }
            var invoice = await store.FindAsync(cardExternalId, month, token);
            if (invoice is null)
            {
                invoice = Invoice.Create(cardExternalId, month);
                await store.SaveAsync(invoice, token);
            }
            return await UploadAsync(new(AttachmentOwnerType.Invoice, invoice.ExternalId), kind, fileName, content, token);
        }, x => x.Succeeded, cancellationToken);

    public Task<AttachmentResult> UploadPaymentAsync(Guid paymentExternalId, AttachmentKind kind, string fileName, Stream content, CancellationToken cancellationToken = default)
        => ChangeAsync(new(AttachmentOwnerType.InvoicePayment, paymentExternalId), owner => UploadAsync(owner, kind, fileName, content, cancellationToken), cancellationToken);
    public Task<AttachmentResult> TrashAsync(AttachmentOwner owner, Guid attachmentExternalId, CancellationToken cancellationToken = default)
        => ChangeAsync(owner, validated => Catalog.TrashAsync(validated, attachmentExternalId, cancellationToken), cancellationToken);
    public Task<AttachmentResult> RestoreAsync(AttachmentOwner owner, Guid attachmentExternalId, CancellationToken cancellationToken = default)
        => ChangeAsync(owner, validated => Catalog.RestoreAsync(validated, attachmentExternalId, cancellationToken), cancellationToken);
    private Task<AttachmentResult> UploadAsync(AttachmentOwner owner, AttachmentKind kind, string name, Stream content, CancellationToken cancellationToken)
        => kind is AttachmentKind.Invoice or AttachmentKind.Receipt or AttachmentKind.Other
            ? Catalog.UploadAsync(owner, kind, name, content, cancellationToken)
            : Task.FromResult(new AttachmentResult(null, "Selecione Fatura, Comprovante ou Outro."));
    private Task<AttachmentResult> ChangeAsync(AttachmentOwner owner, Func<AttachmentOwner, Task<AttachmentResult>> change, CancellationToken cancellationToken)
        => operations.ExecuteAsync(async token =>
        {
            var exists = owner.Type switch
            {
                AttachmentOwnerType.Invoice => (await store.GetAsync(token)).Any(x => x.ExternalId == owner.ExternalId),
                AttachmentOwnerType.InvoicePayment => (await store.GetPaymentsAsync(token)).Any(x => x.ExternalId == owner.ExternalId && x.Kind == Domain.InvoicePaymentKind.Payment),
                _ => false
            };
            return exists ? await change(owner) : new(null, "O dono deste anexo foi excluído ou não existe.");
        }, x => x.Succeeded, cancellationToken);
}
