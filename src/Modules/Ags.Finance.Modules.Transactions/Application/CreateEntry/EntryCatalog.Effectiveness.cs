using Ags.Finance.Modules.Transactions.Contracts;
using Ags.Finance.Modules.Transactions.Domain;
using Ags.Finance.SharedKernel;

namespace Ags.Finance.Modules.Transactions.Application.CreateEntry;

internal sealed partial class EntryCatalog
{
    public Task<ClassificationResult> ConfirmEffectiveAsync(Guid externalId, DateOnly effectiveDate, CancellationToken cancellationToken = default)
        => operations.ExecuteAsync(async token =>
        {
            var entry = (await store.GetAsync(token)).FirstOrDefault(x => x.ExternalId == externalId);
            return entry is null ? new ClassificationResult(null, "Lançamento não encontrado.")
                : await ConfirmEffectiveCoreAsync(externalId, new(effectiveDate, new Money(Math.Abs(entry.Amount.Amount)), entry.AccountExternalId), token);
        }, x => x.Succeeded, cancellationToken);

    public Task<ClassificationResult> ConfirmEffectiveAsync(Guid externalId, ConfirmEffectiveRequest request, CancellationToken cancellationToken = default)
        => operations.ExecuteAsync(token => ConfirmEffectiveCoreAsync(externalId, request, token), x => x.Succeeded, cancellationToken);

    private async Task<ClassificationResult> ConfirmEffectiveCoreAsync(Guid externalId, ConfirmEffectiveRequest request, CancellationToken cancellationToken)
    {
        var entry = (await store.GetAsync(cancellationToken)).FirstOrDefault(x => x.ExternalId == externalId);
        if (entry is null || entry.CardExternalId is not null || request.Date == DateOnly.MinValue)
        {
            return new(null, "Selecione um lançamento fora do cartão e uma data de baixa válida.");
        }
        if (entry.IsEffective) { return new(entry.ExternalId, null); }
        var prepared = await PrepareAsync(ToRequest(entry) with { Date = request.Date, Amount = request.Amount, AccountExternalId = request.AccountExternalId }, cancellationToken, entry);
        if (prepared.Entry is not { } updated) { return new(null, prepared.Error); }
        var error = await RequireReimbursementChangeAsync(entry, ChangesProtectedData(entry, updated, false), cancellationToken);
        if (error is not null) { return new(null, error); }
        entry.ApplyChange(updated, false);
        entry.SetEffective(request.Date, clock.GetUtcNow());
        return await store.SaveEntriesAsync([entry], cancellationToken) ? new(entry.ExternalId, null) : new(null, "Não foi possível registrar a baixa.");
    }

    public Task<ClassificationResult> UndoEffectiveAsync(Guid externalId, CancellationToken cancellationToken = default)
        => operations.ExecuteAsync(async token =>
        {
            var entry = (await store.GetAsync(token)).FirstOrDefault(x => x.ExternalId == externalId);
            if (entry is null || entry.CardExternalId is not null) { return new ClassificationResult(null, "Selecione um lançamento fora do cartão."); }
            entry.SetPending();
            return await store.SaveEntriesAsync([entry], token) ? new ClassificationResult(externalId, null) : new ClassificationResult(null, "Não foi possível desfazer a baixa.");
        }, x => x.Succeeded, cancellationToken);

    private void ApplyPaidState(TransactionEntry entry, bool? isPaid)
    {
        if (entry.CardExternalId is not null || isPaid is null) { return; }
        if (!isPaid.Value) { entry.SetPending(); }
        else if (!entry.IsEffective) { entry.SetEffective(entry.Date, clock.GetUtcNow()); }
    }
}
