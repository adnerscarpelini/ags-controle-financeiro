using Ags.Finance.Modules.Invoices.Contracts;
using Ags.Finance.Modules.Transactions.Contracts;

namespace Ags.Finance.Modules.Invoices.Application.AdjustInvoiceTotal;

internal sealed class InvoiceAdjustmentCatalog(IEntryCatalog entries) : IInvoiceAdjustmentCatalog
{
    public async Task<InvoiceResult> AdjustTotalAsync(AdjustInvoiceTotalRequest request, CancellationToken cancellationToken = default)
    {
        var result = await entries.AdjustInvoiceAsync(new(request.ConfirmationId, request.CardExternalId, request.Month, request.CorrectTotal, request.ExpectedTotal), cancellationToken);
        return new(result.Error);
    }
}
