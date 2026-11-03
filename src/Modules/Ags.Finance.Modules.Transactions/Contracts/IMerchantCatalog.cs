namespace Ags.Finance.Modules.Transactions.Contracts;

public sealed record MerchantDetails(Guid ExternalId, string Name, Guid? DefaultSubcategoryExternalId, bool IsActive);

public interface IMerchantCatalog
{
    Task<IReadOnlyList<MerchantDetails>> FindAsync(string search = "", bool activeOnly = true, CancellationToken cancellationToken = default);
    Task<ClassificationResult> SaveAsync(Guid? externalId, string name, Guid? defaultSubcategoryExternalId, Guid? newExternalId = null, CancellationToken cancellationToken = default);
    Task<ClassificationResult> DeactivateAsync(Guid externalId, CancellationToken cancellationToken = default);
    Task<ClassificationResult> MergeAsync(Guid sourceExternalId, Guid targetExternalId, CancellationToken cancellationToken = default);
}
