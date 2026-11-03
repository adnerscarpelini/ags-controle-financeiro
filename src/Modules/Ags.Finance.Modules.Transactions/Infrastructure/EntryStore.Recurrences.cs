using Ags.Finance.Modules.Transactions.Domain;
using Microsoft.EntityFrameworkCore;

namespace Ags.Finance.Modules.Transactions.Infrastructure;

internal sealed partial class EntryStore
{
    public async Task SaveRecurringChangesAsync(RecurringEntry series, IReadOnlyList<TransactionEntry> updated, IReadOnlyList<TransactionEntry> deleted, CancellationToken cancellationToken)
    {
        await using var context = await factory.CreateDbContextAsync(cancellationToken);
        context.Entry(series).State = EntityState.Modified;
        foreach (var entry in updated) { context.Entry(entry).State = EntityState.Modified; }
        context.RemoveRange(deleted);
        await context.SaveChangesAsync(cancellationToken);
    }
    public async Task<IReadOnlyList<RecurringEntry>> GetRecurringAsync(CancellationToken cancellationToken)
    {
        await using var context = await factory.CreateDbContextAsync(cancellationToken);
        return await context.Set<RecurringEntry>().AsNoTracking().ToListAsync(cancellationToken);
    }
    public async Task AddRecurringAsync(RecurringEntry series, TransactionEntry first, CancellationToken cancellationToken)
    {
        await using var context = await factory.CreateDbContextAsync(cancellationToken);
        context.Add(series);
        context.Add(first);
        await context.SaveChangesAsync(cancellationToken);
    }
    public async Task AddOccurrencesAsync(IReadOnlyList<TransactionEntry> entries, CancellationToken cancellationToken)
    {
        if (entries.Count == 0) { return; }
        await using var context = await factory.CreateDbContextAsync(cancellationToken);
        context.AddRange(entries);
        await context.SaveChangesAsync(cancellationToken);
    }
}
