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

    public async Task<IReadOnlyList<InstallmentPurchase>> GetPurchasesAsync(CancellationToken cancellationToken)
    {
        await using var context = await factory.CreateDbContextAsync(cancellationToken);
        return await context.Set<InstallmentPurchase>().AsNoTracking().ToListAsync(cancellationToken);
    }

    public async Task<bool> AddPurchaseAsync(InstallmentPurchase purchase, IReadOnlyList<TransactionEntry> entries, CancellationToken cancellationToken)
    {
        await using var context = await factory.CreateDbContextAsync(cancellationToken);
        context.Add(purchase);
        context.AddRange(entries);
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

    public async Task<bool> SaveEntriesAsync(IReadOnlyList<TransactionEntry> entries, CancellationToken cancellationToken)
    {
        await using var context = await factory.CreateDbContextAsync(cancellationToken);
        foreach (var entry in entries) { context.Entry(entry).State = EntityState.Modified; }
        try
        {
            await context.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.ForeignKeyViolation or PostgresErrorCodes.UniqueViolation })
        {
            return false;
        }
    }

    public async Task DeleteEntriesAsync(IReadOnlyList<TransactionEntry> entries, CancellationToken cancellationToken)
    {
        await using var context = await factory.CreateDbContextAsync(cancellationToken);
        context.RemoveRange(entries);
        var ids = entries.Select(x => x.ExternalId).ToArray();
        foreach (var purchaseId in entries.Where(x => x.InstallmentPurchaseExternalId is not null).Select(x => x.InstallmentPurchaseExternalId!.Value).Distinct())
        {
            if (!await context.Set<TransactionEntry>().AnyAsync(x => x.InstallmentPurchaseExternalId == purchaseId && !ids.Contains(x.ExternalId), cancellationToken))
            {
                var purchase = await context.Set<InstallmentPurchase>().SingleAsync(x => x.ExternalId == purchaseId, cancellationToken);
                context.Remove(purchase);
            }
        }
        await context.SaveChangesAsync(cancellationToken);
    }
}
