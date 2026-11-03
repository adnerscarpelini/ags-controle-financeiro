using Ags.Finance.SharedKernel;

namespace Ags.Finance.Modules.Reimbursements.Domain;

/// <summary>Reembolso recebido de uma pessoa, ligado à receita criada em Transactions (<see cref="EntryExternalId"/>).</summary>
internal sealed class ReimbursementReceipt : Entity
{
    private ReimbursementReceipt() { }

    public Guid PersonExternalId { get; private set; }
    public Guid EntryExternalId { get; private set; }
    public Guid AccountExternalId { get; private set; }
    public DateOnly Date { get; private set; }
    public AccountingMonth Month { get; private set; }
    public Money Amount { get; private set; }

    public static ReimbursementReceipt? Create(Guid personExternalId, Guid entryExternalId, Guid accountExternalId, DateOnly date, AccountingMonth month, Money amount)
        => personExternalId == Guid.Empty || entryExternalId == Guid.Empty || accountExternalId == Guid.Empty || month.Year == 0 || amount.Amount <= 0
            ? null
            : new() { PersonExternalId = personExternalId, EntryExternalId = entryExternalId, AccountExternalId = accountExternalId, Date = date, Month = month, Amount = amount };
}
