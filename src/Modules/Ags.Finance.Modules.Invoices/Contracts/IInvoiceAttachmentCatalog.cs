using Ags.Finance.Modules.Attachments.Contracts;
using Ags.Finance.SharedKernel;

namespace Ags.Finance.Modules.Invoices.Contracts;

public interface IInvoiceAttachmentCatalog
{
    Task<AttachmentOwner?> FindInvoiceOwnerAsync(Guid cardExternalId, AccountingMonth month, CancellationToken cancellationToken = default);
    Task<AttachmentResult> UploadInvoiceAsync(Guid cardExternalId, AccountingMonth month, AttachmentKind kind, string fileName, Stream content, CancellationToken cancellationToken = default);
    Task<AttachmentResult> UploadPaymentAsync(Guid paymentExternalId, AttachmentKind kind, string fileName, Stream content, CancellationToken cancellationToken = default);
    Task<AttachmentResult> TrashAsync(AttachmentOwner owner, Guid attachmentExternalId, CancellationToken cancellationToken = default);
    Task<AttachmentResult> RestoreAsync(AttachmentOwner owner, Guid attachmentExternalId, CancellationToken cancellationToken = default);
}
