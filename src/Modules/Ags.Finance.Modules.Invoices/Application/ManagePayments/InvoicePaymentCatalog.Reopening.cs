using Ags.Finance.Modules.Invoices.Contracts;
using Ags.Finance.SharedKernel;

namespace Ags.Finance.Modules.Invoices.Application.ManagePayments;

internal sealed partial class InvoicePaymentCatalog
{
    public Task<InvoiceResult> ReopenUndoingPaymentsAsync(Guid cardExternalId, ReferenceMonth month, CancellationToken cancellationToken = default)
        => operations.ExecuteAsync(async token =>
        {
            if (month.Year == 0) { return new InvoiceResult("Informe um mês de fatura válido."); }
            var invoice = await store.FindAsync(cardExternalId, month, token);
            if (invoice is null) { return new InvoiceResult("Fatura não encontrada."); }
            var payments = (await store.GetPaymentsAsync(token)).Where(x => x.InvoiceExternalId == invoice.ExternalId && x.Kind == Domain.InvoicePaymentKind.Payment && x.UndoneAt is null).ToList();
            if (invoice.Status == Domain.InvoiceStatus.Open && payments.Count == 0) { return new InvoiceResult(null); }
            var error = invoice.ReopenUndoingPayments();
            if (error is not null) { return new InvoiceResult(error); }
            foreach (var payment in payments)
            {
                payment.Undo(clock.GetUtcNow());
                await store.SavePaymentAsync(payment, token);
                var removed = await movements.RemoveInvoiceDebitAsync(payment.ExternalId, token);
                if (!removed.Succeeded) { return new InvoiceResult(removed.Error); }
            }
            await store.SaveAsync(invoice, token);
            return new InvoiceResult(null);
        }, x => x.Succeeded, cancellationToken);
}
