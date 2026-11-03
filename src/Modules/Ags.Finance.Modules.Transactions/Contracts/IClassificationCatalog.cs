namespace Ags.Finance.Modules.Transactions.Contracts;

public enum CategoryKind
{
    Expense,
    Income,
    Transfer
}

public sealed record CategoryDetails(Guid ExternalId, string Name, CategoryKind Kind, bool IsActive);
public sealed record SubcategoryDetails(Guid ExternalId, Guid CategoryExternalId, string Name, string DisplayName, CategoryKind Kind, bool IsActive);
public sealed record ClassificationResult(Guid? ExternalId, string? Error)
{
    public bool Succeeded => Error is null;
}

public interface IClassificationCatalog
{
    Task<IReadOnlyList<CategoryDetails>> GetCategoriesAsync(bool activeOnly = true, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SubcategoryDetails>> GetSubcategoriesAsync(bool activeOnly = true, CancellationToken cancellationToken = default);
    Task<ClassificationResult> SaveCategoryAsync(Guid? externalId, string name, CategoryKind kind, Guid? newExternalId = null, CancellationToken cancellationToken = default);
    Task<ClassificationResult> SaveSubcategoryAsync(Guid? externalId, Guid categoryExternalId, string name, Guid? newExternalId = null, CancellationToken cancellationToken = default);
    Task<ClassificationResult> DeactivateAsync(Guid externalId, bool subcategory, CancellationToken cancellationToken = default);
}
