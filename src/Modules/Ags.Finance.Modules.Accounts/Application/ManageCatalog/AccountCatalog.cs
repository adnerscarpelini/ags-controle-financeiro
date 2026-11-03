using Ags.Finance.Modules.Accounts.Contracts;
using Ags.Finance.Modules.Accounts.Domain;
using AccountKind = Ags.Finance.Modules.Accounts.Domain.AccountKind;
using CardBrand = Ags.Finance.Modules.Accounts.Domain.CardBrand;

namespace Ags.Finance.Modules.Accounts.Application.ManageCatalog;

internal sealed partial class AccountCatalog(IAccountsStore store, TimeProvider clock, IFinancialOperation operations) : IAccountCatalog
{
    public async Task<IReadOnlyList<BankDetails>> GetBanksAsync(CancellationToken cancellationToken = default)
        => (await store.GetBanksAsync(cancellationToken)).Select(x => new BankDetails(x.ExternalId, x.Name, x.LogoVersion)).ToList();

    public async Task<IReadOnlyList<AccountDetails>> GetAccountsAsync(bool activeOnly = true, CancellationToken cancellationToken = default)
        => (await store.GetAccountsAsync(cancellationToken)).Where(x => !activeOnly || x.IsActive)
            .Select(x => new AccountDetails(x.ExternalId, x.BankExternalId, x.Name, (Contracts.AccountKind)x.Kind, x.Color, x.IsActive, x.OpeningBalance, x.OpeningDate)).ToList();

    public async Task<IReadOnlyList<CreditCardDetails>> GetCreditCardsAsync(bool activeOnly = true, CancellationToken cancellationToken = default)
    {
        var accounts = await store.GetAccountsAsync(cancellationToken);
        return (await store.GetCreditCardsAsync(cancellationToken))
            .Where(x => !activeOnly || (x.IsActive && accounts.Any(a => a.ExternalId == x.AccountExternalId && a.IsActive)))
            .Select(x => new CreditCardDetails(x.ExternalId, x.AccountExternalId, x.Name, (Contracts.CardBrand)x.Brand, x.CreditLimit, x.ClosingDay, x.DueDay, x.IncludesClosingDay, x.Color, x.IsActive, accounts.Any(a => a.ExternalId == x.AccountExternalId && a.IsActive))).ToList();
    }

    public async Task<CatalogResult> CreateBankAsync(string name, Guid? externalId = null, CancellationToken cancellationToken = default)
    {
        var bank = Bank.Create(name);
        if (bank is null)
        {
            return new(null, "Informe um nome com até 100 caracteres.");
        }
        if (externalId is { } bankId)
        {
            if (bankId == Guid.Empty || (await store.GetBanksAsync(cancellationToken)).Any(x => x.ExternalId == bankId)) { return new(null, "Identidade pública inválida ou já utilizada."); }
            bank.UseExternalId(bankId);
        }
        return await PersistAsync(bank, cancellationToken);
    }

    public Task<CatalogResult> SaveAccountAsync(SaveAccountRequest request, CancellationToken cancellationToken = default)
        => operations.ExecuteAsync(token => SaveAccountCoreAsync(request, token), x => x.Succeeded, cancellationToken);

    private async Task<CatalogResult> SaveAccountCoreAsync(SaveAccountRequest request, CancellationToken cancellationToken)
    {
        var accounts = await store.GetAccountsAsync(cancellationToken);
        var account = request.ExternalId is null ? Account.Create() : accounts.FirstOrDefault(x => x.ExternalId == request.ExternalId);
        if (account is null)
        {
            return new(null, "Conta não encontrada.");
        }
        if (request.ExternalId is null && request.NewExternalId is { } accountId)
        {
            if (accountId == Guid.Empty || accounts.Any(x => x.ExternalId == accountId)) { return new(null, "Identidade pública inválida ou já utilizada."); }
            account.UseExternalId(accountId);
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
        if (request.OpeningBalance is { } openingBalance)
        {
            error = account.SetOpeningBalance(openingBalance, request.OpeningDate);
        }
        else if (request.ExternalId is null && account.Kind == AccountKind.Checking)
        {
            error = account.SetOpeningBalance(default, request.OpeningDate ?? DateOnly.FromDateTime(clock.GetLocalNow().DateTime));
        }
        return error is null ? await PersistAsync(account, cancellationToken) : new(null, error);
    }

    public Task<CatalogResult> SaveCreditCardAsync(SaveCreditCardRequest request, CancellationToken cancellationToken = default)
        => operations.ExecuteAsync(token => SaveCreditCardCoreAsync(request, token), x => x.Succeeded, cancellationToken);

    private async Task<CatalogResult> SaveCreditCardCoreAsync(SaveCreditCardRequest request, CancellationToken cancellationToken)
    {
        var card = request.ExternalId is null ? CreditCard.Create() : (await store.GetCreditCardsAsync(cancellationToken)).FirstOrDefault(x => x.ExternalId == request.ExternalId);
        if (card is null)
        {
            return new(null, "Cartão não encontrado.");
        }
        if (request.ExternalId is null && request.NewExternalId is { } cardId)
        {
            if (cardId == Guid.Empty || (await store.GetCreditCardsAsync(cancellationToken)).Any(x => x.ExternalId == cardId)) { return new(null, "Identidade pública inválida ou já utilizada."); }
            card.UseExternalId(cardId);
        }
        var account = (await store.GetAccountsAsync(cancellationToken)).FirstOrDefault(x => x.ExternalId == request.AccountExternalId);
        if (account is null || account.Kind != AccountKind.Checking || !account.IsActive)
        {
            return new(null, "Selecione uma conta corrente ativa cadastrada.");
        }
        var error = card.Update(request.AccountExternalId, request.Name, (CardBrand)request.Brand, request.CreditLimit, request.ClosingDay, request.DueDay, request.IncludesClosingDay, request.Color);
        return error is null ? await PersistAsync(card, cancellationToken) : new(null, error);
    }

    public Task<CatalogResult> DeactivateAccountAsync(Guid externalId, CancellationToken cancellationToken = default)
        => operations.ExecuteAsync(token => DeactivateAccountCoreAsync(externalId, token), x => x.Succeeded, cancellationToken);

    private async Task<CatalogResult> DeactivateAccountCoreAsync(Guid externalId, CancellationToken cancellationToken)
    {
        var account = (await store.GetAccountsAsync(cancellationToken)).FirstOrDefault(x => x.ExternalId == externalId);
        if (account is null)
        {
            return new(null, "Conta não encontrada.");
        }
        account.Deactivate();
        return await PersistAsync(account, cancellationToken);
    }

    public Task<CatalogResult> DeactivateCreditCardAsync(Guid externalId, CancellationToken cancellationToken = default)
        => operations.ExecuteAsync(token => DeactivateCreditCardCoreAsync(externalId, token), x => x.Succeeded, cancellationToken);

    private async Task<CatalogResult> DeactivateCreditCardCoreAsync(Guid externalId, CancellationToken cancellationToken)
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
