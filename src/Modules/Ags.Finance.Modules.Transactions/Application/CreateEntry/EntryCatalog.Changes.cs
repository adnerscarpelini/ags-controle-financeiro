using Ags.Finance.Modules.Transactions.Contracts;
using Ags.Finance.Modules.Transactions.Domain;
using Ags.Finance.SharedKernel;

namespace Ags.Finance.Modules.Transactions.Application.CreateEntry;

internal sealed partial class EntryCatalog
{
    public Task<ClassificationResult> EditAsync(Guid externalId, CreateEntryRequest request, EntryChangeScope scope, CancellationToken cancellationToken = default)
        => operations.ExecuteAsync(token => EditCoreAsync(externalId, request, scope, token), x => x.Succeeded, cancellationToken);

    private async Task<ClassificationResult> EditCoreAsync(Guid externalId, CreateEntryRequest request, EntryChangeScope scope, CancellationToken cancellationToken)
    {
        var all = await store.GetAsync(cancellationToken);
        var selected = all.FirstOrDefault(x => x.ExternalId == externalId);
        if (selected is null || !Enum.IsDefined(scope) || (scope == EntryChangeScope.ThisAndFollowing && selected.InstallmentPurchaseExternalId is null))
        {
            return new(null, "Selecione um lançamento existente e um alcance válido.");
        }
        if (selected.RecurringEntryExternalId is not null) { return new(null, "Escolha o alcance da recorrência para alterar esta ocorrência."); }
        var affected = Affected(all, selected, scope);
        var dateOrCardChanged = request.Date != selected.Date || request.CardExternalId != selected.CardExternalId;
        var month = request.Month ?? selected.Month;
        if (dateOrCardChanged && !request.UseSelectedInvoice)
        {
            var calculated = await calendar.CalculateAsync(request.Date, request.CardExternalId, cancellationToken);
            if (calculated.Month is not { } calculatedMonth) { return new(null, calculated.Error); }
            var offset = request.CardExternalId is not null ? (selected.InstallmentNumber ?? 1) - 1 : 0;
            if (MonthIndex(calculatedMonth.FirstDay) + offset >= 119988) { return new(null, "A alteração ultrapassa o calendário suportado."); }
            month = calculatedMonth.AddMonths(offset);
        }
        if (month.Year == 0) { return new(null, "Informe um mês contábil válido."); }
        var shift = (month.Year - selected.Month.Year) * 12 + month.Month - selected.Month.Month;
        var selectedAmount = new Money(Math.Abs(selected.Amount.Amount));
        var baseline = ToRequest(selected);
        foreach (var entry in affected)
        {
            var error = await RequireSourceOpenAsync(entry, cancellationToken);
            if (error is not null) { return new(null, error); }
            if (dateOrCardChanged && await invoices.IsAnticipatedAsync(entry.ExternalId, cancellationToken)) { return new(null, "Uma parcela antecipada não permite alterar a data ou o cartão."); }
            var monthIndex = (entry.Month.Year - 1) * 12 + entry.Month.Month - 1 + shift;
            if (monthIndex is < 0 or >= 119988) { return new(null, "A mudança ultrapassa o calendário suportado."); }
            var changed = request with
            {
                Date = request.Date == baseline.Date ? entry.Date : request.Date,
                Month = new AccountingMonth(monthIndex / 12 + 1, monthIndex % 12 + 1),
                MerchantExternalId = request.MerchantExternalId == baseline.MerchantExternalId ? entry.MerchantExternalId : request.MerchantExternalId,
                SubcategoryExternalId = request.SubcategoryExternalId == baseline.SubcategoryExternalId ? entry.SubcategoryExternalId : request.SubcategoryExternalId,
                AccountExternalId = request.AccountExternalId == baseline.AccountExternalId ? entry.AccountExternalId : request.AccountExternalId,
                CardExternalId = request.CardExternalId == baseline.CardExternalId ? entry.CardExternalId : request.CardExternalId,
                CardLastDigitsExternalId = request.CardLastDigitsExternalId == baseline.CardLastDigitsExternalId ? entry.CardLastDigitsExternalId : request.CardLastDigitsExternalId,
                Amount = request.Amount == selectedAmount ? new Money(Math.Abs(entry.Amount.Amount)) : request.Amount,
                Details = request.Details == baseline.Details ? entry.Details : request.Details,
                Reimbursement = request.Reimbursement == baseline.Reimbursement ? ReimbursementOf(entry) : request.Reimbursement
            };
            var prepared = await PrepareAsync(changed, cancellationToken, entry);
            if (prepared.Entry is not { } updated) { return new(null, prepared.Error); }
            var keepAmount = request.Amount == selectedAmount && request.SubcategoryExternalId == baseline.SubcategoryExternalId;
            error = await RequireReimbursementChangeAsync(entry, ChangesProtectedData(entry, updated, keepAmount), cancellationToken);
            if (error is not null) { return new(null, error); }
            entry.ApplyChange(updated, keepAmount);
            ApplyPaidState(entry, request.IsPaid);
        }
        return await store.SaveEntriesAsync(affected, cancellationToken) ? new(externalId, null) : new(null, "Não foi possível salvar todos os lançamentos.");
    }

