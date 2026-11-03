using Ags.Finance.SharedKernel;

namespace Ags.Finance.Modules.Reimbursements.Contracts;

public sealed record ReimbursementResult(Guid? ExternalId, string? Error)
{
    public bool Succeeded => Error is null;
}

public sealed record PersonDetails(Guid ExternalId, string Name, string PayerDescription);

public sealed record SharingRuleDetails(Guid ExternalId, Guid PersonExternalId, Guid SavedFilterExternalId, string SavedFilterName, decimal Percent, AccountingMonth StartMonth);

public interface IPersonCatalog
{
    Task<IReadOnlyList<PersonDetails>> GetAsync(CancellationToken cancellationToken = default);
    Task<ReimbursementResult> SaveAsync(Guid? externalId, string name, string payerDescription, Guid? newExternalId = null, CancellationToken cancellationToken = default);
    Task<ReimbursementResult> DeleteAsync(Guid externalId, CancellationToken cancellationToken = default);
}

public interface ISharingRuleCatalog
{
    Task<IReadOnlyList<SharingRuleDetails>> GetAsync(Guid? personExternalId = null, CancellationToken cancellationToken = default);
    Task<ReimbursementResult> SaveAsync(Guid? externalId, Guid personExternalId, Guid savedFilterExternalId, decimal percent, AccountingMonth startMonth, CancellationToken cancellationToken = default);
    Task<ReimbursementResult> DeleteAsync(Guid externalId, CancellationToken cancellationToken = default);
}
