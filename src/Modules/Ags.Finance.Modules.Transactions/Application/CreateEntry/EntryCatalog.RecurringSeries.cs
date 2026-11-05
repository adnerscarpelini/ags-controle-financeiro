using Ags.Finance.Modules.Transactions.Contracts;
using Ags.Finance.Modules.Transactions.Domain;
using Ags.Finance.SharedKernel;

namespace Ags.Finance.Modules.Transactions.Application.CreateEntry;

/// <summary>
/// Tela de lançamentos fixos (#125): altera a série a partir do que está em aberto, sem perguntar alcance. Em aberto é, a partir do mês atual,
/// a ocorrência pendente fora do cartão; no cartão, a ocorrência de fatura Aberta. O que já passou não muda.
/// </summary>
internal sealed partial class EntryCatalog
{
    public Task<IReadOnlyList<RecurringSeriesDetails>> GetRecurringSeriesAsync(CancellationToken cancellationToken = default)
        => operations.ExecuteAsync<IReadOnlyList<RecurringSeriesDetails>>(async token =>
        {
            var current = CurrentMonth();
            var names = (await merchants.FindAsync(string.Empty, false, token)).ToDictionary(x => x.ExternalId, x => x.Name);
            var classification = (await classifications.GetSubcategoriesAsync(false, token)).ToDictionary(x => x.ExternalId, x => x.DisplayName);
            return (await store.GetRecurringAsync(token))
                .Where(x => x.IsActive && (x.EndMonth is not { } end || end.AddMonths(x.DateMonthOffset) >= current))
                .Select(x => new RecurringSeriesDetails(x.ExternalId, x.Day, names.GetValueOrDefault(x.MerchantExternalId, "Descrição removida"), classification.GetValueOrDefault(x.SubcategoryExternalId, "Sem categoria"),
                    (Contracts.CategoryKind)x.Kind, x.Amount, x.AccountExternalId, x.CardExternalId, x.EndMonth is { } last ? ReferenceMonth.FromDate(last.AddMonths(x.DateMonthOffset)) : null, x.Details, ReimbursementOf(x)))
                .OrderBy(x => x.Day).ThenBy(x => x.MerchantName, StringComparer.CurrentCultureIgnoreCase).ToList();
        }, _ => true, cancellationToken);

    public Task<ClassificationResult> EditRecurringSeriesAsync(Guid seriesExternalId, EditRecurringSeriesRequest request, CancellationToken cancellationToken = default)
        => operations.ExecuteAsync(async token =>
        {
            if (request.Day is < 1 or > 31) { return new ClassificationResult(null, "Informe um dia de 1 a 31."); }
            if (request.Amount.Amount <= 0) { return new ClassificationResult(null, "Informe um valor maior que zero."); }
            var open = await OpenOccurrencesAsync(seriesExternalId, token);
            if (open.Error is not null) { return new ClassificationResult(null, open.Error); }
            if (open.Occurrences.Count == 0) { return new ClassificationResult(null, "Esta série não tem lançamentos em aberto para alterar."); }
            var first = open.Occurrences[0];
            var entry = ToRequest(first) with
            {
                Date = OccurrenceDate(first.Date, request.Day),
                Amount = request.Amount,
                AccountExternalId = first.CardExternalId is null && request.AccountExternalId is { } account ? account : first.AccountExternalId,
            };
            var result = await ChangeRecurringAsync(first.ExternalId, RecurringChangeScope.All, new(entry, RecurringEditScope.ThisAndFutureOpen, request.Day), token);
            return result.Succeeded ? new ClassificationResult(seriesExternalId, null) : result;
        }, x => x.Succeeded, cancellationToken);

    public Task<ClassificationResult> DeleteRecurringSeriesAsync(Guid seriesExternalId, CancellationToken cancellationToken = default)
        => operations.ExecuteAsync(async token =>
        {
            var open = await OpenOccurrencesAsync(seriesExternalId, token);
            if (open.Error is not null) { return new ClassificationResult(null, open.Error); }
            foreach (var entry in open.Occurrences)
            {
                var error = await RequireReimbursementChangeAsync(entry, true, token);
                if (error is not null) { return new ClassificationResult(null, error); }
            }

            var series = (await store.GetRecurringAsync(token)).Single(x => x.ExternalId == seriesExternalId);
            series.Deactivate();
            await store.SaveRecurringChangesAsync(series, [], open.Occurrences, token);
            await TrashDeletedOwnersAsync(open.Occurrences, await store.GetAsync(token), token);
            return new ClassificationResult(seriesExternalId, null);
        }, x => x.Succeeded, cancellationToken);

    private DateOnly CurrentMonth()
    {
        var today = DateOnly.FromDateTime(clock.GetLocalNow().DateTime);
        return new DateOnly(today.Year, today.Month, 1);
    }

    /// <summary>As ocorrências em aberto da série, da mais antiga para a mais nova. Gera antes o mês atual e o seguinte, para sempre haver de onde partir.</summary>
    private async Task<(IReadOnlyList<TransactionEntry> Occurrences, string? Error)> OpenOccurrencesAsync(Guid seriesExternalId, CancellationToken cancellationToken)
    {
        if ((await store.GetRecurringAsync(cancellationToken)).All(x => x.ExternalId != seriesExternalId)) { return ([], "Lançamento fixo não encontrado."); }
        var current = CurrentMonth();
        var generation = await GenerateRecurringAsync(new(FromDate: current, ToDate: current.AddMonths(2).AddDays(-1)), cancellationToken);
        if (!generation.Succeeded) { return ([], generation.Error); }
        var open = new List<TransactionEntry>();
        foreach (var entry in (await store.GetAsync(cancellationToken)).Where(x => x.RecurringEntryExternalId == seriesExternalId).OrderBy(x => x.OccurrenceMonth))
        {
            var isOpen = entry.CardExternalId is null ? !entry.IsEffective && entry.OccurrenceMonth >= current : await RequireSourceOpenAsync(entry, cancellationToken) is null;
            if (isOpen) { open.Add(entry); }
        }

        return (open, null);
    }
}
