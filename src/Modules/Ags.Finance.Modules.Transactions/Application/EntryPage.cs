using Ags.Finance.Modules.Transactions.Domain;
using Ags.Finance.SharedKernel;

namespace Ags.Finance.Modules.Transactions.Application;

internal sealed record EntryPage(IReadOnlyList<TransactionEntry> Entries, Money Income, Money Expenses, int TotalCount, int Page);

internal sealed record EntryAmount(AccountingMonth Month, CategoryKind Kind, Guid SubcategoryExternalId, Money Amount);
