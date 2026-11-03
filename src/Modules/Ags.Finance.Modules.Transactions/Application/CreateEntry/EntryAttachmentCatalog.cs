using Ags.Finance.Modules.Accounts.Contracts;
using Ags.Finance.Modules.Attachments.Contracts;
using Ags.Finance.Modules.Transactions.Contracts;

namespace Ags.Finance.Modules.Transactions.Application.CreateEntry;

internal sealed class EntryAttachmentCatalog(IEntryStore store, IFinancialOperation operations, IAttachmentCatalog? attachments = null) : IEntryAttachmentCatalog
{
    private IAttachmentCatalog Catalog => attachments ?? throw new InvalidOperationException("Attachments module is not registered.");
    public async Task<IReadOnlyList<AttachmentDetails>> ListAsync(CancellationToken cancellationToken = default)
        => attachments is null ? [] : (await attachments.ListAsync(cancellationToken)).Where(x => x.Owner.Type is AttachmentOwnerType.Entry or AttachmentOwnerType.Purchase).ToList();
    public async Task<AttachmentOwner?> FindOwnerAsync(Guid externalId, CancellationToken cancellationToken = default)
    {
        var entries = await store.GetAsync(cancellationToken);
        var entry = entries.FirstOrDefault(x => x.ExternalId == externalId || x.InstallmentPurchaseExternalId == externalId);
        return entry is null ? null : entry.InstallmentPurchaseExternalId is { } purchase
            ? new(AttachmentOwnerType.Purchase, purchase) : new(AttachmentOwnerType.Entry, entry.ExternalId);
    }
    public Task<AttachmentResult> UploadAsync(Guid externalId, AttachmentKind kind, string fileName, Stream content, CancellationToken cancellationToken = default)
        => ChangeAsync(externalId, owner => kind is AttachmentKind.Bill or AttachmentKind.Receipt or AttachmentKind.Other
            ? Catalog.UploadAsync(owner, kind, fileName, content, cancellationToken)
            : Task.FromResult(new AttachmentResult(null, "Selecione Conta/Boleto, Comprovante ou Outro.")), cancellationToken);
    public Task<AttachmentResult> TrashAsync(Guid externalId, Guid attachmentExternalId, CancellationToken cancellationToken = default)
        => ChangeAsync(externalId, owner => Catalog.TrashAsync(owner, attachmentExternalId, cancellationToken), cancellationToken);
    public Task<AttachmentResult> RestoreAsync(Guid externalId, Guid attachmentExternalId, CancellationToken cancellationToken = default)
        => ChangeAsync(externalId, owner => Catalog.RestoreAsync(owner, attachmentExternalId, cancellationToken), cancellationToken);
    private Task<AttachmentResult> ChangeAsync(Guid externalId, Func<AttachmentOwner, Task<AttachmentResult>> change, CancellationToken cancellationToken)
        => operations.ExecuteAsync(async token => await FindOwnerAsync(externalId, token) is { } owner
            ? await change(owner) : new(null, "O dono deste anexo foi excluído ou não existe."), x => x.Succeeded, cancellationToken);
}
