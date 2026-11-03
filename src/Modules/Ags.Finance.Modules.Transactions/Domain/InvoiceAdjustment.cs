using Ags.Finance.SharedKernel;

namespace Ags.Finance.Modules.Transactions.Domain;

internal sealed class InvoiceAdjustment : Entity
{
    private InvoiceAdjustment() { }
    public Guid ConfirmationId { get; private set; }
    public Guid CardExternalId { get; private set; }
    public Guid AccountExternalId { get; private set; }
    public AccountingMonth Month { get; private set; }
    public DateOnly Date { get; private set; }
    public Money ExpenseDifference { get; private set; }
    public Money CorrectTotal { get; private set; }
    public static InvoiceAdjustment? Create(Guid confirmationId, Guid cardId, Guid accountId, AccountingMonth month, DateOnly date, Money difference, Money correctTotal)
    {
        if (confirmationId == Guid.Empty || cardId == Guid.Empty || accountId == Guid.Empty || month.Year == 0 || date == DateOnly.MinValue
            || difference.IsZero || Math.Abs(difference.Amount) > 92233720368547758.07m || Math.Abs(correctTotal.Amount) > 92233720368547758.07m) { return null; }
        return new() { ConfirmationId = confirmationId, CardExternalId = cardId, AccountExternalId = accountId, Month = month, Date = date, ExpenseDifference = difference, CorrectTotal = correctTotal };
    }
}
