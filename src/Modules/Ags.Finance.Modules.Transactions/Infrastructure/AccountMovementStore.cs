using Ags.Finance.Modules.Transactions.Application;
using Ags.Finance.Modules.Transactions.Domain;
using Microsoft.EntityFrameworkCore;

namespace Ags.Finance.Modules.Transactions.Infrastructure;

internal sealed class AccountMovementStore(IDbContextFactory<TransactionsDbContext> factory) : IAccountMovementStore
{
    public async Task<IReadOnlyList<AccountMovement>> GetAsync(CancellationToken cancellationToken)
    {
        await using var context = await factory.CreateDbContextAsync(cancellationToken);
        return await context.Set<AccountMovement>().AsNoTracking().ToListAsync(cancellationToken);
    }
    public async Task AddAsync(IReadOnlyList<AccountMovement> movements, CancellationToken cancellationToken)
    {
        await using var context = await factory.CreateDbContextAsync(cancellationToken);
        context.AddRange(movements);
        await context.SaveChangesAsync(cancellationToken);
    }
}
