using Ags.Finance.Modules.Accounts.Contracts;
using Ags.Finance.Modules.Accounts.Domain;
using AccountKind = Ags.Finance.Modules.Accounts.Domain.AccountKind;
using CardBrand = Ags.Finance.Modules.Accounts.Domain.CardBrand;

namespace Ags.Finance.Modules.Accounts.Application.ManageCatalog;

internal sealed class AccountCatalog(IAccountsStore store) : IAccountCatalog
{
    public async Task<IReadOnlyList<BankDetails>> GetBanksAsync(CancellationToken cancellationToken = default)
        => (await store.GetBanksAsync(cancellationToken)).Select(x => new BankDetails(x.ExternalId, x.Name)).ToList();

    public async Task<IReadOnlyList<AccountDetails>> GetAccountsAsync(bool activeOnly = true, CancellationToken cancellationToken = default)
        => (await store.GetAccountsAsync(cancellationToken)).Where(x => !activeOnly || x.IsActive)
            .Select(x => new AccountDetails(x.ExternalId, x.BankExternalId, x.Name, (Contracts.AccountKind)x.Kind, x.Color, x.IsActive)).ToList();

    public async Task<IReadOnlyList<CreditCardDetails>> GetCreditCardsAsync(bool activeOnly = true, CancellationToken cancellationToken = default)
    {
        var accounts = await store.GetAccountsAsync(cancellationToken);
        return (await store.GetCreditCardsAsync(cancellationToken))
            .Where(x => !activeOnly || (x.IsActive && accounts.Any(a => a.ExternalId == x.AccountExternalId && a.IsActive)))
            .Select(x => new CreditCardDetails(x.ExternalId, x.AccountExternalId, x.Name, (Contracts.CardBrand)x.Brand, x.CreditLimit, x.ClosingDay, x.DueDay, x.IncludesClosingDay, x.Color, x.IsActive)).ToList();
    }

    public async Task<CatalogResult> CreateBankAsync(string name, CancellationToken cancellationToken = default)
    {
        var bank = Bank.Create(name);
        if (bank is null)
        {
            return new(null, "Informe um nome com até 100 caracteres.");
        }
        return await PersistAsync(bank, cancellationToken);
    }

    public async Task<CatalogResult> SaveAccountAsync(SaveAccountRequest request, CancellationToken cancellationToken = default)
    {
        var accounts = await store.GetAccountsAsync(cancellationToken);
        var account = request.ExternalId is null ? Account.Create() : accounts.FirstOrDefault(x => x.ExternalId == request.ExternalId);
        if (account is null)
        {
            return new(null, "Conta não encontrada.");
        }
        if (!(await store.GetBanksAsync(cancellationToken)).Any(x => x.ExternalId == request.BankExternalId))
        {
            return new(null, "Selecione um banco cadastrado.");
        }
        var error = account.Update(request.BankExternalId, request.Name, (AccountKind)request.Kind, request.Color);
        if (error is not null)
        {
            return new(null, error);
        }
        if (accounts.Any(x => x.ExternalId != account.ExternalId && x.BankExternalId == account.BankExternalId && x.NormalizedName == account.NormalizedName))
        {
            return new(null, "Já existe uma conta com esse nome neste banco.");
        }
        if (account.Kind != AccountKind.Checking && (await store.GetCreditCardsAsync(cancellationToken)).Any(x => x.AccountExternalId == account.ExternalId))
        {
            return new(null, "Uma conta vinculada a cartão deve continuar sendo corrente.");
        }
        return await PersistAsync(account, cancellationToken);
    }

    public async Task<CatalogResult> SaveCreditCardAsync(SaveCreditCardRequest request, CancellationToken cancellationToken = default)
    {
        var card = request.ExternalId is null ? CreditCard.Create() : (await store.GetCreditCardsAsync(cancellationToken)).FirstOrDefault(x => x.ExternalId == request.ExternalId);
        if (card is null)
        {
            return new(null, "Cartão não encontrado.");
        }
        var account = (await store.GetAccountsAsync(cancellationToken)).FirstOrDefault(x => x.ExternalId == request.AccountExternalId);
        if (account is null || account.Kind != AccountKind.Checking || !account.IsActive)
        {
            return new(null, "Selecione uma conta corrente ativa cadastrada.");
        }
        var error = card.Update(request.AccountExternalId, request.Name, (CardBrand)request.Brand, request.CreditLimit, request.ClosingDay, request.DueDay, request.IncludesClosingDay, request.Color);
        return error is null ? await PersistAsync(card, cancellationToken) : new(null, error);
    }

    public async Task<CatalogResult> DeactivateAccountAsync(Guid externalId, CancellationToken cancellationToken = default)
    {
        var account = (await store.GetAccountsAsync(cancellationToken)).FirstOrDefault(x => x.ExternalId == externalId);
        if (account is null)
        {
            return new(null, "Conta não encontrada.");
        }
        account.Deactivate();
        return await PersistAsync(account, cancellationToken);
    }

    public async Task<CatalogResult> DeactivateCreditCardAsync(Guid externalId, CancellationToken cancellationToken = default)
    {
        var card = (await store.GetCreditCardsAsync(cancellationToken)).FirstOrDefault(x => x.ExternalId == externalId);
        if (card is null)
        {
            return new(null, "Cartão não encontrado.");
        }
        card.Deactivate();
        return await PersistAsync(card, cancellationToken);
    }

    private async Task<CatalogResult> PersistAsync(Entity entity, CancellationToken cancellationToken)
        => await store.SaveAsync(entity, cancellationToken) ? new(entity.ExternalId, null) : new(null, "Já existe uma conta com esse nome neste banco.");
}
