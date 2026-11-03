using Ags.Finance.SharedKernel;

namespace Ags.Finance.Modules.Reimbursements.Contracts;

public enum ReimbursementStatus { Pending, Received }

/// <summary>Sem <see cref="Month"/>, considera todos os meses.</summary>
public sealed record ReimbursementFilter(ReferenceMonth? Month = null, Guid? PersonExternalId = null, ReimbursementStatus? Status = null);

public sealed record ReimbursementItem(Guid EntryExternalId, Guid PersonExternalId, string PersonName, DateOnly Date, string Description, string Classification, ReferenceMonth Month,
    Money ExpenseAmount, decimal Percent, Money Expected, Money? Received, ReimbursementStatus Status, bool IsExpenseEffective, Guid? ReceiptExternalId = null, DateOnly? ReceivedDate = null, Guid? AccountExternalId = null);

public sealed record ReimbursementPersonTotal(Guid PersonExternalId, string PersonName, Money Expected, Money Received, Money Pending);

/// <summary><see cref="PendingBeforeMonth"/> soma as pendências de meses anteriores ao consultado, respeitando a pessoa filtrada.</summary>
public sealed record ReimbursementOverview(IReadOnlyList<ReimbursementItem> Items, IReadOnlyList<ReimbursementPersonTotal> People, Money Expected, Money Received, Money Pending,
    Money PendingBeforeMonth, string? Error = null);

public interface IReimbursementOverviews
{
    Task<ReimbursementOverview> GetAsync(ReimbursementFilter filter, CancellationToken cancellationToken = default);
}
