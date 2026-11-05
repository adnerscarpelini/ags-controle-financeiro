using Ags.Finance.SharedKernel;

namespace Ags.Finance.Modules.Transactions.Domain;

/// <summary>
/// Um mês de uma <see cref="RecurringTransfer"/>. Pendente não movimenta as contas; confirmada aponta para os dois
/// <see cref="AccountMovement"/> pela <see cref="OperationExternalId"/>.
/// </summary>
internal sealed class TransferOccurrence : Entity
{
    private TransferOccurrence() { }
    public Guid RecurringTransferExternalId { get; private set; }
    public DateOnly OccurrenceMonth { get; private set; }
    public DateOnly Date { get; private set; }
    public Guid SourceAccountExternalId { get; private set; }
    public Guid DestinationAccountExternalId { get; private set; }
    public Money Amount { get; private set; }
    public Guid? OperationExternalId { get; private set; }

    public bool IsConfirmed => OperationExternalId is not null;

    public static TransferOccurrence Pending(RecurringTransfer series, DateOnly month, DateOnly date) => new()
    {
        RecurringTransferExternalId = series.ExternalId,
        OccurrenceMonth = month,
        Date = date,
        SourceAccountExternalId = series.SourceAccountExternalId,
        DestinationAccountExternalId = series.DestinationAccountExternalId,
        Amount = series.Amount,
    };

    public void Confirm(Guid operationExternalId, DateOnly date, Money amount)
    {
        OperationExternalId = operationExternalId;
        Date = date;
        Amount = amount;
    }

    public void Undo() => OperationExternalId = null;

    public void Change(Guid source, Guid destination, DateOnly date, Money amount)
    {
        SourceAccountExternalId = source;
        DestinationAccountExternalId = destination;
        Date = date;
        Amount = amount;
    }
}
