using Ags.Finance.Modules.Transactions.Contracts;
using Ags.Finance.Modules.Transactions.Domain;

namespace Ags.Finance.Modules.Transactions.Application.CreateEntry;

internal sealed partial class EntryCatalog
{
    public Task<ClassificationResult> CreateRepeatedAsync(CreateEntryRequest request, int count, Contracts.RepeatPeriod period, CancellationToken cancellationToken = default)
        => operations.ExecuteAsync(async token =>
        {
            if (request.CardExternalId is not null) { return new ClassificationResult(null, "Repetir exige um lançamento fora do cartão; compras no cartão usam parcelas."); }
            var repetition = EntryRepetition.Create(count, (Domain.RepeatPeriod)period);
            if (repetition is null) { return new ClassificationResult(null, "Informe de 2 a 120 vezes e um período válido."); }
            if (repetition.DateAt(request.Date, count - 1) is null) { return new ClassificationResult(null, "A repetição ultrapassa o calendário suportado."); }
            var entries = new List<TransactionEntry>();
            for (var index = 0; index < count; index++)
            {
                var prepared = await PrepareAsync(request with { Date = repetition.DateAt(request.Date, index)!.Value, Month = null }, token);
                if (prepared.Entry is not { } entry) { return new ClassificationResult(null, prepared.Error); }
                entry.SetRepetition(repetition.ExternalId, index + 1);
                if (index > 0 || request.IsPaid == false) { entry.SetPending(); }
                entries.Add(entry);
            }
            return await store.AddRepetitionAsync(repetition, entries, token) ? new ClassificationResult(repetition.ExternalId, null) : new ClassificationResult(null, "Não foi possível gravar a repetição inteira.");
        }, x => x.Succeeded, cancellationToken);
}
