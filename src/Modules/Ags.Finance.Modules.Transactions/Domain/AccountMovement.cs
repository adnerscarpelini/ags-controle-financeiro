using Ags.Finance.SharedKernel;

namespace Ags.Finance.Modules.Transactions.Domain;

internal enum AccountMovementKind { Transfer, BalanceAdjustment, InvoicePayment }

internal sealed class AccountMovement : Entity
{
    private AccountMovement() { }
    public Guid OperationExternalId { get; private set; }
    public Guid AccountExternalId { get; private set; }
    public DateOnly Date { get; private set; }
    public Money Amount { get; private set; }
    public AccountMovementKind Kind { get; private set; }
    public Money TargetBalance { get; private set; }

    public static AccountMovement? InvoiceDebit(Guid paymentId, Guid accountId, DateOnly date, Money amount)
    {
        if (paymentId == Guid.Empty || accountId == Guid.Empty || date == DateOnly.MinValue || amount.Amount <= 0 || amount.Amount > 92233720368547758.07m) { return null; }
        return new() { OperationExternalId = paymentId, AccountExternalId = accountId, Date = date, Amount = -amount, Kind = AccountMovementKind.InvoicePayment };
    }

    public static AccountMovement? Adjustment(Guid operationId, Guid accountId, DateOnly date, Money difference, Money target)
    {
        if (operationId == Guid.Empty || accountId == Guid.Empty || date == DateOnly.MinValue || difference.IsZero
            || Math.Abs(difference.Amount) > 92233720368547758.07m || Math.Abs(target.Amount) > 92233720368547758.07m) { return null; }
        return new() { OperationExternalId = operationId, AccountExternalId = accountId, Date = date, Amount = difference, Kind = AccountMovementKind.BalanceAdjustment, TargetBalance = target };
    }

    public static IReadOnlyList<AccountMovement>? Transfer(Guid operationId, Guid sourceId, Guid destinationId, DateOnly date, Money amount)
    {
        if (operationId == Guid.Empty || sourceId == Guid.Empty || destinationId == Guid.Empty || sourceId == destinationId
            || date == DateOnly.MinValue || amount.Amount <= 0 || amount.Amount > 92233720368547758.07m) { return null; }
        return [new() { OperationExternalId = operationId, AccountExternalId = sourceId, Date = date, Amount = -amount },
            new() { OperationExternalId = operationId, AccountExternalId = destinationId, Date = date, Amount = amount }];
    }
}
