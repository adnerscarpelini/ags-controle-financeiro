namespace Ags.Finance.Modules.Accounts.Contracts;

public enum CardUsage
{
    Own,
    Shared,
    Subscription
}

public sealed record CardLastDigitsDetails(Guid ExternalId, Guid CardExternalId, string Digits, CardUsage Usage, string Label, bool IsActive);
public sealed record SaveCardLastDigitsRequest(Guid? ExternalId, Guid CardExternalId, string Digits, CardUsage Usage, string Label, Guid? NewExternalId = null);

public interface ICardLastDigitsCatalog
{
    Task<IReadOnlyList<CardLastDigitsDetails>> GetAsync(Guid cardExternalId, bool activeOnly = true, CancellationToken cancellationToken = default);
    Task<CatalogResult> SaveAsync(SaveCardLastDigitsRequest request, CancellationToken cancellationToken = default);
    Task<CatalogResult> DeactivateAsync(Guid externalId, CancellationToken cancellationToken = default);
}
