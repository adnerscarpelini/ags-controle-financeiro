using Ags.Finance.SharedKernel;

namespace Ags.Finance.Modules.Invoices.Domain;

internal enum InvoicePaymentKind { Payment, Cashback, Discount }

internal sealed class InvoicePayment : Entity
{
    public Guid ConfirmationId { get; private set; }
    public Guid InvoiceExternalId { get; private set; }
    public DateOnly Date { get; private set; }
    public Money Amount { get; private set; }
    public InvoicePaymentKind Kind { get; private set; } = InvoicePaymentKind.Cashback;
    public Guid? AccountExternalId { get; private set; }
    public string Details { get; private set; } = string.Empty;

    public static InvoicePayment? Confirm(Guid confirmationId, Guid invoiceExternalId, Guid accountExternalId, DateOnly date, Money amount, string details)
    {
        if (confirmationId == Guid.Empty || invoiceExternalId == Guid.Empty || accountExternalId == Guid.Empty || date == DateOnly.MinValue
            || amount.Amount <= 0 || amount.Amount > 92233720368547758.07m || details.Length > 1000) { return null; }
        return new()
        {
            ConfirmationId = confirmationId,
            InvoiceExternalId = invoiceExternalId,
            AccountExternalId = accountExternalId,
            Date = date,
            Amount = amount,
            Kind = InvoicePaymentKind.Payment,
            Details = details.Trim()
        };
    }

    public string? SetAllowance(Guid confirmationId, Guid invoiceExternalId, DateOnly date, Money amount, InvoicePaymentKind kind, string details)
    {
        if (Kind == InvoicePaymentKind.Payment) { return "Pagamentos confirmados são irreversíveis."; }
        if (confirmationId == Guid.Empty || invoiceExternalId == Guid.Empty || date == DateOnly.MinValue || amount.Amount <= 0
            || amount.Amount > 92233720368547758.07m || kind is not (InvoicePaymentKind.Cashback or InvoicePaymentKind.Discount) || details.Length > 1000)
        {
            return "Informe confirmação, fatura, data, valor positivo e tipo de abatimento válidos. Detalhes: até 1000 caracteres.";
        }
        ConfirmationId = confirmationId;
        InvoiceExternalId = invoiceExternalId;
        Date = date;
        Amount = amount;
        Kind = kind;
        Details = details.Trim();
        return null;
    }
}
