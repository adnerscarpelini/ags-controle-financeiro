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
    public DateTimeOffset? UndoneAt { get; private set; }
    public void Undo(DateTimeOffset now) { if (Kind == InvoicePaymentKind.Payment) { UndoneAt ??= now; } }
    public string Details { get; private set; } = string.Empty;

    /// <summary>Recebimento de cashback que gerou este abatimento; só é removido ao desfazer o recebimento.</summary>
    public Guid? CashbackReceiptExternalId { get; private set; }

    public static InvoicePayment FromCashbackReceipt(Guid receiptExternalId, Guid targetInvoiceExternalId, DateOnly date, Money amount, string details) => new()
    {
        ConfirmationId = Guid.CreateVersion7(),
        InvoiceExternalId = targetInvoiceExternalId,
        Date = date,
        Amount = amount,
        Kind = InvoicePaymentKind.Cashback,
        Details = details,
        CashbackReceiptExternalId = receiptExternalId
    };

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
        if (Kind == InvoicePaymentKind.Payment) { return "Pagamentos são desfeitos pela reabertura da fatura."; }
        if (Kind == InvoicePaymentKind.Cashback && Id != 0) { return "Cashback é confirmado no painel de Cashback e não pode ser alterado aqui."; }
        if (kind == InvoicePaymentKind.Cashback) { return "Cashback é confirmado no painel de Cashback, depois que a fatura estiver Paga."; }
        if (confirmationId == Guid.Empty || invoiceExternalId == Guid.Empty || date == DateOnly.MinValue || amount.Amount <= 0
            || amount.Amount > 92233720368547758.07m || kind != InvoicePaymentKind.Discount || details.Length > 1000)
        {
            return "Informe confirmação, fatura, data, valor positivo e desconto válidos. Detalhes: até 1000 caracteres.";
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
