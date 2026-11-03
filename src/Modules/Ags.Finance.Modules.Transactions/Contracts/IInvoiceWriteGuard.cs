using Ags.Finance.SharedKernel;

namespace Ags.Finance.Modules.Transactions.Contracts;

public interface IInvoiceWriteGuard
{
    Task<string?> RequireAdjustmentAsync(Guid cardExternalId, AccountingMonth month, CancellationToken cancellationToken = default);
    Task<string?> RequireOpenAsync(Guid cardExternalId, AccountingMonth month, CancellationToken cancellationToken = default);
    Task<string?> RequireRecurringAsync(Guid cardExternalId, AccountingMonth month, DateTimeOffset plannedAt, CancellationToken cancellationToken = default);
}
