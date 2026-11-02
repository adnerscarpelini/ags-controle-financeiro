using Ags.Finance.Modules.Accounts.Contracts;
using Ags.Finance.Modules.Invoices.Domain;
using Ags.Finance.Modules.Transactions.Contracts;
using Ags.Finance.SharedKernel;

namespace Ags.Finance.Modules.Invoices.Application.CloseInvoices;

internal sealed class InvoiceWriteGuard(IInvoiceStore store, IAccountCatalog accounts, TimeProvider clock) : IInvoiceWriteGuard
{
    public Task<string?> RequireOpenAsync(Guid cardExternalId, AccountingMonth month, CancellationToken cancellationToken = default)
        => RequireCoreAsync(cardExternalId, month, null, cancellationToken);

    public Task<string?> RequireRecurringAsync(Guid cardExternalId, AccountingMonth month, DateTimeOffset plannedAt, CancellationToken cancellationToken = default)
        => RequireCoreAsync(cardExternalId, month, plannedAt, cancellationToken);

    private async Task<string?> RequireCoreAsync(Guid cardExternalId, AccountingMonth month, DateTimeOffset? plannedAt, CancellationToken cancellationToken)
    {
        var card = (await accounts.GetCreditCardsAsync(false, cancellationToken)).FirstOrDefault(x => x.ExternalId == cardExternalId);
        if (card is null) { return "Cartão não encontrado."; }
        var invoice = await store.FindAsync(cardExternalId, month, cancellationToken);
        var cutoff = InvoiceClosingDate.Cutoff(month, card.ClosingDay, card.DueDay, card.IncludesClosingDay, clock.LocalTimeZone);
        if (invoice?.Status is InvoiceStatus.Closed or InvoiceStatus.Paid || (invoice?.AutoCloseSuppressed != true && cutoff <= clock.GetUtcNow() && !(plannedAt < cutoff)))
        {
            return $"A fatura {month} está protegida. Escolha uma fatura Aberta ou reabra manualmente uma Fechada sem pagamentos.";
        }
        if (invoice is null) { await store.SaveAsync(Invoice.Create(cardExternalId, month), cancellationToken); }
        return null;
    }
}
