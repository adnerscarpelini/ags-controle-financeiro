using Ags.Finance.Modules.Transactions.Application;
using Ags.Finance.Modules.Transactions.Domain;
using Microsoft.EntityFrameworkCore;

namespace Ags.Finance.Modules.Transactions.Infrastructure;

internal sealed class RecurringTransferStore(IDbContextFactory<TransactionsDbContext> factory) : IRecurringTransferStore
{
    public async Task<IReadOnlyList<RecurringTransfer>> GetSeriesAsync(CancellationToken cancellationToken)
    {
        await using var context = await factory.CreateDbContextAsync(cancellationToken);
        return await context.Set<RecurringTransfer>().AsNoTracking().ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TransferOccurrence>> GetOccurrencesAsync(CancellationToken cancellationToken)
    {
        await using var context = await factory.CreateDbContextAsync(cancellationToken);
        return await context.Set<TransferOccurrence>().AsNoTracking().ToListAsync(cancellationToken);
    }

    public async Task AddSeriesAsync(RecurringTransfer series, TransferOccurrence first, CancellationToken cancellationToken)
    {
        await using var context = await factory.CreateDbContextAsync(cancellationToken);
        context.Add(series);
        context.Add(first);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task AddOccurrencesAsync(IReadOnlyList<TransferOccurrence> occurrences, CancellationToken cancellationToken)
    {
        await using var context = await factory.CreateDbContextAsync(cancellationToken);
        context.AddRange(occurrences);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task SaveAsync(RecurringTransfer? series, IReadOnlyList<TransferOccurrence> updated, IReadOnlyList<TransferOccurrence> removed, CancellationToken cancellationToken)
    {
        await using var context = await factory.CreateDbContextAsync(cancellationToken);
        if (series is not null) { context.Entry(series).State = EntityState.Modified; }
        foreach (var occurrence in updated) { context.Entry(occurrence).State = EntityState.Modified; }
        context.RemoveRange(removed);
        await context.SaveChangesAsync(cancellationToken);
    }
}
