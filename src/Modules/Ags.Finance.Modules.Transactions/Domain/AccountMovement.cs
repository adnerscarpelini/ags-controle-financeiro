using Ags.Finance.SharedKernel;

namespace Ags.Finance.Modules.Transactions.Domain;

internal enum AccountMovementKind { Transfer }

internal sealed class AccountMovement : Entity
{
    private AccountMovement() { }
    public Guid OperationExternalId { get; private set; }
    public Guid AccountExternalId { get; private set; }
    public DateOnly Date { get; private set; }
    public Money Amount { get; private set; }
    public AccountMovementKind Kind { get; private set; }

    public static IReadOnlyList<AccountMovement>? Transfer(Guid operationId, Guid sourceId, Guid destinationId, DateOnly date, Money amount)
    {
        if (operationId == Guid.Empty || sourceId == Guid.Empty || destinationId == Guid.Empty || sourceId == destinationId
            || date == DateOnly.MinValue || amount.Amount <= 0 || amount.Amount > 92233720368547758.07m) { return null; }
        return [new() { OperationExternalId = operationId, AccountExternalId = sourceId, Date = date, Amount = -amount },
            new() { OperationExternalId = operationId, AccountExternalId = destinationId, Date = date, Amount = amount }];
    }
}
