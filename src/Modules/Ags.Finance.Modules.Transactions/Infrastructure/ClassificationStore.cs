using Ags.Finance.Modules.Transactions.Application;
using Ags.Finance.Modules.Transactions.Domain;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Ags.Finance.Modules.Transactions.Infrastructure;

internal sealed class ClassificationStore(IDbContextFactory<TransactionsDbContext> factory) : IClassificationStore
{
    public async Task<IReadOnlyList<Category>> GetCategoriesAsync(CancellationToken cancellationToken)
    {
        await using var context = await factory.CreateDbContextAsync(cancellationToken);
        return await context.Set<Category>().AsNoTracking().OrderBy(x => x.Name).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Subcategory>> GetSubcategoriesAsync(CancellationToken cancellationToken)
    {
        await using var context = await factory.CreateDbContextAsync(cancellationToken);
        return await context.Set<Subcategory>().AsNoTracking().OrderBy(x => x.Name).ToListAsync(cancellationToken);
    }

    public async Task<bool> SaveAsync(Entity entity, CancellationToken cancellationToken)
    {
        await using var context = await factory.CreateDbContextAsync(cancellationToken);
        context.Entry(entity).State = entity.Id == 0 ? EntityState.Added : EntityState.Modified;
        try
        {
            await context.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return false;
        }
    }
}
