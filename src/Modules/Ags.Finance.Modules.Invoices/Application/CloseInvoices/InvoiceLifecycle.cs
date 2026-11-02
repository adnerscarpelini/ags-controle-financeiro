using Ags.Finance.Modules.Accounts.Contracts;
using Ags.Finance.Modules.Invoices.Contracts;
using Ags.Finance.Modules.Invoices.Domain;
using Ags.Finance.Modules.Transactions.Contracts;
using Ags.Finance.SharedKernel;

namespace Ags.Finance.Modules.Invoices.Application.CloseInvoices;

internal sealed class InvoiceLifecycle(IInvoiceStore store, IAccountCatalog accounts, IEntryCatalog entries, IFinancialOperation operations, TimeProvider clock) : IInvoiceLifecycle
{
    public Task<InvoiceResult> CloseAsync(Guid cardExternalId, AccountingMonth month, CancellationToken cancellationToken = default)
        => operations.ExecuteAsync(token => ChangeAsync(cardExternalId, month, false, token), x => x.Succeeded, cancellationToken);
    public Task<InvoiceResult> ReopenAsync(Guid cardExternalId, AccountingMonth month, CancellationToken cancellationToken = default)
        => operations.ExecuteAsync(token => ChangeAsync(cardExternalId, month, true, token), x => x.Succeeded, cancellationToken);

    private async Task<InvoiceResult> ChangeAsync(Guid cardExternalId, AccountingMonth month, bool reopen, CancellationToken cancellationToken)
    {
        if (month.Year == 0 || !(await accounts.GetCreditCardsAsync(false, cancellationToken)).Any(x => x.ExternalId == cardExternalId))
        {
            return new("Selecione cartão e mês contábil válidos.");
        }
        var invoice = await store.FindAsync(cardExternalId, month, cancellationToken);
        if (invoice is null && reopen) { return new("Fatura não encontrada."); }
        if (!reopen && (invoice is null || invoice.Status == Domain.InvoiceStatus.Open))
        {
            var generated = await entries.EnsureRecurringMonthAsync(month, cancellationToken);
            if (!generated.Succeeded) { return new(generated.Error); }
            invoice = await store.FindAsync(cardExternalId, month, cancellationToken);
        }
        invoice ??= Invoice.Create(cardExternalId, month);
        var error = reopen ? invoice.Reopen() : invoice.Close(clock.GetUtcNow());
        if (error is not null) { return new(error); }
        await store.SaveAsync(invoice, cancellationToken);
        return new(null);
    }

    public Task<int> CloseDueAsync(CancellationToken cancellationToken = default)
        => operations.ExecuteAsync(CloseDueCoreAsync, _ => true, cancellationToken);

    private async Task<int> CloseDueCoreAsync(CancellationToken cancellationToken)
    {
        var cards = (await accounts.GetCreditCardsAsync(false, cancellationToken)).ToDictionary(x => x.ExternalId);
        var sourceEntries = await entries.GetAsync(cancellationToken);
        var invoices = (await store.GetAsync(cancellationToken)).ToList();
        var keys = invoices.Select(x => (x.CardExternalId, x.Month)).ToHashSet();
        foreach (var entry in sourceEntries.Where(x => x.CardExternalId is not null))
        {
            if (keys.Add((entry.CardExternalId!.Value, entry.Month))) { invoices.Add(Invoice.Create(entry.CardExternalId.Value, entry.Month)); }
        }
        var count = 0;
        foreach (var invoice in invoices)
        {
            if (invoice.Status != Domain.InvoiceStatus.Open || invoice.AutoCloseSuppressed) { continue; }
            var card = cards[invoice.CardExternalId];
            var cutoff = InvoiceClosingDate.Cutoff(invoice.Month, card.ClosingDay, card.DueDay, card.IncludesClosingDay, clock.LocalTimeZone);
            if (cutoff is not null && cutoff <= clock.GetUtcNow())
            {
                invoice.Close(clock.GetUtcNow());
                await store.SaveAsync(invoice, cancellationToken);
                count++;
            }
        }
        return count;
    }
}
