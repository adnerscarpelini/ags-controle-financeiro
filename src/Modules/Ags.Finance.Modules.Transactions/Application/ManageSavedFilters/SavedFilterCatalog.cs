using Ags.Finance.Modules.Transactions.Contracts;
using Ags.Finance.Modules.Transactions.Domain;
using Ags.Finance.SharedKernel;

namespace Ags.Finance.Modules.Transactions.Application.ManageSavedFilters;

internal sealed class SavedFilterCatalog(ISavedFilterStore store, IEntryCatalog entries) : ISavedFilterCatalog
{
    public async Task<IReadOnlyList<SavedFilterDetails>> GetAsync(CancellationToken cancellationToken = default)
        => (await store.GetAsync(cancellationToken)).Select(Describe).ToList();

    public async Task<SavedFilterDetails?> FindAsync(Guid externalId, CancellationToken cancellationToken = default)
        => (await store.GetAsync(cancellationToken)).Where(x => x.ExternalId == externalId).Select(Describe).FirstOrDefault();

    public async Task<ClassificationResult> SaveAsync(Guid? externalId, string name, SavedFilterCriteria criteria, CancellationToken cancellationToken = default)
    {
        var filters = await store.GetAsync(cancellationToken);
        var filter = externalId is null ? new SavedFilter() : filters.FirstOrDefault(x => x.ExternalId == externalId);
        if (filter is null) { return new(null, "Filtro não encontrado."); }
        var error = filter.Update(name, criteria.CategoryExternalIds, criteria.SubcategoryExternalIds, criteria.MerchantExternalIds, criteria.AccountExternalIds,
            criteria.CardExternalIds, criteria.CardLastDigitsExternalIds, criteria.Text, criteria.IsEffective);
        if (error is not null) { return new(null, error); }
        var duplicate = filters.FirstOrDefault(x => x.ExternalId != filter.ExternalId && x.NormalizedName == filter.NormalizedName);
        if (duplicate is not null) { return new(null, $"Já existe um filtro com este nome: {duplicate.Name}."); }
        return await store.SaveAsync(filter, cancellationToken) ? new(filter.ExternalId, null) : new(null, "Já existe um filtro com este nome.");
    }

    public async Task<ClassificationResult> DeleteAsync(Guid externalId, CancellationToken cancellationToken = default)
    {
        var filter = (await store.GetAsync(cancellationToken)).FirstOrDefault(x => x.ExternalId == externalId);
        if (filter is null) { return new(null, "Filtro não encontrado."); }
        await store.DeleteAsync(filter, cancellationToken);
        return new(externalId, null);
    }

    public async Task<EntryQueryResult> RunAsync(Guid externalId, ReferenceMonth? month = null, DateOnly? fromDate = null, DateOnly? toDate = null, int page = 1, int pageSize = 100, EntrySortColumn? sortColumn = null, bool sortDescending = true, CancellationToken cancellationToken = default)
    {
        var filter = await FindAsync(externalId, cancellationToken);
        if (filter is null) { return new([], default, default, default, 0, 1, pageSize, "Filtro não encontrado."); }
        if (month is null && fromDate is null && toDate is null) { return new([], default, default, default, 0, 1, pageSize, "Informe o mês de referência ou o intervalo para aplicar o filtro."); }
        var c = filter.Criteria;
        return await entries.QueryAsync(new(month, fromDate, toDate, Text: c.Text, IsEffective: c.IsEffective, Page: page, PageSize: pageSize,
            AccountExternalIds: c.AccountExternalIds, CardExternalIds: c.CardExternalIds, CardLastDigitsExternalIds: c.CardLastDigitsExternalIds,
            CategoryExternalIds: c.CategoryExternalIds, SubcategoryExternalIds: c.SubcategoryExternalIds, MerchantExternalIds: c.MerchantExternalIds, SortColumn: sortColumn, SortDescending: sortDescending), cancellationToken);
    }

    private static SavedFilterDetails Describe(SavedFilter x)
        => new(x.ExternalId, x.Name, new(x.CategoryExternalIds, x.SubcategoryExternalIds, x.MerchantExternalIds, x.AccountExternalIds, x.CardExternalIds, x.CardLastDigitsExternalIds, x.Text, x.IsEffective));
}
