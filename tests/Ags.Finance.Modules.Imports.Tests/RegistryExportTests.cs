using System.Text;
using Ags.Finance.Modules.Accounts.Contracts;
using Ags.Finance.Modules.Imports.Application.Csv;
using Ags.Finance.Modules.Imports.Application.Registries;
using Ags.Finance.Modules.Imports.Contracts;

namespace Ags.Finance.Modules.Imports.Tests;

public sealed class RegistryExportTests : ImportsFixture
{
    private IRegistryExport Export => Get<IRegistryExport>();

    [Fact]
    public async Task EveryKindIsExportedWithExactlyTheOfficialVersionAndHeaders()
    {
        await SeedAsync();
        foreach (var kind in Enum.GetValues<RegistryKind>())
        {
            var layout = RegistryLayout.For(kind);
            var file = await Export.ExportAsync(kind);
            var text = Encoding.UTF8.GetString(file.Content, 3, file.Content.Length - 3);
            Assert.StartsWith($"ags-finance-csv,{layout.Type},{layout.Version}\r\n{string.Join(",", layout.Columns)}\r\n", text, StringComparison.Ordinal);
            Assert.Equal(layout.Columns.Count, Parse(file, kind).Rows[0].Fields.Count);
            Assert.Equal($"cadastro-{layout.Type.Replace('_', '-')}-v1.csv", file.FileName);
        }
    }

    [Fact]
    public async Task ExportedRowsCarryPublicIdentitiesAndLinksAndNeverInternalIds()
    {
        var seed = await SeedAsync();
        string[] Row(RegistryKind kind, int index = 0) => Parse(Export.ExportAsync(kind).Result, kind).Rows[index].Fields.ToArray();
        Assert.Equal(seed.Bank.ToString("D"), Row(RegistryKind.Banks)[0]);
        var account = Row(RegistryKind.Accounts);
        Assert.Equal([seed.Account.ToString("D"), seed.Bank.ToString("D")], account[..2]);
        Assert.Equal(["Checking", "green", "true", "123456", "2026-01-01"], account[3..]);
        var card = Row(RegistryKind.Cards);
        Assert.Equal([seed.Card.ToString("D"), seed.Account.ToString("D"), "Cartão Exemplo", "Mastercard", "500001", "3", "10", "true", "violet", "true"], card);
        Assert.Equal([seed.Final.ToString("D"), seed.Card.ToString("D"), "1234", "Shared"], Row(RegistryKind.CardDigits)[..4]);
        Assert.Equal([seed.Category.ToString("D"), "Mercado", "Expense", "true"], Row(RegistryKind.Categories));
        Assert.Equal([seed.Subcategory.ToString("D"), seed.Category.ToString("D"), "Feira", "true"], Row(RegistryKind.Subcategories));
        Assert.Equal([seed.Merchant.ToString("D"), "Padaria Ação", seed.Subcategory.ToString("D"), "true"], Row(RegistryKind.Merchants));
        Assert.Equal([seed.Person.ToString("D"), "Maria Exemplo", "Maria, pagadora"], Row(RegistryKind.People));
    }

    [Fact]
    public async Task ArchivedRegistriesKeepTheirInactiveStateInTheFile()
    {
        var seed = await SeedAsync();
        var rows = Parse(await Export.ExportAsync(RegistryKind.Accounts), RegistryKind.Accounts).Rows;
        Assert.Equal(2, rows.Count);
        var archived = rows.Single(x => x.Fields[0] == seed.ArchivedAccount.ToString("D"));
        Assert.Equal("false", archived.Fields[5]);
        Assert.Equal("true", rows.Single(x => x.Fields[0] == seed.Account.ToString("D")).Fields[5]);
    }

    [Fact]
    public async Task TextWithAccentsSeparatorsQuotesNewlinesAndFormulasComesBackExactly()
    {
        await SeedAsync();
        var bank = Parse(await Export.ExportAsync(RegistryKind.Banks), RegistryKind.Banks).Rows.Single();
        Assert.Equal("Banco \"São João\", Filial", CsvFormat.UnescapeText(bank.Fields[1]));
        var account = Parse(await Export.ExportAsync(RegistryKind.Accounts), RegistryKind.Accounts).Rows.Single(x => x.Fields[5] == "true");
        Assert.Equal("'=Conta\nPrincipal", account.Fields[2]);
        Assert.Equal("=Conta\nPrincipal", CsvFormat.UnescapeText(account.Fields[2]));
        var digits = Parse(await Export.ExportAsync(RegistryKind.CardDigits), RegistryKind.CardDigits).Rows.Single();
        Assert.Equal("@Pessoa, \"apelido\"", CsvFormat.UnescapeText(digits.Fields[4]));
    }

    [Fact]
    public async Task ExportingIsReadOnlyAndLeavesRegistriesAndBalancesUnchanged()
    {
        await SeedAsync();
        var accounts = Get<IAccountCatalog>();
        var before = await accounts.GetAccountsAsync(false);
        var cardsBefore = await accounts.GetCreditCardsAsync(false);
        foreach (var kind in Enum.GetValues<RegistryKind>()) { _ = await Export.ExportAsync(kind); }
        Assert.Equal(before, await accounts.GetAccountsAsync(false));
        Assert.Equal(cardsBefore, await accounts.GetCreditCardsAsync(false));
    }

    [Fact]
    public async Task TemplateHasOnlyTheVersionAndHeadersAndExampleUsesFictionalDataInTheSameLayout()
    {
        foreach (var kind in Enum.GetValues<RegistryKind>())
        {
            var template = Parse(Export.Template(kind), kind);
            Assert.Empty(template.Rows);
            var example = Parse(Export.Example(kind), kind);
            Assert.NotEmpty(example.Rows);
            Assert.StartsWith("modelo-", Export.Template(kind).FileName, StringComparison.Ordinal);
            Assert.StartsWith("exemplo-", Export.Example(kind).FileName, StringComparison.Ordinal);
        }
        Assert.Empty(await Get<IAccountCatalog>().GetBanksAsync());
    }

    [Fact]
    public async Task EmptyRegistryExportsAValidFileWithoutRecords()
    {
        foreach (var kind in Enum.GetValues<RegistryKind>())
        {
            Assert.Empty(Parse(await Export.ExportAsync(kind), kind).Rows);
        }
    }
}
