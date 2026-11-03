using Ags.Finance.Modules.Accounts.Contracts;
using Ags.Finance.Modules.Invoices.Domain;
using Ags.Finance.Modules.Transactions.Contracts;
using Ags.Finance.SharedKernel;

namespace Ags.Finance.Modules.Invoices.Application.CloseInvoices;

internal sealed class InvoiceWriteGuard(IInvoiceStore store, IAccountCatalog accounts, TimeProvider clock) : IInvoiceWriteGuard
{
    public async Task<bool> IsAnticipatedAsync(Guid entryExternalId, CancellationToken cancellationToken = default)
        => (await store.GetAnticipationsAsync(cancellationToken)).Any(x => x.EntryExternalIds.Contains(entryExternalId));

    public async Task<string?> RequireAdjustmentAsync(Guid cardExternalId, AccountingMonth month, CancellationToken cancellationToken = default)
        => (await store.FindAsync(cardExternalId, month, cancellationToken))?.Status == InvoiceStatus.Paid ? "Fatura Paga não permite ajuste de total." : null;
    public Task<string?> RequireOpenAsync(Guid cardExternalId, AccountingMonth month, CancellationToken cancellationToken = default)
        => RequireCoreAsync(cardExternalId, month, null, true, cancellationToken);

    public Task<string?> RequireRecurringAsync(Guid cardExternalId, AccountingMonth month, DateTimeOffset plannedAt, CancellationToken cancellationToken = default)
        => RequireCoreAsync(cardExternalId, month, plannedAt, true, cancellationToken);

    public Task<string?> CheckRecurringAsync(Guid cardExternalId, AccountingMonth month, DateTimeOffset plannedAt, CancellationToken cancellationToken = default)
        => RequireCoreAsync(cardExternalId, month, plannedAt, false, cancellationToken);

    private async Task<string?> RequireCoreAsync(Guid cardExternalId, AccountingMonth month, DateTimeOffset? plannedAt, bool registerInvoice, CancellationToken cancellationToken)
    {
        var card = (await accounts.GetCreditCardsAsync(false, cancellationToken)).FirstOrDefault(x => x.ExternalId == cardExternalId);
        if (card is null) { return "Cartão não encontrado."; }
        var invoice = await store.FindAsync(cardExternalId, month, cancellationToken);
        var cutoff = InvoiceClosingDate.Cutoff(month, card.ClosingDay, card.DueDay, card.IncludesClosingDay, clock.LocalTimeZone);
        if (invoice?.Status is InvoiceStatus.Closed or InvoiceStatus.Paid || (invoice?.AutoCloseSuppressed != true && cutoff <= clock.GetUtcNow() && !(plannedAt < cutoff)))
        {
            return $"A fatura {month} está protegida. Escolha uma fatura Aberta ou reabra manualmente uma Fechada sem pagamentos.";
        }
        if (registerInvoice && invoice is null) { await store.SaveAsync(Invoice.Create(cardExternalId, month), cancellationToken); }
        return null;
    }
}
