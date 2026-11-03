using Ags.Finance.SharedKernel;

namespace Ags.Finance.Modules.Accounts.Contracts;

public enum AccountKind
{
    Checking,
    Investment,
    Savings
}
public enum CardBrand
{
    Visa,
    Mastercard,
    Elo
}
public sealed record BankDetails(Guid ExternalId, string Name);
public sealed record AccountDetails(Guid ExternalId, Guid BankExternalId, string Name, AccountKind Kind, string Color, bool IsActive, Money OpeningBalance = default, DateOnly? OpeningDate = null);
public sealed record CreditCardDetails(Guid ExternalId, Guid AccountExternalId, string Name, CardBrand Brand, Money CreditLimit, int ClosingDay, int DueDay, bool IncludesClosingDay, string Color, bool IsActive, bool IsAccountActive = true)
{
    public bool IsAvailable => IsActive && IsAccountActive;
}
public sealed record SaveAccountRequest(Guid? ExternalId, Guid BankExternalId, string Name, AccountKind Kind, string Color, Money? OpeningBalance = null, DateOnly? OpeningDate = null);
public sealed record SaveCreditCardRequest(Guid? ExternalId, Guid AccountExternalId, string Name, CardBrand Brand, Money CreditLimit, int ClosingDay, int DueDay, bool IncludesClosingDay, string Color);
public sealed record CatalogResult(Guid? ExternalId, string? Error)
{
    public bool Succeeded => Error is null;
}
public interface IAccountCatalog
{
    Task<IReadOnlyList<BankDetails>> GetBanksAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AccountDetails>> GetAccountsAsync(bool activeOnly = true, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CreditCardDetails>> GetCreditCardsAsync(bool activeOnly = true, CancellationToken cancellationToken = default);
    Task<CatalogResult> CreateBankAsync(string name, CancellationToken cancellationToken = default);
    Task<CatalogResult> SaveAccountAsync(SaveAccountRequest request, CancellationToken cancellationToken = default);
    Task<CatalogResult> SaveCreditCardAsync(SaveCreditCardRequest request, CancellationToken cancellationToken = default);
    Task<CatalogResult> DeactivateAccountAsync(Guid externalId, CancellationToken cancellationToken = default);
    Task<CatalogResult> DeactivateCreditCardAsync(Guid externalId, CancellationToken cancellationToken = default);
    Task<CatalogResult> ReactivateAccountAsync(Guid externalId, CancellationToken cancellationToken = default);
    Task<CatalogResult> ReactivateCreditCardAsync(Guid externalId, CancellationToken cancellationToken = default);
}
