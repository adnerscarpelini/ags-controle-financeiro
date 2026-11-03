using Ags.Finance.Modules.Transactions.Contracts;
using Ags.Finance.SharedKernel;

namespace Ags.Finance.Modules.Reports.Application;

/// <summary>Base única de apuração dos relatórios (#24/#25/#27): totais por mês contábil de receitas e despesas, com ajustes de fatura uma vez.</summary>
internal sealed class Ledger(IReadOnlyList<MonthlyTotal> rows)
{
    public IReadOnlyList<MonthlyTotal> Rows { get; } = rows;

    public Money Income(AccountingMonth month) => new(Rows.Where(x => x.Month == month && x.Kind == CategoryKind.Income).Sum(x => x.Amount.Amount));

    /// <summary>Despesas do mês como valor positivo; reduções de ajuste diminuem o total.</summary>
    public Money Expenses(AccountingMonth month) => new(-Rows.Where(x => x.Month == month && x.Kind == CategoryKind.Expense).Sum(x => x.Amount.Amount));

    public bool HasExpenses(AccountingMonth month) => Rows.Any(x => x.Month == month && x.Kind == CategoryKind.Expense);

    /// <summary>Meses de janeiro até <paramref name="month"/> (do mesmo ano) com ao menos um lançamento de despesa.</summary>
    public IReadOnlyList<AccountingMonth> ExpenseMonthsUpTo(AccountingMonth month)
        => YearToDate(month).Where(HasExpenses).ToList();

    public static IEnumerable<AccountingMonth> YearToDate(AccountingMonth month)
        => Enumerable.Range(1, month.Month).Select(x => new AccountingMonth(month.Year, x));

    /// <summary>Média das despesas dos meses com lançamento até <paramref name="month"/>; zero quando não há nenhum.</summary>
    public Money AverageExpenses(AccountingMonth month)
    {
        var months = ExpenseMonthsUpTo(month);
        return months.Count == 0 ? Money.Zero : new Money(months.Sum(x => Expenses(x).Amount) / months.Count);
    }
}
