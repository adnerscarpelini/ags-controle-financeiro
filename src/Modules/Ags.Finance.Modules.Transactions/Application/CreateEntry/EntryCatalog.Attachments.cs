using Ags.Finance.Modules.Attachments.Contracts;
using Ags.Finance.Modules.Transactions.Domain;

namespace Ags.Finance.Modules.Transactions.Application.CreateEntry;

internal sealed partial class EntryCatalog
{
    private async Task TrashDeletedOwnersAsync(IReadOnlyList<TransactionEntry> deleted, IReadOnlyList<TransactionEntry> all, CancellationToken cancellationToken)
    {
        if (attachments is null) { return; }
        var deletedIds = deleted.Select(x => x.ExternalId).ToHashSet();
        foreach (var entry in deleted.Where(x => x.InstallmentPurchaseExternalId is null))
        { await attachments.TrashOwnerAsync(new(AttachmentOwnerType.Entry, entry.ExternalId), cancellationToken); }
        foreach (var purchase in deleted.Where(x => x.InstallmentPurchaseExternalId is not null).Select(x => x.InstallmentPurchaseExternalId!.Value).Distinct())
        {
            if (!all.Any(x => x.InstallmentPurchaseExternalId == purchase && !deletedIds.Contains(x.ExternalId)))
            { await attachments.TrashOwnerAsync(new(AttachmentOwnerType.Purchase, purchase), cancellationToken); }
        }
    }
}
