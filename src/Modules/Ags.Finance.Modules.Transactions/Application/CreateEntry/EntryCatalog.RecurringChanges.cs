using Ags.Finance.Modules.Transactions.Contracts;
using Ags.Finance.Modules.Transactions.Domain;
using Ags.Finance.SharedKernel;

namespace Ags.Finance.Modules.Transactions.Application.CreateEntry;

internal sealed partial class EntryCatalog
{
    public Task<ClassificationResult> EditRecurringAsync(Guid externalId, EditRecurringEntryRequest request, CancellationToken cancellationToken = default)
        => operations.ExecuteAsync(token => ChangeRecurringAsync(externalId, request.Scope, request, token), x => x.Succeeded, cancellationToken);

    public Task<ClassificationResult> DeleteRecurringAsync(Guid externalId, RecurringChangeScope scope, CancellationToken cancellationToken = default)
        => operations.ExecuteAsync(token => ChangeRecurringAsync(externalId, scope, null, token), x => x.Succeeded, cancellationToken);

    private async Task<ClassificationResult> ChangeRecurringAsync(Guid externalId, RecurringChangeScope scope, EditRecurringEntryRequest? change, CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(scope)) { return new(null, "Escolha o alcance da recorrência."); }
        var generation = await GenerateRecurringAsync(new(), cancellationToken);
        if (!generation.Succeeded) { return new(null, generation.Error); }
        var all = await store.GetAsync(cancellationToken);
        var selected = all.FirstOrDefault(x => x.ExternalId == externalId);
        if (selected?.RecurringEntryExternalId is not { } seriesId || selected.OccurrenceMonth is not { } period)
        {
            return new(null, "Ocorrência recorrente não encontrada.");
        }
        var sourceError = await RequireSourceOpenAsync(selected, cancellationToken);
        if (sourceError is not null) { return new(null, sourceError); }
        var series = (await store.GetRecurringAsync(cancellationToken)).Single(x => x.ExternalId == seriesId);
        var today = DateOnly.FromDateTime(clock.GetLocalNow().DateTime);
        var affected = all.Where(x => x.RecurringEntryExternalId == seriesId && (scope switch
        {
            RecurringChangeScope.OnlyThis => x.ExternalId == externalId,
            RecurringChangeScope.ThisAndFollowing => x.OccurrenceMonth >= period,
            _ => !(x.IsEffective && x.Date < today)
        })).ToList();
        foreach (var entry in affected)
        {
            var error = await RequireSourceOpenAsync(entry, cancellationToken);
            if (change is null) { error ??= await RequireReimbursementChangeAsync(entry, true, cancellationToken); }
            if (error is not null) { return new(null, error); }
        }
        if (change is null)
        {
            if (scope == RecurringChangeScope.OnlyThis) { series.Exclude(period); }
            else if (scope == RecurringChangeScope.ThisAndFollowing) { series.StopBefore(period); }
            else { series.Deactivate(); }
            await store.SaveRecurringChangesAsync(series, null, [], affected, cancellationToken);
            await TrashDeletedOwnersAsync(affected, all, cancellationToken);
            return new(externalId, null);
        }
        var request = change.Entry;
        var baseline = ToRequest(selected);
        var day = change.Day ?? (request.Date == baseline.Date ? series.Day : request.Date.Day);
        if (day is < 1 or > 31 || request.Month is { Year: 0 }) { return new(null, "Informe dia e mês válidos."); }
        if (scope != RecurringChangeScope.OnlyThis && change.Day is not null) { request = request with { Date = OccurrenceDate(request.Date, day) }; }
        request = request with { Month = null };
        var dateShift = MonthIndex(request.Date) - MonthIndex(baseline.Date);
        var dateOffset = series.DateMonthOffset + dateShift;
        var dateChanged = request.Date != baseline.Date || day != series.Day;
        var preparedSelected = await PrepareAsync(request, cancellationToken, selected);
        if (preparedSelected.Entry is not { } selectedTemplate) { return new(null, preparedSelected.Error); }
        RecurringEntry? following = null;
        if (scope != RecurringChangeScope.OnlyThis)
        {
            var startIndex = MonthIndex(scope == RecurringChangeScope.All ? series.StartDate : period) + dateOffset;
            var selectedIndex = MonthIndex(period) + dateOffset;
            if (startIndex is < 0 or >= 119988 || selectedIndex is < 0 or >= 119988 || (series.EndMonth is { } end && MonthIndex(end) + dateOffset is < 0 or >= 119988))
            {
                return new(null, "A alteração ultrapassa o calendário suportado.");
            }
            // The selected occurrence may be an exception. Unchanged fields keep the series configuration.
            var templateRequest = ChangedRecurringRequest(request, baseline, new(series.StartDate, series.MerchantExternalId,
                series.SubcategoryExternalId, series.AccountExternalId, series.Amount, series.Details,
                selected.Month, series.CardExternalId, series.CardLastDigitsExternalId, ReimbursementOf(series))) with
            { Date = request.Date, Month = selectedTemplate.Month };
            var prepared = await PrepareAsync(templateRequest, cancellationToken, selected);
            if (prepared.Entry is not { } template) { return new(null, prepared.Error); }
            const int Offset = 0;
            if (scope == RecurringChangeScope.ThisAndFollowing)
            {
                var firstDate = OccurrenceDate(period, day);
                var prototype = new TransactionEntry();
                var error = prototype.Update(firstDate, template.Month, template.MerchantExternalId, template.SubcategoryExternalId,
                    template.AccountExternalId, template.Kind, new Money(Math.Abs(template.Amount.Amount)), template.Details);
                if (error is not null) { return new(null, error); }
                if (template.CardExternalId is { } card) { prototype.SetCard(card, template.CardLastDigitsExternalId); }
                following = RecurringEntry.Create(prototype, series.EndMonth);
                if (following is null) { return new(null, "A ocorrência está após o término da série."); }
                following.Configure(template, day, Offset, dateOffset);
                foreach (var excluded in series.ExcludedMonths.Where(x => x >= period)) { following.Exclude(excluded); }
                series.StopBefore(period);
            }
            else { series.Configure(template, day, Offset, dateOffset); }
        }
        foreach (var entry in affected)
        {
            var index = MonthIndex(entry.Month.FirstDay);
            var dateIndex = MonthIndex(entry.Date) + dateShift;
            if (index is < 0 or >= 119988 || dateIndex is < 0 or >= 119988) { return new(null, "A alteração ultrapassa o calendário suportado."); }
            var changed = scope == RecurringChangeScope.OnlyThis ? request : ChangedRecurringRequest(request, baseline, ToRequest(entry)) with
            {
                Date = dateChanged ? entry.ExternalId == externalId ? request.Date : OccurrenceDate(new DateOnly(dateIndex / 12 + 1, dateIndex % 12 + 1, 1), day) : entry.Date,
                Month = null
            };
            var prepared = await PrepareAsync(changed, cancellationToken, entry);
            if (prepared.Entry is not { } updated) { return new(null, prepared.Error); }
            var reimbursementError = await RequireReimbursementChangeAsync(entry, ChangesProtectedData(entry, updated, false), cancellationToken);
            if (reimbursementError is not null) { return new(null, reimbursementError); }
            entry.ApplyChange(updated, false);
            if (entry.ExternalId == externalId) { ApplyPaidState(entry, request.IsPaid); }
            if (following is not null) { entry.SetRecurring(following.ExternalId, entry.OccurrenceMonth!.Value); }
        }
        await store.SaveRecurringChangesAsync(series, following, affected, [], cancellationToken);
        return new(externalId, null);
    }

    internal static DateOnly OccurrenceDate(DateOnly period, int day) => new(period.Year, period.Month, Math.Min(day, DateTime.DaysInMonth(period.Year, period.Month)));

    private static CreateEntryRequest ChangedRecurringRequest(CreateEntryRequest request, CreateEntryRequest baseline, CreateEntryRequest original) => original with
    {
        MerchantExternalId = request.MerchantExternalId == baseline.MerchantExternalId ? original.MerchantExternalId : request.MerchantExternalId,
        SubcategoryExternalId = request.SubcategoryExternalId == baseline.SubcategoryExternalId ? original.SubcategoryExternalId : request.SubcategoryExternalId,
        AccountExternalId = request.AccountExternalId == baseline.AccountExternalId ? original.AccountExternalId : request.AccountExternalId,
        CardExternalId = request.CardExternalId == baseline.CardExternalId ? original.CardExternalId : request.CardExternalId,
        CardLastDigitsExternalId = request.CardLastDigitsExternalId == baseline.CardLastDigitsExternalId ? original.CardLastDigitsExternalId : request.CardLastDigitsExternalId,
        Amount = request.Amount == baseline.Amount ? original.Amount : request.Amount,
        Details = request.Details == baseline.Details ? original.Details : request.Details,
        Reimbursement = request.Reimbursement == baseline.Reimbursement ? original.Reimbursement : request.Reimbursement
    };
}
