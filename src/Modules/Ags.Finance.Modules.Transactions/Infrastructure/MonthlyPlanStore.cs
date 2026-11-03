using Ags.Finance.Modules.Transactions.Application;
using Ags.Finance.Modules.Transactions.Domain;
using Ags.Finance.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace Ags.Finance.Modules.Transactions.Infrastructure;

internal sealed class MonthlyPlanStore(IDbContextFactory<TransactionsDbContext> factory) : IMonthlyPlanStore
{
    public async Task<MonthlyPlan?> GetAsync(ReferenceMonth month, CancellationToken cancellationToken)
    {
        await using var context = await factory.CreateDbContextAsync(cancellationToken);
        return await context.Set<MonthlyPlan>().AsNoTracking().SingleOrDefaultAsync(x => x.Month == month, cancellationToken);
    }
    public async Task<IReadOnlyList<MonthlyPlanCategory>> GetCategoriesAsync(Guid planExternalId, CancellationToken cancellationToken)
    {
        await using var context = await factory.CreateDbContextAsync(cancellationToken);
        return await context.Set<MonthlyPlanCategory>().AsNoTracking().Where(x => x.MonthlyPlanExternalId == planExternalId).ToListAsync(cancellationToken);
    }
    public async Task SaveAsync(MonthlyPlan plan, IReadOnlyList<MonthlyPlanCategory> categories, CancellationToken cancellationToken)
    {
        await using var context = await factory.CreateDbContextAsync(cancellationToken);
        context.Entry(plan).State = plan.Id == 0 ? EntityState.Added : EntityState.Modified;
        var saved = await context.Set<MonthlyPlanCategory>().AsNoTracking().Where(x => x.MonthlyPlanExternalId == plan.ExternalId).ToListAsync(cancellationToken);
        var retained = categories.Select(x => x.ExternalId).ToHashSet();
        context.RemoveRange(saved.Where(x => !retained.Contains(x.ExternalId)));
        foreach (var category in categories) { context.Entry(category).State = category.Id == 0 ? EntityState.Added : EntityState.Modified; }
        await context.SaveChangesAsync(cancellationToken);
    }
    public async Task DeleteAsync(MonthlyPlan plan, CancellationToken cancellationToken)
    {
        await using var context = await factory.CreateDbContextAsync(cancellationToken);
        context.Remove(plan);
        await context.SaveChangesAsync(cancellationToken);
    }
}
