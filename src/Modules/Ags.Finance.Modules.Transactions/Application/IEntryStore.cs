using Ags.Finance.Modules.Transactions.Domain;

namespace Ags.Finance.Modules.Transactions.Application;

internal interface IEntryStore
{
    Task<IReadOnlyList<TransactionEntry>> GetAsync(CancellationToken cancellationToken);
    Task<bool> AddAsync(TransactionEntry entry, CancellationToken cancellationToken);
}
