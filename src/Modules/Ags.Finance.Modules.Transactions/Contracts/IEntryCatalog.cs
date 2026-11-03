using Ags.Finance.SharedKernel;

namespace Ags.Finance.Modules.Transactions.Contracts;

public enum EntryChangeScope { OnlyThis, ThisAndFollowing, All }
public enum RepeatPeriod { Days, Weeks, Months, Years }

/// <summary>Quem devolve parte de uma despesa e quanto. O percentual fica gravado e não acompanha o padrão da pessoa.</summary>
public sealed record EntryReimbursement(Guid PersonExternalId, decimal Percent);

public sealed record CreateEntryRequest(DateOnly Date, Guid MerchantExternalId, Guid SubcategoryExternalId, Guid AccountExternalId, Money Amount, string Details = "", ReferenceMonth? Month = null, Guid? CardExternalId = null, Guid? CardLastDigitsExternalId = null, EntryReimbursement? Reimbursement = null, bool? IsPaid = null, bool UseSelectedInvoice = false);
public sealed record ConfirmEffectiveRequest(DateOnly Date, Money Amount, Guid AccountExternalId);
public sealed record EntryDetails(Guid ExternalId, DateOnly Date, ReferenceMonth Month, Guid MerchantExternalId, string MerchantName, Guid SubcategoryExternalId, Guid CategoryExternalId, string Classification, Guid AccountExternalId, CategoryKind Kind, Money Amount, string Details, bool IsEffective, DateOnly? EffectiveDate, DateTimeOffset? EffectiveAt, Guid? CardExternalId = null, Guid? CardLastDigitsExternalId = null, Guid? InstallmentPurchaseExternalId = null, int? InstallmentNumber = null, int? InstallmentCount = null, Guid? RecurringEntryExternalId = null, DateOnly? OccurrenceMonth = null, bool IsInvoiceAdjustment = false, EntryReimbursement? Reimbursement = null, Guid? RepetitionExternalId = null, int? RepetitionNumber = null, int? RepetitionCount = null);
public sealed record AccountBalance(Guid AccountExternalId, DateOnly ReferenceDate, Money OpeningBalance, Money EffectiveBalance);

public interface IEntryCatalog
{
    Task<ClassificationResult> AdjustInvoiceAsync(InvoiceAdjustmentRequest request, CancellationToken cancellationToken = default);
    Task<ClassificationResult> CreateAsync(CreateEntryRequest request, CancellationToken cancellationToken = default);
    Task<ClassificationResult> CreateRepeatedAsync(CreateEntryRequest request, int count, RepeatPeriod period, CancellationToken cancellationToken = default);
    Task<ClassificationResult> CreateInstallmentsAsync(CreateEntryRequest request, int count, CancellationToken cancellationToken = default);
    Task<ClassificationResult> EditAsync(Guid externalId, CreateEntryRequest request, EntryChangeScope scope, CancellationToken cancellationToken = default);
    Task<ClassificationResult> DeleteAsync(Guid externalId, EntryChangeScope scope, CancellationToken cancellationToken = default);
    Task<ClassificationResult> DuplicateAsync(Guid externalId, CancellationToken cancellationToken = default);
    Task<ClassificationResult> ConfirmEffectiveAsync(Guid externalId, DateOnly effectiveDate, CancellationToken cancellationToken = default);
    Task<ClassificationResult> ConfirmEffectiveAsync(Guid externalId, ConfirmEffectiveRequest request, CancellationToken cancellationToken = default);
    Task<ClassificationResult> UndoEffectiveAsync(Guid externalId, CancellationToken cancellationToken = default);
    Task<ClassificationResult> CreateRecurringAsync(CreateRecurringEntryRequest request, CancellationToken cancellationToken = default);
    Task<ClassificationResult> EditRecurringAsync(Guid externalId, EditRecurringEntryRequest request, CancellationToken cancellationToken = default);
    Task<ClassificationResult> DeleteRecurringAsync(Guid externalId, RecurringChangeScope scope, CancellationToken cancellationToken = default);
    Task<RecurringEntryDetails?> GetRecurringAsync(Guid externalId, CancellationToken cancellationToken = default);
    Task<OccurrenceGenerationResult> EnsureRecurringMonthAsync(ReferenceMonth month, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<EntryDetails>> GetAsync(CancellationToken cancellationToken = default);
    Task<EntryQueryResult> QueryAsync(EntryFilter filter, CancellationToken cancellationToken = default);
    Task<EntryQueryResult> QueryPendingAsync(EntryFilter filter, CancellationToken cancellationToken = default);
    Task<bool> IsReimbursementPersonUsedAsync(Guid personExternalId, CancellationToken cancellationToken = default);
    Task<AccountBalance?> GetBalanceAsync(Guid accountExternalId, DateOnly referenceDate, CancellationToken cancellationToken = default);
}
