using Ags.Finance.Modules.Transactions.Contracts;
using Ags.Finance.SharedKernel;

namespace Ags.Finance.Modules.Transactions.Application.CreateEntry;

internal sealed class EntryTotals(IEntryStore store) : IEntryTotals
{
    public async Task<IReadOnlyList<MonthlyTotal>> GetMonthlyAsync(AccountingMonth from, AccountingMonth to, CancellationToken cancellationToken = default)
    {
        if (from.Year == 0 || to.Year == 0 || from > to) { return []; }
        var rows = await store.GetMonthlyAmountsAsync(from, to, cancellationToken);
        var entries = rows.GroupBy(x => (x.Month, x.Kind, x.SubcategoryExternalId))
            .Select(group => new MonthlyTotal(group.Key.Month, (Contracts.CategoryKind)group.Key.Kind, group.Key.SubcategoryExternalId, false,
                new Money(group.Sum(x => x.Amount.Amount)), group.Count()));
        var adjustments = (await store.GetInvoiceAdjustmentsAsync(cancellationToken)).Where(x => x.Month >= from && x.Month <= to).GroupBy(x => x.Month)
            .Select(group => new MonthlyTotal(group.Key, Contracts.CategoryKind.Expense, Guid.Empty, true, new Money(-group.Sum(x => x.ExpenseDifference.Amount)), group.Count()));
        return entries.Concat(adjustments).OrderBy(x => x.Month).ThenBy(x => x.Kind).ToList();
    }
}
