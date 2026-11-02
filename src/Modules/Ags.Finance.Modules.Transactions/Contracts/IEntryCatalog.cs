using Ags.Finance.SharedKernel;

namespace Ags.Finance.Modules.Transactions.Contracts;

public sealed record CreateEntryRequest(DateOnly Date, Guid MerchantExternalId, Guid SubcategoryExternalId, Guid AccountExternalId, Money Amount, string Details = "", AccountingMonth? Month = null, Guid? CardExternalId = null, Guid? CardLastDigitsExternalId = null);
public sealed record EntryDetails(Guid ExternalId, DateOnly Date, AccountingMonth Month, Guid MerchantExternalId, string MerchantName, Guid SubcategoryExternalId, Guid CategoryExternalId, string Classification, Guid AccountExternalId, CategoryKind Kind, Money Amount, string Details, bool IsEffective, DateOnly? EffectiveDate, DateTimeOffset? EffectiveAt, Guid? CardExternalId = null, Guid? CardLastDigitsExternalId = null);
public sealed record AccountBalance(Guid AccountExternalId, DateOnly ReferenceDate, Money OpeningBalance, Money EffectiveBalance);

public interface IEntryCatalog
{
    Task<ClassificationResult> CreateAsync(CreateEntryRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<EntryDetails>> GetAsync(CancellationToken cancellationToken = default);
    Task<AccountBalance?> GetBalanceAsync(Guid accountExternalId, DateOnly referenceDate, CancellationToken cancellationToken = default);
}
