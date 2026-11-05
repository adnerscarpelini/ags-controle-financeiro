using Ags.Finance.Modules.Transactions.Contracts;
using Ags.Finance.SharedKernel;

namespace Ags.Finance.Modules.Reports.Application;

/// <summary>Soma totais mensais de receitas e despesas, com ajustes de fatura uma vez. O mês das linhas é o de referência ou, no comportamento do mês, o da data (<see cref="LedgerSource"/>).</summary>
internal sealed class Ledger(IReadOnlyList<MonthlyTotal> rows)
{
    public IReadOnlyList<MonthlyTotal> Rows { get; } = rows;

    public Money Income(ReferenceMonth month) => new(Rows.Where(x => x.Month == month && x.Kind == CategoryKind.Income).Sum(x => x.Amount.Amount));

    /// <summary>Despesas do mês como valor positivo; reduções de ajuste diminuem o total.</summary>
    public Money Expenses(ReferenceMonth month) => new(-Rows.Where(x => x.Month == month && x.Kind == CategoryKind.Expense).Sum(x => x.Amount.Amount));

    public bool HasExpenses(ReferenceMonth month) => Rows.Any(x => x.Month == month && x.Kind == CategoryKind.Expense);

    /// <summary>Meses de janeiro até <paramref name="month"/> (do mesmo ano) com ao menos um lançamento de despesa.</summary>
    public IReadOnlyList<ReferenceMonth> ExpenseMonthsUpTo(ReferenceMonth month)
        => YearToDate(month).Where(HasExpenses).ToList();

    public static IEnumerable<ReferenceMonth> YearToDate(ReferenceMonth month)
        => Enumerable.Range(1, month.Month).Select(x => new ReferenceMonth(month.Year, x));

    /// <summary>Média das despesas dos meses com lançamento até <paramref name="month"/>; zero quando não há nenhum.</summary>
    public Money AverageExpenses(ReferenceMonth month)
    {
        var months = ExpenseMonthsUpTo(month);
        return months.Count == 0 ? Money.Zero : new Money(months.Sum(x => Expenses(x).Amount) / months.Count);
    }
}
