using Ags.Finance.Modules.Attachments.Contracts;

namespace Ags.Finance.Modules.Transactions.Contracts;

public interface IEntryAttachmentCatalog
{
    Task<IReadOnlyList<AttachmentDetails>> ListAsync(CancellationToken cancellationToken = default);
    Task<AttachmentOwner?> FindOwnerAsync(Guid externalId, CancellationToken cancellationToken = default);
    Task<AttachmentResult> UploadAsync(Guid externalId, AttachmentKind kind, string fileName, Stream content, CancellationToken cancellationToken = default);
    Task<AttachmentResult> TrashAsync(Guid externalId, Guid attachmentExternalId, CancellationToken cancellationToken = default);
    Task<AttachmentResult> RestoreAsync(Guid externalId, Guid attachmentExternalId, CancellationToken cancellationToken = default);
}
