using Ags.Finance.Modules.Reports.Contracts;
using Ags.Finance.Modules.Transactions.Contracts;
using Ags.Finance.SharedKernel;

namespace Ags.Finance.Modules.Reports.Application.HeatMap;

internal sealed class HeatMapReports(LedgerSource source, IClassificationCatalog classifications) : IHeatMapReports
{
    private const string AdjustmentName = "Ajuste de fatura";

    public async Task<Contracts.HeatMap> GetAsync(ReferenceMonth selectedMonth, CancellationToken cancellationToken = default)
    {
        var months = Enumerable.Range(1, 12).Select(x => new ReferenceMonth(selectedMonth.Year, x)).ToList();
        // Só até o mês selecionado são geradas ocorrências recorrentes; meses seguintes mostram o que já está lançado (parcelas, por exemplo).
        var ledger = await source.LoadAsync(months[0], months[^1], selectedMonth, cancellationToken);
        var categories = (await classifications.GetCategoriesAsync(false, cancellationToken)).ToDictionary(x => x.ExternalId, x => x.Name);
        var subcategories = (await classifications.GetSubcategoriesAsync(false, cancellationToken)).ToDictionary(x => x.ExternalId);

        var expenseRows = ledger.Rows.Where(x => x.Kind == CategoryKind.Expense).Select(x =>
        {
            if (x.IsInvoiceAdjustment) { return (Category: AdjustmentName, Sub: (string?)null, Row: x); }
            return subcategories.TryGetValue(x.SubcategoryExternalId, out var s)
                ? (Category: categories.GetValueOrDefault(s.CategoryExternalId, "Sem categoria"), Sub: s.Name, Row: x)
                : (Category: "Sem categoria", Sub: (string?)null, Row: x);
        }).ToList();

        var rows = new List<HeatRow>();
        foreach (var category in expenseRows.GroupBy(x => x.Category).Select(g => (g.Key, Items: g.ToList(), Total: g.Sum(x => -x.Row.Amount.Amount))).OrderByDescending(x => x.Total).ThenBy(x => x.Key, StringComparer.CurrentCultureIgnoreCase))
        {
            rows.Add(BuildRow(category.Key, 0, category.Key == AdjustmentName, months, category.Items.Select(x => x.Row)));
            foreach (var sub in category.Items.Where(x => x.Sub is not null).GroupBy(x => x.Sub!).Select(g => (g.Key, Items: g.Select(x => x.Row).ToList(), Total: g.Sum(x => -x.Row.Amount.Amount))).OrderByDescending(x => x.Total).ThenBy(x => x.Key, StringComparer.CurrentCultureIgnoreCase))
            {
                rows.Add(BuildRow(sub.Key, 1, false, months, sub.Items));
            }
        }
        var income = months.Select(ledger.Income).ToList();
        var expenses = months.Select(ledger.Expenses).ToList();
        return new(selectedMonth.Year, selectedMonth, income, expenses, income.Zip(expenses, (i, e) => i - e).ToList(), rows);
    }

    private static HeatRow BuildRow(string name, int level, bool adjustment, IReadOnlyList<ReferenceMonth> months, IEnumerable<MonthlyTotal> items)
    {
        var list = items.ToList();
        var amounts = months.Select(m => new Money(-list.Where(x => x.Month == m).Sum(x => x.Amount.Amount))).ToList();
        var max = amounts.Max(x => x.Amount);
        var cells = amounts.Select(x => new HeatCell(x, max <= 0 || x.Amount <= 0 ? 0m : decimal.Round(x.Amount / max, 3))).ToList();
        return new(name, level, adjustment, cells, new Money(amounts.Sum(x => x.Amount)));
    }
}
