using Ags.Finance.Modules.Invoices.Domain;
using Ags.Finance.SharedKernel;

namespace Ags.Finance.Modules.Invoices.Application;

internal interface IInvoiceStore
{
    Task<IReadOnlyList<Invoice>> GetAsync(CancellationToken cancellationToken);
    Task<Invoice?> FindAsync(Guid cardExternalId, AccountingMonth month, CancellationToken cancellationToken);
    Task SaveAsync(Invoice invoice, CancellationToken cancellationToken);
}
