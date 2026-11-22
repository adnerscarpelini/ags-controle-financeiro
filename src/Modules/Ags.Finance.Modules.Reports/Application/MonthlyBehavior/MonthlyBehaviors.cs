using Ags.Finance.Modules.Reports.Contracts;
using Ags.Finance.SharedKernel;

namespace Ags.Finance.Modules.Reports.Application.MonthlyBehavior;

internal sealed class MonthlyBehaviors(LedgerSource source) : IMonthlyBehaviors
{
    public async Task<Contracts.MonthlyBehavior> GetAsync(ReferenceMonth month, CancellationToken cancellationToken = default)
    {
        var spending = await source.LoadSpendingAsync(month, month, cancellationToken);
        return new(month, spending.Expenses(month));
    }
}
