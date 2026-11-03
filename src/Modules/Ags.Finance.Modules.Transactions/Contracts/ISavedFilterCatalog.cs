using Ags.Finance.SharedKernel;

namespace Ags.Finance.Modules.Transactions.Contracts;

/// <summary>Critérios reutilizáveis de lançamentos, sem período: o período é escolhido a cada execução.</summary>
public sealed record SavedFilterCriteria(IReadOnlyList<Guid> CategoryExternalIds, IReadOnlyList<Guid> SubcategoryExternalIds, IReadOnlyList<Guid> MerchantExternalIds,
    IReadOnlyList<Guid> AccountExternalIds, IReadOnlyList<Guid> CardExternalIds, IReadOnlyList<Guid> CardLastDigitsExternalIds, string Text = "", bool? IsEffective = null)
{
    public static SavedFilterCriteria Empty { get; } = new([], [], [], [], [], []);
}

public sealed record SavedFilterDetails(Guid ExternalId, string Name, SavedFilterCriteria Criteria);

public interface ISavedFilterCatalog
{
    Task<IReadOnlyList<SavedFilterDetails>> GetAsync(CancellationToken cancellationToken = default);
    Task<SavedFilterDetails?> FindAsync(Guid externalId, CancellationToken cancellationToken = default);
    Task<ClassificationResult> SaveAsync(Guid? externalId, string name, SavedFilterCriteria criteria, CancellationToken cancellationToken = default);
    Task<ClassificationResult> DeleteAsync(Guid externalId, CancellationToken cancellationToken = default);

    /// <summary>Executa o filtro sobre o mês de referência ou, se informado, sobre o intervalo de datas.</summary>
    Task<EntryQueryResult> RunAsync(Guid externalId, ReferenceMonth? month = null, DateOnly? fromDate = null, DateOnly? toDate = null, int page = 1, int pageSize = 100, CancellationToken cancellationToken = default);
}
