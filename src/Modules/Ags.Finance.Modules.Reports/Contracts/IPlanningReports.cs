using Ags.Finance.SharedKernel;

namespace Ags.Finance.Modules.Reports.Contracts;

/// <summary>
/// Valor de um mês, positivo para receita e despesa (reduções de ajuste de fatura ficam negativas). <see cref="Intensity"/> vai de 0 a 1:
/// o valor dividido pelo maior valor de categoria do mesmo tipo no período; nas linhas de total é sempre 0.
/// </summary>
public sealed record PlanningCell(Money Launched, Money Forecast, decimal Intensity = 0m)
{
    public Money Amount => Launched + Forecast;
    public bool HasForecast => !Forecast.IsZero;
}

public sealed record PlanningSubcategory(string Name, IReadOnlyList<PlanningCell> Cells, Money Total);

public sealed record PlanningCategory(string Name, bool IsInvoiceAdjustment, IReadOnlyList<PlanningCell> Cells, Money Total, IReadOnlyList<PlanningSubcategory> Subcategories);

/// <summary>
/// Planejamento de receitas e despesas (#77): listas com uma posição por item de <see cref="Months"/>. <see cref="Balance"/> é o saldo do mês.
/// <see cref="AccumulatedBalance"/> parte de zero no mês atual e é <c>null</c> antes dele; não é saldo de conta.
/// </summary>
public sealed record PlanningReport(
    ReferenceMonth CurrentMonth,
    IReadOnlyList<ReferenceMonth> Months,
    IReadOnlyList<PlanningCell> Income,
    IReadOnlyList<PlanningCell> Expenses,
    IReadOnlyList<Money> Balance,
    IReadOnlyList<Money?> AccumulatedBalance,
    IReadOnlyList<PlanningCategory> IncomeCategories,
    IReadOnlyList<PlanningCategory> ExpenseCategories,
    string? ProjectionError = null)
{
    public bool IsEmpty => IncomeCategories.Count == 0 && ExpenseCategories.Count == 0;
}

public interface IPlanningReports
{
    /// <summary>Período de 19 meses a partir de <paramref name="firstMonth"/>; sem ele, 6 meses anteriores, o atual e 12 seguintes.</summary>
    Task<PlanningReport> GetAsync(ReferenceMonth? firstMonth = null, CancellationToken cancellationToken = default);
}