    public Task<ClassificationResult> DeleteAsync(Guid externalId, EntryChangeScope scope, CancellationToken cancellationToken = default)
        => operations.ExecuteAsync(token => DeleteCoreAsync(externalId, scope, token), x => x.Succeeded, cancellationToken);

    private async Task<ClassificationResult> DeleteCoreAsync(Guid externalId, EntryChangeScope scope, CancellationToken cancellationToken)
    {
        var all = await store.GetAsync(cancellationToken);
        var selected = all.FirstOrDefault(x => x.ExternalId == externalId);
        if (selected is null || !Enum.IsDefined(scope) || (scope == EntryChangeScope.ThisAndFollowing && selected.InstallmentPurchaseExternalId is null))
        {
            return new(null, "Selecione um lançamento existente e um alcance válido.");
        }
        if (selected.RecurringEntryExternalId is not null) { return new(null, "Escolha o alcance da recorrência para alterar esta ocorrência."); }
        var affected = Affected(all, selected, scope);
        foreach (var entry in affected)
        {
            var error = await RequireSourceOpenAsync(entry, cancellationToken);
            error ??= await RequireReimbursementChangeAsync(entry, true, cancellationToken);
            if (error is not null) { return new(null, error); }
        }
        await store.DeleteEntriesAsync(affected, cancellationToken);
        await TrashDeletedOwnersAsync(affected, all, cancellationToken);
        return new(externalId, null);
    }

    public Task<ClassificationResult> DuplicateAsync(Guid externalId, CancellationToken cancellationToken = default)
        => operations.ExecuteAsync(async token =>
        {
            var source = (await store.GetAsync(token)).FirstOrDefault(x => x.ExternalId == externalId);
            if (source is null) { return new ClassificationResult(null, "Lançamento não encontrado."); }
            return await CreateCoreAsync(new(DateOnly.FromDateTime(clock.GetLocalNow().DateTime), source.MerchantExternalId,
                source.SubcategoryExternalId, source.AccountExternalId, new Money(Math.Abs(source.Amount.Amount)), source.Details,
                source.Month, source.CardExternalId, source.CardLastDigitsExternalId, ReimbursementOf(source)), token);
        }, x => x.Succeeded, cancellationToken);

    private static CreateEntryRequest ToRequest(TransactionEntry entry)
        => new(entry.Date, entry.MerchantExternalId, entry.SubcategoryExternalId, entry.AccountExternalId, new Money(Math.Abs(entry.Amount.Amount)), entry.Details, entry.Month, entry.CardExternalId, entry.CardLastDigitsExternalId, ReimbursementOf(entry));

    private Task<string?> RequireSourceOpenAsync(TransactionEntry entry, CancellationToken cancellationToken)
        => entry.CardExternalId is { } card ? invoices.RequireOpenAsync(card, entry.Month, cancellationToken) : Task.FromResult<string?>(null);

    private async Task<string?> RequireReimbursementChangeAsync(TransactionEntry entry, bool changesProtectedData, CancellationToken cancellationToken)
        => await reimbursements.RequireEntryChangeAsync(entry.ExternalId, changesProtectedData, cancellationToken) is { } error ? $"Lançamento de {entry.Date:dd/MM/yyyy}: {error}" : null;

    private static bool ChangesProtectedData(TransactionEntry entry, TransactionEntry updated, bool keepAmount)
        => (!keepAmount && updated.Amount != entry.Amount) || updated.Kind != entry.Kind
            || updated.ReimbursementPersonExternalId != entry.ReimbursementPersonExternalId || updated.ReimbursementPercent != entry.ReimbursementPercent;

    private static List<TransactionEntry> Affected(IReadOnlyList<TransactionEntry> entries, TransactionEntry selected, EntryChangeScope scope)
        => scope == EntryChangeScope.OnlyThis ? [selected] : entries.Where(x => x.InstallmentPurchaseExternalId == selected.InstallmentPurchaseExternalId && x.InstallmentNumber >= selected.InstallmentNumber)
            .OrderBy(x => x.InstallmentNumber).ToList();
}
