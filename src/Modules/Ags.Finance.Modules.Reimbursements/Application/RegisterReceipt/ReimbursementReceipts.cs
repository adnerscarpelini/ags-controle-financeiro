using Ags.Finance.Modules.Accounts.Contracts;
using Ags.Finance.Modules.Reimbursements.Contracts;
using Ags.Finance.Modules.Reimbursements.Domain;
using Ags.Finance.Modules.Transactions.Contracts;
using Ags.Finance.SharedKernel;

namespace Ags.Finance.Modules.Reimbursements.Application.RegisterReceipt;

internal sealed class ReimbursementReceipts(IReimbursementStore store, IFinancialOperation operations, IAccountCatalog accounts, IClassificationCatalog classifications,
    IMerchantCatalog merchants, IEntryCatalog entries) : IReimbursementReceipts
{
    internal const string CategoryName = "Reembolsos";
    internal const string SubcategoryName = "Reembolsos recebidos";

    public Task<ReimbursementResult> RegisterAsync(Guid personExternalId, DateOnly date, Money amount, Guid accountExternalId, CancellationToken cancellationToken = default)
        => operations.ExecuteAsync(token => RegisterCoreAsync(personExternalId, date, amount, accountExternalId, token), x => x.Succeeded, cancellationToken);

    private async Task<ReimbursementResult> RegisterCoreAsync(Guid personExternalId, DateOnly date, Money amount, Guid accountExternalId, CancellationToken cancellationToken)
    {
        if (amount.Amount <= 0) { return new(null, "Informe um valor maior que zero."); }
        if (date == DateOnly.MinValue) { return new(null, "Informe a data do recebimento."); }
        var person = (await store.GetPeopleAsync(cancellationToken)).FirstOrDefault(x => x.ExternalId == personExternalId);
        if (person is null) { return new(null, "Selecione uma pessoa cadastrada."); }
        var account = (await accounts.GetAccountsAsync(true, cancellationToken)).FirstOrDefault(x => x.ExternalId == accountExternalId);
        if (account is null) { return new(null, "Selecione uma conta ativa para receber o valor."); }
        var subcategory = await EnsureSubcategoryAsync(cancellationToken);
        if (subcategory.Error is not null) { return new(null, subcategory.Error); }
        var merchant = await EnsureMerchantAsync(person, subcategory.ExternalId!.Value, cancellationToken);
        if (merchant.Error is not null) { return new(null, merchant.Error); }
        var entry = await entries.CreateAsync(new(date, merchant.ExternalId!.Value, subcategory.ExternalId!.Value, accountExternalId, amount, $"Reembolso recebido de {person.Name}"), cancellationToken);
        if (!entry.Succeeded) { return new(null, entry.Error); }
        var receipt = ReimbursementReceipt.Create(personExternalId, entry.ExternalId!.Value, accountExternalId, date, AccountingMonth.FromDate(date), amount);
        if (receipt is null) { return new(null, "Dados do recebimento inválidos."); }
        await store.AddReceiptAsync(receipt, cancellationToken);
        return new(receipt.ExternalId, null);
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
        var existing = (await merchants.FindAsync(person.PayerDescription, false, cancellationToken)).FirstOrDefault(x => Person.Normalize(x.Name) == Person.Normalize(person.PayerDescription));
        if (existing is not null)
        {
            return existing.IsActive ? new(existing.ExternalId, null) : new(null, $"A descrição \"{existing.Name}\" está desativada. Escolha outra descrição do pagador para {person.Name}.");
        }
        return await merchants.SaveAsync(null, person.PayerDescription, subcategoryId, cancellationToken: cancellationToken);
    }

    private static bool Same(string left, string right) => string.Equals(left.Trim(), right, StringComparison.OrdinalIgnoreCase);
}
