using Ags.Finance.Modules.Transactions.Contracts;
using Ags.Finance.Modules.Transactions.Domain;

namespace Ags.Finance.Modules.Transactions.Application.ManageMerchants;

internal sealed class MerchantCatalog(IMerchantStore store, IClassificationCatalog classifications) : IMerchantCatalog
{
    public async Task<IReadOnlyList<MerchantDetails>> FindAsync(string search = "", bool activeOnly = true, CancellationToken cancellationToken = default)
    {
        var normalized = Merchant.Normalize(search);
        return (await store.GetAsync(cancellationToken)).Where(x => (!activeOnly || x.IsActive) && x.NormalizedName.Contains(normalized, StringComparison.Ordinal))
            .Select(x => new MerchantDetails(x.ExternalId, x.Name, x.DefaultSubcategoryExternalId, x.IsActive)).ToList();
    }

    public async Task<ClassificationResult> SaveAsync(Guid? externalId, string name, Guid? defaultSubcategoryExternalId, CancellationToken cancellationToken = default)
    {
        var merchants = await store.GetAsync(cancellationToken);
        var merchant = externalId is null ? new Merchant() : merchants.FirstOrDefault(x => x.ExternalId == externalId);
        if (merchant is null)
        {
            return new(null, "Descrição não encontrada.");
        }
        if (defaultSubcategoryExternalId is { } subcategoryId && !(await classifications.GetSubcategoriesAsync(cancellationToken: cancellationToken)).Any(x => x.ExternalId == subcategoryId))
        {
            return new(null, "Selecione uma subcategoria ativa.");
        }
        var error = merchant.Update(name, defaultSubcategoryExternalId);
        if (error is not null)
        {
            return new(null, error);
        }
        var duplicate = merchants.FirstOrDefault(x => x.ExternalId != merchant.ExternalId && x.NormalizedName == merchant.NormalizedName);
        if (duplicate is not null)
        {
            return new(duplicate.ExternalId, $"Esta descrição já existe: {duplicate.Name}.");
        }
        return await SaveAsync(merchant, cancellationToken);
    }

    public async Task<ClassificationResult> DeactivateAsync(Guid externalId, CancellationToken cancellationToken = default)
    {
        var merchant = (await store.GetAsync(cancellationToken)).FirstOrDefault(x => x.ExternalId == externalId);
        if (merchant is null)
        {
            return new(null, "Descrição não encontrada.");
        }
        merchant.Deactivate();
        return await SaveAsync(merchant, cancellationToken);
    }

    public async Task<ClassificationResult> MergeAsync(Guid sourceExternalId, Guid targetExternalId, CancellationToken cancellationToken = default)
    {
        if (sourceExternalId == targetExternalId)
        {
            return new(null, "Escolha duas descrições diferentes.");
        }
        var error = await store.MergeAsync(sourceExternalId, targetExternalId, cancellationToken);
        return new(error is null ? targetExternalId : null, error);
    }

    private async Task<ClassificationResult> SaveAsync(Merchant merchant, CancellationToken cancellationToken)
        => await store.SaveAsync(merchant, cancellationToken) ? new(merchant.ExternalId, null) : new(null, "Já existe uma descrição equivalente. Consulte as sugestões.");
}
