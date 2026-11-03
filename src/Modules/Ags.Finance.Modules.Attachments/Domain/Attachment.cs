namespace Ags.Finance.Modules.Attachments.Domain;

internal sealed class Attachment : Entity
{
    private Attachment() { }
    public string OwnerType { get; private set; } = string.Empty;
    public Guid OwnerExternalId { get; private set; }
    public string Kind { get; private set; } = string.Empty;
    public string OriginalFileName { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = string.Empty;
    public long SizeBytes { get; private set; }
    public string Sha256 { get; private set; } = string.Empty;
    public string StorageKey { get; private set; } = string.Empty;
    public DateTimeOffset? DeletedAt { get; private set; }

    public static Attachment Create(string ownerType, Guid ownerExternalId, string kind, string fileName,
        string contentType, long sizeBytes, string sha256)
        => new()
        {
            OwnerType = ownerType,
            OwnerExternalId = ownerExternalId,
            Kind = kind,
            OriginalFileName = fileName,
            ContentType = contentType,
            SizeBytes = sizeBytes,
            Sha256 = sha256
        };
    public void SetStorageKey(string storageKey) => StorageKey = storageKey;
    public void Trash(DateTimeOffset now) => DeletedAt ??= now;
    public void Restore() => DeletedAt = null;
}
