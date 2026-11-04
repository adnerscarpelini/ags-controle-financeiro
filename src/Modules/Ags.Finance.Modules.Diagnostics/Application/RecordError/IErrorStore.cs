namespace Ags.Finance.Modules.Diagnostics.Application.RecordError;

internal interface IErrorStore
{
    Task SaveAsync(ErrorOccurrence occurrence, CancellationToken cancellationToken);

    Task<int> PurgeAsync(DateTimeOffset lastOccurredBefore, CancellationToken cancellationToken);
}
