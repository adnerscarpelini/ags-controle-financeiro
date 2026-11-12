using Ags.Finance.Modules.Transactions.Contracts;
using Ags.Finance.Modules.Transactions.Domain;
using Ags.Finance.SharedKernel;

namespace Ags.Finance.Modules.Transactions.Application.CreateEntry;

/// <summary>
/// Transferências e ajustes de saldo exibidos entre os lançamentos (#141). Ficam fora de receitas, despesas e totais:
/// só entram na lista quando a consulta pede e nenhum filtro próprio de lançamento (cartão, categoria, tipo) está ativo.
/// </summary>
internal sealed partial class EntryCatalog
{
    private const string TransferClassification = "Transferência";
    private const string AdjustmentClassification = "Ajuste de saldo";

    private async Task<IReadOnlyList<EntryMovement>> GetMovementsAsync(EntryFilter filter, CancellationToken cancellationToken)
    {
        if (!filter.IncludeAccountMovements || HasEntryOnlyCriteria(filter)) { return []; }
        var names = (await accounts.GetAccountsAsync(false, cancellationToken)).ToDictionary(x => x.ExternalId, x => x.Name);
        string Name(Guid id) => names.GetValueOrDefault(id, "Conta registrada");

        var occurrences = await transferForecast.GetOccurrencesAsync(GenerationMonth(filter), cancellationToken);
        var byOperation = occurrences.Where(x => x.OperationExternalId is not null).ToDictionary(x => x.OperationExternalId.GetValueOrDefault(), x => x.Occurrence);
        var movements = new List<EntryMovement>();
        var stored = await cashMovements.GetAsync(cancellationToken);
        foreach (var operation in stored.Where(x => x.Kind == Domain.AccountMovementKind.Transfer).GroupBy(x => x.OperationExternalId))
        {
            var source = operation.FirstOrDefault(x => x.Amount.IsNegative);
            var destination = operation.FirstOrDefault(x => !x.Amount.IsNegative);
            if (source is null || destination is null) { continue; }
            var occurrence = byOperation.GetValueOrDefault(operation.Key);
            movements.AddRange(TransferLegs(operation.Key, source.AccountExternalId, destination.AccountExternalId, source.Date, destination.Amount, true, occurrence, Name));
        }

        movements.AddRange(stored.Where(x => x.Kind == Domain.AccountMovementKind.BalanceAdjustment).Select(x => new EntryMovement(x.OperationExternalId,
            EntryMovementKind.BalanceAdjustment, x.AccountExternalId, null, x.Date, x.Amount, true, AdjustmentClassification, AdjustmentClassification,
            $"Saldo corrigido para {x.TargetBalance}")));
        foreach (var (occurrence, _) in occurrences.Where(x => x.OperationExternalId is null))
        {
            movements.AddRange(TransferLegs(occurrence.ExternalId, occurrence.SourceAccountExternalId, occurrence.DestinationAccountExternalId, occurrence.Date, occurrence.Amount, false, occurrence, Name));
        }

        return movements.Where(x => MatchesMovement(x, filter)).ToList();
    }

    private static IEnumerable<EntryMovement> TransferLegs(Guid operation, Guid source, Guid destination, DateOnly date, Money amount, bool isEffective,
        RecurringTransferOccurrence? occurrence, Func<Guid, string> name)
    {
        var details = occurrence is null ? string.Empty : "Recorrente";
        yield return new(operation, EntryMovementKind.TransferOut, source, destination, date, -amount, isEffective, $"Transferência para {name(destination)}",
            TransferClassification, details, occurrence);
        yield return new(operation, EntryMovementKind.TransferIn, destination, source, date, amount, isEffective, $"Transferência de {name(source)}",
            TransferClassification, details, occurrence);
    }

    /// <summary>As ocorrências recorrentes precisam existir até o fim do período consultado, como os lançamentos recorrentes.</summary>
    private ReferenceMonth GenerationMonth(EntryFilter filter) => filter.Month
        ?? ReferenceMonth.FromDate(filter.ToDate ?? filter.FromDate ?? DateOnly.FromDateTime(clock.GetLocalNow().DateTime));

    // Movimentos não têm cartão, categoria, estabelecimento, reembolso nem tipo de receita ou despesa.
    private static bool HasEntryOnlyCriteria(EntryFilter filter)
        => filter.Kind is not null || filter.HasReimbursement || filter.CardExternalId is not null || filter.CardLastDigitsExternalId is not null
            || filter.CategoryExternalId is { } category && category != Guid.Empty || filter.SubcategoryExternalId is { } subcategory && subcategory != Guid.Empty
            || filter.CardExternalIds is { Count: > 0 } || filter.CardLastDigitsExternalIds is { Count: > 0 } || filter.CategoryExternalIds is { Count: > 0 }
            || filter.SubcategoryExternalIds is { Count: > 0 } || filter.MerchantExternalIds is { Count: > 0 };

    private static bool MatchesMovement(EntryMovement movement, EntryFilter filter)
        => (filter.Month is null || ReferenceMonth.FromDate(movement.Date) == filter.Month)
            && (filter.FromDate is null || movement.Date >= filter.FromDate) && (filter.ToDate is null || movement.Date <= filter.ToDate)
            && (filter.AccountExternalId is null || filter.AccountExternalId == movement.AccountExternalId)
            && (filter.AccountExternalIds is not { Count: > 0 } accountIds || accountIds.Contains(movement.AccountExternalId))
            && (filter.IsEffective is null || filter.IsEffective == movement.IsEffective)
            && (!filter.PendingRecurringOnly || !movement.IsEffective)
            && (string.IsNullOrWhiteSpace(filter.Text)
                || SearchText.Normalize($"{movement.Description} {movement.Classification} {movement.Details}").Contains(SearchText.Normalize(filter.Text.Trim()), StringComparison.Ordinal));
}
