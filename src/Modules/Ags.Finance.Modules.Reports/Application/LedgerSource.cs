using Ags.Finance.Modules.Transactions.Contracts;
using Ags.Finance.SharedKernel;

namespace Ags.Finance.Modules.Reports.Application;

internal sealed record MonthlyPlanEntries(IReadOnlyList<EntryDetails> Entries, string? Error = null);

internal sealed class LedgerSource(IEntryCatalog entries, IEntryTotals totals)
{
    public async Task<MonthlyPlanEntries> LoadMonthlyPlanEntriesAsync(ReferenceMonth month, MonthlyPlanCardView cardView, CancellationToken cancellationToken)
    {
        var generation = await entries.EnsureRecurringMonthAsync(month, cancellationToken);
        if (!generation.Succeeded) { return new([], generation.Error); }
        if (cardView == MonthlyPlanCardView.PurchaseDate)
        {
            var dated = await entries.QueryAsync(new(FromDate: month.FirstDay, ToDate: month.LastDay, PageSize: 1), cancellationToken);
            if (dated.Error is not null) { return new([], dated.Error); }
        }
        return new((await entries.GetAsync(cancellationToken)).Where(x => x.IsInvoiceAdjustment ? x.Month == month
            : x.CardExternalId is null || cardView == MonthlyPlanCardView.PurchaseDate ? ReferenceMonth.FromDate(x.Date) == month : x.Month == month).ToList());
    }

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
