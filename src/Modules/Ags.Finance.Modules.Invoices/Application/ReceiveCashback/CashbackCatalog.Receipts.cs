using Ags.Finance.Modules.Accounts.Contracts;
using Ags.Finance.Modules.Invoices.Contracts;
using Ags.Finance.Modules.Invoices.Domain;
using Ags.Finance.Modules.Transactions.Contracts;
using Ags.Finance.SharedKernel;

namespace Ags.Finance.Modules.Invoices.Application.ReceiveCashback;

internal sealed partial class CashbackCatalog
{
    public Task<CashbackResult> ReceiveAsync(ReceiveCashbackRequest request, CancellationToken cancellationToken = default)
        => operations.ExecuteAsync(token => ReceiveCoreAsync(request, token), x => x.Succeeded, cancellationToken);

    private async Task<CashbackResult> ReceiveCoreAsync(ReceiveCashbackRequest request, CancellationToken cancellationToken)
    {
        if (request.ConfirmationId == Guid.Empty || request.Month.Year == 0) { return Failed("Selecione o cashback de uma fatura."); }
        var receipts = await store.GetCashbackReceiptsAsync(cancellationToken);
        var source = await store.FindAsync(request.CardExternalId, request.Month, cancellationToken);
        if (receipts.FirstOrDefault(x => x.ConfirmationId == request.ConfirmationId) is { } repeated)
        {
            // Clique repetido ou reenvio: devolve o mesmo recebimento, sem nova receita nem novo abatimento.
            return repeated.InvoiceExternalId == source?.ExternalId && repeated.Received == request.Received
                ? new(repeated.ExternalId, null) : Failed("Esta confirmação já foi utilizada.");
        }
        if (source is null || source.Status != Domain.InvoiceStatus.Paid) { return Failed("O cashback só pode ser recebido depois que a fatura estiver Paga."); }
        if (receipts.Any(x => x.InvoiceExternalId == source.ExternalId)) { return Failed("O cashback desta fatura já foi recebido. Para corrigir, desfaça o recebimento."); }
        var card = (await accounts.GetCreditCardsAsync(false, cancellationToken)).FirstOrDefault(x => x.ExternalId == request.CardExternalId);
        if (card is null) { return Failed("Cartão não encontrado."); }
        var expected = (await invoices.GetMonthAsync(request.Month, cancellationToken)).FirstOrDefault(x => x.CardExternalId == card.ExternalId)?.ExpectedCashback ?? Money.Zero;
        var (receipt, error) = CashbackReceipt.Create(request.ConfirmationId, source.ExternalId, request.Date, expected, request.Received, (Domain.CashbackDestination)request.Destination);
        if (receipt is null) { return Failed(error ?? "Recebimento inválido."); }
        InvoicePayment? allowance = null;
        if (receipt.Destination == Domain.CashbackDestination.Account)
        {
            var account = (await accounts.GetAccountsAsync(true, cancellationToken)).FirstOrDefault(x => x.ExternalId == request.AccountExternalId);
            if (account is not { Kind: AccountKind.Checking }) { return Failed("Selecione uma conta corrente ativa para receber o cashback."); }
            var income = await CreateIncomeAsync(card, request.Month, request.Date, account.ExternalId, request.Received, cancellationToken);
            if (income.ExternalId is not { } incomeId) { return Failed(income.Error ?? "Não foi possível lançar a receita do cashback."); }
            receipt.CreditAccount(account.ExternalId, incomeId);
        }
        else if (receipt.Destination == Domain.CashbackDestination.Invoice)
        {
            if (request.TargetMonth is not { Year: > 0 } targetMonth || targetMonth == request.Month) { return Failed("Escolha uma fatura Aberta do cartão para receber o abatimento."); }
            // O guard confere a situação e o corte de fechamento e registra a fatura Aberta que ainda não existe.
            if (await guard.RequireOpenAsync(card.ExternalId, targetMonth, cancellationToken) is not null
                || await store.FindAsync(card.ExternalId, targetMonth, cancellationToken) is not { } target)
            {
                return Failed($"A fatura {targetMonth} precisa estar Aberta para receber o abatimento.");
            }
            receipt.CreditInvoice(target.ExternalId);
            allowance = InvoicePayment.FromCashbackReceipt(receipt.ExternalId, target.ExternalId, request.Date, request.Received, $"Cashback da fatura {request.Month}");
        }
        await store.AddCashbackReceiptAsync(receipt, allowance, cancellationToken);
        return new(receipt.ExternalId, null);
    }

