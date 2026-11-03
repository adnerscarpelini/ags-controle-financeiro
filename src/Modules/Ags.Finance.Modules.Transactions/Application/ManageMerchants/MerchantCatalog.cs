using Ags.Finance.Modules.Accounts.Contracts;
using Ags.Finance.Modules.Transactions.Contracts;
using Ags.Finance.Modules.Transactions.Domain;

namespace Ags.Finance.Modules.Transactions.Application.ManageMerchants;

internal sealed class MerchantCatalog(IMerchantStore store, IClassificationCatalog classifications, IEntryStore entries, IInvoiceWriteGuard invoices, IFinancialOperation? operations = null) : IMerchantCatalog
{
    public async Task<IReadOnlyList<MerchantDetails>> FindAsync(string search = "", bool activeOnly = true, CancellationToken cancellationToken = default)
    {
        var normalized = Merchant.Normalize(search);
        return (await store.GetAsync(cancellationToken)).Where(x => (!activeOnly || x.IsActive) && x.NormalizedName.Contains(normalized, StringComparison.Ordinal))
            .Select(x => new MerchantDetails(x.ExternalId, x.Name, x.DefaultSubcategoryExternalId, x.IsActive)).ToList();
    }

    public async Task<ClassificationResult> SaveAsync(Guid? externalId, string name, Guid? defaultSubcategoryExternalId, Guid? newExternalId = null, CancellationToken cancellationToken = default)
    {
        var merchants = await store.GetAsync(cancellationToken);
        var merchant = externalId is null ? new Merchant() : merchants.FirstOrDefault(x => x.ExternalId == externalId);
        if (merchant is null)
        {
            return new(null, "Descrição não encontrada.");
        }
        if (defaultSubcategoryExternalId is { } subcategoryId && !(await classifications.GetSubcategoriesAsync(cancellationToken: cancellationToken)).Any(x => x.ExternalId == subcategoryId))
        {
            return new(null, "Selecione uma subcategoria ativa.");
        }
        if (externalId is null && newExternalId is { } merchantId)
        {
            if (merchantId == Guid.Empty || merchants.Any(x => x.ExternalId == merchantId)) { return new(null, "Identidade pública inválida ou já utilizada."); }
            merchant.UseExternalId(merchantId);
        }
        var error = merchant.Update(name, defaultSubcategoryExternalId);
        if (error is not null)
        {
            return new(null, error);
        }
        var duplicate = merchants.FirstOrDefault(x => x.ExternalId != merchant.ExternalId && x.NormalizedName == merchant.NormalizedName);
        if (duplicate is not null)
        {
            return new(duplicate.ExternalId, $"Esta descrição já existe: {duplicate.Name}.");
        }
        return await SaveAsync(merchant, cancellationToken);
    }

    public async Task<ClassificationResult> DeactivateAsync(Guid externalId, CancellationToken cancellationToken = default)
    {
        var merchant = (await store.GetAsync(cancellationToken)).FirstOrDefault(x => x.ExternalId == externalId);
        if (merchant is null)
        {
            return new(null, "Descrição não encontrada.");
        }
        merchant.Deactivate();
        return await SaveAsync(merchant, cancellationToken);
    }

    public Task<ClassificationResult> MergeAsync(Guid sourceExternalId, Guid targetExternalId, CancellationToken cancellationToken = default)
        => operations is null ? MergeCoreAsync(sourceExternalId, targetExternalId, cancellationToken)
            : operations.ExecuteAsync(token => MergeCoreAsync(sourceExternalId, targetExternalId, token), x => x.Succeeded, cancellationToken);

    private async Task<ClassificationResult> MergeCoreAsync(Guid sourceExternalId, Guid targetExternalId, CancellationToken cancellationToken)
    {
        if (sourceExternalId == targetExternalId)
        {
            return new(null, "Escolha duas descrições diferentes.");
        }
        var affected = (await entries.GetAsync(cancellationToken)).Where(x => x.MerchantExternalId == sourceExternalId && x.CardExternalId is not null)
            .Select(x => (Card: x.CardExternalId!.Value, x.Month)).Distinct();
        foreach (var invoice in affected)
        {
            var conflict = await invoices.RequireOpenAsync(invoice.Card, invoice.Month, cancellationToken);
            if (conflict is not null) { return new(null, conflict); }
        }
        var error = await store.MergeAsync(sourceExternalId, targetExternalId, cancellationToken);
        return new(error is null ? targetExternalId : null, error);
    }

    private async Task<ClassificationResult> SaveAsync(Merchant merchant, CancellationToken cancellationToken)
        => await store.SaveAsync(merchant, cancellationToken) ? new(merchant.ExternalId, null) : new(null, "Já existe uma descrição equivalente. Consulte as sugestões.");
}
