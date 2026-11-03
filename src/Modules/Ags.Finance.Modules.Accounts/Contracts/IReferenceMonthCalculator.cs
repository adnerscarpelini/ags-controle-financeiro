using Ags.Finance.SharedKernel;

namespace Ags.Finance.Modules.Accounts.Contracts;

public sealed record ReferenceMonthSuggestion(ReferenceMonth? Month, string? Error)
{
    public bool Succeeded => Month.HasValue && Error is null;
}

public interface IReferenceMonthCalculator
{
    Task<ReferenceMonthSuggestion> CalculateAsync(DateOnly purchaseDate, Guid? cardExternalId = null, CancellationToken cancellationToken = default);
}
