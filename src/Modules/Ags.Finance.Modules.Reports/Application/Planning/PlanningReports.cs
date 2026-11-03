using Ags.Finance.Modules.Reports.Contracts;
using Ags.Finance.Modules.Transactions.Contracts;
using Ags.Finance.SharedKernel;

namespace Ags.Finance.Modules.Reports.Application.Planning;

internal sealed class PlanningReports(IEntryCatalog entries, IEntryTotals totals, IClassificationCatalog classifications, TimeProvider clock) : IPlanningReports
{
    private const int MonthsBefore = 6;
    private const int MonthCount = 19;
    private const string AdjustmentName = "Ajuste de fatura";
    private const string NoCategoryName = "Sem categoria";

    public async Task<PlanningReport> GetAsync(ReferenceMonth? firstMonth = null, CancellationToken cancellationToken = default)
    {
        var current = ReferenceMonth.FromDate(DateOnly.FromDateTime(clock.GetLocalNow().DateTime));
        var first = firstMonth ?? current.AddMonths(-MonthsBefore);
        var months = Enumerable.Range(0, MonthCount).Select(first.AddMonths).ToList();
        // O acumulado parte do mês atual mesmo quando ele fica antes do período exibido.
        var loadFrom = current < first ? current : first;
        for (var month = loadFrom; month <= months[^1] && month <= current; month = month.AddMonths(1))
        {
            await entries.EnsureRecurringMonthAsync(month, cancellationToken);
        }
        var planned = await totals.GetPlannedAsync(loadFrom, months[^1], cancellationToken);
        var visible = planned.Rows.Where(x => x.Month >= first).Select(Normalize).ToList();

        var categories = (await classifications.GetCategoriesAsync(false, cancellationToken)).ToDictionary(x => x.ExternalId, x => x.Name);
        var subcategories = (await classifications.GetSubcategoriesAsync(false, cancellationToken)).ToDictionary(x => x.ExternalId);
        var classified = visible.Select(x =>
        {
            if (x.IsInvoiceAdjustment) { return new ClassifiedRow(AdjustmentName, null, x); }
            return subcategories.TryGetValue(x.SubcategoryExternalId, out var sub)
                ? new ClassifiedRow(categories.GetValueOrDefault(sub.CategoryExternalId, NoCategoryName), sub.Name, x)
                : new ClassifiedRow(NoCategoryName, null, x);
        }).ToList();

        var income = Totals(months, visible.Where(x => x.Kind == CategoryKind.Income));
        var expenses = Totals(months, visible.Where(x => x.Kind == CategoryKind.Expense));
        var balance = income.Zip(expenses, (i, e) => i.Amount - e.Amount).ToList();
        return new(current, months, income, expenses, balance, Accumulated(planned.Rows, months, current),
            BuildCategories(months, classified.Where(x => x.Row.Kind == CategoryKind.Income).ToList()),
            BuildCategories(months, classified.Where(x => x.Row.Kind == CategoryKind.Expense).ToList()), planned.Error);
    }

    /// <summary>Despesas vêm negativas de Transactions e aqui viram valor positivo; uma redução de ajuste fica negativa e diminui o total.</summary>
    private static PlannedTotal Normalize(PlannedTotal row)
        => row.Kind == CategoryKind.Expense ? row with { Launched = -row.Launched, Forecast = -row.Forecast } : row;

    private static List<PlanningCell> Totals(IReadOnlyList<ReferenceMonth> months, IEnumerable<PlannedTotal> rows)
    {
        var list = rows.ToList();
        return months.Select(month => new PlanningCell(Sum(list.Where(x => x.Month == month), x => x.Launched), Sum(list.Where(x => x.Month == month), x => x.Forecast))).ToList();
    }

    private static List<Money?> Accumulated(IReadOnlyList<PlannedTotal> rows, IReadOnlyList<ReferenceMonth> months, ReferenceMonth current)
    {
        var result = new List<Money?>();
        foreach (var month in months)
        {
            if (month < current) { result.Add(null); continue; }
            // O sinal de Transactions já é o do saldo: receita positiva e despesa negativa.
            result.Add(Sum(rows.Where(x => x.Month >= current && x.Month <= month), x => x.Launched + x.Forecast));
        }
        return result;
    }

    private static List<PlanningCategory> BuildCategories(IReadOnlyList<ReferenceMonth> months, IReadOnlyList<ClassifiedRow> rows)
    {
        var groups = rows.GroupBy(x => x.Category).ToList();
        var maximum = groups.SelectMany(group => months.Select(month => Sum(group.Where(x => x.Row.Month == month).Select(x => x.Row), x => x.Launched + x.Forecast).Amount)).DefaultIfEmpty(0m).Max();
        return groups.Select(group =>
        {
            var cells = Cells(months, group.Select(x => x.Row), maximum);
            var subs = group.Where(x => x.Subcategory is not null).GroupBy(x => x.Subcategory!).Select(sub =>
            {
                var subCells = Cells(months, sub.Select(x => x.Row), maximum);
                return new PlanningSubcategory(sub.Key, subCells, Total(subCells));
            }).OrderByDescending(x => x.Total.Amount).ThenBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
            return new PlanningCategory(group.Key, group.Key == AdjustmentName, cells, Total(cells), subs);
        }).OrderByDescending(x => x.Total.Amount).ThenBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    private static List<PlanningCell> Cells(IReadOnlyList<ReferenceMonth> months, IEnumerable<PlannedTotal> rows, decimal maximum)
    {
        var list = rows.ToList();
        return months.Select(month =>
        {
            var launched = Sum(list.Where(x => x.Month == month), x => x.Launched);
            var forecast = Sum(list.Where(x => x.Month == month), x => x.Forecast);
            var amount = (launched + forecast).Amount;
            return new PlanningCell(launched, forecast, maximum <= 0 || amount <= 0 ? 0m : decimal.Round(Math.Min(amount / maximum, 1m), 3));
        }).ToList();
    }

    private static Money Total(IEnumerable<PlanningCell> cells) => new(cells.Sum(x => x.Amount.Amount));

    private static Money Sum(IEnumerable<PlannedTotal> rows, Func<PlannedTotal, Money> selector) => new(rows.Sum(x => selector(x).Amount));

    private sealed record ClassifiedRow(string Category, string? Subcategory, PlannedTotal Row);
}
