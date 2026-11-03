using Ags.Finance.SharedKernel;

namespace Ags.Finance.Modules.Transactions.Contracts;

public enum RecurringChangeScope { OnlyThis, ThisAndFollowing, All }
public enum RecurringEditScope { OnlyThis, AllPending, All }
public sealed record EditRecurringEntryRequest(CreateEntryRequest Entry, RecurringEditScope Scope, int? Day = null);

public sealed record CreateRecurringEntryRequest(CreateEntryRequest Entry, ReferenceMonth? EndMonth = null);
public sealed record RecurringEntryDetails(Guid ExternalId, DateOnly StartDate, int Day, ReferenceMonth? EndMonth, bool IsActive, CreateEntryRequest Template);
public sealed record OccurrenceGenerationResult(int GeneratedCount, int ProtectedCount, string? Error = null)
{
    public bool Succeeded => Error is null;
}
