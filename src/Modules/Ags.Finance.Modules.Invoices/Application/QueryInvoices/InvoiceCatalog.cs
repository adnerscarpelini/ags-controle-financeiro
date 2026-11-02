using Ags.Finance.Modules.Accounts.Contracts;
using Ags.Finance.Modules.Invoices.Contracts;
using Ags.Finance.Modules.Transactions.Contracts;
using Ags.Finance.SharedKernel;

namespace Ags.Finance.Modules.Invoices.Application.QueryInvoices;

internal sealed class InvoiceCatalog(IEntryCatalog entries, IAccountCatalog accounts) : IInvoiceCatalog
{
    public async Task<IReadOnlyList<InvoiceDetails>> GetAsync(CancellationToken cancellationToken = default)
    {
        var cards = (await accounts.GetCreditCardsAsync(false, cancellationToken)).ToDictionary(x => x.ExternalId);
        return (await entries.GetAsync(cancellationToken)).Where(x => x.CardExternalId is not null)
            .GroupBy(x => (CardExternalId: x.CardExternalId!.Value, x.Month))
            .OrderByDescending(x => x.Key.Month).ThenBy(x => cards[x.Key.CardExternalId].Name, StringComparer.CurrentCulture)
            .Select(x => new InvoiceDetails(x.Key.CardExternalId, cards[x.Key.CardExternalId].Name, x.Key.Month, new Money(-x.Sum(e => e.Amount.Amount)), x.ToList())).ToList();
    }
}
