using Ags.Finance.Modules.Reports.Contracts;
using Ags.Finance.SharedKernel;

namespace Ags.Finance.Modules.Reports.Application.MonthlyBehavior;

internal sealed class MonthlyBehaviors(LedgerSource source) : IMonthlyBehaviors
{
    public async Task<Contracts.MonthlyBehavior> GetAsync(ReferenceMonth month, CancellationToken cancellationToken = default)
    {
        var previous = month.AddMonths(-1);
        var from = previous.Year < month.Year ? previous : new ReferenceMonth(month.Year, 1);
        var spending = await source.LoadSpendingAsync(from, month, cancellationToken);
        return new(month, spending.Expenses(month), spending.Expenses(previous), spending.AverageExpenses(month), spending.ExpenseMonthsUpTo(month).Count);
    }
}
