using Ags.Finance.Modules.Transactions.Domain;
using Ags.Finance.SharedKernel;

namespace Ags.Finance.Modules.Transactions.Application;

internal interface IMonthlyPlanStore
{
    Task<MonthlyPlan?> GetAsync(ReferenceMonth month, CancellationToken cancellationToken);
    Task<IReadOnlyList<MonthlyPlanCategory>> GetCategoriesAsync(Guid planExternalId, CancellationToken cancellationToken);
    Task SaveAsync(MonthlyPlan plan, IReadOnlyList<MonthlyPlanCategory> categories, CancellationToken cancellationToken);
    Task DeleteAsync(MonthlyPlan plan, CancellationToken cancellationToken);
}
