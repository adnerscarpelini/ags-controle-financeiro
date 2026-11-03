using Ags.Finance.Modules.Reports.Contracts;
using Ags.Finance.SharedKernel;

namespace Ags.Finance.Modules.Reports.Application.MonthlySummary;

internal sealed class MonthlySummaries(LedgerSource source) : IMonthlySummaries
{
    public async Task<Contracts.MonthlySummary> GetAsync(AccountingMonth month, CancellationToken cancellationToken = default)
    {
        var previous = month.AddMonths(-1);
        var from = previous.Year < month.Year ? previous : new AccountingMonth(month.Year, 1);
        var ledger = await source.LoadAsync(from, month, month, cancellationToken);
        var income = ledger.Income(month);
        var expenses = ledger.Expenses(month);
        var balance = income - expenses;
        decimal? saved = income.IsZero ? null : decimal.Round(balance.Amount / income.Amount * 100m, 2, MidpointRounding.AwayFromZero);
        return new(month, income, expenses, balance, saved, ledger.Expenses(previous), ledger.AverageExpenses(month), ledger.ExpenseMonthsUpTo(month).Count);
    }
}
