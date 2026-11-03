using Ags.Finance.Modules.Accounts.Contracts;
using Ags.Finance.Modules.Invoices.Contracts;
using Ags.Finance.Modules.Invoices.Domain;
using Ags.Finance.Modules.Transactions.Contracts;

namespace Ags.Finance.Modules.Invoices.Application.ManagePayments;

internal sealed partial class InvoicePaymentCatalog(IInvoiceStore store, IAccountCatalog accounts, IEntryCatalog entries, IAccountMovementCatalog movements, TimeProvider clock, IFinancialOperation operations) : IInvoicePaymentCatalog
{
    public Task<InvoicePaymentResult> SaveAllowanceAsync(Guid? externalId, AllowanceRequest request, CancellationToken cancellationToken = default)
        => operations.ExecuteAsync(token => SaveAllowanceCoreAsync(externalId, request, token), x => x.Succeeded, cancellationToken);

    private async Task<InvoicePaymentResult> SaveAllowanceCoreAsync(Guid? externalId, AllowanceRequest request, CancellationToken cancellationToken)
    {
        if (request.Month.Year == 0) { return new(null, "Informe um mês de fatura válido."); }
        var history = await store.GetPaymentsAsync(cancellationToken);
        var original = externalId is { } id ? history.FirstOrDefault(x => x.ExternalId == id) : null;
        if (externalId is not null && original is null) { return new(null, "Abatimento não encontrado."); }
        if (original?.Kind == Domain.InvoicePaymentKind.Payment) { return new(null, "Pagamentos confirmados são irreversíveis."); }
        var invoices = await store.GetAsync(cancellationToken);
        var source = original is null ? null : invoices.Single(x => x.ExternalId == original.InvoiceExternalId);
        if (source?.Status == Domain.InvoiceStatus.Paid) { return new(null, "Fatura Paga não permite alterar abatimentos."); }
        var card = (await accounts.GetCreditCardsAsync(false, cancellationToken)).FirstOrDefault(x => x.ExternalId == request.CardExternalId);
        var available = (await accounts.GetCreditCardsAsync(true, cancellationToken)).Any(x => x.ExternalId == request.CardExternalId);
        if (card is null || (!available && source?.CardExternalId != card.ExternalId)) { return new(null, "Selecione um cartão disponível cadastrado."); }
        var invoice = invoices.FirstOrDefault(x => x.CardExternalId == request.CardExternalId && x.Month == request.Month) ?? Invoice.Create(request.CardExternalId, request.Month);
        if (invoice.Status == Domain.InvoiceStatus.Paid) { return new(null, "Fatura Paga não permite criar ou mover abatimentos."); }
        if (externalId is null && history.FirstOrDefault(x => x.ConfirmationId == request.ConfirmationId) is { } previous)
        {
            var same = previous.InvoiceExternalId == invoice.ExternalId && previous.Date == request.Date && previous.Amount == request.Amount
                && (Contracts.InvoicePaymentKind)previous.Kind == request.Kind && previous.Details == request.Details.Trim();
            return same ? new(previous.ExternalId, null) : new(null, "Esta confirmação já foi utilizada.");
        }
        if (original is not null && original.ConfirmationId != request.ConfirmationId) { return new(null, "A identificação do abatimento não pode ser substituída."); }
        var payment = original ?? new InvoicePayment();
        var error = payment.SetAllowance(request.ConfirmationId, invoice.ExternalId, request.Date, request.Amount, (Domain.InvoicePaymentKind)request.Kind, request.Details);
        if (error is not null) { return new(null, error); }
        await store.SaveAsync(invoice, cancellationToken);
        await store.SavePaymentAsync(payment, cancellationToken);
        return new(payment.ExternalId, null);
    }

    public Task<InvoiceResult> DeleteAllowanceAsync(Guid externalId, CancellationToken cancellationToken = default)
        => operations.ExecuteAsync(async token =>
        {
            var payment = (await store.GetPaymentsAsync(token)).FirstOrDefault(x => x.ExternalId == externalId);
            if (payment is null) { return new InvoiceResult("Abatimento não encontrado."); }
            if (payment.Kind == Domain.InvoicePaymentKind.Payment) { return new InvoiceResult("Pagamentos confirmados são irreversíveis."); }
            var invoice = (await store.GetAsync(token)).Single(x => x.ExternalId == payment.InvoiceExternalId);
            if (invoice.Status == Domain.InvoiceStatus.Paid) { return new InvoiceResult("Fatura Paga não permite excluir abatimentos."); }
            await store.DeletePaymentAsync(payment, token);
            return new InvoiceResult(null);
        }, x => x.Succeeded, cancellationToken);

    public Task<IReadOnlyList<InvoicePaymentDetails>> GetHistoryAsync(Guid? cardExternalId = null, int? year = null, Contracts.InvoicePaymentKind? kind = null, CancellationToken cancellationToken = default)
        => operations.ExecuteAsync<IReadOnlyList<InvoicePaymentDetails>>(async token =>
        {
            var invoices = (await store.GetAsync(token)).ToDictionary(x => x.ExternalId);
            return (await store.GetPaymentsAsync(token)).Where(x => (cardExternalId is null || invoices[x.InvoiceExternalId].CardExternalId == cardExternalId)
                && (year is null || x.Date.Year == year) && (kind is null || (Contracts.InvoicePaymentKind)x.Kind == kind))
                .OrderByDescending(x => x.Date).ThenByDescending(x => x.Id).Select(x => new InvoicePaymentDetails(x.ExternalId, x.ConfirmationId,
                    invoices[x.InvoiceExternalId].CardExternalId, invoices[x.InvoiceExternalId].Month, x.Date, x.Amount, (Contracts.InvoicePaymentKind)x.Kind,
                    x.AccountExternalId, x.Details, x.CreatedAt)).ToList();
        }, _ => true, cancellationToken);
}
