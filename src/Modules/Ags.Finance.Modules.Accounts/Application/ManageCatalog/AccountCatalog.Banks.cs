using Ags.Finance.Modules.Accounts.Contracts;
using Ags.Finance.Modules.Accounts.Domain;

namespace Ags.Finance.Modules.Accounts.Application.ManageCatalog;

internal sealed partial class AccountCatalog
{
    private const string DuplicateBank = "Já existe um banco com esse nome, inclusive entre os arquivados.";
    private const string InvalidBankName = "Informe um nome com até 100 caracteres.";
    private const string MissingBank = "Banco não encontrado.";

    public async Task<IReadOnlyList<BankDetails>> GetBanksAsync(CancellationToken cancellationToken = default)
        => (await store.GetBanksAsync(cancellationToken)).Select(x => new BankDetails(x.ExternalId, x.Name, x.LogoVersion, x.IsActive)).ToList();

    public Task<CatalogResult> CreateBankAsync(string name, Guid? externalId = null, CancellationToken cancellationToken = default)
        => operations.ExecuteAsync(async token =>
        {
            var bank = Bank.Create(name);
            if (bank is null) { return new CatalogResult(null, InvalidBankName); }
            var banks = await store.GetBanksAsync(token);
            if (externalId is { } bankId)
            {
                if (bankId == Guid.Empty || banks.Any(x => x.ExternalId == bankId)) { return new CatalogResult(null, "Identidade pública inválida ou já utilizada."); }
                bank.UseExternalId(bankId);
            }

            return banks.Any(x => x.NormalizedName == bank.NormalizedName) ? new CatalogResult(null, DuplicateBank) : await PersistBankAsync(bank, token);
        }, x => x.Succeeded, cancellationToken);

    public Task<CatalogResult> RenameBankAsync(Guid externalId, string name, CancellationToken cancellationToken = default)
        => operations.ExecuteAsync(async token =>
        {
            var banks = await store.GetBanksAsync(token);
            var bank = banks.FirstOrDefault(x => x.ExternalId == externalId);
            if (bank is null) { return new CatalogResult(null, MissingBank); }
            if (!bank.Rename(name)) { return new CatalogResult(null, InvalidBankName); }
            return banks.Any(x => x.ExternalId != externalId && x.NormalizedName == bank.NormalizedName) ? new CatalogResult(null, DuplicateBank) : await PersistBankAsync(bank, token);
        }, x => x.Succeeded, cancellationToken);

    public Task<CatalogResult> DeleteBankAsync(Guid externalId, CancellationToken cancellationToken = default)
        => operations.ExecuteAsync(async token =>
        {
            var bank = (await store.GetBanksAsync(token)).FirstOrDefault(x => x.ExternalId == externalId);
            if (bank is null) { return new CatalogResult(null, MissingBank); }

            // Conta arquivada também conta como uso: ela continua apontando para o banco.
            if ((await store.GetAccountsAsync(token)).Any(x => x.BankExternalId == externalId))
            {
                return new CatalogResult(null, "Este banco tem contas vinculadas. Arquive-o em vez de excluir.");
            }

            await store.DeleteAsync(bank, token);
            return new CatalogResult(externalId, null);
        }, x => x.Succeeded, cancellationToken);

    public Task<CatalogResult> DeactivateBankAsync(Guid externalId, CancellationToken cancellationToken = default)
        => ChangeBankSituationAsync(externalId, bank => bank.Deactivate(), cancellationToken);

    public Task<CatalogResult> ReactivateBankAsync(Guid externalId, CancellationToken cancellationToken = default)
        => ChangeBankSituationAsync(externalId, bank => bank.Reactivate(), cancellationToken);

    private Task<CatalogResult> ChangeBankSituationAsync(Guid externalId, Action<Bank> change, CancellationToken cancellationToken)
        => operations.ExecuteAsync(async token =>
        {
            var bank = (await store.GetBanksAsync(token)).FirstOrDefault(x => x.ExternalId == externalId);
            if (bank is null) { return new CatalogResult(null, MissingBank); }
            change(bank);
            return await PersistBankAsync(bank, token);
        }, x => x.Succeeded, cancellationToken);

    private async Task<CatalogResult> PersistBankAsync(Bank bank, CancellationToken cancellationToken)
        => await store.SaveAsync(bank, cancellationToken) ? new(bank.ExternalId, null) : new(null, DuplicateBank);
}
