using Ags.Finance.SharedKernel;

namespace Ags.Finance.Modules.Reimbursements.Domain;

/// <summary>Recebimento de uma pessoa em uma efetivação. Total zero não gera receita, então não tem conta nem <see cref="IncomeEntryExternalId"/>.</summary>
internal sealed class ReimbursementReceipt : Entity
{
    private ReimbursementReceipt() { }

    public Guid PersonExternalId { get; private set; }
    public DateOnly Date { get; private set; }
    public Guid? AccountExternalId { get; private set; }
    public Guid? IncomeEntryExternalId { get; private set; }
    public Money Total { get; private set; }

    public static ReimbursementReceipt Create(Guid personExternalId, DateOnly date, Guid? accountExternalId, Guid? incomeEntryExternalId, Money total)
        => new() { PersonExternalId = personExternalId, Date = date, AccountExternalId = accountExternalId, IncomeEntryExternalId = incomeEntryExternalId, Total = total };
}

/// <summary>Foto dos valores da despesa no recebimento. O índice único em <see cref="ExpenseEntryExternalId"/> impede receber a mesma despesa duas vezes.</summary>
internal sealed class ReimbursementReceiptItem : Entity
{
    private ReimbursementReceiptItem() { }

    public Guid ReceiptExternalId { get; private set; }
    public Guid ExpenseEntryExternalId { get; private set; }
    public Money ExpenseAmount { get; private set; }
    public decimal Percent { get; private set; }
    public Money Expected { get; private set; }
    public Money Received { get; private set; }

    public static ReimbursementReceiptItem Create(Guid receiptExternalId, Guid expenseEntryExternalId, Money expenseAmount, decimal percent, Money expected, Money received)
        => new() { ReceiptExternalId = receiptExternalId, ExpenseEntryExternalId = expenseEntryExternalId, ExpenseAmount = expenseAmount, Percent = percent, Expected = expected, Received = received };
}
