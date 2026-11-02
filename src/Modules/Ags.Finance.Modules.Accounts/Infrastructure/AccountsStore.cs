using Ags.Finance.Modules.Accounts.Application;
using Ags.Finance.Modules.Accounts.Domain;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Ags.Finance.Modules.Accounts.Infrastructure;

internal sealed class AccountsStore(IDbContextFactory<AccountsDbContext> factory) : IAccountsStore
{
    public async Task<IReadOnlyList<Bank>> GetBanksAsync(CancellationToken cancellationToken)
    {
        await using var context = await factory.CreateDbContextAsync(cancellationToken);
        return await context.Set<Bank>().AsNoTracking().OrderBy(x => x.Name).ToListAsync(cancellationToken);
    }
    public async Task<IReadOnlyList<Account>> GetAccountsAsync(CancellationToken cancellationToken)
    {
        await using var context = await factory.CreateDbContextAsync(cancellationToken);
        return await context.Set<Account>().AsNoTracking().OrderBy(x => x.Name).ToListAsync(cancellationToken);
    }
    public async Task<IReadOnlyList<CreditCard>> GetCreditCardsAsync(CancellationToken cancellationToken)
    {
        await using var context = await factory.CreateDbContextAsync(cancellationToken);
        return await context.Set<CreditCard>().AsNoTracking().OrderBy(x => x.Name).ToListAsync(cancellationToken);
    }
    public async Task<IReadOnlyList<CardLastDigits>> GetCardLastDigitsAsync(CancellationToken cancellationToken)
    {
        await using var context = await factory.CreateDbContextAsync(cancellationToken);
        return await context.Set<CardLastDigits>().AsNoTracking().OrderBy(x => x.Digits).ToListAsync(cancellationToken);
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
        catch (DbUpdateException exception) when (exception.InnerException is SqliteException
        {
            SqliteExtendedErrorCode: 2067
        })
        {
            return false;
        }
    }
}
