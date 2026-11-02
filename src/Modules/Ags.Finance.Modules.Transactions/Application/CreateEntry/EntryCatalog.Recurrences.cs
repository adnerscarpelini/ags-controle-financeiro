using Ags.Finance.Modules.Transactions.Contracts;
using Ags.Finance.Modules.Transactions.Domain;
using Ags.Finance.SharedKernel;

namespace Ags.Finance.Modules.Transactions.Application.CreateEntry;

internal sealed partial class EntryCatalog
{
    public Task<ClassificationResult> CreateRecurringAsync(CreateRecurringEntryRequest request, CancellationToken cancellationToken = default)
        => operations.ExecuteAsync(token => CreateRecurringCoreAsync(request, token), x => x.Succeeded, cancellationToken);

    private async Task<ClassificationResult> CreateRecurringCoreAsync(CreateRecurringEntryRequest request, CancellationToken cancellationToken)
    {
        if (request.EndMonth is { Year: 0 }) { return new(null, "Informe um término válido."); }
        var prepared = await PrepareAsync(request.Entry, cancellationToken);
        if (prepared.Entry is not { } first) { return new(null, prepared.Error); }
        var series = RecurringEntry.Create(first, request.EndMonth?.FirstDay);
        if (series is null) { return new(null, "O término não pode ser anterior ao início da recorrência."); }
        first.SetRecurring(series.ExternalId, FirstDay(first.Date));
        if (first.CardExternalId is null) { first.SetPending(); }
        await store.AddRecurringAsync(series, first, cancellationToken);
        return new(series.ExternalId, null);
    }

    public Task<RecurringEntryDetails?> GetRecurringAsync(Guid externalId, CancellationToken cancellationToken = default)
        => operations.ExecuteAsync(async token =>
        {
            var series = (await store.GetRecurringAsync(token)).FirstOrDefault(x => x.ExternalId == externalId);
            return series is null ? null : new RecurringEntryDetails(series.ExternalId, series.StartDate, series.Day,
                series.EndMonth is { } end ? AccountingMonth.FromDate(end) : null, series.IsActive,
                new(series.StartDate, series.MerchantExternalId, series.SubcategoryExternalId, series.AccountExternalId, series.Amount,
                    series.Details, CardExternalId: series.CardExternalId, CardLastDigitsExternalId: series.CardLastDigitsExternalId));
        }, _ => true, cancellationToken);

    public Task<OccurrenceGenerationResult> EnsureRecurringMonthAsync(AccountingMonth month, CancellationToken cancellationToken = default)
        => operations.ExecuteAsync(token => month.Year == 0 ? Task.FromResult(new OccurrenceGenerationResult(0, 0, "Mês inválido."))
            : GenerateRecurringAsync(new(Month: month), token), x => x.Succeeded, cancellationToken);

    private async Task<OccurrenceGenerationResult> GenerateRecurringAsync(EntryFilter filter, CancellationToken cancellationToken)
    {
        var definitions = (await store.GetRecurringAsync(cancellationToken)).Where(x => x.IsActive).ToList();
        if (definitions.Count == 0) { return new(0, 0); }
        var existing = (await store.GetAsync(cancellationToken)).Where(x => x.RecurringEntryExternalId is not null && x.OccurrenceMonth is not null)
            .Select(x => (Series: x.RecurringEntryExternalId!.Value, Month: x.OccurrenceMonth!.Value)).ToHashSet();
        var current = FirstDay(DateOnly.FromDateTime(clock.GetLocalNow().DateTime));
        var generated = new List<TransactionEntry>();
        var protectedCount = 0;
        foreach (var series in definitions)
        {
            var first = MonthIndex(series.StartDate);
            var from = filter.Month is { } month ? Math.Max(0, MonthIndex(month.FirstDay) - series.AccountingMonthOffset - 2) : filter.FromDate is { } fromDate ? MonthIndex(fromDate) : first;
            var to = filter.Month is { } target ? Math.Min(119987, MonthIndex(target.FirstDay) - series.AccountingMonthOffset) : filter.ToDate is { } toDate ? MonthIndex(toDate) : Math.Max(MonthIndex(current), from);
            if (filter.FromDate is { } lower) { from = Math.Max(from, MonthIndex(lower)); }
            if (filter.ToDate is { } upper) { to = Math.Min(to, MonthIndex(upper)); }
            from = Math.Max(from, first);
            if (series.EndMonth is { } end) { to = Math.Min(to, MonthIndex(end)); }
            for (var index = from; index <= to; index++)
            {
                var period = new DateOnly(index / 12 + 1, index % 12 + 1, 1);
                if (existing.Contains((series.ExternalId, period)) || series.ExcludedMonths.Contains(period)) { continue; }
                var date = new DateOnly(period.Year, period.Month, Math.Min(series.Day, DateTime.DaysInMonth(period.Year, period.Month)));
                var suggestion = await calendar.CalculateAsync(date, series.CardExternalId, cancellationToken);
                if (suggestion.Month is not { } accountingMonth)
                {
                    if (series.CardExternalId is not null && period.Year == 9999 && period.Month == 12) { continue; }
                    return new(0, 0, suggestion.Error);
                }
                var shifted = MonthIndex(accountingMonth.FirstDay) + series.AccountingMonthOffset;
                if (shifted is < 0 or >= 119988) { continue; }
                accountingMonth = new AccountingMonth(shifted / 12 + 1, shifted % 12 + 1);
                if (filter.Month is { } requested && accountingMonth != requested) { continue; }
                if (series.CardExternalId is { } card)
                {
                    var conflict = await invoices.RequireRecurringAsync(card, accountingMonth, series.CreatedAt, cancellationToken);
                    if (conflict is not null) { protectedCount++; continue; }
                }
                var entry = new TransactionEntry();
                var error = entry.Update(date, accountingMonth, series.MerchantExternalId, series.SubcategoryExternalId, series.AccountExternalId, series.Kind, series.Amount, series.Details);
                if (error is not null) { return new(0, 0, error); }
                if (series.CardExternalId is { } cardId) { entry.SetCard(cardId, series.CardLastDigitsExternalId); entry.SetEffective(date, clock.GetUtcNow()); }
                else { entry.SetPending(); }
                entry.SetRecurring(series.ExternalId, period);
                generated.Add(entry);
                existing.Add((series.ExternalId, period));
            }
        }
        await store.AddOccurrencesAsync(generated, cancellationToken);
        return new(generated.Count, protectedCount);
    }

    private static DateOnly FirstDay(DateOnly date) => new(date.Year, date.Month, 1);
    private static int MonthIndex(DateOnly date) => (date.Year - 1) * 12 + date.Month - 1;
}
