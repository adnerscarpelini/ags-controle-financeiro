using Ags.Finance.SharedKernel;

namespace Ags.Finance.Modules.Invoices.Domain;

internal enum InvoiceStatus { Open, Closed, Paid }

internal sealed class Invoice : Entity
{
    public Guid CardExternalId { get; private set; }
    public AccountingMonth Month { get; private set; }
    public InvoiceStatus Status { get; private set; }
    public bool AutoCloseSuppressed { get; private set; }
    public bool HasConfirmedPayment { get; private set; }
    public DateTimeOffset? ClosedAt { get; private set; }
    public DateTimeOffset? PaidAt { get; private set; }

    public void RecordConfirmedPayment() => HasConfirmedPayment = true;

    public string? Settle(Money remainingBalance, DateTimeOffset now)
    {
        if (Status == InvoiceStatus.Paid) { return null; }
        if (Status != InvoiceStatus.Closed || remainingBalance.Amount > 0) { return "A quitação exige fatura Fechada com saldo zero ou crédito."; }
        Status = InvoiceStatus.Paid;
        PaidAt = now;
        return null;
    }

    public static Invoice Create(Guid cardExternalId, AccountingMonth month) => new() { CardExternalId = cardExternalId, Month = month };

    public string? Close(DateTimeOffset now)
    {
        if (Status == InvoiceStatus.Paid) { return "Fatura Paga não pode ser alterada."; }
        if (Status == InvoiceStatus.Closed) { return null; }
        Status = InvoiceStatus.Closed;
        AutoCloseSuppressed = false;
        ClosedAt = now;
        return null;
    }

    public string? Reopen()
    {
        if (Status != InvoiceStatus.Closed || HasConfirmedPayment)
        {
            return "Somente fatura Fechada sem pagamento confirmado pode ser reaberta.";
        }
        Status = InvoiceStatus.Open;
        AutoCloseSuppressed = true;
        ClosedAt = null;
        return null;
    }
}
