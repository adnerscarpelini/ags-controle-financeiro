using Ags.Finance.Modules.Invoices.Domain;
using Ags.Finance.SharedKernel;

namespace Ags.Finance.Modules.Invoices.Application;

internal interface IInvoiceStore
{
    Task<IReadOnlyList<Invoice>> GetAsync(CancellationToken cancellationToken);
    Task<Invoice?> FindAsync(Guid cardExternalId, ReferenceMonth month, CancellationToken cancellationToken);
    Task SaveAsync(Invoice invoice, CancellationToken cancellationToken);
    Task<IReadOnlyList<InvoicePayment>> GetPaymentsAsync(CancellationToken cancellationToken);
    Task SavePaymentAsync(InvoicePayment payment, CancellationToken cancellationToken);
    Task DeletePaymentAsync(InvoicePayment payment, CancellationToken cancellationToken);
    Task<IReadOnlyList<CashbackReceipt>> GetCashbackReceiptsAsync(CancellationToken cancellationToken);

    /// <summary>Grava o recebimento e, no destino fatura, o abatimento gerado, numa única gravação.</summary>
    Task AddCashbackReceiptAsync(CashbackReceipt receipt, InvoicePayment? allowance, CancellationToken cancellationToken);

    Task DeleteCashbackReceiptAsync(CashbackReceipt receipt, InvoicePayment? allowance, CancellationToken cancellationToken);
    Task<IReadOnlyList<InstallmentAnticipation>> GetAnticipationsAsync(CancellationToken cancellationToken);
    Task AddAnticipationAsync(InstallmentAnticipation anticipation, CancellationToken cancellationToken);
}
