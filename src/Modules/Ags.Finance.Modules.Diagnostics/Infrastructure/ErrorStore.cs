using Ags.Finance.Modules.Diagnostics.Application.RecordError;
using Ags.Finance.Modules.Diagnostics.Domain;
using Microsoft.EntityFrameworkCore;

namespace Ags.Finance.Modules.Diagnostics.Infrastructure;

internal sealed class ErrorStore(IDbContextFactory<DiagnosticsDbContext> contexts) : IErrorStore
{
    public async Task SaveAsync(ErrorOccurrence occurrence, CancellationToken cancellationToken)
    {
        await using var context = await contexts.CreateDbContextAsync(cancellationToken);
        var existing = await context.Set<ErrorRecord>().FirstOrDefaultAsync(x => x.Fingerprint == occurrence.Fingerprint, cancellationToken);
        if (existing is null)
        {
            context.Add(ErrorRecord.Create(occurrence.Fingerprint, occurrence.ExceptionType, occurrence.Source, occurrence.StackTrace, occurrence.SqlState, occurrence.OccurredAt));
        }
        else
        {
            existing.Recur(occurrence.OccurredAt);
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> PurgeAsync(DateTimeOffset lastOccurredBefore, CancellationToken cancellationToken)
    {
        await using var context = await contexts.CreateDbContextAsync(cancellationToken);
        return await context.Set<ErrorRecord>().Where(x => x.LastOccurredAt < lastOccurredBefore).ExecuteDeleteAsync(cancellationToken);
    }
}
