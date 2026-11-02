using Ags.Finance.Modules.Transactions.Contracts;

namespace Ags.Finance.Modules.Transactions.Application.CreateEntry;

internal sealed partial class EntryCatalog
{
    public Task<ClassificationResult> ConfirmEffectiveAsync(Guid externalId, DateOnly effectiveDate, CancellationToken cancellationToken = default)
        => operations.ExecuteAsync(async token =>
        {
            var entry = (await store.GetAsync(token)).FirstOrDefault(x => x.ExternalId == externalId);
            if (entry is null || entry.CardExternalId is not null || effectiveDate == DateOnly.MinValue)
            {
                return new ClassificationResult(null, "Selecione um lançamento fora do cartão e uma data efetiva válida.");
            }
            if (entry.IsEffective) { return new ClassificationResult(entry.ExternalId, null); }
            if (entry.RecurringEntryExternalId is null) { return new ClassificationResult(null, "A baixa manual exige uma ocorrência recorrente pendente."); }
            entry.SetEffective(effectiveDate, clock.GetUtcNow());
            return await store.SaveEntriesAsync([entry], token)
                ? new ClassificationResult(entry.ExternalId, null) : new ClassificationResult(null, "Não foi possível registrar a baixa.");
        }, x => x.Succeeded, cancellationToken);
}
