using Ags.Finance.Modules.Transactions.Contracts;
using Ags.Finance.SharedKernel;

namespace Ags.Finance.Modules.Reports.Application;

internal sealed class LedgerSource(IEntryCatalog entries, IEntryTotals totals)
{
    /// <summary>
    /// Carrega o razão de <paramref name="from"/> a <paramref name="to"/>. Antes, garante as ocorrências recorrentes dos meses
    /// do intervalo que não passam de <paramref name="generateUntil"/>, como a tela de Lançamentos faz ao consultar um mês.
    /// </summary>
    public async Task<Ledger> LoadAsync(ReferenceMonth from, ReferenceMonth to, ReferenceMonth generateUntil, CancellationToken cancellationToken)
    {
        for (var month = from; month <= to && month <= generateUntil; month = month.AddMonths(1))
        {
            await entries.EnsureRecurringMonthAsync(month, cancellationToken);
        }
        return new(await totals.GetMonthlyAsync(from, to, cancellationToken));
    }
}
