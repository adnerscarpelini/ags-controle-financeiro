using Ags.Finance.Modules.Attachments.Contracts;
using Ags.Finance.SharedKernel;

namespace Ags.Finance.Modules.Invoices.Contracts;

public sealed record InvoiceAttachmentReference(AttachmentOwner Owner, Guid CardExternalId, string CardName, ReferenceMonth Month);

public interface IInvoiceAttachmentCatalog
{
    Task<IReadOnlyList<InvoiceAttachmentReference>> GetReferencesAsync(CancellationToken cancellationToken = default);
    Task<AttachmentOwner?> FindInvoiceOwnerAsync(Guid cardExternalId, ReferenceMonth month, CancellationToken cancellationToken = default);
    Task<AttachmentResult> UploadInvoiceAsync(Guid cardExternalId, ReferenceMonth month, AttachmentKind kind, string fileName, Stream content, CancellationToken cancellationToken = default);
    Task<AttachmentResult> UploadPaymentAsync(Guid paymentExternalId, AttachmentKind kind, string fileName, Stream content, CancellationToken cancellationToken = default);
    Task<AttachmentResult> TrashAsync(AttachmentOwner owner, Guid attachmentExternalId, CancellationToken cancellationToken = default);
    Task<AttachmentResult> RestoreAsync(AttachmentOwner owner, Guid attachmentExternalId, CancellationToken cancellationToken = default);
}
