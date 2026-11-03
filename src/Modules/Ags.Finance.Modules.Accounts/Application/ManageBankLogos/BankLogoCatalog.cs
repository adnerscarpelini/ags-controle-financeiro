using Ags.Finance.Modules.Accounts.Contracts;

namespace Ags.Finance.Modules.Accounts.Application.ManageBankLogos;

internal sealed class BankLogoCatalog(IAccountsStore store, ILogoProcessor processor, IFinancialOperation operations) : IBankLogoCatalog
{
    public LogoPreviewResult CreatePreview(byte[] content, LogoCrop crop) => processor.CreatePreview(content, crop);

    public Task<CatalogResult> SaveAsync(Guid bankExternalId, byte[] content, LogoCrop crop, CancellationToken cancellationToken = default)
    {
        var preview = processor.CreatePreview(content, crop);
        if (preview.Png is not { } png) { return Task.FromResult(new CatalogResult(null, preview.Error)); }
        return SetAsync(bankExternalId, png, cancellationToken);
    }
    public Task<CatalogResult> RemoveAsync(Guid bankExternalId, CancellationToken cancellationToken = default) => SetAsync(bankExternalId, null, cancellationToken);

    private Task<CatalogResult> SetAsync(Guid bankExternalId, byte[]? png, CancellationToken cancellationToken)
        => operations.ExecuteAsync(async token =>
        {
            var bank = (await store.GetBanksAsync(token)).FirstOrDefault(x => x.ExternalId == bankExternalId);
            if (bank is null) { return new CatalogResult(null, "Banco não encontrado."); }
            var error = bank.SetLogo(png);
            if (error is not null) { return new CatalogResult(null, error); }
            return await store.SaveAsync(bank, token) ? new CatalogResult(bank.ExternalId, null) : new CatalogResult(null, "Não foi possível salvar o logo.");
        }, x => x.Succeeded, cancellationToken);

    public async Task<BankLogoDetails?> GetAsync(Guid bankExternalId, CancellationToken cancellationToken = default)
    {
        var bank = (await store.GetBanksAsync(cancellationToken)).FirstOrDefault(x => x.ExternalId == bankExternalId);
        return bank?.LogoData is { } data && bank.LogoVersion is { } version ? new(data.ToArray(), version) : null;
    }
}
