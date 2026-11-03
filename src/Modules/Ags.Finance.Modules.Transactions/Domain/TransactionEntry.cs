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
    public Guid? InstallmentPurchaseExternalId { get; private set; }
    public int? InstallmentNumber { get; private set; }
    public Guid? RecurringEntryExternalId { get; private set; }
    public DateOnly? OccurrenceMonth { get; private set; }
    public CategoryKind Kind { get; private set; }
    public Money Amount { get; private set; }
    public string Details { get; private set; } = string.Empty;
    public string? NormalizedDetails { get; private set; }
    public bool IsEffective { get; private set; }
    public DateOnly? EffectiveDate { get; private set; }
    public DateTimeOffset? EffectiveAt { get; private set; }
    public Guid? ReimbursementPersonExternalId { get; private set; }
    public decimal? ReimbursementPercent { get; private set; }

    public string? Update(DateOnly date, AccountingMonth month, Guid merchantExternalId, Guid subcategoryExternalId, Guid accountExternalId, CategoryKind kind, Money amount, string details, bool allowZeroAmount = false)
    {
        if (date == DateOnly.MinValue || month.Year == 0 || merchantExternalId == Guid.Empty || subcategoryExternalId == Guid.Empty || accountExternalId == Guid.Empty)
        {
            return "Informe data, mês contábil, descrição, subcategoria e conta válidos.";
        }
        if (amount.IsNegative || (amount.IsZero && !allowZeroAmount) || amount.Amount > 92233720368547758.07m || kind is not (CategoryKind.Income or CategoryKind.Expense))
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
        NormalizedDetails = SearchText.Normalize(Details);
        return null;
    }

    public string? SetReimbursement(Guid? personExternalId, decimal? percent)
    {
        if (personExternalId is not null || percent is not null)
        {
            if (Kind != CategoryKind.Expense) { return "O reembolso só pode ser indicado em despesas."; }
            if (personExternalId is null || personExternalId == Guid.Empty || percent is not { } value) { return "Informe a pessoa e o percentual do reembolso."; }
            if (value is < 0 or > 100 || decimal.Round(value, 2) != value) { return "Informe um percentual de reembolso de 0 a 100, com até duas casas decimais."; }
        }
        ReimbursementPersonExternalId = personExternalId;
        ReimbursementPercent = percent;
        return null;
    }

    public void SetEffective(DateOnly date, DateTimeOffset confirmedAt)
    {
        IsEffective = true;
        EffectiveDate = date;
        EffectiveAt = confirmedAt;
    }

    public void SetPending()
    {
        IsEffective = false;
        EffectiveDate = null;
        EffectiveAt = null;
    }

    public void SetRecurring(Guid recurringEntryExternalId, DateOnly occurrenceMonth)
    {
        RecurringEntryExternalId = recurringEntryExternalId;
        OccurrenceMonth = occurrenceMonth;
    }

    public void ReplaceMerchant(Guid merchantExternalId) => MerchantExternalId = merchantExternalId;

    public void ApplyChange(TransactionEntry prepared, bool keepAmount)
    {
        var dateChanged = Date != prepared.Date;
        Date = prepared.Date;
        Month = prepared.Month;
        MerchantExternalId = prepared.MerchantExternalId;
        SubcategoryExternalId = prepared.SubcategoryExternalId;
        AccountExternalId = prepared.AccountExternalId;
        CardExternalId = prepared.CardExternalId;
        CardLastDigitsExternalId = prepared.CardLastDigitsExternalId;
        Kind = prepared.Kind;
        if (!keepAmount) { Amount = prepared.Amount; }
        Details = prepared.Details;
        NormalizedDetails = prepared.NormalizedDetails;
        ReimbursementPersonExternalId = prepared.ReimbursementPersonExternalId;
        ReimbursementPercent = prepared.ReimbursementPercent;
        if (IsEffective && dateChanged) { EffectiveDate = prepared.Date; }
    }

    public void SetCard(Guid cardExternalId, Guid? cardLastDigitsExternalId)
    {
        CardExternalId = cardExternalId;
        CardLastDigitsExternalId = cardLastDigitsExternalId;
    }

    public void SetInstallment(Guid purchaseExternalId, int number, Money part)
    {
        InstallmentPurchaseExternalId = purchaseExternalId;
        InstallmentNumber = number;
        Amount = -part;
    }
}
