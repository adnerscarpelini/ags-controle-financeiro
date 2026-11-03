using Ags.Finance.Modules.Transactions.Contracts;
using Ags.Finance.SharedKernel;

namespace Ags.Finance.Modules.Transactions.Application.CreateEntry;

internal sealed class OpenInvoiceWriteGuard : IInvoiceWriteGuard
{
    public Task<bool> IsAnticipatedAsync(Guid entryExternalId, CancellationToken cancellationToken = default) => Task.FromResult(false);
    public Task<string?> RequireAdjustmentAsync(Guid cardExternalId, ReferenceMonth month, CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);
    public Task<string?> RequireOpenAsync(Guid cardExternalId, ReferenceMonth month, CancellationToken cancellationToken = default)
        => Task.FromResult<string?>(null);
    public Task<string?> RequireRecurringAsync(Guid cardExternalId, ReferenceMonth month, DateTimeOffset plannedAt, CancellationToken cancellationToken = default)
        => Task.FromResult<string?>(null);
    public Task<string?> CheckRecurringAsync(Guid cardExternalId, ReferenceMonth month, DateTimeOffset plannedAt, CancellationToken cancellationToken = default)
        => Task.FromResult<string?>(null);
}
