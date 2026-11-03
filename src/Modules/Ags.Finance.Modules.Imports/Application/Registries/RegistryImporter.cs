using Ags.Finance.Modules.Accounts.Contracts;
using Ags.Finance.Modules.Imports.Application.Csv;
using Ags.Finance.Modules.Imports.Contracts;
using Ags.Finance.Modules.Reimbursements.Contracts;
using Ags.Finance.Modules.Transactions.Contracts;
using Ags.Finance.SharedKernel;
using AccountsCardUsage = Ags.Finance.Modules.Accounts.Contracts.CardUsage;
using TransactionsCategoryKind = Ags.Finance.Modules.Transactions.Contracts.CategoryKind;

namespace Ags.Finance.Modules.Imports.Application.Registries;

/// <summary>
/// Importa um tipo de cadastro do layout oficial. Toda a gravação acontece pelos Contracts dos módulos dentro de uma operação financeira única
/// (<see cref="IFinancialOperation"/>): a prévia executa o mesmo caminho e descarta a transação, então as invariantes reais dos módulos valem
/// já na validação, e uma falha em qualquer linha desfaz o arquivo inteiro. Registro idêntico é ignorado; registro com a mesma identidade e dados
/// diferentes é conflito e nunca sobrescreve. Cadastros arquivados não são reativados nem arquivados por reimportação.
/// </summary>
internal sealed class RegistryImporter(IFinancialOperation operations, IAccountCatalog accounts, ICardLastDigitsCatalog digits, IClassificationCatalog classifications,
    IMerchantCatalog merchants, IPersonCatalog people) : IRegistryImport
{
    private const string Conflict = "Já existe um registro com este identificador e dados diferentes; o arquivo não sobrescreve cadastros.";

    public Task<RegistryImportReport> PreviewAsync(RegistryKind kind, byte[] content, CancellationToken cancellationToken = default)
        => RunAsync(kind, content, false, cancellationToken);

    public Task<RegistryImportReport> ImportAsync(RegistryKind kind, byte[] content, CancellationToken cancellationToken = default)
        => RunAsync(kind, content, true, cancellationToken);

    private async Task<RegistryImportReport> RunAsync(RegistryKind kind, byte[] content, bool apply, CancellationToken cancellationToken)
    {
        var layout = RegistryLayout.For(kind);
        var parsed = CsvReader.Parse(content, layout.Type, layout.Version, layout.Columns);
        if (parsed.Table is not { } table) { return new(kind, 0, 0, [new(0, "arquivo", parsed.Error!)], false); }
        var report = await operations.ExecuteAsync(token => ApplyAsync(kind, table, token), x => apply && x.Succeeded, cancellationToken);
        return report with { Applied = apply && report.Succeeded };
    }

    private async Task<RegistryImportReport> ApplyAsync(RegistryKind kind, CsvTable table, CancellationToken cancellationToken)
    {
        var pass = new Pass(table.Columns);
        switch (kind)
        {
            case RegistryKind.Banks: await BanksAsync(table, pass, cancellationToken); break;
            case RegistryKind.Accounts: await AccountsAsync(table, pass, cancellationToken); break;
            case RegistryKind.Cards: await CardsAsync(table, pass, cancellationToken); break;
            case RegistryKind.CardDigits: await CardDigitsAsync(table, pass, cancellationToken); break;
            case RegistryKind.Categories: await CategoriesAsync(table, pass, cancellationToken); break;
            case RegistryKind.Subcategories: await SubcategoriesAsync(table, pass, cancellationToken); break;
            case RegistryKind.Merchants: await MerchantsAsync(table, pass, cancellationToken); break;
            case RegistryKind.People: await PeopleAsync(table, pass, cancellationToken); break;
            default: throw new ArgumentOutOfRangeException(nameof(kind));
        }
        return new(kind, pass.New, pass.Identical, pass.Issues, false);
    }

    private async Task BanksAsync(CsvTable table, Pass pass, CancellationToken cancellationToken)
    {
        var existing = (await accounts.GetBanksAsync(cancellationToken)).ToDictionary(x => x.ExternalId);
        foreach (var line in table.Rows)
        {
            var row = pass.Read(line);
            var id = row.Id("external_id");
            var name = row.Text("name");
            if (!row.Valid) { continue; }
            if (existing.TryGetValue(id, out var bank)) { pass.Compare(row, bank.Name == name, "external_id"); continue; }
            var result = await accounts.CreateBankAsync(name, id, cancellationToken);
            if (!pass.Created(row, result.Error, "name")) { continue; }
            existing[id] = new(id, name);
        }
    }

    private async Task AccountsAsync(CsvTable table, Pass pass, CancellationToken cancellationToken)
    {
        var existing = (await accounts.GetAccountsAsync(false, cancellationToken)).ToDictionary(x => x.ExternalId);
        foreach (var line in table.Rows)
        {
            var row = pass.Read(line);
            var id = row.Id("external_id");
            var bank = row.Id("bank_external_id");
            var name = row.Text("name");
            var kind = row.Enum<AccountKind>("kind");
            var color = row.Text("color");
            var active = row.Bool("is_active");
            var cents = row.Integer("opening_balance_cents", -9_223_372_036_854_775_807, 9_223_372_036_854_775_807);
            var opening = row.OptionalDate("opening_date");
            if (!row.Valid) { continue; }
            if (existing.TryGetValue(id, out var account))
            {
                pass.Compare(row, account.BankExternalId == bank && account.Name == name && account.Kind == kind && account.Color == color && account.IsActive == active
                    && account.OpeningBalance.ToCents() == cents && account.OpeningDate == opening, "external_id");
                continue;
            }
            var result = await accounts.SaveAccountAsync(new(null, bank, name, kind, color, Money.FromCents(cents), opening, id), cancellationToken);
            if (!pass.Created(row, result.Error, "name")) { continue; }
            if (!active && !Pass.Step(row, (await accounts.DeactivateAccountAsync(id, cancellationToken)).Error, "is_active")) { continue; }
            existing[id] = new(id, bank, name, kind, color, active, Money.FromCents(cents), opening);
        }
    }

    private async Task CardsAsync(CsvTable table, Pass pass, CancellationToken cancellationToken)
    {
        var existing = (await accounts.GetCreditCardsAsync(false, cancellationToken)).ToDictionary(x => x.ExternalId);
        foreach (var line in table.Rows)
        {
            var row = pass.Read(line);
            var id = row.Id("external_id");
            var account = row.Id("account_external_id");
            var name = row.Text("name");
            var brand = row.Enum<CardBrand>("brand");
            var limit = row.Integer("credit_limit_cents", 0, 9_223_372_036_854_775_807);
            var closing = (int)row.Integer("closing_day", 1, 31);
            var due = (int)row.Integer("due_day", 1, 31);
            var includes = row.Bool("includes_closing_day");
            var color = row.Text("color");
            var active = row.Bool("is_active");
            if (!row.Valid) { continue; }
            if (existing.TryGetValue(id, out var card))
            {
                pass.Compare(row, card.AccountExternalId == account && card.Name == name && card.Brand == brand && card.CreditLimit.ToCents() == limit && card.ClosingDay == closing
                    && card.DueDay == due && card.IncludesClosingDay == includes && card.Color == color && card.IsActive == active, "external_id");
                continue;
            }
            var result = await WithActiveAccountAsync(account, () => accounts.SaveCreditCardAsync(new(null, account, name, brand, Money.FromCents(limit), closing, due, includes, color, id), cancellationToken), cancellationToken);
            if (!pass.Created(row, result.Error, "name")) { continue; }
            if (!active && !Pass.Step(row, (await accounts.DeactivateCreditCardAsync(id, cancellationToken)).Error, "is_active")) { continue; }
            existing[id] = new(id, account, name, brand, Money.FromCents(limit), closing, due, includes, color, active);
        }
    }

    private async Task CardDigitsAsync(CsvTable table, Pass pass, CancellationToken cancellationToken)
    {
        var cards = (await accounts.GetCreditCardsAsync(false, cancellationToken)).ToDictionary(x => x.ExternalId);
        var existing = new Dictionary<Guid, CardLastDigitsDetails>();
        foreach (var card in cards.Values)
        {
            foreach (var item in await digits.GetAsync(card.ExternalId, false, cancellationToken)) { existing[item.ExternalId] = item; }
        }
        foreach (var line in table.Rows)
        {
            var row = pass.Read(line);
            var id = row.Id("external_id");
            var cardId = row.Id("card_external_id");
            var number = row.Text("digits");
            var usage = row.Enum<AccountsCardUsage>("usage");
            var label = row.OptionalText("label");
            var active = row.Bool("is_active");
            if (!row.Valid) { continue; }
            if (existing.TryGetValue(id, out var item))
            {
                pass.Compare(row, item.CardExternalId == cardId && item.Digits == number && item.Usage == usage && item.Label == label && item.IsActive == active, "external_id");
                continue;
            }
            if (!cards.TryGetValue(cardId, out var owner)) { row.Problem("card_external_id", "Cartão não encontrado. Importe os cartões antes dos finais."); continue; }
            var result = await WithActiveCardAsync(owner, () => digits.SaveAsync(new(null, cardId, number, usage, label, id), cancellationToken), cancellationToken);
            if (!pass.Created(row, result.Error, "digits")) { continue; }
            if (!active && !Pass.Step(row, (await digits.DeactivateAsync(id, cancellationToken)).Error, "is_active")) { continue; }
            existing[id] = new(id, cardId, number, usage, label, active);
        }
    }

    private async Task CategoriesAsync(CsvTable table, Pass pass, CancellationToken cancellationToken)
    {
        var existing = (await classifications.GetCategoriesAsync(false, cancellationToken)).ToDictionary(x => x.ExternalId);
        foreach (var line in table.Rows)
        {
            var row = pass.Read(line);
            var id = row.Id("external_id");
            var name = row.Text("name");
            var kind = row.Enum<TransactionsCategoryKind>("kind");
            var active = row.Bool("is_active");
            if (!row.Valid) { continue; }
            if (existing.TryGetValue(id, out var category)) { pass.Compare(row, category.Name == name && category.Kind == kind && category.IsActive == active, "external_id"); continue; }
            var result = await classifications.SaveCategoryAsync(null, name, kind, id, cancellationToken);
            if (!pass.Created(row, result.Error, "name")) { continue; }
            if (!active && !Pass.Step(row, (await classifications.DeactivateAsync(id, false, cancellationToken)).Error, "is_active")) { continue; }
            existing[id] = new(id, name, kind, active);
        }
    }

    private async Task SubcategoriesAsync(CsvTable table, Pass pass, CancellationToken cancellationToken)
    {
        var existing = (await classifications.GetSubcategoriesAsync(false, cancellationToken)).ToDictionary(x => x.ExternalId);
        foreach (var line in table.Rows)
        {
            var row = pass.Read(line);
            var id = row.Id("external_id");
            var category = row.Id("category_external_id");
            var name = row.Text("name");
            var active = row.Bool("is_active");
            if (!row.Valid) { continue; }
            if (existing.TryGetValue(id, out var subcategory)) { pass.Compare(row, subcategory.CategoryExternalId == category && subcategory.Name == name && subcategory.IsActive == active, "external_id"); continue; }
            var result = await classifications.SaveSubcategoryAsync(null, category, name, id, cancellationToken);
            if (!pass.Created(row, result.Error, "category_external_id")) { continue; }
            if (!active && !Pass.Step(row, (await classifications.DeactivateAsync(id, true, cancellationToken)).Error, "is_active")) { continue; }
            existing[id] = (await classifications.GetSubcategoriesAsync(false, cancellationToken)).First(x => x.ExternalId == id);
        }
    }

    private async Task MerchantsAsync(CsvTable table, Pass pass, CancellationToken cancellationToken)
    {
        var existing = (await merchants.FindAsync(string.Empty, false, cancellationToken)).ToDictionary(x => x.ExternalId);
        foreach (var line in table.Rows)
        {
            var row = pass.Read(line);
            var id = row.Id("external_id");
            var name = row.Text("name");
            var subcategory = row.OptionalId("default_subcategory_external_id");
            var active = row.Bool("is_active");
            if (!row.Valid) { continue; }
            if (existing.TryGetValue(id, out var merchant)) { pass.Compare(row, merchant.Name == name && merchant.DefaultSubcategoryExternalId == subcategory && merchant.IsActive == active, "external_id"); continue; }
            var result = await merchants.SaveAsync(null, name, subcategory, id, cancellationToken);
            if (!pass.Created(row, result.Error, "name")) { continue; }
            if (!active && !Pass.Step(row, (await merchants.DeactivateAsync(id, cancellationToken)).Error, "is_active")) { continue; }
            existing[id] = new(id, name, subcategory, active);
        }
    }

    private async Task PeopleAsync(CsvTable table, Pass pass, CancellationToken cancellationToken)
    {
        var existing = (await people.GetAsync(cancellationToken)).ToDictionary(x => x.ExternalId);
        foreach (var line in table.Rows)
        {
            var row = pass.Read(line);
            var id = row.Id("external_id");
            var name = row.Text("name");
            var percent = row.Percent("default_percent");
            if (!row.Valid) { continue; }
            if (existing.TryGetValue(id, out var person)) { pass.Compare(row, person.Name == name && person.DefaultPercent == percent, "external_id"); continue; }
            var result = await people.SaveAsync(null, name, percent, id, cancellationToken);
            if (!pass.Created(row, result.Error, "name")) { continue; }
            existing[id] = new(id, name, percent);
        }
    }

    /// <summary>A conta de um cartão precisa estar ativa para ser escolhida. Se está arquivada, reativa só durante o passo e arquiva de novo.</summary>
    private async Task<CatalogResult> WithActiveAccountAsync(Guid accountId, Func<Task<CatalogResult>> action, CancellationToken cancellationToken)
    {
        var account = (await accounts.GetAccountsAsync(false, cancellationToken)).FirstOrDefault(x => x.ExternalId == accountId);
        if (account is null || account.IsActive) { return await action(); }
        var reactivated = await accounts.ReactivateAccountAsync(accountId, cancellationToken);
        if (!reactivated.Succeeded) { return reactivated; }
        var result = await action();
        var archived = await accounts.DeactivateAccountAsync(accountId, cancellationToken);
        return result.Succeeded && !archived.Succeeded ? archived : result;
    }

    /// <summary>Um final só entra em cartão ativo. Cartão (e conta) arquivados são reativados só durante o passo e arquivados de novo.</summary>
    private async Task<CatalogResult> WithActiveCardAsync(CreditCardDetails card, Func<Task<CatalogResult>> action, CancellationToken cancellationToken)
    {
        if (card.IsActive) { return await action(); }
        return await WithActiveAccountAsync(card.AccountExternalId, async () =>
        {
            var reactivated = await accounts.ReactivateCreditCardAsync(card.ExternalId, cancellationToken);
            if (!reactivated.Succeeded) { return reactivated; }
            var result = await action();
            var archived = await accounts.DeactivateCreditCardAsync(card.ExternalId, cancellationToken);
            return result.Succeeded && !archived.Succeeded ? archived : result;
        }, cancellationToken);
    }

    private sealed class Pass(IReadOnlyList<string> columns)
    {
        public List<ImportIssue> Issues { get; } = [];
        public int New { get; private set; }
        public int Identical { get; private set; }

        public RowReader Read(CsvRow row) => new(row, columns, Issues);

        public void Compare(RowReader row, bool same, string column)
        {
            if (same) { Identical++; }
            else { row.Problem(column, Conflict); }
        }

        /// <summary>Passo complementar de um registro novo (arquivar): não conta de novo, só registra o erro devolvido pelo módulo.</summary>
        public static bool Step(RowReader row, string? error, string column)
        {
            if (error is null) { return true; }
            row.Problem(column, error);
            return false;
        }

        /// <summary>Conta o registro como novo ou registra o erro de domínio devolvido pelo módulo.</summary>
        public bool Created(RowReader row, string? error, string column)
        {
            if (error is null) { New++; return true; }
            row.Problem(column, error);
            return false;
        }
    }
}
