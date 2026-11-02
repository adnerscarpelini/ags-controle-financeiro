using Ags.Finance.Modules.Accounts.Contracts;
using Ags.Finance.Modules.Transactions.Contracts;
using Ags.Finance.Modules.Transactions.Domain;
using Ags.Finance.SharedKernel;

namespace Ags.Finance.Modules.Transactions.Application.CreateEntry;

internal sealed class EntryCatalog(IEntryStore store, IClassificationCatalog classifications, IMerchantCatalog merchants, IAccountCatalog accounts, ICardLastDigitsCatalog cardDigits, IAccountingMonthCalculator calendar, TimeProvider clock, IFinancialOperation operations, IInvoiceWriteGuard invoices) : IEntryCatalog
{
    public Task<ClassificationResult> CreateAsync(CreateEntryRequest request, CancellationToken cancellationToken = default)
        => operations.ExecuteAsync(token => CreateCoreAsync(request, token), x => x.Succeeded, cancellationToken);

    private async Task<ClassificationResult> CreateCoreAsync(CreateEntryRequest request, CancellationToken cancellationToken)
    {
        var merchant = (await merchants.FindAsync(cancellationToken: cancellationToken)).FirstOrDefault(x => x.ExternalId == request.MerchantExternalId);
        var subcategoryId = request.SubcategoryExternalId == Guid.Empty ? merchant?.DefaultSubcategoryExternalId : request.SubcategoryExternalId;
        var subcategory = (await classifications.GetSubcategoriesAsync(cancellationToken: cancellationToken)).FirstOrDefault(x => x.ExternalId == subcategoryId);
        var card = request.CardExternalId is null ? null : (await accounts.GetCreditCardsAsync(cancellationToken: cancellationToken)).FirstOrDefault(x => x.ExternalId == request.CardExternalId);
        if (request.CardExternalId is not null && card is null)
        {
            return new(null, "Selecione um cartão ativo cadastrado.");
        }
        if (request.CardLastDigitsExternalId is { } digitsId && (card is null || !(await cardDigits.GetAsync(card.ExternalId, cancellationToken: cancellationToken)).Any(x => x.ExternalId == digitsId)))
        {
            return new(null, "Selecione um final ativo pertencente ao cartão escolhido.");
        }
        if (card is not null && subcategory?.Kind != Contracts.CategoryKind.Expense)
        {
            return new(null, "Compras no cartão exigem classificação de despesa.");
        }
        var accountId = card?.AccountExternalId ?? request.AccountExternalId;
        var account = (await accounts.GetAccountsAsync(cancellationToken: cancellationToken)).FirstOrDefault(x => x.ExternalId == accountId);
        if (merchant is null || subcategory is null || account is null)
        {
            return new(null, "Selecione descrição, subcategoria e conta ativas cadastradas.");
        }
        var suggestion = await calendar.CalculateAsync(request.Date, card?.ExternalId, cancellationToken);
        if (!suggestion.Succeeded)
        {
            return new(null, suggestion.Error);
        }
        var entry = new TransactionEntry();
        var error = entry.Update(request.Date, request.Month ?? suggestion.Month!.Value, merchant.ExternalId, subcategory.ExternalId, account.ExternalId, (Domain.CategoryKind)subcategory.Kind, request.Amount, request.Details);
        if (error is not null)
        {
            return new(null, error);
        }
        if (card is not null)
        {
            error = await invoices.RequireOpenAsync(card.ExternalId, entry.Month, cancellationToken);
            if (error is not null) { return new(null, error); }
        }
        entry.SetEffective(request.Date, clock.GetUtcNow());
        if (card is not null) { entry.SetCard(card.ExternalId, request.CardLastDigitsExternalId); }
        return await store.AddAsync(entry, cancellationToken) ? new(entry.ExternalId, null) : new(null, "Um cadastro foi alterado durante a operação. Selecione os dados novamente.");
    }

    public async Task<IReadOnlyList<EntryDetails>> GetAsync(CancellationToken cancellationToken = default)
    {
        var descriptions = (await merchants.FindAsync(activeOnly: false, cancellationToken: cancellationToken)).ToDictionary(x => x.ExternalId);
        var subcategories = (await classifications.GetSubcategoriesAsync(false, cancellationToken)).ToDictionary(x => x.ExternalId);
        return (await store.GetAsync(cancellationToken)).Select(x =>
        {
            var subcategory = subcategories[x.SubcategoryExternalId];
            return new EntryDetails(x.ExternalId, x.Date, x.Month, x.MerchantExternalId, descriptions[x.MerchantExternalId].Name,
                x.SubcategoryExternalId, subcategory.CategoryExternalId, subcategory.DisplayName, x.AccountExternalId, (Contracts.CategoryKind)x.Kind,
                x.Amount, x.Details, x.IsEffective, x.EffectiveDate, x.EffectiveAt, x.CardExternalId, x.CardLastDigitsExternalId);
        }).ToList();
    }

    public async Task<AccountBalance?> GetBalanceAsync(Guid accountExternalId, DateOnly referenceDate, CancellationToken cancellationToken = default)
    {
        var account = (await accounts.GetAccountsAsync(false, cancellationToken)).FirstOrDefault(x => x.ExternalId == accountExternalId);
        if (account is null) { return null; }
        var opening = account.OpeningDate is null || account.OpeningDate <= referenceDate ? account.OpeningBalance : Money.Zero;
        var movements = (await store.GetAsync(cancellationToken)).Where(x => x.AccountExternalId == accountExternalId && x.CardExternalId is null && x.IsEffective
            && x.EffectiveDate <= referenceDate && (account.OpeningDate is null || x.EffectiveDate >= account.OpeningDate)).Sum(x => x.Amount.Amount);
        return new(accountExternalId, referenceDate, opening, opening + new Money(movements));
    }
}
