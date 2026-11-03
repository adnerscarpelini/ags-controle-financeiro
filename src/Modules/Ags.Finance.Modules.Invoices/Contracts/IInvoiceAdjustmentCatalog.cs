using Ags.Finance.SharedKernel;

namespace Ags.Finance.Modules.Invoices.Contracts;

public sealed record AdjustInvoiceTotalRequest(Guid ConfirmationId, Guid CardExternalId, ReferenceMonth Month, Money CorrectTotal, Money? ExpectedTotal = null);
public interface IInvoiceAdjustmentCatalog
{
    Task<InvoiceResult> AdjustTotalAsync(AdjustInvoiceTotalRequest request, CancellationToken cancellationToken = default);
}
