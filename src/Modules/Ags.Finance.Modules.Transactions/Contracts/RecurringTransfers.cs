using Ags.Finance.SharedKernel;

namespace Ags.Finance.Modules.Transactions.Contracts;

/// <summary><see cref="IsConfirmed"/> vale só para a primeira ocorrência; as seguintes nascem pendentes.</summary>
public sealed record CreateRecurringTransferRequest(Guid SourceAccountExternalId, Guid DestinationAccountExternalId, DateOnly Date, Money Amount, ReferenceMonth? EndMonth = null, bool IsConfirmed = true);

public sealed record RecurringTransferOccurrence(Guid ExternalId, Guid SeriesExternalId, ReferenceMonth Month, DateOnly Date, Guid SourceAccountExternalId, Guid DestinationAccountExternalId, Money Amount, bool IsConfirmed);

/// <summary>Aceita <see cref="RecurringEditScope.OnlyThis"/> e <see cref="RecurringEditScope.ThisAndFutureOpen"/>. <see cref="Day"/> é o dia das próximas ocorrências.</summary>
public sealed record EditRecurringTransferRequest(Guid SourceAccountExternalId, Guid DestinationAccountExternalId, DateOnly Date, Money Amount, RecurringEditScope Scope, int? Day = null);

/// <summary>Uma série ativa, para a tela de lançamentos fixos.</summary>
public sealed record RecurringTransferSeriesDetails(Guid ExternalId, int Day, Guid SourceAccountExternalId, Guid DestinationAccountExternalId, Money Amount, ReferenceMonth? EndMonth);

public sealed record EditRecurringTransferSeriesRequest(Guid SourceAccountExternalId, Guid DestinationAccountExternalId, Money Amount, int Day);

public interface IRecurringTransferCatalog
{
    Task<ClassificationResult> CreateAsync(CreateRecurringTransferRequest request, CancellationToken cancellationToken = default);
    Task<bool> HasSeriesAsync(CancellationToken cancellationToken = default);

    /// <summary>As ocorrências do mês, geradas até ele se preciso.</summary>
    Task<IReadOnlyList<RecurringTransferOccurrence>> GetMonthAsync(ReferenceMonth month, CancellationToken cancellationToken = default);

    /// <summary>As ocorrências pendentes com data no intervalo fechado. Sem início, inclui as atrasadas.</summary>
    Task<IReadOnlyList<RecurringTransferOccurrence>> GetPendingAsync(DateOnly? from, DateOnly to, CancellationToken cancellationToken = default);

    /// <summary>Movimenta as contas uma única vez, com o valor e a data informados. Confirmar de novo não muda nada.</summary>
    Task<ClassificationResult> ConfirmAsync(Guid occurrenceExternalId, DateOnly date, Money amount, CancellationToken cancellationToken = default);

    /// <summary>Remove os movimentos e deixa a ocorrência pendente.</summary>
    Task<ClassificationResult> UndoAsync(Guid occurrenceExternalId, CancellationToken cancellationToken = default);

    Task<ClassificationResult> EditAsync(Guid occurrenceExternalId, EditRecurringTransferRequest request, CancellationToken cancellationToken = default);

    /// <summary>Ocorrências confirmadas continuam no histórico.</summary>
    Task<ClassificationResult> DeleteAsync(Guid occurrenceExternalId, RecurringChangeScope scope, CancellationToken cancellationToken = default);

    /// <summary>As séries ativas e ainda não encerradas.</summary>
    Task<IReadOnlyList<RecurringTransferSeriesDetails>> GetSeriesAsync(CancellationToken cancellationToken = default);

    /// <summary>Altera a série e os meses pendentes a partir do atual. Confirmados e meses anteriores não mudam.</summary>
    Task<ClassificationResult> EditSeriesAsync(Guid seriesExternalId, EditRecurringTransferSeriesRequest request, CancellationToken cancellationToken = default);

    /// <summary>Encerra a série e remove os meses pendentes a partir do atual. Confirmados e meses anteriores continuam.</summary>
    Task<ClassificationResult> DeleteSeriesAsync(Guid seriesExternalId, CancellationToken cancellationToken = default);
}
