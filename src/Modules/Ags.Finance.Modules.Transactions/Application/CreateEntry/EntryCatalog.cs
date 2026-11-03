using Ags.Finance.Modules.Accounts.Contracts;
using Ags.Finance.Modules.Attachments.Contracts;
using Ags.Finance.Modules.Transactions.Contracts;
using Ags.Finance.Modules.Transactions.Domain;
using Ags.Finance.SharedKernel;

namespace Ags.Finance.Modules.Transactions.Application.CreateEntry;

internal sealed partial class EntryCatalog(IEntryStore store, IAccountMovementStore cashMovements, IClassificationCatalog classifications, IMerchantCatalog merchants, IAccountCatalog accounts, ICardLastDigitsCatalog cardDigits, IAccountingMonthCalculator calendar, TimeProvider clock, IFinancialOperation operations, IInvoiceWriteGuard invoices, IReimbursementWriteGuard reimbursements, IAttachmentCatalog? attachments = null) : IEntryCatalog
{
    public Task<ClassificationResult> CreateAsync(CreateEntryRequest request, CancellationToken cancellationToken = default)
        => operations.ExecuteAsync(token => CreateCoreAsync(request, token), x => x.Succeeded, cancellationToken);

    private async Task<ClassificationResult> CreateCoreAsync(CreateEntryRequest request, CancellationToken cancellationToken)
    {
        var prepared = await PrepareAsync(request, cancellationToken);
        if (prepared.Entry is not { } entry) { return new(null, prepared.Error); }
        return await store.AddAsync(entry, cancellationToken) ? new(entry.ExternalId, null) : new(null, "Um cadastro foi alterado durante a operação. Selecione os dados novamente.");
    }

    public Task<ClassificationResult> CreateInstallmentsAsync(CreateEntryRequest request, int count, CancellationToken cancellationToken = default)
        => operations.ExecuteAsync(token => CreateInstallmentsCoreAsync(request, count, token), x => x.Succeeded, cancellationToken);

    private async Task<ClassificationResult> CreateInstallmentsCoreAsync(CreateEntryRequest request, int count, CancellationToken cancellationToken)
    {
        if (request.CardExternalId is null || count is < 2 or > 48) { return new(null, "Selecione um cartão e informe de 2 a 48 parcelas."); }
        var prepared = await PrepareAsync(request, cancellationToken);
        if (prepared.Entry is not { CardExternalId: { } cardId } template) { return new(null, prepared.Error); }
        if ((9999 - template.Month.Year) * 12 + 12 - template.Month.Month < count - 1) { return new(null, "O parcelamento ultrapassa o calendário suportado."); }
        var purchase = InstallmentPurchase.Create(cardId, template.Month, request.Amount, count);
        if (purchase is null) { return new(null, "Dados de parcelamento inválidos."); }
        var parts = request.Amount.Split(count);
        var generated = new List<TransactionEntry>();
        for (var index = 0; index < count; index++)
        {
            var month = template.Month.AddMonths(index);
            var error = await invoices.RequireOpenAsync(cardId, month, cancellationToken);
            if (error is not null) { return new(null, error); }
            var entry = new TransactionEntry();
            error = entry.Update(template.Date, month, template.MerchantExternalId, template.SubcategoryExternalId, template.AccountExternalId, template.Kind, request.Amount, template.Details);
            if (error is not null) { return new(null, error); }
            entry.SetCard(cardId, template.CardLastDigitsExternalId);
            entry.SetEffective(template.Date, clock.GetUtcNow());
            entry.SetInstallment(purchase.ExternalId, index + 1, parts[index]);
            error = entry.SetReimbursement(template.ReimbursementPersonExternalId, template.ReimbursementPercent);
            if (error is not null) { return new(null, error); }
            generated.Add(entry);
        }
        return await store.AddPurchaseAsync(purchase, generated, cancellationToken) ? new(purchase.ExternalId, null) : new(null, "Não foi possível gravar a compra parcelada.");
    }

