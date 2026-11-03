using Ags.Finance.Modules.Transactions.Application;
using Ags.Finance.Modules.Transactions.Domain;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Ags.Finance.Modules.Transactions.Infrastructure;

internal sealed class SavedFilterStore(IDbContextFactory<TransactionsDbContext> factory) : ISavedFilterStore
{
    public async Task<IReadOnlyList<SavedFilter>> GetAsync(CancellationToken cancellationToken)
    {
        await using var context = await factory.CreateDbContextAsync(cancellationToken);
        return await context.Set<SavedFilter>().AsNoTracking().OrderBy(x => x.Name).ToListAsync(cancellationToken);
    }

    public async Task<bool> SaveAsync(SavedFilter filter, CancellationToken cancellationToken)
    {
        await using var context = await factory.CreateDbContextAsync(cancellationToken);
        context.Entry(filter).State = filter.Id == 0 ? EntityState.Added : EntityState.Modified;
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

    public async Task DeleteAsync(SavedFilter filter, CancellationToken cancellationToken)
    {
        await using var context = await factory.CreateDbContextAsync(cancellationToken);
        context.Entry(filter).State = EntityState.Deleted;
        await context.SaveChangesAsync(cancellationToken);
    }
}
