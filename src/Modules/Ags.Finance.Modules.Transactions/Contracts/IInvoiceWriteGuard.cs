using Ags.Finance.SharedKernel;

namespace Ags.Finance.Modules.Transactions.Contracts;

public interface IInvoiceWriteGuard
{
    Task<string?> RequireOpenAsync(Guid cardExternalId, AccountingMonth month, CancellationToken cancellationToken = default);
    Task<string?> RequireRecurringAsync(Guid cardExternalId, AccountingMonth month, DateTimeOffset plannedAt, CancellationToken cancellationToken = default);
}
