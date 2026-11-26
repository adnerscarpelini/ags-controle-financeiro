using Ags.Finance.SharedKernel;

namespace Ags.Finance.Modules.Invoices.Domain;

internal enum CashbackDestination { None, Account, Invoice }

/// <summary>
/// Recebimento do cashback de uma fatura Paga. A situação Prevista/A receber é derivada na leitura; só o recebimento é gravado,
/// com o previsto no momento da confirmação e o valor que o banco de fato devolveu.
/// </summary>
internal sealed class CashbackReceipt : Entity
{
    public Guid ConfirmationId { get; private set; }
    public Guid InvoiceExternalId { get; private set; }
    public DateOnly Date { get; private set; }
    public Money Expected { get; private set; }
    public Money Received { get; private set; }
    public CashbackDestination Destination { get; private set; }
    public Guid? AccountExternalId { get; private set; }
    public Guid? IncomeEntryExternalId { get; private set; }
    public Guid? TargetInvoiceExternalId { get; private set; }

    public static (CashbackReceipt? Receipt, string? Error) Create(Guid confirmationId, Guid invoiceExternalId, DateOnly date, Money expected, Money received, CashbackDestination destination)
    {
        if (confirmationId == Guid.Empty || invoiceExternalId == Guid.Empty || date == DateOnly.MinValue) { return (null, "Informe a data do recebimento."); }
        if (received.IsNegative || received.Amount > 92233720368547758.07m) { return (null, "O valor recebido não pode ser negativo."); }
        if (expected.IsNegative) { return (null, "O cashback previsto não pode ser negativo."); }
        if (!Enum.IsDefined(destination)) { return (null, "Escolha onde o cashback foi recebido."); }
        if (received.IsZero) { destination = CashbackDestination.None; }
        else if (destination == CashbackDestination.None) { return (null, "Escolha onde o cashback foi recebido: conta corrente ou abatimento na fatura."); }
        return (new CashbackReceipt
        {
            ConfirmationId = confirmationId,
            InvoiceExternalId = invoiceExternalId,
            Date = date,
            Expected = expected,
            Received = received,
            Destination = destination
        }, null);
    }

    public void CreditAccount(Guid accountExternalId, Guid incomeEntryExternalId)
    {
        AccountExternalId = accountExternalId;
        IncomeEntryExternalId = incomeEntryExternalId;
    }

    public void CreditInvoice(Guid targetInvoiceExternalId) => TargetInvoiceExternalId = targetInvoiceExternalId;
}
