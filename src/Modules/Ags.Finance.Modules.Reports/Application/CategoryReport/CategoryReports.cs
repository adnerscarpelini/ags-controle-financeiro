using Ags.Finance.Modules.Reports.Contracts;
using Ags.Finance.Modules.Transactions.Contracts;
using Ags.Finance.SharedKernel;

namespace Ags.Finance.Modules.Reports.Application.CategoryReport;

internal sealed class CategoryReports(LedgerSource source, IClassificationCatalog classifications) : ICategoryReports
{
    private const string AdjustmentName = "Ajuste de fatura";

    public async Task<Contracts.CategoryReport> GetAsync(AccountingMonth month, CancellationToken cancellationToken = default)
    {
        var previous = month.AddMonths(-1);
        var from = previous.Year < month.Year ? previous : new AccountingMonth(month.Year, 1);
        var ledger = await source.LoadAsync(from, month, month, cancellationToken);
        var categories = (await classifications.GetCategoriesAsync(false, cancellationToken)).ToDictionary(x => x.ExternalId, x => x.Name);
        var subcategories = (await classifications.GetSubcategoriesAsync(false, cancellationToken)).ToDictionary(x => x.ExternalId);
        return new(month, ledger.Expenses(month),
            Lines(ledger, month, previous, CategoryKind.Expense, row => row.IsInvoiceAdjustment ? AdjustmentName : subcategories.TryGetValue(row.SubcategoryExternalId, out var s) ? categories.GetValueOrDefault(s.CategoryExternalId, s.DisplayName) : "Sem categoria"),
            ledger.Income(month),
            Lines(ledger, month, previous, CategoryKind.Income, row => subcategories.TryGetValue(row.SubcategoryExternalId, out var s) ? s.DisplayName : "Sem categoria"));
    }

    private static List<CategoryLine> Lines(Ledger ledger, AccountingMonth month, AccountingMonth previous, CategoryKind kind, Func<MonthlyTotal, string> name)
    {
        var rows = ledger.Rows.Where(x => x.Kind == kind).Select(x => (Name: name(x), Row: x)).ToList();
        var sign = kind == CategoryKind.Expense ? -1m : 1m;
        decimal Sum(IEnumerable<(string Name, MonthlyTotal Row)> items) => sign * items.Sum(x => x.Row.Amount.Amount);
        var monthsWith = Ledger.YearToDate(month).Count(m => rows.Any(x => x.Row.Month == m));
        var total = Sum(rows.Where(x => x.Row.Month == month));
        return rows.Where(x => x.Row.Month == month).GroupBy(x => x.Name)
            .Select(group =>
            {
                var amount = new Money(Sum(group));
                var average = monthsWith == 0 ? Money.Zero : new Money(Sum(rows.Where(x => x.Name == group.Key && x.Row.Month.Year == month.Year && x.Row.Month <= month)) / monthsWith);
                return new CategoryLine(group.Key, group.Any(x => x.Row.IsInvoiceAdjustment), amount, total == 0 ? 0m : decimal.Round(amount.Amount / total * 100m, 2, MidpointRounding.AwayFromZero),
                    new Money(Sum(rows.Where(x => x.Name == group.Key && x.Row.Month == previous))), average, amount - average);
            })
            .OrderByDescending(x => x.Amount).ThenBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }
}
