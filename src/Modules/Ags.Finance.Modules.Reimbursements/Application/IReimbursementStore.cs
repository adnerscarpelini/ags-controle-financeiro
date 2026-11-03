using Ags.Finance.Modules.Reimbursements.Domain;

namespace Ags.Finance.Modules.Reimbursements.Application;

internal interface IReimbursementStore
{
    Task<IReadOnlyList<Person>> GetPeopleAsync(CancellationToken cancellationToken);
    Task<bool> SavePersonAsync(Person person, CancellationToken cancellationToken);
    Task DeletePersonAsync(Person person, CancellationToken cancellationToken);
    Task<IReadOnlyList<ReimbursementReceipt>> GetReceiptsAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<ReimbursementReceiptItem>> GetReceiptItemsAsync(CancellationToken cancellationToken);
    Task AddReceiptAsync(ReimbursementReceipt receipt, IReadOnlyList<ReimbursementReceiptItem> items, CancellationToken cancellationToken);
    Task<bool> IsIncomeOfReceiptAsync(Guid entryExternalId, CancellationToken cancellationToken);
    Task<bool> IsReceivedExpenseAsync(Guid entryExternalId, CancellationToken cancellationToken);
}
