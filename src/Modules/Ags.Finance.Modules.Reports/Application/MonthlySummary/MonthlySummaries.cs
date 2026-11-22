using Ags.Finance.Modules.Reports.Contracts;
using Ags.Finance.SharedKernel;

namespace Ags.Finance.Modules.Reports.Application.MonthlySummary;

internal sealed class MonthlySummaries(LedgerSource source) : IMonthlySummaries
{
    public async Task<Contracts.MonthlySummary> GetAsync(ReferenceMonth month, CancellationToken cancellationToken = default)
    {
        var ledger = await source.LoadAsync(month, month, month, cancellationToken);
        return new(month, ledger.Income(month), ledger.Expenses(month));
    }
}
