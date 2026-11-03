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
        if (first.CardExternalId is null && request.Entry.IsPaid == false) { first.SetPending(); }
        await store.AddRecurringAsync(series, first, cancellationToken);
        return new(series.ExternalId, null);
    }

    public Task<RecurringEntryDetails?> GetRecurringAsync(Guid externalId, CancellationToken cancellationToken = default)
        => operations.ExecuteAsync(async token =>
        {
            var series = (await store.GetRecurringAsync(token)).FirstOrDefault(x => x.ExternalId == externalId);
            var start = series is null ? default : OccurrenceDate(series.StartDate.AddMonths(series.DateMonthOffset), series.Day);
            return series is null ? null : new RecurringEntryDetails(series.ExternalId, start, series.Day,
                series.EndMonth is { } end ? ReferenceMonth.FromDate(end.AddMonths(series.DateMonthOffset)) : null, series.IsActive,
                new(start, series.MerchantExternalId, series.SubcategoryExternalId, series.AccountExternalId, series.Amount,
                    series.Details, CardExternalId: series.CardExternalId, CardLastDigitsExternalId: series.CardLastDigitsExternalId, Reimbursement: ReimbursementOf(series)));
        }, _ => true, cancellationToken);

    public Task<OccurrenceGenerationResult> EnsureRecurringMonthAsync(ReferenceMonth month, CancellationToken cancellationToken = default)
        => operations.ExecuteAsync(token => month.Year == 0 ? Task.FromResult(new OccurrenceGenerationResult(0, 0, "Mês inválido."))
            : GenerateRecurringAsync(new(Month: month), token), x => x.Succeeded, cancellationToken);

    private async Task<OccurrenceGenerationResult> GenerateRecurringAsync(EntryFilter filter, CancellationToken cancellationToken)
    {
        var definitions = (await store.GetRecurringAsync(cancellationToken)).Where(x => x.IsActive && (!filter.PendingRecurringOnly || x.CardExternalId is null)).ToList();
        if (definitions.Count == 0) { return new(0, 0); }
        var existing = RecurringOccurrencePlanner.ExistingOccurrences(await store.GetAsync(cancellationToken));
        var plan = await new RecurringOccurrencePlanner(calendar, clock).PlanAsync(definitions, existing, filter, invoices.RequireRecurringAsync, cancellationToken);
        if (plan.Error is not null) { return new(0, 0, plan.Error); }
        await store.AddOccurrencesAsync(plan.Entries, cancellationToken);
        return new(plan.Entries.Count, plan.ProtectedCount);
    }

    internal static DateOnly FirstDay(DateOnly date) => new(date.Year, date.Month, 1);
    internal static int MonthIndex(DateOnly date) => (date.Year - 1) * 12 + date.Month - 1;
}
