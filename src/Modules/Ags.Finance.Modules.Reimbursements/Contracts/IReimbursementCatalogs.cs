namespace Ags.Finance.Modules.Reimbursements.Contracts;

public sealed record ReimbursementResult(Guid? ExternalId, string? Error)
{
    public bool Succeeded => Error is null;
}

public sealed record PersonDetails(Guid ExternalId, string Name, decimal DefaultPercent);

public interface IPersonCatalog
{
    Task<IReadOnlyList<PersonDetails>> GetAsync(CancellationToken cancellationToken = default);
    Task<ReimbursementResult> SaveAsync(Guid? externalId, string name, decimal defaultPercent, Guid? newExternalId = null, CancellationToken cancellationToken = default);
    Task<ReimbursementResult> DeleteAsync(Guid externalId, CancellationToken cancellationToken = default);
}
