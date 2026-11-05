using Ags.Finance.SharedKernel;

namespace Ags.Finance.Modules.Transactions.Contracts;

public enum RecurringChangeScope { OnlyThis, ThisAndFollowing, All }
public enum RecurringEditScope { OnlyThis, ThisAndFutureOpen, All }
public sealed record EditRecurringEntryRequest(CreateEntryRequest Entry, RecurringEditScope Scope, int? Day = null);

public sealed record CreateRecurringEntryRequest(CreateEntryRequest Entry, ReferenceMonth? EndMonth = null);
public sealed record RecurringEntryDetails(Guid ExternalId, DateOnly StartDate, int Day, ReferenceMonth? EndMonth, bool IsActive, CreateEntryRequest Template);
/// <summary>Uma série ativa, para a tela de lançamentos fixos. <see cref="Amount"/> é positivo; <see cref="Kind"/> distingue despesa de receita.</summary>
public sealed record RecurringSeriesDetails(Guid ExternalId, int Day, string MerchantName, string Classification, CategoryKind Kind, Money Amount, Guid AccountExternalId, Guid? CardExternalId, ReferenceMonth? EndMonth, string Details, EntryReimbursement? Reimbursement);

/// <summary><see cref="AccountExternalId"/> só vale fora do cartão; vazio mantém a conta.</summary>
public sealed record EditRecurringSeriesRequest(Money Amount, int Day, Guid? AccountExternalId = null);

public sealed record OccurrenceGenerationResult(int GeneratedCount, int ProtectedCount, string? Error = null)
{
    public bool Succeeded => Error is null;
}
