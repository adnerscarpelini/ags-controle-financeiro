using Ags.Finance.Modules.Invoices.Contracts;
using Ags.Finance.Modules.Transactions.Contracts;

namespace Ags.Finance.Modules.Invoices.Application.AdjustInvoiceTotal;

internal sealed class InvoiceAdjustmentCatalog(IEntryCatalog entries, IInvoiceStore store, TimeProvider clock) : IInvoiceAdjustmentCatalog
{
    public async Task<InvoiceResult> AdjustTotalAsync(AdjustInvoiceTotalRequest request, CancellationToken cancellationToken = default)
    {
        // Em fatura Aberta o total informado vem do banco, que ainda não cobrou as recorrentes futuras: compara só com o lançado até hoje.
        var invoice = await store.FindAsync(request.CardExternalId, request.Month, cancellationToken);
        DateOnly? cutoff = (invoice?.Status ?? Domain.InvoiceStatus.Open) == Domain.InvoiceStatus.Open ? DateOnly.FromDateTime(clock.GetLocalNow().DateTime) : null;
        var result = await entries.AdjustInvoiceAsync(new(request.ConfirmationId, request.CardExternalId, request.Month, request.CorrectTotal, request.ExpectedTotal, cutoff), cancellationToken);
        return new(result.Error);
    }
}
