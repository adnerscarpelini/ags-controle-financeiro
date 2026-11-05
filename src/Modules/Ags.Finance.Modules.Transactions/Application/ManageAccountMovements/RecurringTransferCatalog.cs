using Ags.Finance.Modules.Accounts.Contracts;
using Ags.Finance.Modules.Transactions.Application.CreateEntry;
using Ags.Finance.Modules.Transactions.Contracts;
using Ags.Finance.Modules.Transactions.Domain;
using Ags.Finance.SharedKernel;

namespace Ags.Finance.Modules.Transactions.Application.ManageAccountMovements;

/// <summary>O efeito das transferências pendentes no saldo previsto de uma conta, dentro da operação de quem pergunta.</summary>
internal interface IRecurringTransferForecast
{
    Task<Money> GetPendingEffectAsync(Guid accountExternalId, ReferenceMonth month, CancellationToken cancellationToken);
}

internal sealed class RecurringTransferCatalog(IRecurringTransferStore store, IAccountMovementStore movements, IAccountCatalog accounts, IFinancialOperation operations, TimeProvider clock)
    : IRecurringTransferCatalog, IRecurringTransferForecast
{
    private const string NotFound = "Transferência recorrente não encontrada.";

    public Task<ClassificationResult> CreateAsync(CreateRecurringTransferRequest request, CancellationToken cancellationToken = default)
        => operations.ExecuteAsync(async token =>
        {
            var error = await TransferRules.CheckAsync(accounts, request.SourceAccountExternalId, request.DestinationAccountExternalId, request.Date, request.Amount, token);
            if (error is not null) { return new ClassificationResult(null, error); }
            var series = RecurringTransfer.Create(request.SourceAccountExternalId, request.DestinationAccountExternalId, request.Date, request.Amount, request.EndMonth?.FirstDay);
            if (series is null) { return new ClassificationResult(null, "O término não pode ser anterior ao início."); }
            var first = TransferOccurrence.Pending(series, series.StartMonth, request.Date);
            var legs = request.IsConfirmed ? ConfirmCore(first, request.Date, request.Amount) : [];
            await store.AddSeriesAsync(series, first, token);
            if (legs.Count > 0) { await movements.AddAsync(legs, token); }
            return new ClassificationResult(series.ExternalId, null);
        }, x => x.Succeeded, cancellationToken);

    public Task<bool> HasSeriesAsync(CancellationToken cancellationToken = default)
        => operations.ExecuteAsync(async token => (await store.GetSeriesAsync(token)).Count > 0, _ => true, cancellationToken);

    public Task<IReadOnlyList<RecurringTransferOccurrence>> GetMonthAsync(ReferenceMonth month, CancellationToken cancellationToken = default)
        => operations.ExecuteAsync<IReadOnlyList<RecurringTransferOccurrence>>(async token =>
        {
            var occurrences = await EnsureThroughAsync(month.FirstDay, token);
            return occurrences.Where(x => x.OccurrenceMonth == month.FirstDay).OrderBy(x => x.Date).ThenBy(x => x.Id).Select(Describe).ToList();
        }, _ => true, cancellationToken);

    public Task<IReadOnlyList<RecurringTransferOccurrence>> GetPendingAsync(DateOnly? from, DateOnly to, CancellationToken cancellationToken = default)
        => operations.ExecuteAsync<IReadOnlyList<RecurringTransferOccurrence>>(async token =>
        {
            var occurrences = await EnsureThroughAsync(new DateOnly(to.Year, to.Month, 1), token);
            return occurrences.Where(x => !x.IsConfirmed && x.Date <= to && (from is null || x.Date >= from)).OrderBy(x => x.Date).ThenBy(x => x.Id).Select(Describe).ToList();
        }, _ => true, cancellationToken);

    public async Task<Money> GetPendingEffectAsync(Guid accountExternalId, ReferenceMonth month, CancellationToken cancellationToken)
    {
        var occurrences = await EnsureThroughAsync(month.FirstDay, cancellationToken);
        var pending = occurrences.Where(x => !x.IsConfirmed && x.OccurrenceMonth == month.FirstDay).ToList();
        return new Money(pending.Where(x => x.DestinationAccountExternalId == accountExternalId).Sum(x => x.Amount.Amount)
            - pending.Where(x => x.SourceAccountExternalId == accountExternalId).Sum(x => x.Amount.Amount));
    }

    public Task<ClassificationResult> ConfirmAsync(Guid occurrenceExternalId, DateOnly date, Money amount, CancellationToken cancellationToken = default)
        => operations.ExecuteAsync(async token =>
        {
            var occurrence = (await store.GetOccurrencesAsync(token)).FirstOrDefault(x => x.ExternalId == occurrenceExternalId);
            if (occurrence is null) { return new ClassificationResult(null, NotFound); }
            if (occurrence.IsConfirmed) { return new ClassificationResult(occurrence.ExternalId, null); }
            var error = await TransferRules.CheckAsync(accounts, occurrence.SourceAccountExternalId, occurrence.DestinationAccountExternalId, date, amount, token);
            if (error is not null) { return new ClassificationResult(null, error); }
            var legs = ConfirmCore(occurrence, date, amount);
            await movements.AddAsync(legs, token);
            await store.SaveAsync(null, [occurrence], [], token);
            return new ClassificationResult(occurrence.ExternalId, null);
        }, x => x.Succeeded, cancellationToken);

    public Task<ClassificationResult> UndoAsync(Guid occurrenceExternalId, CancellationToken cancellationToken = default)
        => operations.ExecuteAsync(async token =>
        {
            var occurrence = (await store.GetOccurrencesAsync(token)).FirstOrDefault(x => x.ExternalId == occurrenceExternalId);
            if (occurrence is null) { return new ClassificationResult(null, NotFound); }
            if (!occurrence.IsConfirmed) { return new ClassificationResult(occurrence.ExternalId, null); }
            var legs = (await movements.GetAsync(token)).Where(x => x.OperationExternalId == occurrence.OperationExternalId).ToList();
            await movements.DeleteAsync(legs, token);
            occurrence.Undo();
            await store.SaveAsync(null, [occurrence], [], token);
            return new ClassificationResult(occurrence.ExternalId, null);
        }, x => x.Succeeded, cancellationToken);

    public Task<ClassificationResult> EditAsync(Guid occurrenceExternalId, EditRecurringTransferRequest request, CancellationToken cancellationToken = default)
        => operations.ExecuteAsync(token => EditCoreAsync(occurrenceExternalId, request, token), x => x.Succeeded, cancellationToken);

    private async Task<ClassificationResult> EditCoreAsync(Guid occurrenceExternalId, EditRecurringTransferRequest request, CancellationToken cancellationToken)
    {
        if (request.Scope is not (RecurringEditScope.OnlyThis or RecurringEditScope.ThisAndFutureOpen)) { return new(null, "Escolha o alcance da alteração."); }
        var all = await EnsureThroughAsync(new DateOnly(request.Date.Year, request.Date.Month, 1), cancellationToken);
        var selected = all.FirstOrDefault(x => x.ExternalId == occurrenceExternalId);
        if (selected is null) { return new(null, NotFound); }
        if (selected.IsConfirmed) { return new(null, "Desfaça a transferência antes de editar."); }
        var error = await TransferRules.CheckAsync(accounts, request.SourceAccountExternalId, request.DestinationAccountExternalId, request.Date, request.Amount, cancellationToken);
        if (error is not null) { return new(null, error); }
        if (request.Scope == RecurringEditScope.OnlyThis)
        {
            selected.Change(request.SourceAccountExternalId, request.DestinationAccountExternalId, request.Date, request.Amount);
            await store.SaveAsync(null, [selected], [], cancellationToken);
            return new(selected.ExternalId, null);
        }

        var series = (await store.GetSeriesAsync(cancellationToken)).Single(x => x.ExternalId == selected.RecurringTransferExternalId);
        var day = request.Day ?? (request.Date == selected.Date ? series.Day : request.Date.Day);
        if (day is < 1 or > 31) { return new(null, "Informe um dia válido."); }

        // Só o que mudou se propaga: uma exceção feita antes em outro mês continua valendo nos demais campos.
        var sourceChanged = request.SourceAccountExternalId != selected.SourceAccountExternalId;
        var destinationChanged = request.DestinationAccountExternalId != selected.DestinationAccountExternalId;
        var amountChanged = request.Amount != selected.Amount;
        var dateChanged = request.Date != selected.Date || day != series.Day;
        var later = all.Where(x => x.RecurringTransferExternalId == series.ExternalId && x.OccurrenceMonth > selected.OccurrenceMonth && !x.IsConfirmed).ToList();
        foreach (var occurrence in later)
        {
            occurrence.Change(sourceChanged ? request.SourceAccountExternalId : occurrence.SourceAccountExternalId,
                destinationChanged ? request.DestinationAccountExternalId : occurrence.DestinationAccountExternalId,
                dateChanged ? EntryCatalog.OccurrenceDate(occurrence.OccurrenceMonth, day) : occurrence.Date,
                amountChanged ? request.Amount : occurrence.Amount);
        }

        series.Configure(sourceChanged ? request.SourceAccountExternalId : series.SourceAccountExternalId,
            destinationChanged ? request.DestinationAccountExternalId : series.DestinationAccountExternalId,
            amountChanged ? request.Amount : series.Amount, day);
        selected.Change(request.SourceAccountExternalId, request.DestinationAccountExternalId, request.Date, request.Amount);
        await store.SaveAsync(series, [selected, .. later], [], cancellationToken);
        return new(selected.ExternalId, null);
    }

    public Task<ClassificationResult> DeleteAsync(Guid occurrenceExternalId, RecurringChangeScope scope, CancellationToken cancellationToken = default)
        => operations.ExecuteAsync(token => DeleteCoreAsync(occurrenceExternalId, scope, token), x => x.Succeeded, cancellationToken);

    private async Task<ClassificationResult> DeleteCoreAsync(Guid occurrenceExternalId, RecurringChangeScope scope, CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(scope)) { return new(null, "Escolha o alcance da exclusão."); }
        var all = await store.GetOccurrencesAsync(cancellationToken);
        var selected = all.FirstOrDefault(x => x.ExternalId == occurrenceExternalId);
        if (selected is null) { return new(null, NotFound); }
        if (scope == RecurringChangeScope.OnlyThis && selected.IsConfirmed) { return new(null, "Desfaça a transferência antes de excluir."); }
        var series = (await store.GetSeriesAsync(cancellationToken)).Single(x => x.ExternalId == selected.RecurringTransferExternalId);
        var ownPending = all.Where(x => x.RecurringTransferExternalId == series.ExternalId && !x.IsConfirmed);
        IReadOnlyList<TransferOccurrence> removed;
        switch (scope)
        {
            case RecurringChangeScope.OnlyThis:
                series.Exclude(selected.OccurrenceMonth);
                removed = [selected];
                break;
            case RecurringChangeScope.ThisAndFollowing:
                series.StopBefore(selected.OccurrenceMonth);
                removed = ownPending.Where(x => x.OccurrenceMonth >= selected.OccurrenceMonth).ToList();
                break;
            default:
                series.Deactivate();
                removed = ownPending.ToList();
                break;
        }

        await store.SaveAsync(series, [], removed, cancellationToken);
        return new(selected.ExternalId, null);
    }

    private static IReadOnlyList<AccountMovement> ConfirmCore(TransferOccurrence occurrence, DateOnly date, Money amount)
    {
        var operation = Guid.CreateVersion7();
        occurrence.Confirm(operation, date, amount);
        return AccountMovement.Transfer(operation, occurrence.SourceAccountExternalId, occurrence.DestinationAccountExternalId, date, amount) ?? [];
    }

    /// <summary>Gera os meses que faltam até o mês atual ou até <paramref name="month"/>, o que vier depois, e devolve todas as ocorrências.</summary>
    private async Task<IReadOnlyList<TransferOccurrence>> EnsureThroughAsync(DateOnly month, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(clock.GetLocalNow().DateTime);
        var current = new DateOnly(today.Year, today.Month, 1);
        var through = month > current ? month : current;
        var existing = await store.GetOccurrencesAsync(cancellationToken);
        var known = existing.Select(x => (x.RecurringTransferExternalId, x.OccurrenceMonth)).ToHashSet();
        var missing = new List<TransferOccurrence>();
        foreach (var series in (await store.GetSeriesAsync(cancellationToken)).Where(x => x.IsActive))
        {
            for (var period = series.StartMonth; period <= through; period = period.AddMonths(1))
            {
                if (series.Includes(period) && !known.Contains((series.ExternalId, period)))
                {
                    missing.Add(TransferOccurrence.Pending(series, period, EntryCatalog.OccurrenceDate(period, series.Day)));
                }
            }
        }

        if (missing.Count == 0) { return existing; }
        await store.AddOccurrencesAsync(missing, cancellationToken);
        return [.. existing, .. missing];
    }

    private static RecurringTransferOccurrence Describe(TransferOccurrence x) => new(x.ExternalId, x.RecurringTransferExternalId, ReferenceMonth.FromDate(x.OccurrenceMonth), x.Date,
        x.SourceAccountExternalId, x.DestinationAccountExternalId, x.Amount, x.IsConfirmed);
}
