using Ags.Finance.Modules.Transactions.Contracts;
using Ags.Finance.Modules.Transactions.Domain;
using Ags.Finance.SharedKernel;

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
        var adjustments = (await store.GetInvoiceAdjustmentsAsync(cancellationToken)).Where(x => MatchesAdjustment(x, filter)).ToList();
        if (filter.GroupCardEntries && !filter.PendingRecurringOnly) { return await QueryGroupedAsync(filter, adjustments, cancellationToken); }
        var page = await store.QueryAsync(filter, cancellationToken);
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

    // O agrupamento vale para o resultado inteiro, não para uma página: a soma de um cartão não pode depender de onde a página corta.
    private async Task<EntryQueryResult> QueryGroupedAsync(EntryFilter filter, IReadOnlyList<InvoiceAdjustment> adjustments, CancellationToken cancellationToken)
    {
        var all = await store.QueryAsync(filter, cancellationToken, 0, int.MaxValue);
        var described = SortMerged(await DescribeWithAdjustmentsAsync(all.Entries, adjustments, cancellationToken), filter);
        var cards = (await accounts.GetCreditCardsAsync(false, cancellationToken)).ToDictionary(x => x.ExternalId, x => x.Name);
        var rows = SortGrouped(GroupByCard(described, cards), filter);
        var number = Math.Min(filter.Page, Math.Max(1, (int)Math.Ceiling(rows.Count / (decimal)filter.PageSize)));
        var pageRows = rows.Skip((number - 1) * filter.PageSize).Take(filter.PageSize).ToList();
        var expenses = all.Expenses - new Money(adjustments.Sum(x => x.ExpenseDifference.Amount));
        return new(pageRows.Select(x => x.Entry).OfType<EntryDetails>().ToList(), all.Income, expenses, all.Income + expenses,
            rows.Count, number, filter.PageSize, GroupedRows: pageRows);
    }

    private static List<EntryRow> GroupByCard(IReadOnlyList<EntryDetails> entries, Dictionary<Guid, string> cards)
    {
        var groups = entries.Where(x => x.CardExternalId is not null)
            .ToLookup(x => (Card: x.CardExternalId.GetValueOrDefault(), x.Month));
        var rows = entries.Where(x => x.CardExternalId is null).Select(x => new EntryRow(x)).ToList();
        rows.AddRange(groups.Select(group => new EntryRow(null, new EntryCardGroup(group.Key.Card, cards.GetValueOrDefault(group.Key.Card, "Cartão registrado"),
            group.Key.Month, group.Max(x => x.Date), new Money(group.Sum(x => x.Amount.Amount)), group.Count()))));
        return rows;
    }

    private static List<EntryRow> SortGrouped(List<EntryRow> rows, EntryFilter filter)
    {
        static DateOnly DateOf(EntryRow row) => row.CardGroup?.LastDate ?? row.Entry?.Date ?? default;
        static decimal AmountOf(EntryRow row) => (row.CardGroup?.Amount ?? row.Entry?.Amount ?? default).Amount;
        static string NameOf(EntryRow row) => row.CardGroup?.CardName ?? row.Entry?.MerchantName ?? string.Empty;
        static string CategoryOf(EntryRow row) => row.Entry?.Classification ?? string.Empty;
        return (filter.SortColumn, filter.SortDescending) switch
        {
            (EntrySortColumn.Description, true) => rows.OrderByDescending(NameOf, StringComparer.CurrentCulture).ToList(),
            (EntrySortColumn.Description, false) => rows.OrderBy(NameOf, StringComparer.CurrentCulture).ToList(),
            (EntrySortColumn.Category, true) => rows.OrderByDescending(CategoryOf, StringComparer.CurrentCulture).ToList(),
            (EntrySortColumn.Category, false) => rows.OrderBy(CategoryOf, StringComparer.CurrentCulture).ToList(),
            (EntrySortColumn.Amount, true) => rows.OrderByDescending(AmountOf).ToList(),
            (EntrySortColumn.Amount, false) => rows.OrderBy(AmountOf).ToList(),
            (EntrySortColumn.Date, false) => rows.OrderBy(DateOf).ToList(),
            _ => rows.OrderByDescending(DateOf).ToList(),
        };
    }
}
