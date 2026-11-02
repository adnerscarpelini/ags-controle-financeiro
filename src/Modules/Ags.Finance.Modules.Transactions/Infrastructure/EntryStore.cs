using Ags.Finance.Modules.Transactions.Application;
using Ags.Finance.Modules.Transactions.Domain;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Ags.Finance.Modules.Transactions.Infrastructure;

internal sealed class EntryStore(IDbContextFactory<TransactionsDbContext> factory) : IEntryStore
{
    public async Task<IReadOnlyList<TransactionEntry>> GetAsync(CancellationToken cancellationToken)
    {
        await using var context = await factory.CreateDbContextAsync(cancellationToken);
        return await context.Set<TransactionEntry>().AsNoTracking().OrderByDescending(x => x.Date).ThenByDescending(x => x.Id).ToListAsync(cancellationToken);
    }

    public async Task<bool> AddAsync(TransactionEntry entry, CancellationToken cancellationToken)
    {
        await using var context = await factory.CreateDbContextAsync(cancellationToken);
        context.Add(entry);
        try
        {
            await context.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.ForeignKeyViolation })
        {
            return false;
        }
    }
}