    private async Task<(TransactionEntry? Entry, string? Error)> PrepareAsync(CreateEntryRequest request, CancellationToken cancellationToken, TransactionEntry? original = null)
    {
        var merchant = (await merchants.FindAsync(activeOnly: false, cancellationToken: cancellationToken)).FirstOrDefault(x => x.ExternalId == request.MerchantExternalId && (x.IsActive || x.ExternalId == original?.MerchantExternalId));
        var subcategoryId = request.SubcategoryExternalId == Guid.Empty ? merchant?.DefaultSubcategoryExternalId : request.SubcategoryExternalId;
        var subcategory = (await classifications.GetSubcategoriesAsync(false, cancellationToken)).FirstOrDefault(x => x.ExternalId == subcategoryId && (x.IsActive || x.ExternalId == original?.SubcategoryExternalId));
        var card = request.CardExternalId is null ? null : (await accounts.GetCreditCardsAsync(cancellationToken: cancellationToken)).FirstOrDefault(x => x.ExternalId == request.CardExternalId);
        if (card is null && request.CardExternalId is not null && request.CardExternalId == original?.CardExternalId)
        {
            card = (await accounts.GetCreditCardsAsync(false, cancellationToken)).FirstOrDefault(x => x.ExternalId == request.CardExternalId);
        }
        if (request.CardExternalId is not null && card is null)
        {
            return (null, "Selecione um cartão ativo cadastrado.");
        }
        if (request.CardLastDigitsExternalId is { } digitsId && (card is null || !(await cardDigits.GetAsync(card.ExternalId, false, cancellationToken)).Any(x => x.ExternalId == digitsId && (x.IsActive || (original is not null && x.ExternalId == original.CardLastDigitsExternalId && card.ExternalId == original.CardExternalId)))))
        {
            return (null, "Selecione um final ativo pertencente ao cartão escolhido.");
        }
        if (card is not null && subcategory?.Kind != Contracts.CategoryKind.Expense && !(original is not null && original.SubcategoryExternalId == subcategory?.ExternalId && original.Kind == Domain.CategoryKind.Expense))
        {
            return (null, "Compras no cartão exigem classificação de despesa.");
        }
        var accountId = card is not null && !(original is not null && card.ExternalId == original.CardExternalId && request.AccountExternalId == original.AccountExternalId)
            ? card.AccountExternalId : request.AccountExternalId;
        var account = (await accounts.GetAccountsAsync(false, cancellationToken)).FirstOrDefault(x => x.ExternalId == accountId && (x.IsActive || x.ExternalId == original?.AccountExternalId));
        if (merchant is null || subcategory is null || account is null)
        {
            return (null, "Selecione descrição, subcategoria e conta ativas cadastradas.");
        }
        var suggestion = request.Month is { } selectedMonth ? new AccountingMonthSuggestion(selectedMonth, null) : await calendar.CalculateAsync(request.Date, card?.ExternalId, cancellationToken);
        if (!suggestion.Succeeded)
        {
            return (null, suggestion.Error);
        }
        var entry = new TransactionEntry();
        var kind = original is not null && original.SubcategoryExternalId == subcategory.ExternalId ? original.Kind : (Domain.CategoryKind)subcategory.Kind;
        var error = entry.Update(request.Date, request.Month ?? suggestion.Month!.Value, merchant.ExternalId, subcategory.ExternalId, account.ExternalId, kind, request.Amount, request.Details, allowZeroAmount: original?.InstallmentPurchaseExternalId is not null && request.Amount.IsZero);
        if (error is not null)
        {
            return (null, error);
        }
        error = entry.SetReimbursement(request.Reimbursement?.PersonExternalId, request.Reimbursement?.Percent);
        if (error is not null) { return (null, error); }
        if (request.Reimbursement is { } reimbursement)
        {
            error = await reimbursements.RequirePersonAsync(reimbursement.PersonExternalId, cancellationToken);
            if (error is not null) { return (null, error); }
        }
        if (card is not null)
        {
            error = await invoices.RequireOpenAsync(card.ExternalId, entry.Month, cancellationToken);
            if (error is not null) { return (null, error); }
        }
        entry.SetEffective(request.Date, clock.GetUtcNow());
        if (card is not null) { entry.SetCard(card.ExternalId, request.CardLastDigitsExternalId); }
        return (entry, null);
    }

