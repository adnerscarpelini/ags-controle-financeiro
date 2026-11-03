using Ags.Finance.Modules.Transactions.Contracts;
using Ags.Finance.Modules.Transactions.Domain;
using Ags.Finance.SharedKernel;

namespace Ags.Finance.Modules.Transactions.Application;

internal interface IEntryStore
{
    Task<IReadOnlyList<TransactionEntry>> GetAsync(CancellationToken cancellationToken);
    Task<EntryPage> QueryAsync(EntryFilter filter, CancellationToken cancellationToken, int? offset = null, int? limit = null);
    Task<IReadOnlyList<EntryAmount>> GetMonthlyAmountsAsync(AccountingMonth from, AccountingMonth to, CancellationToken cancellationToken);
    Task<IReadOnlyList<EntryPlanningAmount>> GetPlanningAmountsAsync(AccountingMonth from, AccountingMonth to, CancellationToken cancellationToken);
    Task<IReadOnlyList<InvoiceAdjustment>> GetInvoiceAdjustmentsAsync(CancellationToken cancellationToken);
    Task AddInvoiceAdjustmentAsync(InvoiceAdjustment adjustment, CancellationToken cancellationToken);
    Task<bool> AddAsync(TransactionEntry entry, CancellationToken cancellationToken);
    Task<IReadOnlyList<InstallmentPurchase>> GetPurchasesAsync(CancellationToken cancellationToken);
    Task<bool> AddPurchaseAsync(InstallmentPurchase purchase, IReadOnlyList<TransactionEntry> entries, CancellationToken cancellationToken);
    Task<bool> SaveEntriesAsync(IReadOnlyList<TransactionEntry> entries, CancellationToken cancellationToken);
    Task DeleteEntriesAsync(IReadOnlyList<TransactionEntry> entries, CancellationToken cancellationToken);
    Task<IReadOnlyList<RecurringEntry>> GetRecurringAsync(CancellationToken cancellationToken);
    Task AddRecurringAsync(RecurringEntry series, TransactionEntry first, CancellationToken cancellationToken);
    Task AddOccurrencesAsync(IReadOnlyList<TransactionEntry> entries, CancellationToken cancellationToken);
    Task SaveRecurringChangesAsync(RecurringEntry series, RecurringEntry? following, IReadOnlyList<TransactionEntry> updated, IReadOnlyList<TransactionEntry> deleted, CancellationToken cancellationToken);
}
