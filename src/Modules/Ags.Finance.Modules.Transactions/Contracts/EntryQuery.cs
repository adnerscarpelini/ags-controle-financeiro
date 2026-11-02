using Ags.Finance.SharedKernel;

namespace Ags.Finance.Modules.Transactions.Contracts;

public sealed record EntryFilter(AccountingMonth? Month = null, DateOnly? FromDate = null, DateOnly? ToDate = null,
    Guid? AccountExternalId = null, Guid? CardExternalId = null, Guid? CardLastDigitsExternalId = null,
    Guid? CategoryExternalId = null, Guid? SubcategoryExternalId = null, string Text = "", bool? IsEffective = null,
    CategoryKind? Kind = null, int Page = 1, int PageSize = 100, bool PendingRecurringOnly = false);

public sealed record EntryQueryResult(IReadOnlyList<EntryDetails> Entries, Money Income, Money Expenses, Money Balance,
    int TotalCount, int Page, int PageSize, string? Error = null)
{
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCount / (decimal)PageSize));
}
