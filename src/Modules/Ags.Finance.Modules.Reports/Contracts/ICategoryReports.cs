using Ags.Finance.SharedKernel;

namespace Ags.Finance.Modules.Reports.Contracts;

/// <summary>
/// Uma linha do relatório por categoria (despesas) ou por categoria &gt; subcategoria (receitas). Valores positivos.
/// <see cref="DifferenceToAverage"/> é o valor do mês menos a média do ano: positivo = acima da média.
/// </summary>
public sealed record CategoryLine(string Name, bool IsInvoiceAdjustment, Money Amount, decimal PercentOfMonth, Money PreviousMonth, Money YearAverage, Money DifferenceToAverage)
{
    public bool AboveAverage => DifferenceToAverage > Money.Zero;
}

/// <summary>
/// As despesas mostram onde se gastou no mês, pela data da despesa: <see cref="TotalExpenses"/> é o gasto do <see cref="MonthlyBehavior"/>. As receitas seguem o mês de referência:
/// <see cref="TotalIncome"/> é a receita do <see cref="MonthlySummary"/>. O ajuste de fatura (#52) aparece como linha própria, uma vez, para a soma das linhas coincidir com o total.
/// </summary>
public sealed record CategoryReport(ReferenceMonth Month, Money TotalExpenses, IReadOnlyList<CategoryLine> Expenses, Money TotalIncome, IReadOnlyList<CategoryLine> Income);

public interface ICategoryReports
{
    Task<CategoryReport> GetAsync(ReferenceMonth month, CancellationToken cancellationToken = default);
}
