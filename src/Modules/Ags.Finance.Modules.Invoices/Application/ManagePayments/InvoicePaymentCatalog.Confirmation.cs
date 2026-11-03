using Ags.Finance.Modules.Invoices.Contracts;
using Ags.Finance.Modules.Invoices.Domain;
using Ags.Finance.SharedKernel;

namespace Ags.Finance.Modules.Invoices.Application.ManagePayments;

internal sealed partial class InvoicePaymentCatalog
{
    public Task<InvoicePaymentResult> ConfirmPaymentAsync(ConfirmInvoicePaymentRequest request, CancellationToken cancellationToken = default)
        => operations.ExecuteAsync(token => ConfirmPaymentCoreAsync(request, token), x => x.Succeeded, cancellationToken);

    private async Task<InvoicePaymentResult> ConfirmPaymentCoreAsync(ConfirmInvoicePaymentRequest request, CancellationToken cancellationToken)
    {
        if (request.Month.Year == 0) { return new(null, "Informe um mês de fatura válido."); }
        var history = await store.GetPaymentsAsync(cancellationToken);
        if (history.FirstOrDefault(x => x.ConfirmationId == request.ConfirmationId) is { } previous)
        {
            var source = (await store.GetAsync(cancellationToken)).Single(x => x.ExternalId == previous.InvoiceExternalId);
            var same = previous.Kind == Domain.InvoicePaymentKind.Payment && source.CardExternalId == request.CardExternalId && source.Month == request.Month
                && previous.AccountExternalId == request.AccountExternalId && previous.Date == request.Date && previous.Amount == request.Amount && previous.Details == request.Details.Trim();
            return same ? new(previous.ExternalId, null) : new(null, "Esta confirmação já foi utilizada em outra operação.");
        }
        if (!(await accounts.GetCreditCardsAsync(false, cancellationToken)).Any(x => x.ExternalId == request.CardExternalId)) { return new(null, "Cartão não encontrado."); }
        var generation = await entries.EnsureRecurringMonthAsync(request.Month, cancellationToken);
        if (!generation.Succeeded) { return new(null, generation.Error); }
        var invoice = await store.FindAsync(request.CardExternalId, request.Month, cancellationToken) ?? Invoice.Create(request.CardExternalId, request.Month);
        if (invoice.Status == Domain.InvoiceStatus.Paid) { return new(null, "Fatura Paga não permite outro pagamento."); }
        var payment = InvoicePayment.Confirm(request.ConfirmationId, invoice.ExternalId, request.AccountExternalId, request.Date, request.Amount, request.Details);
        if (payment is null) { return new(null, "Informe confirmação, conta, data, valor positivo e detalhes válidos."); }
        var remaining = await RemainingAsync(invoice, cancellationToken) - request.Amount;
        if (request.ConfirmSettlement)
        {
            var error = invoice.Settle(remaining, clock.GetUtcNow());
            if (error is not null) { return new(null, error); }
        }
        invoice.RecordConfirmedPayment();
        await store.SaveAsync(invoice, cancellationToken);
        var debit = await movements.RecordInvoiceDebitAsync(new(payment.ExternalId, request.AccountExternalId, request.Date, request.Amount), cancellationToken);
        if (!debit.Succeeded) { return new(null, debit.Error); }
        await store.SavePaymentAsync(payment, cancellationToken);
        return new(payment.ExternalId, null);
    }

    public Task<InvoiceResult> ConfirmSettlementAsync(Guid cardExternalId, AccountingMonth month, CancellationToken cancellationToken = default)
        => operations.ExecuteAsync(async token =>
        {
            if (month.Year == 0) { return new InvoiceResult("Informe um mês válido."); }
            var invoice = await store.FindAsync(cardExternalId, month, token);
            if (invoice is null) { return new InvoiceResult("Fatura não encontrada."); }
            var error = invoice.Settle(await RemainingAsync(invoice, token), clock.GetUtcNow());
            if (error is not null) { return new InvoiceResult(error); }
            await store.SaveAsync(invoice, token);
            return new InvoiceResult(null);
        }, x => x.Succeeded, cancellationToken);

    private async Task<Money> RemainingAsync(Invoice invoice, CancellationToken cancellationToken)
    {
        var total = -(await entries.GetAsync(cancellationToken)).Where(x => x.CardExternalId == invoice.CardExternalId && x.Month == invoice.Month).Sum(x => x.Amount.Amount);
        var allowances = (await store.GetPaymentsAsync(cancellationToken)).Where(x => x.InvoiceExternalId == invoice.ExternalId).Sum(x => x.Amount.Amount);
        return new Money(total - allowances);
    }
}
