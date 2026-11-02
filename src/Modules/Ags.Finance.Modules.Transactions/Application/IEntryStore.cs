using Ags.Finance.Modules.Transactions.Contracts;
using Ags.Finance.Modules.Transactions.Domain;

namespace Ags.Finance.Modules.Transactions.Application;

internal interface IEntryStore
{
    Task<IReadOnlyList<TransactionEntry>> GetAsync(CancellationToken cancellationToken);
    Task<EntryPage> QueryAsync(EntryFilter filter, CancellationToken cancellationToken);
    Task<bool> AddAsync(TransactionEntry entry, CancellationToken cancellationToken);
    Task<IReadOnlyList<InstallmentPurchase>> GetPurchasesAsync(CancellationToken cancellationToken);
    Task<bool> AddPurchaseAsync(InstallmentPurchase purchase, IReadOnlyList<TransactionEntry> entries, CancellationToken cancellationToken);
    Task<bool> SaveEntriesAsync(IReadOnlyList<TransactionEntry> entries, CancellationToken cancellationToken);
    Task DeleteEntriesAsync(IReadOnlyList<TransactionEntry> entries, CancellationToken cancellationToken);
}
