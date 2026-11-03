using Ags.Finance.Modules.Imports.Application.Csv;
using Ags.Finance.Modules.Imports.Application.Transactions;
using Ags.Finance.Modules.Imports.Contracts;
using Ags.Finance.Modules.Transactions.Contracts;
using Ags.Finance.SharedKernel;

namespace Ags.Finance.Modules.Imports.Tests;

public sealed class TransactionExportTests : ImportsFixture
{
    private ITransactionExport Export => Get<ITransactionExport>();
    private IEntryCatalog Entries => Get<IEntryCatalog>();

    private static CsvTable Read(CsvExportResult result, TransactionFileKind kind)
    {
        Assert.True(result.Succeeded, result.Error);
        var layout = TransactionLayout.For(kind);
        var parsed = CsvReader.Parse(result.File!.Content, layout.Type, layout.Version, layout.Columns);
        Assert.True(parsed.Succeeded, parsed.Error);
        return parsed.Table!;
    }

    private async Task<CsvTable> ExportRowsAsync(TransactionExportFilter filter) => Read(await Export.ExportAsync(filter), TransactionFileKind.Transactions);

    private static string Field(CsvRow row, string column) => row.Fields[TransactionLayout.For(TransactionFileKind.Transactions).Columns.ToList().IndexOf(column)];

    private async Task<Guid> AddAsync(Seed seed, DateOnly date, decimal amount, string details = "", ReferenceMonth? month = null, Guid? card = null, Guid? final = null)
    {
        var result = await Entries.CreateAsync(new(date, seed.Merchant, seed.Subcategory, seed.Account, new Money(amount), details, month, card, final));
        Assert.True(result.Succeeded, result.Error);
        return result.ExternalId!.Value;
    }

    [Fact]
    public async Task FilterReturnsOnlyMatchingEntriesWithTheOfficialColumns()
    {
        var seed = await SeedAsync();
        var october = await AddAsync(seed, new DateOnly(2026, 10, 5), 10m);
        await AddAsync(seed, new DateOnly(2026, 11, 5), 20m);
        var table = await ExportRowsAsync(new(Month: new ReferenceMonth(2026, 10)));
        var row = Assert.Single(table.Rows);
        Assert.Equal(TransactionLayout.For(TransactionFileKind.Transactions).Columns.Count, row.Fields.Count);
        Assert.Equal((october.ToString("D"), "2026-10-05", "2026-10", "Expense", "1000"), (Field(row, "external_id"), Field(row, "date"), Field(row, "reference_month"), Field(row, "kind"), Field(row, "amount_cents")));
        Assert.Equal((seed.Merchant.ToString("D"), seed.Subcategory.ToString("D"), seed.Account.ToString("D"), "true"),
            (Field(row, "merchant_external_id"), Field(row, "subcategory_external_id"), Field(row, "account_external_id"), Field(row, "is_effective")));
    }

    [Fact]
    public async Task AutomaticCashMonthAndExactCentsArePreserved()
    {
        var seed = await SeedAsync();
        await AddAsync(seed, new DateOnly(2026, 10, 5), 0.01m, month: new ReferenceMonth(2027, 2));
        await AddAsync(seed, new DateOnly(2026, 10, 6), 1234.56m);
        await AddAsync(seed, new DateOnly(2026, 10, 7), 92233720368547m);
        var rows = (await ExportRowsAsync(new(FromDate: new DateOnly(2026, 10, 1), ToDate: new DateOnly(2026, 10, 31)))).Rows;
        Assert.Equal(["2026-10", "2026-10", "2026-10"], rows.OrderBy(x => x.Fields[1], StringComparer.Ordinal).Select(x => Field(x, "reference_month")));
        Assert.Equal(["1", "123456", "9223372036854700"], rows.OrderBy(x => x.Fields[1], StringComparer.Ordinal).Select(x => Field(x, "amount_cents")));
    }

    [Fact]
    public async Task InstallmentsAndRecurrenceAreExplicitLinksAndNeverTheDescription()
    {
        var seed = await SeedAsync();
        var parcels = await Entries.CreateInstallmentsAsync(new(new DateOnly(2026, 10, 6), seed.Merchant, seed.Subcategory, seed.Account, new Money(100m), "Geladeira (1/3)", null, seed.Card, seed.Final), 3);
        Assert.True(parcels.Succeeded, parcels.Error);
        var series = await Entries.CreateRecurringAsync(new(new(new DateOnly(2026, 10, 5), seed.Merchant, seed.Subcategory, seed.Account, new Money(99.9m), "Assinatura")));
        Assert.True(series.Succeeded, series.Error);
        var all = new TransactionExportFilter(FromDate: new DateOnly(2026, 10, 1), ToDate: new DateOnly(2026, 12, 31));
        var rows = (await ExportRowsAsync(all)).Rows;
        var installments = rows.Where(x => Field(x, "installment_purchase_external_id").Length > 0).ToList();
        Assert.Equal(3, installments.Count);
        Assert.Single(installments.Select(x => Field(x, "installment_purchase_external_id")).Distinct());
        Assert.Equal(["1", "2", "3"], installments.Select(x => Field(x, "installment_number")).Order());
        Assert.All(installments, x => Assert.Equal("3", Field(x, "installment_count")));
        var recurring = rows.Where(x => Field(x, "recurring_entry_external_id").Length > 0).ToList();
        Assert.NotEmpty(recurring);
        Assert.All(recurring, x => Assert.Equal(series.ExternalId!.Value.ToString("D"), Field(x, "recurring_entry_external_id")));
        Assert.All(recurring, x => Assert.Matches(@"^\d{4}-\d{2}-01$", Field(x, "occurrence_month")));
        var definition = Assert.Single(Read(await Export.ExportSeriesAsync(all), TransactionFileKind.RecurringSeries).Rows);
        Assert.Equal((series.ExternalId!.Value.ToString("D"), "2026-10-05", "5", "9990"), (definition.Fields[0], definition.Fields[1], definition.Fields[2], definition.Fields[9]));
    }

