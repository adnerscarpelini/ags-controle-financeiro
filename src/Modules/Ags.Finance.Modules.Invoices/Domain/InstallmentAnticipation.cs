using Ags.Finance.SharedKernel;

namespace Ags.Finance.Modules.Invoices.Domain;

internal sealed class InstallmentAnticipation : Entity
{
    private InstallmentAnticipation() { }
    public Guid ConfirmationId { get; private set; }
    public Guid EntryExternalId { get; private set; }
    public Guid PurchaseExternalId { get; private set; }
    public Guid CardExternalId { get; private set; }
    public AccountingMonth Month { get; private set; }
    public Guid[] EntryExternalIds { get; private set; } = [];
    public void SetEntries(IEnumerable<Guid> entryExternalIds) => EntryExternalIds = entryExternalIds.ToArray();
    public int Count { get; private set; }
    public Money Total { get; private set; }
    public Money Discount { get; private set; }
    public static InstallmentAnticipation? Create(Guid confirmationId, Guid entryId, Guid purchaseId, Guid cardId, AccountingMonth month, int count, Money total, Money discount)
    {
        if (confirmationId == Guid.Empty || entryId == Guid.Empty || purchaseId == Guid.Empty || cardId == Guid.Empty || month.Year == 0
            || count is < 1 or > 48 || total.IsNegative || discount.IsNegative || (!discount.IsZero && discount >= total)) { return null; }
        return new() { ConfirmationId = confirmationId, EntryExternalId = entryId, PurchaseExternalId = purchaseId, CardExternalId = cardId, Month = month, Count = count, Total = total, Discount = discount };
    }
}
