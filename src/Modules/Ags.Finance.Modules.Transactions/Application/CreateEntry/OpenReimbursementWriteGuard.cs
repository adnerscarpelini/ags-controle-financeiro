using Ags.Finance.Modules.Transactions.Contracts;

namespace Ags.Finance.Modules.Transactions.Application.CreateEntry;

internal sealed class OpenReimbursementWriteGuard : IReimbursementWriteGuard
{
    public Task<string?> RequirePersonAsync(Guid personExternalId, CancellationToken cancellationToken = default)
        => Task.FromResult<string?>(null);
}