    public Task<CashbackResult> UndoAsync(Guid receiptExternalId, CancellationToken cancellationToken = default)
        => operations.ExecuteAsync(token => UndoCoreAsync(receiptExternalId, token), x => x.Succeeded, cancellationToken);

    private async Task<CashbackResult> UndoCoreAsync(Guid receiptExternalId, CancellationToken cancellationToken)
    {
        var receipt = (await store.GetCashbackReceiptsAsync(cancellationToken)).FirstOrDefault(x => x.ExternalId == receiptExternalId);
        if (receipt is null) { return Failed("Este recebimento não existe mais. Confira o painel."); }
        InvoicePayment? allowance = null;
        if (receipt.TargetInvoiceExternalId is { } targetId)
        {
            var target = (await store.GetAsync(cancellationToken)).Single(x => x.ExternalId == targetId);
            if (target.Status == Domain.InvoiceStatus.Paid) { return Failed($"A fatura {target.Month}, que recebeu o abatimento, está Paga. Reabra-a para desfazer o recebimento."); }
            allowance = (await store.GetPaymentsAsync(cancellationToken)).FirstOrDefault(x => x.CashbackReceiptExternalId == receipt.ExternalId);
        }
        // O registro sai primeiro: enquanto ele existir, o guard protege a receita gerada contra exclusão.
        await store.DeleteCashbackReceiptAsync(receipt, allowance, cancellationToken);
        if (receipt.IncomeEntryExternalId is { } income)
        {
            var deleted = await entries.DeleteAsync(income, EntryChangeScope.OnlyThis, cancellationToken);
            if (!deleted.Succeeded) { return Failed(deleted.Error ?? "Não foi possível excluir a receita do cashback."); }
        }
        return new(receipt.ExternalId, null);
    }

    private async Task<ClassificationResult> CreateIncomeAsync(CreditCardDetails card, ReferenceMonth month, DateOnly date, Guid accountExternalId, Money amount, CancellationToken cancellationToken)
    {
        var subcategory = await EnsureSubcategoryAsync(cancellationToken);
        if (subcategory.ExternalId is not { } subcategoryId) { return subcategory; }
        var merchant = await EnsureMerchantAsync($"Cashback {card.Name}", subcategoryId, cancellationToken);
        if (merchant.ExternalId is not { } merchantId) { return merchant; }
        return await entries.CreateAsync(new(date, merchantId, subcategoryId, accountExternalId, amount, $"Cashback da fatura {month}"), cancellationToken);
    }

    private async Task<ClassificationResult> EnsureSubcategoryAsync(CancellationToken cancellationToken)
    {
        var category = (await classifications.GetCategoriesAsync(true, cancellationToken)).FirstOrDefault(x => x.Kind == CategoryKind.Income && Same(x.Name, CategoryName));
        Guid categoryId;
        if (category is null)
        {
            var created = await classifications.SaveCategoryAsync(null, CategoryName, CategoryKind.Income, cancellationToken: cancellationToken);
            if (created.ExternalId is not { } createdId) { return created; }
            categoryId = createdId;
        }
        else { categoryId = category.ExternalId; }
        var subcategory = (await classifications.GetSubcategoriesAsync(true, cancellationToken)).FirstOrDefault(x => x.CategoryExternalId == categoryId && Same(x.Name, SubcategoryName));
        return subcategory is not null ? new(subcategory.ExternalId, null) : await classifications.SaveSubcategoryAsync(null, categoryId, SubcategoryName, cancellationToken: cancellationToken);
    }

    private async Task<ClassificationResult> EnsureMerchantAsync(string name, Guid subcategoryId, CancellationToken cancellationToken)
    {
        var existing = (await merchants.FindAsync(name, false, cancellationToken)).FirstOrDefault(x => Same(x.Name, name));
        if (existing is not null)
        {
            return existing.IsActive ? new(existing.ExternalId, null) : new(null, $"A descrição \"{existing.Name}\" está desativada. Reative-a para receber o cashback.");
        }
        return await merchants.SaveAsync(null, name, subcategoryId, cancellationToken: cancellationToken);
    }

    private static bool Same(string left, string right) => string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);

    private static CashbackResult Failed(string error) => new(null, error);
}