    public Task<IReadOnlyList<EntryDetails>> GetAsync(CancellationToken cancellationToken = default)
        => operations.ExecuteAsync(GetCoreAsync, _ => true, cancellationToken);

    private async Task<IReadOnlyList<EntryDetails>> GetCoreAsync(CancellationToken cancellationToken)
    {
        var generated = await GenerateRecurringAsync(new(), cancellationToken);
        if (!generated.Succeeded) { throw new InvalidOperationException(generated.Error); }
        return await DescribeWithAdjustmentsAsync(await store.GetAsync(cancellationToken), await store.GetInvoiceAdjustmentsAsync(cancellationToken), cancellationToken);
    }

    private async Task<IReadOnlyList<EntryDetails>> DescribeAsync(IReadOnlyList<TransactionEntry> rows, CancellationToken cancellationToken)
    {
        var purchases = (await store.GetPurchasesAsync(cancellationToken)).ToDictionary(x => x.ExternalId);
        var descriptions = (await merchants.FindAsync(activeOnly: false, cancellationToken: cancellationToken)).ToDictionary(x => x.ExternalId);
        var subcategories = (await classifications.GetSubcategoriesAsync(false, cancellationToken)).ToDictionary(x => x.ExternalId);
        return rows.Select(x =>
        {
            var subcategory = subcategories[x.SubcategoryExternalId];
            return new EntryDetails(x.ExternalId, x.Date, x.Month, x.MerchantExternalId, descriptions[x.MerchantExternalId].Name,
                x.SubcategoryExternalId, subcategory.CategoryExternalId, subcategory.DisplayName, x.AccountExternalId, (Contracts.CategoryKind)x.Kind,
                x.Amount, x.Details, x.IsEffective, x.EffectiveDate, x.EffectiveAt, x.CardExternalId, x.CardLastDigitsExternalId, x.InstallmentPurchaseExternalId, x.InstallmentNumber,
                x.InstallmentPurchaseExternalId is { } purchaseId ? purchases[purchaseId].Count : null, x.RecurringEntryExternalId, x.OccurrenceMonth, Reimbursement: ReimbursementOf(x));
        }).ToList();
    }

    private static EntryReimbursement? ReimbursementOf(TransactionEntry entry)
        => entry.ReimbursementPersonExternalId is { } person && entry.ReimbursementPercent is { } percent ? new(person, percent) : null;

    public Task<bool> IsReimbursementPersonUsedAsync(Guid personExternalId, CancellationToken cancellationToken = default)
        => operations.ExecuteAsync(async token => (await store.GetAsync(token)).Any(x => x.ReimbursementPersonExternalId == personExternalId), _ => true, cancellationToken);

    public Task<AccountBalance?> GetBalanceAsync(Guid accountExternalId, DateOnly referenceDate, CancellationToken cancellationToken = default)
        => operations.ExecuteAsync(token => GetBalanceCoreAsync(accountExternalId, referenceDate, token), _ => true, cancellationToken);

    private async Task<AccountBalance?> GetBalanceCoreAsync(Guid accountExternalId, DateOnly referenceDate, CancellationToken cancellationToken)
    {
        var account = (await accounts.GetAccountsAsync(false, cancellationToken)).FirstOrDefault(x => x.ExternalId == accountExternalId);
        if (account is null) { return null; }
        var opening = account.OpeningDate is null || account.OpeningDate <= referenceDate ? account.OpeningBalance : Money.Zero;
        var movements = (await store.GetAsync(cancellationToken)).Where(x => x.AccountExternalId == accountExternalId && x.CardExternalId is null && x.IsEffective
            && x.EffectiveDate <= referenceDate && (account.OpeningDate is null || x.EffectiveDate >= account.OpeningDate)).Sum(x => x.Amount.Amount);
        var nonBudget = (await cashMovements.GetAsync(cancellationToken)).Where(x => x.AccountExternalId == accountExternalId
            && x.Date <= referenceDate && (account.OpeningDate is null || x.Date >= account.OpeningDate)).Sum(x => x.Amount.Amount);
        return new(accountExternalId, referenceDate, opening, opening + new Money(movements + nonBudget));
    }
}
