using Ags.Finance.SharedKernel;

namespace Ags.Finance.Modules.Transactions.Contracts;

public enum AccountMovementKind { Transfer }
public sealed record TransferRequest(Guid ConfirmationId, Guid SourceAccountExternalId, Guid DestinationAccountExternalId, DateOnly Date, Money Amount);
public sealed record AccountMovementDetails(Guid ExternalId, Guid OperationExternalId, Guid AccountExternalId, DateOnly Date, Money Amount, AccountMovementKind Kind, DateTimeOffset ConfirmedAt);

public interface IAccountMovementCatalog
{
    Task<ClassificationResult> TransferAsync(TransferRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AccountMovementDetails>> GetAsync(Guid? accountExternalId = null, CancellationToken cancellationToken = default);
}
