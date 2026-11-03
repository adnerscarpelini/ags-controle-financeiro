using Ags.Finance.Modules.Accounts.Contracts;
using Ags.Finance.Modules.Reimbursements.Contracts;
using Ags.Finance.Modules.Reimbursements.Domain;
using Ags.Finance.Modules.Transactions.Contracts;
using Ags.Finance.SharedKernel;

namespace Ags.Finance.Modules.Reimbursements.Application.ReceiveReimbursements;

internal sealed class ReimbursementReceipts(IReimbursementStore store, IFinancialOperation operations, IAccountCatalog accounts, IClassificationCatalog classifications,
    IMerchantCatalog merchants, IEntryCatalog entries) : IReimbursementReceipts
{
    internal const string CategoryName = "Reembolsos";
    internal const string SubcategoryName = "Reembolsos recebidos";

    public Task<ReceiveReimbursementsResult> ReceiveAsync(ReceiveReimbursementsRequest request, CancellationToken cancellationToken = default)
        => operations.ExecuteAsync(token => ReceiveCoreAsync(request, token), x => x.Succeeded, cancellationToken);

    private async Task<ReceiveReimbursementsResult> ReceiveCoreAsync(ReceiveReimbursementsRequest request, CancellationToken cancellationToken)
    {
        if (request.Items.Count == 0) { return Failed("Selecione ao menos um reembolso."); }
        if (request.Date == DateOnly.MinValue) { return Failed("Informe a data do recebimento."); }
        if (request.Items.Any(x => x.Received.IsNegative)) { return Failed("O valor recebido não pode ser negativo."); }
        if (request.Items.Select(x => x.EntryExternalId).Distinct().Count() != request.Items.Count) { return Failed("Um reembolso foi selecionado mais de uma vez."); }
        var rows = (await entries.GetAsync(cancellationToken)).ToDictionary(x => x.ExternalId);
        var received = (await store.GetReceiptItemsAsync(cancellationToken)).Select(x => x.ExpenseEntryExternalId).ToHashSet();
        var selected = new List<(ReceiveReimbursementItem Item, EntryDetails Entry, EntryReimbursement Reimbursement)>();
        foreach (var item in request.Items)
        {
            if (received.Contains(item.EntryExternalId) || !rows.TryGetValue(item.EntryExternalId, out var entry) || entry.Reimbursement is not { } reimbursement
                || ReimbursementCalculation.Expected(entry.Amount, reimbursement.Percent) != item.Expected || item.Expected.IsZero)
            {
                return Failed("Um reembolso selecionado mudou ou já foi recebido. Confira a lista e efetive de novo.");
            }
            selected.Add((item, entry, reimbursement));
        }
        var hasIncome = selected.GroupBy(x => x.Reimbursement.PersonExternalId).Any(x => x.Sum(y => y.Item.Received.Amount) > 0);
        if (hasIncome)
        {
            var account = (await accounts.GetAccountsAsync(true, cancellationToken)).FirstOrDefault(x => x.ExternalId == request.AccountExternalId);
            if (account is not { Kind: AccountKind.Checking }) { return Failed("Selecione uma conta corrente ativa para receber o valor."); }
        }
        var people = (await store.GetPeopleAsync(cancellationToken)).ToDictionary(x => x.ExternalId);
        var created = new List<Guid>();
        foreach (var group in selected.GroupBy(x => x.Reimbursement.PersonExternalId))
        {
            if (!people.TryGetValue(group.Key, out var person)) { return Failed("Uma pessoa selecionada não existe mais."); }
            var total = new Money(group.Sum(x => x.Item.Received.Amount));
            Guid? income = null;
            if (!total.IsZero)
            {
                var entry = await CreateIncomeAsync(person, request.Date, request.AccountExternalId!.Value, total, cancellationToken);
                if (!entry.Succeeded) { return Failed(entry.Error!); }
                income = entry.ExternalId;
            }
            var receipt = ReimbursementReceipt.Create(person.ExternalId, request.Date, income is null ? null : request.AccountExternalId, income, total);
            var items = group.Select(x => ReimbursementReceiptItem.Create(receipt.ExternalId, x.Entry.ExternalId, new Money(Math.Abs(x.Entry.Amount.Amount)), x.Reimbursement.Percent, x.Item.Expected, x.Item.Received)).ToList();
            await store.AddReceiptAsync(receipt, items, cancellationToken);
            created.Add(receipt.ExternalId);
        }
        return new(created, null);
    }

    private async Task<ClassificationResult> CreateIncomeAsync(Person person, DateOnly date, Guid accountExternalId, Money total, CancellationToken cancellationToken)
    {
        var subcategory = await EnsureSubcategoryAsync(cancellationToken);
        if (!subcategory.Succeeded) { return subcategory; }
        var merchant = await EnsureMerchantAsync(person, subcategory.ExternalId!.Value, cancellationToken);
        if (!merchant.Succeeded) { return merchant; }
        return await entries.CreateAsync(new(date, merchant.ExternalId!.Value, subcategory.ExternalId!.Value, accountExternalId, total, $"Reembolso recebido de {person.Name}"), cancellationToken);
    }

    private async Task<ClassificationResult> EnsureSubcategoryAsync(CancellationToken cancellationToken)
    {
        var category = (await classifications.GetCategoriesAsync(true, cancellationToken)).FirstOrDefault(x => x.Kind == CategoryKind.Income && Same(x.Name, CategoryName));
        Guid categoryId;
        if (category is null)
        {
            var created = await classifications.SaveCategoryAsync(null, CategoryName, CategoryKind.Income, cancellationToken: cancellationToken);
            if (!created.Succeeded) { return created; }
            categoryId = created.ExternalId!.Value;
        }
        else { categoryId = category.ExternalId; }
        var subcategory = (await classifications.GetSubcategoriesAsync(true, cancellationToken)).FirstOrDefault(x => x.CategoryExternalId == categoryId && Same(x.Name, SubcategoryName));
        return subcategory is not null ? new(subcategory.ExternalId, null) : await classifications.SaveSubcategoryAsync(null, categoryId, SubcategoryName, cancellationToken: cancellationToken);
    }

    private async Task<ClassificationResult> EnsureMerchantAsync(Person person, Guid subcategoryId, CancellationToken cancellationToken)
    {
        var existing = (await merchants.FindAsync(person.Name, false, cancellationToken)).FirstOrDefault(x => Person.Normalize(x.Name) == person.NormalizedName);
        if (existing is not null)
        {
            return existing.IsActive ? new(existing.ExternalId, null) : new(null, $"A descrição \"{existing.Name}\" está desativada. Reative-a para receber reembolsos de {person.Name}.");
        }
        return await merchants.SaveAsync(null, person.Name, subcategoryId, cancellationToken: cancellationToken);
    }

    private static bool Same(string left, string right) => string.Equals(left.Trim(), right, StringComparison.OrdinalIgnoreCase);

    private static ReceiveReimbursementsResult Failed(string error) => new([], error);
}
