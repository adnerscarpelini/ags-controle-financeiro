using Ags.Finance.Modules.Attachments.Domain;

namespace Ags.Finance.Modules.Attachments.Application;

internal interface IAttachmentStore
{
    Task<IReadOnlyList<Attachment>> ListAsync(CancellationToken cancellationToken);
    Task<bool> SaveAsync(Attachment attachment, CancellationToken cancellationToken);
    Task DeleteAsync(Attachment attachment, CancellationToken cancellationToken);
    Task<IAsyncDisposable> AcquireMaintenanceLeaseAsync(CancellationToken cancellationToken);
}

internal interface IAttachmentFiles
{
    Task<(long Bytes, string Sha256)> WriteAsync(string temporaryKey, Stream source, CancellationToken cancellationToken);
    (string ContentType, string Extension)? Identify(string temporaryKey);
    void Move(string temporaryKey, string storageKey);
    void Delete(string storageKey);
    Stream Open(string storageKey);
}
