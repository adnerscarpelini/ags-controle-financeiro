using Ags.Finance.Modules.Transactions.Contracts;

namespace Ags.Finance.Modules.Transactions.Application.CreateEntry;

internal sealed partial class EntryCatalog
{
    public Task<EntryQueryResult> QueryAsync(EntryFilter filter, CancellationToken cancellationToken = default)
        => operations.ExecuteAsync(token => QueryCoreAsync(filter, token), x => x.Error is null, cancellationToken);

    private async Task<EntryQueryResult> QueryCoreAsync(EntryFilter filter, CancellationToken cancellationToken)
    {
        if (filter.Month is { Year: 0 } || filter.FromDate > filter.ToDate || filter.Page < 1 || filter.PageSize is < 1 or > 200
            || filter.Text.Length > 200 || (filter.Kind is { } kind && !Enum.IsDefined(kind)))
        {
            return new([], default, default, default, 0, 1, 100, "Informe filtros válidos, intervalo ordenado e página de 1 a 200 registros.");
        }
        var generated = await GenerateRecurringAsync(filter, cancellationToken);
        if (!generated.Succeeded) { return new([], default, default, default, 0, 1, filter.PageSize, generated.Error); }
        var page = await store.QueryAsync(filter, cancellationToken);
        var described = await DescribeAsync(page.Entries, cancellationToken);
        return new(described, page.Income, page.Expenses, page.Income + page.Expenses, page.TotalCount, page.Page, filter.PageSize);
    }
}
