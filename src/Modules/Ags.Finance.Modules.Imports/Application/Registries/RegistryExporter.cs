using Ags.Finance.Modules.Accounts.Contracts;
using Ags.Finance.Modules.Imports.Application.Csv;
using Ags.Finance.Modules.Imports.Contracts;
using Ags.Finance.Modules.Reimbursements.Contracts;
using Ags.Finance.Modules.Transactions.Contracts;

namespace Ags.Finance.Modules.Imports.Application.Registries;

/// <summary>Exportação somente leitura dos cadastros, obtidos pelos Contracts dos módulos (nunca por tabelas nem pelo Id interno).</summary>
internal sealed class RegistryExporter(IAccountCatalog accounts, ICardLastDigitsCatalog digits, IClassificationCatalog classifications, IMerchantCatalog merchants, IPersonCatalog people) : IRegistryExport
{
    public async Task<CsvFile> ExportAsync(RegistryKind kind, CancellationToken cancellationToken = default)
    {
        var layout = RegistryLayout.For(kind);
        var writer = new CsvWriter(layout.Type, layout.Version, layout.Columns);
        foreach (var row in await RowsAsync(kind, cancellationToken)) { writer.Row(row); }
        return new(layout.FileName, writer.ToBytes());
    }

    public CsvFile Template(RegistryKind kind)
    {
        var layout = RegistryLayout.For(kind);
        return new($"modelo-{layout.FileName}", new CsvWriter(layout.Type, layout.Version, layout.Columns).ToBytes());
    }

    public CsvFile Example(RegistryKind kind)
    {
        var layout = RegistryLayout.For(kind);
        var writer = new CsvWriter(layout.Type, layout.Version, layout.Columns);
        foreach (var row in layout.Examples) { writer.Row(row); }
        return new($"exemplo-{layout.FileName}", writer.ToBytes());
    }

    private async Task<IReadOnlyList<IReadOnlyList<string>>> RowsAsync(RegistryKind kind, CancellationToken cancellationToken)
    {
        static string T(string value) => CsvFormat.EscapeText(value);
        static string B(bool value) => CsvFormat.Bool(value);
        switch (kind)
        {
            case RegistryKind.Banks:
                return (await accounts.GetBanksAsync(cancellationToken)).Select(x => (IReadOnlyList<string>)[CsvFormat.Id(x.ExternalId), T(x.Name)]).ToList();
            case RegistryKind.Accounts:
                return (await accounts.GetAccountsAsync(false, cancellationToken)).Select(x => (IReadOnlyList<string>)[CsvFormat.Id(x.ExternalId), CsvFormat.Id(x.BankExternalId), T(x.Name),
                    x.Kind.ToString(), x.Color, B(x.IsActive), CsvFormat.Number(x.OpeningBalance.ToCents()), x.OpeningDate is { } date ? CsvFormat.Date(date) : string.Empty]).ToList();
            case RegistryKind.Cards:
                return (await accounts.GetCreditCardsAsync(false, cancellationToken)).Select(x => (IReadOnlyList<string>)[CsvFormat.Id(x.ExternalId), CsvFormat.Id(x.AccountExternalId), T(x.Name),
                    x.Brand.ToString(), CsvFormat.Number(x.CreditLimit.ToCents()), CsvFormat.Number(x.ClosingDay), CsvFormat.Number(x.DueDay), B(x.IncludesClosingDay), x.Color, B(x.IsActive)]).ToList();
            case RegistryKind.CardDigits:
                var rows = new List<IReadOnlyList<string>>();
                foreach (var card in await accounts.GetCreditCardsAsync(false, cancellationToken))
                {
                    rows.AddRange((await digits.GetAsync(card.ExternalId, false, cancellationToken)).Select(x => (IReadOnlyList<string>)[CsvFormat.Id(x.ExternalId), CsvFormat.Id(x.CardExternalId),
                        x.Digits, x.Usage.ToString(), T(x.Label), B(x.IsActive)]));
                }
                return rows;
            case RegistryKind.Categories:
                return (await classifications.GetCategoriesAsync(false, cancellationToken)).Select(x => (IReadOnlyList<string>)[CsvFormat.Id(x.ExternalId), T(x.Name), x.Kind.ToString(), B(x.IsActive)]).ToList();
            case RegistryKind.Subcategories:
                return (await classifications.GetSubcategoriesAsync(false, cancellationToken)).Select(x => (IReadOnlyList<string>)[CsvFormat.Id(x.ExternalId), CsvFormat.Id(x.CategoryExternalId), T(x.Name), B(x.IsActive)]).ToList();
            case RegistryKind.Merchants:
                return (await merchants.FindAsync(string.Empty, false, cancellationToken)).Select(x => (IReadOnlyList<string>)[CsvFormat.Id(x.ExternalId), T(x.Name), CsvFormat.Id(x.DefaultSubcategoryExternalId), B(x.IsActive)]).ToList();
            case RegistryKind.People:
                return (await people.GetAsync(cancellationToken)).Select(x => (IReadOnlyList<string>)[CsvFormat.Id(x.ExternalId), T(x.Name), CsvFormat.Percent(x.DefaultPercent)]).ToList();
            default:
                throw new ArgumentOutOfRangeException(nameof(kind));
        }
    }
}
