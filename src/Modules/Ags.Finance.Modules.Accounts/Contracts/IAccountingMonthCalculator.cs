using Ags.Finance.SharedKernel;

namespace Ags.Finance.Modules.Accounts.Contracts;

public sealed record AccountingMonthSuggestion(AccountingMonth? Month, string? Error)
{
    public bool Succeeded => Month.HasValue && Error is null;
}

public interface IAccountingMonthCalculator
{
    Task<AccountingMonthSuggestion> CalculateAsync(DateOnly purchaseDate, Guid? cardExternalId = null, CancellationToken cancellationToken = default);
}
