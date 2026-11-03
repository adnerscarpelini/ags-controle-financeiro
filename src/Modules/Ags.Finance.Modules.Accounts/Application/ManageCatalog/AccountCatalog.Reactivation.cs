using Ags.Finance.Modules.Accounts.Contracts;

namespace Ags.Finance.Modules.Accounts.Application.ManageCatalog;

internal sealed partial class AccountCatalog
{
    public Task<CatalogResult> ReactivateAccountAsync(Guid externalId, CancellationToken cancellationToken = default)
        => operations.ExecuteAsync(async token =>
        {
            var account = (await store.GetAccountsAsync(token)).FirstOrDefault(x => x.ExternalId == externalId);
            if (account is null) { return new CatalogResult(null, "Conta não encontrada."); }
            account.Reactivate();
            return await PersistAsync(account, token);
        }, x => x.Succeeded, cancellationToken);

    public Task<CatalogResult> ReactivateCreditCardAsync(Guid externalId, CancellationToken cancellationToken = default)
        => operations.ExecuteAsync(async token =>
        {
            var card = (await store.GetCreditCardsAsync(token)).FirstOrDefault(x => x.ExternalId == externalId);
            if (card is null) { return new CatalogResult(null, "Cartão não encontrado."); }
            if (!(await store.GetAccountsAsync(token)).Any(x => x.ExternalId == card.AccountExternalId)) { return new CatalogResult(null, "Conta vinculada não encontrada."); }
            card.Reactivate();
            return await PersistAsync(card, token);
        }, x => x.Succeeded, cancellationToken);
}
