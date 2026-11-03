using Ags.Finance.SharedKernel;

namespace Ags.Finance.Modules.Invoices.Contracts;

public sealed record AnticipateInstallmentsRequest(Guid ConfirmationId, Guid EntryExternalId, int Count, Money Discount, ReferenceMonth? ExpectedMonth = null);
public interface IInvoiceAnticipationCatalog
{
    Task<InvoiceResult> AnticipateAsync(AnticipateInstallmentsRequest request, CancellationToken cancellationToken = default);
}
