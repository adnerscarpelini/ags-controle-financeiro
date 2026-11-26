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
public sealed record BankDetails(Guid ExternalId, string Name, Guid? LogoVersion = null, bool IsActive = true);
public sealed record AccountDetails(Guid ExternalId, Guid BankExternalId, string Name, AccountKind Kind, string Color, bool IsActive, Money OpeningBalance = default, DateOnly? OpeningDate = null);
/// <param name="CashbackPercent">Percentual de cashback gravado nas compras novas; nulo quando o cartão não usa cashback.</param>
public sealed record CreditCardDetails(Guid ExternalId, Guid AccountExternalId, string Name, CardBrand Brand, Money CreditLimit, int ClosingDay, int DueDay, bool IncludesClosingDay, string Color, bool IsActive, bool IsAccountActive = true, decimal? CashbackPercent = null)
{
    public bool IsAvailable => IsActive && IsAccountActive;

    public bool UsesCashback => CashbackPercent is not null;
}
public sealed record SaveAccountRequest(Guid? ExternalId, Guid BankExternalId, string Name, AccountKind Kind, string Color, Money? OpeningBalance = null, DateOnly? OpeningDate = null, Guid? NewExternalId = null);
/// <param name="Cashback">Nulo mantém a configuração atual (por exemplo, na importação CSV); <see cref="CardCashback.None"/> desliga o cashback.</param>
public sealed record SaveCreditCardRequest(Guid? ExternalId, Guid AccountExternalId, string Name, CardBrand Brand, Money CreditLimit, int ClosingDay, int DueDay, bool IncludesClosingDay, string Color, Guid? NewExternalId = null, CardCashback? Cashback = null);

/// <summary>Configuração de cashback do cartão: <c>Percent</c> nulo significa que o cartão não usa cashback.</summary>
public sealed record CardCashback(decimal? Percent)
{
    public static CardCashback None { get; } = new((decimal?)null);
}
public sealed record CatalogResult(Guid? ExternalId, string? Error)
{
    public bool Succeeded => Error is null;
}
public interface IAccountCatalog
{
    /// <summary>Traz também os arquivados: contas antigas e relatórios continuam mostrando o banco. Novas seleções filtram por IsActive.</summary>
    Task<IReadOnlyList<BankDetails>> GetBanksAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AccountDetails>> GetAccountsAsync(bool activeOnly = true, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CreditCardDetails>> GetCreditCardsAsync(bool activeOnly = true, CancellationToken cancellationToken = default);
    Task<CatalogResult> CreateBankAsync(string name, Guid? externalId = null, CancellationToken cancellationToken = default);
    Task<CatalogResult> RenameBankAsync(Guid externalId, string name, CancellationToken cancellationToken = default);
    Task<CatalogResult> DeleteBankAsync(Guid externalId, CancellationToken cancellationToken = default);
    Task<CatalogResult> DeactivateBankAsync(Guid externalId, CancellationToken cancellationToken = default);
    Task<CatalogResult> ReactivateBankAsync(Guid externalId, CancellationToken cancellationToken = default);
    Task<CatalogResult> SaveAccountAsync(SaveAccountRequest request, CancellationToken cancellationToken = default);
    Task<CatalogResult> SaveCreditCardAsync(SaveCreditCardRequest request, CancellationToken cancellationToken = default);
    Task<CatalogResult> DeactivateAccountAsync(Guid externalId, CancellationToken cancellationToken = default);
    Task<CatalogResult> DeactivateCreditCardAsync(Guid externalId, CancellationToken cancellationToken = default);
    Task<CatalogResult> ReactivateAccountAsync(Guid externalId, CancellationToken cancellationToken = default);
    Task<CatalogResult> ReactivateCreditCardAsync(Guid externalId, CancellationToken cancellationToken = default);
}
