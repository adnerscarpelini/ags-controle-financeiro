using Ags.Finance.SharedKernel;

namespace Ags.Finance.Modules.Reimbursements.Contracts;

/// <summary>Um lançamento de despesa coberto por uma regra: <see cref="Due"/> é <see cref="Total"/> × <see cref="Percent"/>.</summary>
public sealed record ReimbursementItem(Guid EntryExternalId, DateOnly Date, string Description, string Classification, Money Total, decimal Percent, Money Due,
    Guid RuleExternalId, string SavedFilterName);

public sealed record ReceivedReimbursement(Guid ExternalId, Guid EntryExternalId, DateOnly Date, Money Amount);

/// <summary>Saldo negativo significa crédito da pessoa (recebeu mais do que devia).</summary>
public sealed record ReimbursementStatement(Guid PersonExternalId, AccountingMonth Month, IReadOnlyList<ReimbursementItem> Items,
    IReadOnlyList<ReceivedReimbursement> Receipts, Money TotalDue, Money TotalReceived, Money Balance, string? Error = null)
{
    public bool HasCredit => Balance.IsNegative;
}

public interface IReimbursementStatements
{
    Task<ReimbursementStatement> GetAsync(Guid personExternalId, AccountingMonth month, CancellationToken cancellationToken = default);
}
