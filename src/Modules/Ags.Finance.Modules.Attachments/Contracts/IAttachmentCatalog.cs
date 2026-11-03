namespace Ags.Finance.Modules.Attachments.Contracts;

public enum AttachmentOwnerType { Entry, Purchase, Invoice, InvoicePayment }
public enum AttachmentKind { Bill, Receipt, Invoice, Other }
public sealed record AttachmentOwner(AttachmentOwnerType Type, Guid ExternalId);
public sealed record AttachmentDetails(Guid ExternalId, AttachmentOwner Owner, AttachmentKind Kind,
    string OriginalFileName, string ContentType, long SizeBytes, string Sha256, string StorageKey,
    DateTimeOffset CreatedAt, DateTimeOffset? DeletedAt);
public sealed record AttachmentResult(Guid? ExternalId, string? Error)
{
    public bool Succeeded => Error is null;
}
public sealed record AttachmentContent(AttachmentDetails Details, Stream Stream);
public sealed record AttachmentStorageOptions(string Directory, long MaxFileSizeBytes = 20 * 1024 * 1024, string? Error = null);

/// <summary>Storage boundary. Business modules validate owners before mutating their attachments.</summary>
public interface IAttachmentCatalog
{
    Task<IReadOnlyList<AttachmentDetails>> ListAsync(CancellationToken cancellationToken = default);
    /// <summary>Inside an ambient transaction, the caller holds the maintenance lease through commit.</summary>
    Task<AttachmentResult> UploadAsync(AttachmentOwner owner, AttachmentKind kind, string originalFileName, Stream content, CancellationToken cancellationToken = default);
    Task<AttachmentContent?> OpenAsync(Guid externalId, CancellationToken cancellationToken = default);
    Task<AttachmentResult> TrashAsync(AttachmentOwner owner, Guid externalId, CancellationToken cancellationToken = default);
    Task TrashOwnerAsync(AttachmentOwner owner, CancellationToken cancellationToken = default);
    Task<AttachmentResult> RestoreAsync(AttachmentOwner owner, Guid externalId, CancellationToken cancellationToken = default);
    Task PurgeAsync(CancellationToken cancellationToken = default);
    Task<IAsyncDisposable> AcquireMaintenanceLeaseAsync(CancellationToken cancellationToken = default);
}
