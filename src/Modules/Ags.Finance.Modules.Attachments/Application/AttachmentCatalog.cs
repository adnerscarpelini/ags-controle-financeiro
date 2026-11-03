using Ags.Finance.Modules.Attachments.Contracts;
using Ags.Finance.Modules.Attachments.Domain;

namespace Ags.Finance.Modules.Attachments.Application;

internal sealed class AttachmentCatalog(IAttachmentStore store, IAttachmentFiles files, AttachmentStorageOptions options, TimeProvider clock) : IAttachmentCatalog
{
    public async Task<IReadOnlyList<AttachmentDetails>> ListAsync(CancellationToken cancellationToken = default)
        => (await store.ListAsync(cancellationToken)).Select(Describe).ToList();

    public async Task<AttachmentResult> UploadAsync(AttachmentOwner owner, AttachmentKind kind, string originalFileName, Stream content, CancellationToken cancellationToken = default)
    {
        if (options.Error is { } error) { return new(null, error); }
        if (!Enum.IsDefined(owner.Type) || !Enum.IsDefined(kind) || owner.ExternalId == Guid.Empty)
        { return new(null, "Informe o dono e o tipo do documento."); }
        var name = Path.GetFileName(originalFileName.Replace('\\', '/'));
        if (string.IsNullOrWhiteSpace(name) || name.Length > 255 || name.Any(char.IsControl))
        { return new(null, "Informe um nome de arquivo válido com até 255 caracteres."); }
        var temporary = ".upload-" + Guid.NewGuid().ToString("N");
        string? definitive = null;
        var saved = false;
        try
        {
            var (size, hash) = await files.WriteAsync(temporary, content, cancellationToken);
            var format = files.Identify(temporary);
            if (format is null) { return new(null, "Envie um PDF, JPEG, PNG ou WebP válido."); }
            var row = Attachment.Create(owner.Type.ToString(), owner.ExternalId, kind.ToString(), name, format.Value.ContentType, size, hash);
            var now = clock.GetUtcNow();
            var folder = owner.Type switch { AttachmentOwnerType.Invoice => "invoices", AttachmentOwnerType.InvoicePayment => "payments", _ => "entries" };
            definitive = FormattableString.Invariant($"{folder}/{now:yyyy/MM/dd}/{now:yyyyMMddTHHmmssZ}_{kind.ToString().ToLowerInvariant()}_{row.ExternalId}{format.Value.Extension}");
            row.SetStorageKey(definitive);
            files.Move(temporary, definitive);
            saved = await store.SaveAsync(row, cancellationToken);
            return saved ? new(row.ExternalId, null) : new(null, "Este arquivo já está anexado ao mesmo dono, inclusive na lixeira.");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        { return new(null, "Não foi possível guardar o documento. Confira a pasta de anexos, as permissões e o limite de tamanho."); }
        finally
        {
            files.Delete(temporary);
            if (!saved && definitive is not null) { files.Delete(definitive); }
        }
    }

    public async Task<AttachmentContent?> OpenAsync(Guid externalId, CancellationToken cancellationToken = default)
    {
        var row = (await store.ListAsync(cancellationToken)).FirstOrDefault(x => x.ExternalId == externalId);
        if (row is null || row.DeletedAt <= clock.GetUtcNow().AddDays(-30) || options.Error is not null) { return null; }
        try { return new(Describe(row), files.Open(row.StorageKey)); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { return null; }
    }

    public async Task<AttachmentResult> TrashAsync(AttachmentOwner owner, Guid externalId, CancellationToken cancellationToken = default)
    {
        var row = (await store.ListAsync(cancellationToken)).FirstOrDefault(x => x.ExternalId == externalId && Owns(x, owner));
        if (row is null) { return new(null, "Anexo não encontrado para este dono."); }
        row.Trash(clock.GetUtcNow());
        await store.SaveAsync(row, cancellationToken);
        return new(externalId, null);
    }

    public async Task TrashOwnerAsync(AttachmentOwner owner, CancellationToken cancellationToken = default)
    {
        foreach (var row in (await store.ListAsync(cancellationToken)).Where(x => Owns(x, owner)))
        { row.Trash(clock.GetUtcNow()); await store.SaveAsync(row, cancellationToken); }
    }

    public async Task<AttachmentResult> RestoreAsync(AttachmentOwner owner, Guid externalId, CancellationToken cancellationToken = default)
    {
        var row = (await store.ListAsync(cancellationToken)).FirstOrDefault(x => x.ExternalId == externalId && Owns(x, owner));
        if (row is null || row.DeletedAt <= clock.GetUtcNow().AddDays(-30)) { return new(null, "O prazo de restauração deste anexo terminou."); }
        row.Restore();
        await store.SaveAsync(row, cancellationToken);
        return new(externalId, null);
    }

    public Task<IAsyncDisposable> AcquireMaintenanceLeaseAsync(CancellationToken cancellationToken = default)
        => store.AcquireMaintenanceLeaseAsync(cancellationToken);

    public async Task PurgeAsync(CancellationToken cancellationToken = default)
    {
        if (options.Error is not null) { return; }
        await using var lease = await AcquireMaintenanceLeaseAsync(cancellationToken);
        foreach (var row in (await store.ListAsync(cancellationToken)).Where(x => x.DeletedAt <= clock.GetUtcNow().AddDays(-30)))
        {
            files.Delete(row.StorageKey);
            await store.DeleteAsync(row, cancellationToken);
        }
    }

    private static bool Owns(Attachment row, AttachmentOwner owner) => row.OwnerType == owner.Type.ToString() && row.OwnerExternalId == owner.ExternalId;
    private static AttachmentDetails Describe(Attachment row) => new(row.ExternalId, new(Enum.Parse<AttachmentOwnerType>(row.OwnerType), row.OwnerExternalId),
        Enum.Parse<AttachmentKind>(row.Kind), row.OriginalFileName, row.ContentType, row.SizeBytes, row.Sha256, row.StorageKey, row.CreatedAt, row.DeletedAt);
}
