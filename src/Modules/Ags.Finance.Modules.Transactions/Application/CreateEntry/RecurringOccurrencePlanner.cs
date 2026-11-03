using Ags.Finance.Modules.Accounts.Contracts;
using Ags.Finance.Modules.Transactions.Contracts;
using Ags.Finance.Modules.Transactions.Domain;
using Ags.Finance.SharedKernel;

namespace Ags.Finance.Modules.Transactions.Application.CreateEntry;

internal sealed record PlannedOccurrences(IReadOnlyList<TransactionEntry> Entries, int ProtectedCount, string? Error = null);

/// <summary>Conflito de fatura que impede a ocorrência; <c>null</c> quando ela é aceita.</summary>
internal delegate Task<string?> RecurringInvoiceCheck(Guid cardExternalId, AccountingMonth month, DateTimeOffset plannedAt, CancellationToken cancellationToken);

/// <summary>
/// Calcula as ocorrências recorrentes de um filtro sem gravá-las. É a regra única da geração (#16) e da projeção do planejamento (#77),
/// para o previsto não divergir do que depois será gravado. Quem grava é o chamador, assim como a escolha de <see cref="RecurringInvoiceCheck"/>.
/// </summary>
internal sealed class RecurringOccurrencePlanner(IAccountingMonthCalculator calendar, TimeProvider clock)
{
    public static HashSet<(Guid Series, DateOnly Month)> ExistingOccurrences(IEnumerable<TransactionEntry> entries)
        => entries.Where(x => x.RecurringEntryExternalId is not null && x.OccurrenceMonth is not null)
            .Select(x => (Series: x.RecurringEntryExternalId!.Value, Month: x.OccurrenceMonth!.Value)).ToHashSet();

    /// <summary>As ocorrências calculadas entram em <paramref name="existing"/>, então chamadas seguidas com o mesmo conjunto não repetem uma ocorrência.</summary>
    public async Task<PlannedOccurrences> PlanAsync(IReadOnlyList<RecurringEntry> definitions, HashSet<(Guid Series, DateOnly Month)> existing, EntryFilter filter, RecurringInvoiceCheck check, CancellationToken cancellationToken)
    {
        var current = EntryCatalog.FirstDay(DateOnly.FromDateTime(clock.GetLocalNow().DateTime));
        var generated = new List<TransactionEntry>();
        var protectedCount = 0;
        foreach (var series in definitions)
        {
            var first = EntryCatalog.MonthIndex(series.StartDate);
            var from = filter.Month is { } month ? EntryCatalog.MonthIndex(month.FirstDay) - series.AccountingMonthOffset - series.DateMonthOffset - 2 : filter.FromDate is { } fromDate ? EntryCatalog.MonthIndex(fromDate) - series.DateMonthOffset : first;
            var to = filter.Month is { } target ? EntryCatalog.MonthIndex(target.FirstDay) - series.AccountingMonthOffset - series.DateMonthOffset : filter.ToDate is { } toDate ? EntryCatalog.MonthIndex(toDate) - series.DateMonthOffset : Math.Max(EntryCatalog.MonthIndex(current) - series.DateMonthOffset, from);
            if (filter.FromDate is { } lower) { from = Math.Max(from, EntryCatalog.MonthIndex(lower) - series.DateMonthOffset); }
            if (filter.ToDate is { } upper) { to = Math.Min(to, EntryCatalog.MonthIndex(upper) - series.DateMonthOffset); }
            from = Math.Max(0, from);
            to = Math.Min(119987, to);
            from = Math.Max(from, first);
            if (series.EndMonth is { } end) { to = Math.Min(to, EntryCatalog.MonthIndex(end)); }
            for (var index = from; index <= to; index++)
            {
                var period = new DateOnly(index / 12 + 1, index % 12 + 1, 1);
                if (existing.Contains((series.ExternalId, period)) || series.ExcludedMonths.Contains(period)) { continue; }
                var dateIndex = index + series.DateMonthOffset;
                if (dateIndex is < 0 or >= 119988) { continue; }
                var date = EntryCatalog.OccurrenceDate(new DateOnly(dateIndex / 12 + 1, dateIndex % 12 + 1, 1), series.Day);
                var suggestion = await calendar.CalculateAsync(date, series.CardExternalId, cancellationToken);
                if (suggestion.Month is not { } accountingMonth)
                {
                    if (series.CardExternalId is not null && period.Year == 9999 && period.Month == 12) { continue; }
                    return new([], 0, suggestion.Error);
                }
                var shifted = EntryCatalog.MonthIndex(accountingMonth.FirstDay) + series.AccountingMonthOffset;
                if (shifted is < 0 or >= 119988) { continue; }
                accountingMonth = new AccountingMonth(shifted / 12 + 1, shifted % 12 + 1);
                if (filter.Month is { } requested && accountingMonth != requested) { continue; }
                if (series.CardExternalId is { } card)
                {
                    var conflict = await check(card, accountingMonth, series.CreatedAt, cancellationToken);
                    if (conflict is not null) { protectedCount++; continue; }
                }
                var entry = new TransactionEntry();
                var error = entry.Update(date, accountingMonth, series.MerchantExternalId, series.SubcategoryExternalId, series.AccountExternalId, series.Kind, series.Amount, series.Details);
                error ??= entry.SetReimbursement(series.ReimbursementPersonExternalId, series.ReimbursementPercent);
                if (error is not null) { return new([], 0, error); }
                if (series.CardExternalId is { } cardId) { entry.SetCard(cardId, series.CardLastDigitsExternalId); entry.SetEffective(date, clock.GetUtcNow()); }
                else { entry.SetPending(); }
                entry.SetRecurring(series.ExternalId, period);
                generated.Add(entry);
                existing.Add((series.ExternalId, period));
            }
        }
        return new(generated, protectedCount);
    }
}
