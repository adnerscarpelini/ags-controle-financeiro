using Ags.Finance.Modules.Transactions.Contracts;
using Ags.Finance.SharedKernel;

namespace Ags.Finance.Modules.Invoices.Contracts;

public sealed record InvoiceDetails(Guid CardExternalId, string CardName, AccountingMonth Month, Money Total, IReadOnlyList<EntryDetails> Entries, InvoiceStatus Status = InvoiceStatus.Open, Guid? ExternalId = null);

public interface IInvoiceCatalog
{
    Task<IReadOnlyList<InvoiceDetails>> GetAsync(CancellationToken cancellationToken = default);
}
