using Ags.Finance.Modules.Attachments.Contracts;

namespace Ags.Finance.Modules.Transactions.Contracts;

public sealed record EntryAttachmentReference(AttachmentOwner Owner, DateOnly Date, string Description);

public interface IEntryAttachmentCatalog
{
    Task<IReadOnlyList<EntryAttachmentReference>> GetReferencesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AttachmentDetails>> ListAsync(CancellationToken cancellationToken = default);
    Task<AttachmentOwner?> FindOwnerAsync(Guid externalId, CancellationToken cancellationToken = default);
    Task<AttachmentResult> UploadAsync(Guid externalId, AttachmentKind kind, string fileName, Stream content, CancellationToken cancellationToken = default);
    Task<AttachmentResult> TrashAsync(Guid externalId, Guid attachmentExternalId, CancellationToken cancellationToken = default);
    Task<AttachmentResult> RestoreAsync(Guid externalId, Guid attachmentExternalId, CancellationToken cancellationToken = default);
}
