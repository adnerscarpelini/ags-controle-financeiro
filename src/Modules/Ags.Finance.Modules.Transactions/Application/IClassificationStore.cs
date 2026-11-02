using Ags.Finance.Modules.Transactions.Domain;

namespace Ags.Finance.Modules.Transactions.Application;

internal interface IClassificationStore
{
    Task<IReadOnlyList<Category>> GetCategoriesAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<Subcategory>> GetSubcategoriesAsync(CancellationToken cancellationToken);
    Task<bool> SaveAsync(Entity entity, CancellationToken cancellationToken);
}
