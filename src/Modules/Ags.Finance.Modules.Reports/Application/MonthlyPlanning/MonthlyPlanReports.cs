using Ags.Finance.Modules.Reports.Contracts;
using Ags.Finance.Modules.Transactions.Contracts;
using Ags.Finance.SharedKernel;

namespace Ags.Finance.Modules.Reports.Application.MonthlyPlanning;

internal sealed class MonthlyPlanReports(IMonthlyPlanCatalog plans, LedgerSource source) : IMonthlyPlanReports
{
    public async Task<MonthlyPlanReport> GetAsync(ReferenceMonth month, CancellationToken cancellationToken = default)
    {
        if (month.Year == 0) { return Empty(month, "Informe um mês válido."); }
        var plan = await plans.GetAsync(month, cancellationToken);
        if (plan is null) { return Empty(month); }
        var loaded = await source.LoadMonthlyPlanEntriesAsync(month, plan.CardView, cancellationToken);
        if (loaded.Error is not null) { return Empty(month, loaded.Error) with { Plan = plan }; }
        var entries = loaded.Entries;
        var ledger = new Ledger(entries.Where(x => x.Kind == CategoryKind.Expense || x.IsEffective).GroupBy(x => new { x.Kind, x.SubcategoryExternalId, x.IsInvoiceAdjustment })
            .Select(x => new MonthlyTotal(month, x.Key.Kind, x.Key.SubcategoryExternalId, x.Key.IsInvoiceAdjustment, new Money(x.Sum(row => row.Amount.Amount)), x.Count())).ToList());
        var expenses = entries.Where(x => x.Kind == CategoryKind.Expense).ToList();
        var lines = plan.Categories.Select(category => Line(category.CategoryExternalId, category.Name, category.Target, expenses.Where(x => x.CategoryExternalId == category.CategoryExternalId))).ToList();
        var plannedIds = plan.Categories.Select(x => x.CategoryExternalId).ToHashSet();
        var remaining = Line(null, "Categorias restantes", plan.UnclassifiedBudget, expenses.Where(x => !plannedIds.Contains(x.CategoryExternalId)));
        var summary = Line(null, "Orçamento", plan.SpendingBudget, expenses);
        var received = ledger.Income(month);
        var balance = received - plan.SpendingBudget;
        return new(month, plan, lines, remaining, received, plan.SpendingBudget, balance, received.IsZero ? 0m : balance.Amount / received.Amount * 100m,
            summary.Paid, summary.Forecast, ledger.Expenses(month), summary.Remaining, summary.PaidProgress, summary.ForecastProgress);
    }

    private static MonthlyPlanLine Line(Guid? categoryExternalId, string name, Money target, IEnumerable<EntryDetails> entries)
    {
        var list = entries.ToList();
        var paid = new Money(-list.Where(x => x.IsEffective).Sum(x => x.Amount.Amount));
        var forecast = new Money(-list.Where(x => !x.IsEffective).Sum(x => x.Amount.Amount));
        var paidProgress = target.Amount <= 0 ? 0m : Math.Clamp(paid.Amount / target.Amount, 0m, 1m);
        var forecastProgress = target.Amount <= 0 ? 0m : Math.Clamp(forecast.Amount / target.Amount, 0m, 1m - paidProgress);
        return new(categoryExternalId, name, target, paid, forecast, paid + forecast, target - paid - forecast, paidProgress, forecastProgress);
    }
    private static MonthlyPlanReport Empty(ReferenceMonth month, string? error = null)
        => new(month, null, [], null, Money.Zero, Money.Zero, Money.Zero, 0m, Money.Zero, Money.Zero, Money.Zero, Money.Zero, 0m, 0m, error);
}
