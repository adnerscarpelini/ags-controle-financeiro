using Ags.Finance.Modules.Transactions.Domain;

namespace Ags.Finance.Modules.Transactions.Application;

internal interface ISavedFilterStore
{
    Task<IReadOnlyList<SavedFilter>> GetAsync(CancellationToken cancellationToken);
    Task<bool> SaveAsync(SavedFilter filter, CancellationToken cancellationToken);
    Task DeleteAsync(SavedFilter filter, CancellationToken cancellationToken);
}
