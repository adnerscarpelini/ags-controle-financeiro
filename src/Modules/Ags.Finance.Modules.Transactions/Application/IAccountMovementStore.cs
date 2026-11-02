using Ags.Finance.Modules.Transactions.Domain;

namespace Ags.Finance.Modules.Transactions.Application;

internal interface IAccountMovementStore
{
    Task<IReadOnlyList<AccountMovement>> GetAsync(CancellationToken cancellationToken);
    Task AddAsync(IReadOnlyList<AccountMovement> movements, CancellationToken cancellationToken);
}
