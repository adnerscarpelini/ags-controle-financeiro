using Ags.Finance.Modules.Transactions.Domain;

namespace Ags.Finance.Modules.Transactions.Application;

internal interface IRecurringTransferStore
{
    Task<IReadOnlyList<RecurringTransfer>> GetSeriesAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<TransferOccurrence>> GetOccurrencesAsync(CancellationToken cancellationToken);
    Task AddSeriesAsync(RecurringTransfer series, TransferOccurrence first, CancellationToken cancellationToken);
    Task AddOccurrencesAsync(IReadOnlyList<TransferOccurrence> occurrences, CancellationToken cancellationToken);
    Task SaveAsync(RecurringTransfer? series, IReadOnlyList<TransferOccurrence> updated, IReadOnlyList<TransferOccurrence> removed, CancellationToken cancellationToken);
}
