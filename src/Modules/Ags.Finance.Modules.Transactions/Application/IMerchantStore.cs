using Ags.Finance.Modules.Transactions.Domain;

namespace Ags.Finance.Modules.Transactions.Application;

internal interface IMerchantStore
{
    Task<IReadOnlyList<Merchant>> GetAsync(CancellationToken cancellationToken);
    Task<bool> SaveAsync(Merchant merchant, CancellationToken cancellationToken);
    Task<string?> MergeAsync(Guid sourceExternalId, Guid targetExternalId, CancellationToken cancellationToken);
}
