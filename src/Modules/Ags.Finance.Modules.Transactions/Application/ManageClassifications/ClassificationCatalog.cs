using Ags.Finance.Modules.Transactions.Contracts;
using Ags.Finance.Modules.Transactions.Domain;
using CategoryKind = Ags.Finance.Modules.Transactions.Contracts.CategoryKind;

namespace Ags.Finance.Modules.Transactions.Application.ManageClassifications;

internal sealed class ClassificationCatalog(IClassificationStore store) : IClassificationCatalog
{
    public async Task<IReadOnlyList<CategoryDetails>> GetCategoriesAsync(bool activeOnly = true, CancellationToken cancellationToken = default)
        => (await store.GetCategoriesAsync(cancellationToken)).Where(x => !activeOnly || x.IsActive)
            .Select(x => new CategoryDetails(x.ExternalId, x.Name, (CategoryKind)x.Kind, x.IsActive)).ToList();

    public async Task<IReadOnlyList<SubcategoryDetails>> GetSubcategoriesAsync(bool activeOnly = true, CancellationToken cancellationToken = default)
    {
        var categories = await store.GetCategoriesAsync(cancellationToken);
        return (await store.GetSubcategoriesAsync(cancellationToken))
            .Select(x => (Subcategory: x, Category: categories.First(c => c.ExternalId == x.CategoryExternalId)))
            .Where(x => !activeOnly || (x.Subcategory.IsActive && x.Category.IsActive))
            .Select(x => new SubcategoryDetails(x.Subcategory.ExternalId, x.Category.ExternalId, x.Subcategory.Name,
                $"{x.Category.Name} > {x.Subcategory.Name}", (CategoryKind)x.Category.Kind, x.Subcategory.IsActive && x.Category.IsActive)).ToList();
    }

    public async Task<ClassificationResult> SaveCategoryAsync(Guid? externalId, string name, CategoryKind kind, Guid? newExternalId = null, CancellationToken cancellationToken = default)
    {
        var category = externalId is null ? new Category() : (await store.GetCategoriesAsync(cancellationToken)).FirstOrDefault(x => x.ExternalId == externalId);
        if (category is null)
        {
            return new(null, "Categoria não encontrada.");
        }
        if (externalId is null && newExternalId is { } categoryId)
        {
            if (categoryId == Guid.Empty || (await store.GetCategoriesAsync(cancellationToken)).Any(x => x.ExternalId == categoryId)) { return new(null, "Identidade pública inválida ou já utilizada."); }
            category.UseExternalId(categoryId);
        }
        var error = category.Update(name, (Domain.CategoryKind)kind);
        return error is null ? await PersistAsync(category, cancellationToken) : new(null, error);
    }

    public async Task<ClassificationResult> SaveSubcategoryAsync(Guid? externalId, Guid categoryExternalId, string name, Guid? newExternalId = null, CancellationToken cancellationToken = default)
    {
        if (!(await store.GetCategoriesAsync(cancellationToken)).Any(x => x.ExternalId == categoryExternalId && x.IsActive))
        {
            return new(null, "Selecione uma categoria ativa.");
        }
        var subcategories = await store.GetSubcategoriesAsync(cancellationToken);
        var subcategory = externalId is null ? new Subcategory() : subcategories.FirstOrDefault(x => x.ExternalId == externalId);
        if (subcategory is null)
        {
            return new(null, "Subcategoria não encontrada.");
        }
        if (externalId is null && newExternalId is { } subcategoryId)
        {
            if (subcategoryId == Guid.Empty || subcategories.Any(x => x.ExternalId == subcategoryId)) { return new(null, "Identidade pública inválida ou já utilizada."); }
            subcategory.UseExternalId(subcategoryId);
        }
        var error = subcategory.Update(categoryExternalId, name);
        if (error is not null)
        {
            return new(null, error);
        }
        if (subcategories.Any(x => x.ExternalId != subcategory.ExternalId && x.CategoryExternalId == categoryExternalId && x.NormalizedName == subcategory.NormalizedName))
        {
            return new(null, "Já existe uma subcategoria com esse nome na categoria.");
        }
        return await PersistAsync(subcategory, cancellationToken);
    }

    public async Task<ClassificationResult> DeactivateAsync(Guid externalId, bool subcategory, CancellationToken cancellationToken = default)
    {
        if (subcategory)
        {
            var item = (await store.GetSubcategoriesAsync(cancellationToken)).FirstOrDefault(x => x.ExternalId == externalId);
            if (item is null)
            {
                return new(null, "Subcategoria não encontrada.");
            }
            item.Deactivate();
            return await PersistAsync(item, cancellationToken);
        }
        var category = (await store.GetCategoriesAsync(cancellationToken)).FirstOrDefault(x => x.ExternalId == externalId);
        if (category is null)
        {
            return new(null, "Categoria não encontrada.");
        }
        category.Deactivate();
        return await PersistAsync(category, cancellationToken);
    }

    private async Task<ClassificationResult> PersistAsync(Entity entity, CancellationToken cancellationToken)
        => await store.SaveAsync(entity, cancellationToken) ? new(entity.ExternalId, null) : new(null, "Já existe uma subcategoria com esse nome na categoria.");
}
