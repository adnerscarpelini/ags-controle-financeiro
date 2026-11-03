using Ags.Finance.Modules.Accounts.Contracts;
using Ags.Finance.Modules.Transactions.Contracts;
using Ags.Finance.SharedKernel;

namespace Ags.Finance.Modules.Transactions.Application.CreateEntry;

internal sealed class EntryTotals(IEntryStore store, IAccountingMonthCalculator calendar, TimeProvider clock, IInvoiceWriteGuard invoices) : IEntryTotals
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

    public async Task<PlannedTotals> GetPlannedAsync(AccountingMonth from, AccountingMonth to, CancellationToken cancellationToken = default)
    {
        if (from.Year == 0 || to.Year == 0 || from > to) { return new([]); }
        var today = DateOnly.FromDateTime(clock.GetLocalNow().DateTime);
        var parts = new List<PlannedPart>();
        var openInvoices = new Dictionary<(Guid Card, AccountingMonth Month), bool>();
        foreach (var row in await store.GetPlanningAmountsAsync(from, to, cancellationToken))
        {
            var isForecast = row.RecurringEntryExternalId is not null && (row.CardExternalId is { } card
                ? row.Date > today && await IsOpenAsync(card, row.Month, openInvoices, cancellationToken)
                : !row.IsEffective);
            parts.Add(new(row.Month, (Contracts.CategoryKind)row.Kind, row.SubcategoryExternalId, false, row.Amount.Amount, isForecast));
        }
        parts.AddRange((await store.GetInvoiceAdjustmentsAsync(cancellationToken)).Where(x => x.Month >= from && x.Month <= to)
            .Select(x => new PlannedPart(x.Month, Contracts.CategoryKind.Expense, Guid.Empty, true, -x.ExpenseDifference.Amount, false)));

        var error = await AddProjectionAsync(parts, from, to, AccountingMonth.FromDate(today), cancellationToken);
        var rows = parts.GroupBy(x => (x.Month, x.Kind, x.SubcategoryExternalId, x.IsInvoiceAdjustment))
            .Select(group => new PlannedTotal(group.Key.Month, group.Key.Kind, group.Key.SubcategoryExternalId, group.Key.IsInvoiceAdjustment,
                new Money(group.Where(x => !x.IsForecast).Sum(x => x.Amount)), new Money(group.Where(x => x.IsForecast).Sum(x => x.Amount))))
            .OrderBy(x => x.Month).ThenBy(x => x.Kind).ToList();
        return new(rows, error);
    }

    /// <summary>A ocorrência já existe, então só a situação gravada da fatura decide; a data de corte só vale para o que ainda será lançado.</summary>
    private async Task<bool> IsOpenAsync(Guid card, AccountingMonth month, Dictionary<(Guid Card, AccountingMonth Month), bool> known, CancellationToken cancellationToken)
    {
        if (known.TryGetValue((card, month), out var open)) { return open; }
        open = await invoices.CheckRecurringAsync(card, month, DateTimeOffset.MinValue, cancellationToken) is null;
        known[(card, month)] = open;
        return open;
    }

    private async Task<string?> AddProjectionAsync(List<PlannedPart> parts, AccountingMonth from, AccountingMonth to, AccountingMonth current, CancellationToken cancellationToken)
    {
        var first = from > current ? from : current.AddMonths(1);
        if (first > to) { return null; }
        var definitions = (await store.GetRecurringAsync(cancellationToken)).Where(x => x.IsActive).ToList();
        if (definitions.Count == 0) { return null; }
        var existing = RecurringOccurrencePlanner.ExistingOccurrences(await store.GetAsync(cancellationToken));
        var planner = new RecurringOccurrencePlanner(calendar, clock);
        for (var month = first; month <= to; month = month.AddMonths(1))
        {
            var plan = await planner.PlanAsync(definitions, existing, new(Month: month), invoices.CheckRecurringAsync, cancellationToken);
            if (plan.Error is not null) { return plan.Error; }
            parts.AddRange(plan.Entries.Select(x => new PlannedPart(x.Month, (Contracts.CategoryKind)x.Kind, x.SubcategoryExternalId, false, x.Amount.Amount, true)));
        }
        return null;
    }

    private sealed record PlannedPart(AccountingMonth Month, Contracts.CategoryKind Kind, Guid SubcategoryExternalId, bool IsInvoiceAdjustment, decimal Amount, bool IsForecast);
}
