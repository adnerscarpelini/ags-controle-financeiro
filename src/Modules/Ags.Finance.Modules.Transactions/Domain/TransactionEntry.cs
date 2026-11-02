using Ags.Finance.SharedKernel;

namespace Ags.Finance.Modules.Transactions.Domain;

internal sealed class TransactionEntry : Entity
{
    public DateOnly Date { get; private set; }
    public AccountingMonth Month { get; private set; }
    public Guid MerchantExternalId { get; private set; }
    public Guid SubcategoryExternalId { get; private set; }
    public Guid AccountExternalId { get; private set; }
    public Guid? CardExternalId { get; private set; }
    public Guid? CardLastDigitsExternalId { get; private set; }
    public CategoryKind Kind { get; private set; }
    public Money Amount { get; private set; }
    public string Details { get; private set; } = string.Empty;
    public bool IsEffective { get; private set; }
    public DateOnly? EffectiveDate { get; private set; }
    public DateTimeOffset? EffectiveAt { get; private set; }

    public string? Update(DateOnly date, AccountingMonth month, Guid merchantExternalId, Guid subcategoryExternalId, Guid accountExternalId, CategoryKind kind, Money amount, string details)
    {
        if (date == DateOnly.MinValue || month.Year == 0 || merchantExternalId == Guid.Empty || subcategoryExternalId == Guid.Empty || accountExternalId == Guid.Empty)
        {
            return "Informe data, mês contábil, descrição, subcategoria e conta válidos.";
        }
        if (amount <= Money.Zero || amount.Amount > 92233720368547758.07m || kind is not (CategoryKind.Income or CategoryKind.Expense))
        {
            return "Informe valor positivo e classificação de receita ou despesa. Transferências usam uma operação própria.";
        }
        if (details.Length > 1000)
        {
            return "Os detalhes devem ter até 1000 caracteres.";
        }
        Date = date;
        Month = month;
        MerchantExternalId = merchantExternalId;
        SubcategoryExternalId = subcategoryExternalId;
        AccountExternalId = accountExternalId;
        Kind = kind;
        Amount = kind == CategoryKind.Expense ? -amount : amount;
        Details = details.Trim();
        return null;
    }

    public void SetEffective(DateOnly date, DateTimeOffset confirmedAt)
    {
        IsEffective = true;
        EffectiveDate = date;
        EffectiveAt = confirmedAt;
    }

    public void ReplaceMerchant(Guid merchantExternalId) => MerchantExternalId = merchantExternalId;

    public void SetCard(Guid cardExternalId, Guid? cardLastDigitsExternalId)
    {
        CardExternalId = cardExternalId;
        CardLastDigitsExternalId = cardLastDigitsExternalId;
    }
}
