using Ags.Finance.Modules.Transactions.Contracts;

namespace Ags.Finance.Modules.Transactions.Application.CreateEntry;

internal sealed partial class EntryCatalog
{
    public Task<EntryQueryResult> QueryPendingAsync(EntryFilter filter, CancellationToken cancellationToken = default)
        => QueryAsync(filter with { PendingRecurringOnly = true }, cancellationToken);

    public Task<EntryQueryResult> QueryAsync(EntryFilter filter, CancellationToken cancellationToken = default)
        => operations.ExecuteAsync(token => QueryCoreAsync(filter, token), x => x.Error is null, cancellationToken);

    private static bool ListsTooLarge(EntryFilter filter)
        => new[] { filter.AccountExternalIds, filter.CardExternalIds, filter.CardLastDigitsExternalIds, filter.CategoryExternalIds, filter.SubcategoryExternalIds, filter.MerchantExternalIds }
            .Any(x => x is { Count: > 200 });

    // Os ajustes de fatura não são linhas da tabela de lançamentos: a ordem escolhida é refeita depois de intercalá-los.
    private static IReadOnlyList<EntryDetails> SortMerged(IReadOnlyList<EntryDetails> entries, EntryFilter filter) => (filter.SortColumn, filter.SortDescending) switch
    {
        (EntrySortColumn.Description, true) => entries.OrderByDescending(x => x.MerchantName, StringComparer.CurrentCulture).ToList(),
        (EntrySortColumn.Description, false) => entries.OrderBy(x => x.MerchantName, StringComparer.CurrentCulture).ToList(),
        (EntrySortColumn.Category, true) => entries.OrderByDescending(x => x.Classification, StringComparer.CurrentCulture).ToList(),
        (EntrySortColumn.Category, false) => entries.OrderBy(x => x.Classification, StringComparer.CurrentCulture).ToList(),
        (EntrySortColumn.Amount, true) => entries.OrderByDescending(x => x.Amount.Amount).ToList(),
        (EntrySortColumn.Amount, false) => entries.OrderBy(x => x.Amount.Amount).ToList(),
        (EntrySortColumn.Date, false) => entries.OrderBy(x => x.Date).ToList(),
        _ => entries,
    };

    private async Task<EntryQueryResult> QueryCoreAsync(EntryFilter filter, CancellationToken cancellationToken)
    {
        if (filter.Month is { Year: 0 } || filter.FromDate > filter.ToDate || filter.Page < 1 || filter.PageSize is < 1 or > 200
            || filter.Text.Length > 200 || ListsTooLarge(filter) || (filter.Kind is { } kind && !Enum.IsDefined(kind)) || (filter.SortColumn is { } sort && !Enum.IsDefined(sort)))
        {
            return new([], default, default, default, 0, 1, 100, "Informe filtros válidos, intervalo ordenado e página de 1 a 200 registros.");
        }
        var generated = await GenerateRecurringAsync(filter, cancellationToken);
        if (!generated.Succeeded) { return new([], default, default, default, 0, 1, filter.PageSize, generated.Error); }
        var page = await store.QueryAsync(filter, cancellationToken);
        var adjustments = (await store.GetInvoiceAdjustmentsAsync(cancellationToken)).Where(x => MatchesAdjustment(x, filter)).ToList();
        if (adjustments.Count > 0)
        {
            var totalCount = page.TotalCount + adjustments.Count;
            var number = Math.Min(filter.Page, Math.Max(1, (int)Math.Ceiling(totalCount / (decimal)filter.PageSize)));
            var targetOffset = (number - 1) * filter.PageSize;
            var normalOffset = Math.Max(0, targetOffset - adjustments.Count);
            var window = await store.QueryAsync(filter, cancellationToken, normalOffset, filter.PageSize + adjustments.Count);
            var merged = SortMerged(await DescribeWithAdjustmentsAsync(window.Entries, adjustments, cancellationToken), filter);
            var expenses = window.Expenses - new Ags.Finance.SharedKernel.Money(adjustments.Sum(x => x.ExpenseDifference.Amount));
            return new(merged.Skip(targetOffset - normalOffset).Take(filter.PageSize).ToList(), window.Income, expenses,
                window.Income + expenses, totalCount, number, filter.PageSize);
        }
        var described = await DescribeAsync(page.Entries, cancellationToken);
        return new(described, page.Income, page.Expenses, page.Income + page.Expenses, page.TotalCount, page.Page, filter.PageSize);
    }
}