    [Fact]
    public async Task PendingAndEffectiveStatesAreKeptAndExportingNeverEffectivatesAnything()
    {
        var seed = await SeedAsync();
        Assert.True((await Entries.CreateRecurringAsync(new(new(new DateOnly(2026, 11, 5), seed.Merchant, seed.Subcategory, seed.Account, new Money(40m), IsPaid: false)))).Succeeded);
        await AddAsync(seed, new DateOnly(2026, 11, 6), 15m);
        var balance = (await Entries.GetBalanceAsync(seed.Account, new DateOnly(2026, 12, 31)))!.EffectiveBalance;
        var rows = (await ExportRowsAsync(new(Month: new ReferenceMonth(2026, 11)))).Rows;
        var pending = Assert.Single(rows, x => Field(x, "is_effective") == "false");
        Assert.Equal(string.Empty, Field(pending, "effective_date"));
        var effective = Assert.Single(rows, x => Field(x, "is_effective") == "true");
        Assert.Equal("2026-11-06", Field(effective, "effective_date"));
        Assert.Single((await Entries.QueryPendingAsync(new(Month: new ReferenceMonth(2026, 11)))).Entries);
        Assert.Equal(balance, (await Entries.GetBalanceAsync(seed.Account, new DateOnly(2026, 12, 31)))!.EffectiveBalance);
        Assert.Single((await ExportRowsAsync(new(IsEffective: false, Month: new ReferenceMonth(2026, 11)))).Rows);
    }

    [Fact]
    public async Task DescriptionWithAccentsQuotesSeparatorsNewlinesAndFormulasComesBackExactly()
    {
        var seed = await SeedAsync();
        const string Description = "=Pão, \"café\"\nsegunda linha";
        await AddAsync(seed, new DateOnly(2026, 10, 5), 5m, Description);
        var row = Assert.Single((await ExportRowsAsync(new(Month: new ReferenceMonth(2026, 10)))).Rows);
        Assert.Equal("'" + Description, Field(row, "details"));
        Assert.Equal(Description, CsvFormat.UnescapeText(Field(row, "details")));
    }

    [Fact]
    public async Task EmptyResultStillProducesAValidFileWithVersionAndHeadersOnly()
    {
        await SeedAsync();
        Assert.Empty((await ExportRowsAsync(new(Month: new ReferenceMonth(2030, 1)))).Rows);
        Assert.Empty(Read(await Export.ExportSeriesAsync(new(Month: new ReferenceMonth(2030, 1))), TransactionFileKind.RecurringSeries).Rows);
    }

    [Fact]
    public async Task InvoiceAdjustmentsAreNotPartOfTheLayoutAndTheSavedFilterIsHonored()
    {
        var seed = await SeedAsync();
        await AddAsync(seed, new DateOnly(2026, 10, 1), 100m, card: seed.Card, final: seed.Final);
        await AddAsync(seed, new DateOnly(2026, 10, 2), 7m);
        Assert.True((await Entries.AdjustInvoiceAsync(new(Guid.CreateVersion7(), seed.Card, new ReferenceMonth(2026, 10), new Money(130m)))).Succeeded);
        var month = new ReferenceMonth(2026, 10);
        Assert.Equal(2, (await ExportRowsAsync(new(Month: month))).Rows.Count);
        var filter = await Get<ISavedFilterCatalog>().SaveAsync(null, "Só cartão", SavedFilterCriteria.Empty with { CardExternalIds = [seed.Card] });
        var viaFilter = await ExportRowsAsync(new(Month: month, SavedFilterExternalId: filter.ExternalId));
        Assert.Equal("100".PadRight(5, '0'), Field(Assert.Single(viaFilter.Rows), "amount_cents"));
    }

    [Fact]
    public async Task InvalidFilterReturnsAnErrorInsteadOfAFile()
    {
        var result = await Export.ExportAsync(new(FromDate: new DateOnly(2026, 12, 31), ToDate: new DateOnly(2026, 1, 1)));
        Assert.False(result.Succeeded);
        Assert.Null(result.File);
        Assert.False((await Export.ExportAsync(new(Month: new ReferenceMonth(2026, 10), SavedFilterExternalId: Guid.NewGuid()))).Succeeded);
    }

    [Fact]
    public void TemplatesAndExamplesFollowTheLayoutWithFictionalData()
    {
        foreach (var kind in Enum.GetValues<TransactionFileKind>())
        {
            var layout = TransactionLayout.For(kind);
            var template = CsvReader.Parse(Export.Template(kind).Content, layout.Type, layout.Version, layout.Columns);
            Assert.True(template.Succeeded, template.Error);
            Assert.Empty(template.Table!.Rows);
            var example = CsvReader.Parse(Export.Example(kind).Content, layout.Type, layout.Version, layout.Columns);
            Assert.True(example.Succeeded, example.Error);
            Assert.NotEmpty(example.Table!.Rows);
        }
    }
}
