using Ags.Finance.SharedKernel;

namespace Ags.Finance.Modules.Transactions.Domain;

internal sealed class InstallmentPurchase : Entity
{
    private InstallmentPurchase() { }
    public Guid CardExternalId { get; private set; }
    public ReferenceMonth FirstMonth { get; private set; }
    public Money Total { get; private set; }
    public int Count { get; private set; }

    public static InstallmentPurchase? Create(Guid cardExternalId, ReferenceMonth firstMonth, Money total, int count)
        => cardExternalId == Guid.Empty || firstMonth.Year == 0 || total <= Money.Zero || count is < 2 or > 48
            ? null : new() { CardExternalId = cardExternalId, FirstMonth = firstMonth, Total = total, Count = count };
}
