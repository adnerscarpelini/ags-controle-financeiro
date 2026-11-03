using Ags.Finance.SharedKernel;

namespace Ags.Finance.Modules.Transactions.Contracts;

public enum AccountMovementKind { Transfer, BalanceAdjustment, InvoicePayment }
public sealed record InvoiceDebitRequest(Guid PaymentExternalId, Guid AccountExternalId, DateOnly Date, Money Amount);
public sealed record TransferRequest(Guid ConfirmationId, Guid SourceAccountExternalId, Guid DestinationAccountExternalId, DateOnly Date, Money Amount);
public sealed record BalanceAdjustmentRequest(Guid ConfirmationId, Guid AccountExternalId, DateOnly Date, Money CorrectBalance, Money? ExpectedBalance = null);
public sealed record AccountMovementDetails(Guid ExternalId, Guid OperationExternalId, Guid AccountExternalId, DateOnly Date, Money Amount, AccountMovementKind Kind, DateTimeOffset ConfirmedAt, Money? AdjustmentTarget = null);

public interface IAccountMovementCatalog
{
    Task<ClassificationResult> TransferAsync(TransferRequest request, CancellationToken cancellationToken = default);
    Task<ClassificationResult> AdjustBalanceAsync(BalanceAdjustmentRequest request, CancellationToken cancellationToken = default);
    Task<ClassificationResult> RemoveInvoiceDebitAsync(Guid paymentExternalId, CancellationToken cancellationToken = default);
    Task<ClassificationResult> RecordInvoiceDebitAsync(InvoiceDebitRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AccountMovementDetails>> GetAsync(Guid? accountExternalId = null, CancellationToken cancellationToken = default);
}
