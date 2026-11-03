using System.Text;
using Ags.Finance.Modules.Accounts;
using Ags.Finance.Modules.Imports.Application.Csv;
using Ags.Finance.Modules.Imports.Application.Transactions;
using Ags.Finance.Modules.Imports.Contracts;
using Ags.Finance.Modules.Reimbursements;
using Ags.Finance.Modules.Transactions;
using Ags.Finance.Modules.Transactions.Contracts;
using Ags.Finance.SharedKernel;
using Ags.Finance.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace Ags.Finance.Modules.Imports.Tests;

public sealed class TransactionImportTests : ImportsFixture
{
    private static readonly TransactionExportFilter s_quarter = new(FromDate: new DateOnly(2026, 10, 1), ToDate: new DateOnly(2026, 12, 31));
    private static readonly string[] s_columns = [.. TransactionLayout.For(TransactionFileKind.Transactions).Columns];

    private static readonly EntryFilter s_quarterFilter = new(FromDate: new DateOnly(2026, 10, 1), ToDate: new DateOnly(2026, 12, 31));

    private IEntryCatalog Entries => Get<IEntryCatalog>();

    /// <summary>Conjunto variado: avulsa, receita, cartão, mês manual, compra parcelada e série recorrente com uma ocorrência efetivada e as demais pendentes.</summary>
    private async Task<Seed> SeedEntriesAsync()
    {
        var seed = await SeedAsync();
        var income = await Get<Ags.Finance.Modules.Transactions.Contracts.IClassificationCatalog>().SaveCategoryAsync(null, "Trabalho", Ags.Finance.Modules.Transactions.Contracts.CategoryKind.Income);
        var salary = (await Get<Ags.Finance.Modules.Transactions.Contracts.IClassificationCatalog>().SaveSubcategoryAsync(null, income.ExternalId!.Value, "Salário")).ExternalId!.Value;
        Assert.True((await Entries.CreateAsync(new(new DateOnly(2026, 10, 5), seed.Merchant, seed.Subcategory, seed.Account, new Money(142.90m), "Mercado =semana"))).Succeeded);
        Assert.True((await Entries.CreateAsync(new(new DateOnly(2026, 10, 6), seed.Merchant, salary, seed.Account, new Money(5000.01m), "Salário, \"líquido\""))).Succeeded);
        Assert.True((await Entries.CreateAsync(new(new DateOnly(2026, 10, 1), seed.Merchant, seed.Subcategory, seed.Account, new Money(100m), "Cartão avulso", null, seed.Card, seed.Final))).Succeeded);
        Assert.True((await Entries.CreateAsync(new(new DateOnly(2026, 10, 8), seed.Merchant, seed.Subcategory, seed.Account, new Money(0.01m), "Mês manual", new AccountingMonth(2027, 2)))).Succeeded);
        Assert.True((await Entries.CreateInstallmentsAsync(new(new DateOnly(2026, 10, 6), seed.Merchant, seed.Subcategory, seed.Account, new Money(100m), "Geladeira", null, seed.Card, seed.Final), 3)).Succeeded);
        var series = await Entries.CreateRecurringAsync(new(new(new DateOnly(2026, 10, 5), seed.Merchant, seed.Subcategory, seed.Account, new Money(40m), "Assinatura", IsPaid: false)));
        Assert.True(series.Succeeded, series.Error);
        var first = (await Entries.QueryAsync(new(Month: new AccountingMonth(2026, 10), PendingRecurringOnly: true))).Entries.Single();
        Assert.True((await Entries.ConfirmEffectiveAsync(first.ExternalId, first.Date)).Succeeded);
        _ = await Entries.QueryAsync(new(FromDate: new DateOnly(2026, 10, 1), ToDate: new DateOnly(2026, 12, 31)));
        return seed;
    }

    private static async Task<(CsvFile Entries, CsvFile Series)> ExportPairAsync(IServiceProvider provider)
    {
        var export = provider.GetRequiredService<ITransactionExport>();
        return ((await export.ExportAsync(s_quarter)).File!, (await export.ExportSeriesAsync(s_quarter)).File!);
    }

    private static async Task<(PostgresTestDatabase Database, ServiceProvider Provider)> EmptyEnvironmentAsync(Dictionary<RegistryKind, CsvFile> registries)
    {
        var database = await PostgresTestDatabase.CreateAsync();
        var provider = Build(database.ConnectionString).BuildServiceProvider();
        await provider.MigrateAccountsModuleAsync();
        await provider.MigrateTransactionsModuleAsync();
        await provider.MigrateReimbursementsModuleAsync();
        foreach (var kind in Enum.GetValues<RegistryKind>())
        {
            var report = await provider.GetRequiredService<IRegistryImport>().ImportAsync(kind, registries[kind].Content);
            Assert.True(report.Succeeded, $"{kind}: {string.Join("; ", report.Issues.Select(x => x.Message))}");
        }
        return (database, provider);
    }

    private async Task<Dictionary<RegistryKind, CsvFile>> ExportRegistriesAsync()
    {
        var files = new Dictionary<RegistryKind, CsvFile>();
        foreach (var kind in Enum.GetValues<RegistryKind>()) { files[kind] = await Get<IRegistryExport>().ExportAsync(kind); }
        return files;
    }

    private static HashSet<string> Rows(CsvFile file, string type, IReadOnlyList<string> columns)
        => CsvReader.Parse(file.Content, type, 2, columns).Table!.Rows.Select(x => string.Join("\u001f", x.Fields)).ToHashSet(StringComparer.Ordinal);

    private static string Text(CsvFile file) => Encoding.UTF8.GetString(file.Content, 3, file.Content.Length - 3);

    private static byte[] Bytes(string text) => Encoding.UTF8.GetBytes(text);

    private static string Replace(string text, string column, string oldValue, string newValue)
    {
        var lines = text.Split("\r\n");
        var index = Array.IndexOf(s_columns, column);
        for (var line = 2; line < lines.Length; line++)
        {
            var fields = lines[line].Split(',');
            if (fields.Length <= index || fields[index] != oldValue) { continue; }
            fields[index] = newValue;
            lines[line] = string.Join(",", fields);
            break;
        }
        return string.Join("\r\n", lines);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SingleCashEntryRoundTripsPendingOrPaidState(bool paid)
    {
        var seed = await SeedAsync();
        Assert.True((await Entries.CreateAsync(new(new DateOnly(2026, 10, 5), seed.Merchant, seed.Subcategory, seed.Account, new Money(100), IsPaid: paid))).Succeeded);
        var (file, _) = await ExportPairAsync(Provider);
        var (database, target) = await EmptyEnvironmentAsync(await ExportRegistriesAsync());
        await using (target)
        await using (database)
        {
            var report = await target.GetRequiredService<ITransactionImport>().ImportAsync(file.Content, null);
            Assert.True(report.Succeeded, string.Join("; ", report.Issues.Select(x => x.Message)));
            var entry = Assert.Single(await target.GetRequiredService<IEntryCatalog>().GetAsync());
            Assert.Equal(paid, entry.IsEffective);
            Assert.Equal(paid ? entry.Date : (DateOnly?)null, entry.EffectiveDate);
        }
    }

    [Fact]
    public async Task ExportedFilesRebuildEntriesInstallmentsAndSeriesInAnEmptyEnvironmentKeepingTotals()
    {
        await SeedEntriesAsync();
        var (entriesFile, seriesFile) = await ExportPairAsync(Provider);
        var registries = await ExportRegistriesAsync();
        var (database, target) = await EmptyEnvironmentAsync(registries);
        await using (target)
        await using (database)
        {
            var import = target.GetRequiredService<ITransactionImport>();
            var preview = await import.PreviewAsync(entriesFile.Content, seriesFile.Content);
            Assert.True(preview.Succeeded, string.Join("; ", preview.Issues.Select(x => $"L{x.Line} {x.Field}: {x.Message}")));
            Assert.Empty(await target.GetRequiredService<IEntryCatalog>().GetAsync());
            var report = await import.ImportAsync(entriesFile.Content, seriesFile.Content);
            Assert.True(report.Applied, string.Join("; ", report.Issues.Select(x => $"L{x.Line} {x.Field}: {x.Message}")));
            Assert.Equal(preview.NewCount, report.NewCount);

            var (rebuiltEntries, rebuiltSeries) = await ExportPairAsync(target);
            Assert.Equal(Rows(entriesFile, "transactions", s_columns), Rows(rebuiltEntries, "transactions", s_columns));
            var seriesColumns = TransactionLayout.For(TransactionFileKind.RecurringSeries).Columns;
            Assert.Equal(Rows(seriesFile, "recurring_series", seriesColumns), Rows(rebuiltSeries, "recurring_series", seriesColumns));

            var originalTotals = await Get<IEntryTotals>().GetMonthlyAsync(new AccountingMonth(2026, 10), new AccountingMonth(2027, 3));
            var rebuiltTotals = await target.GetRequiredService<IEntryTotals>().GetMonthlyAsync(new AccountingMonth(2026, 10), new AccountingMonth(2027, 3));
            Assert.Equal(originalTotals.OrderBy(x => x.Month).ThenBy(x => x.Kind).Select(x => (x.Month, x.Kind, x.Amount, x.Count)),
                rebuiltTotals.OrderBy(x => x.Month).ThenBy(x => x.Kind).Select(x => (x.Month, x.Kind, x.Amount, x.Count)));
            var account = Get<Ags.Finance.Modules.Accounts.Contracts.IAccountCatalog>();
            var seed = (await account.GetAccountsAsync(false)).First(x => x.IsActive);
            Assert.Equal((await Entries.GetBalanceAsync(seed.ExternalId, new DateOnly(2027, 1, 1)))!.EffectiveBalance,
                (await target.GetRequiredService<IEntryCatalog>().GetBalanceAsync(seed.ExternalId, new DateOnly(2027, 1, 1)))!.EffectiveBalance);
            var states = (await target.GetRequiredService<IEntryCatalog>().QueryAsync(new(FromDate: new DateOnly(2026, 10, 1), ToDate: new DateOnly(2026, 12, 31), PageSize: 200))).Entries.Where(x => x.RecurringEntryExternalId is not null).OrderBy(x => x.Date).ToList();
            Assert.Equal([true, false, false], states.Select(x => x.IsEffective));
        }
    }

    [Fact]
    public async Task ImportingTheSameFilesAgainDuplicatesNothing()
    {
        await SeedEntriesAsync();
        var (entriesFile, seriesFile) = await ExportPairAsync(Provider);
        var before = await Entries.QueryAsync(new(FromDate: new DateOnly(2026, 10, 1), ToDate: new DateOnly(2026, 12, 31), PageSize: 200));
        var report = await Get<ITransactionImport>().ImportAsync(entriesFile.Content, seriesFile.Content);
        Assert.True(report.Succeeded, string.Join("; ", report.Issues.Select(x => $"L{x.Line} {x.Field}: {x.Message}")));
        Assert.Equal(0, report.NewCount);
        Assert.True(report.IdenticalCount > 0);
        var after = await Entries.QueryAsync(new(FromDate: new DateOnly(2026, 10, 1), ToDate: new DateOnly(2026, 12, 31), PageSize: 200));
        Assert.Equal(before.TotalCount, after.TotalCount);
        Assert.Equal((before.Income, before.Expenses), (after.Income, after.Expenses));
    }

    [Fact]
    public async Task IncompleteInstallmentGroupIsRejectedWithoutRebuildingFromText()
    {
        await SeedEntriesAsync();
        var (entriesFile, _) = await ExportPairAsync(Provider);
        var registries = await ExportRegistriesAsync();
        var (database, target) = await EmptyEnvironmentAsync(registries);
        await using (target)
        await using (database)
        {
            var lines = Text(entriesFile).Split("\r\n").Where(x => x.Length > 0).ToList();
            var without = lines.Where(x => !x.Contains(",3,3,", StringComparison.Ordinal)).ToList();
            Assert.Equal(lines.Count - 1, without.Count);
            var report = await target.GetRequiredService<ITransactionImport>().ImportAsync(Bytes(string.Join("\r\n", without) + "\r\n"), null);
            Assert.False(report.Succeeded);
            Assert.Contains(report.Issues, x => x.Field == "installment_count");
            Assert.Empty(await target.GetRequiredService<IEntryCatalog>().GetAsync());
        }
    }

    [Fact]
    public async Task RecurringRowsWithoutTheSeriesFileAreRejectedAndNothingIsWritten()
    {
        await SeedEntriesAsync();
        var (entriesFile, _) = await ExportPairAsync(Provider);
        var (database, target) = await EmptyEnvironmentAsync(await ExportRegistriesAsync());
        await using (target)
        await using (database)
        {
            var report = await target.GetRequiredService<ITransactionImport>().ImportAsync(entriesFile.Content, null);
            Assert.False(report.Succeeded);
            Assert.Contains(report.Issues, x => x.Field == "recurring_entry_external_id");
            Assert.Empty(await target.GetRequiredService<IEntryCatalog>().GetAsync());
        }
    }

    [Fact]
    public async Task ConflictOnAnExistingEntryNeverOverwritesIt()
    {
        var seed = await SeedAsync();
        var created = await Entries.CreateAsync(new(new DateOnly(2026, 10, 5), seed.Merchant, seed.Subcategory, seed.Account, new Money(10m), "Original"));
        var file = (await Get<ITransactionExport>().ExportAsync(s_quarter)).File!;
        var changed = Replace(Text(file), "amount_cents", "1000", "2000");
        var report = await Get<ITransactionImport>().ImportAsync(Bytes(changed), null);
        Assert.False(report.Succeeded);
        var issue = Assert.Single(report.Issues);
        Assert.Equal((3, "external_id"), (issue.Line, issue.Field));
        Assert.Equal(new Money(-10m), Assert.Single(await Entries.GetAsync(), x => x.ExternalId == created.ExternalId).Amount);
    }

    [Fact]
    public async Task InconsistentPendingDateAndMissingReferenceAreReportedWithLineAndColumn()
    {
        var seed = await SeedAsync();
        await Entries.CreateAsync(new(new DateOnly(2026, 10, 5), seed.Merchant, seed.Subcategory, seed.Account, new Money(10m), "Original"));
        var file = Text((await Get<ITransactionExport>().ExportAsync(s_quarter)).File!);
        var renamed = Replace(file, "external_id", file.Split("\r\n")[2].Split(',')[0], Guid.CreateVersion7().ToString("D"));
        var pending = Replace(renamed, "is_effective", "true", "false");
        var report = await Get<ITransactionImport>().PreviewAsync(Bytes(pending), null);
        Assert.Contains(report.Issues, x => x is { Line: 3, Field: "effective_date" });
        var fresh = Replace(renamed, "merchant_external_id", seed.Merchant.ToString("D"), Guid.NewGuid().ToString("D"));
        var missing = await Get<ITransactionImport>().PreviewAsync(Bytes(fresh), null);
        Assert.False(missing.Succeeded);
        Assert.Equal((3, "merchant_external_id"), (missing.Issues[0].Line, missing.Issues[0].Field));
    }

    private static string Row(Seed seed, string kind = "Expense", Guid? id = null, string person = "", string percent = "")
        => $"{id ?? Guid.CreateVersion7():D},2026-10-05,2026-10,{kind},10000,{seed.Merchant:D},{seed.Subcategory:D},{seed.Account:D},,,,true,2026-10-05,,,,,,{person},{percent}";

    private static byte[] File(params string[] rows) => Bytes($"ags-finance-csv,transactions,2\r\n{string.Join(",", s_columns)}\r\n{string.Join("\r\n", rows)}\r\n");

    [Fact]
    public async Task ExpenseAndSeriesWithPersonSurviveARoundTripInAnEmptyEnvironment()
    {
        var seed = await SeedAsync();
        Assert.True((await Entries.CreateAsync(new(new DateOnly(2026, 10, 5), seed.Merchant, seed.Subcategory, seed.Account, new Money(100m), Reimbursement: new(seed.Person, 12.5m)))).Succeeded);
        Assert.True((await Entries.CreateRecurringAsync(new(new(new DateOnly(2026, 10, 7), seed.Merchant, seed.Subcategory, seed.Account, new Money(40m), Reimbursement: new(seed.Person, 30m))))).Succeeded);
        _ = await Entries.QueryAsync(s_quarterFilter);
        var (entries, series) = await ExportPairAsync(Provider);
        var registries = new Dictionary<RegistryKind, CsvFile>();
        foreach (var kind in Enum.GetValues<RegistryKind>()) { registries[kind] = await Get<IRegistryExport>().ExportAsync(kind); }
        var (database, target) = await EmptyEnvironmentAsync(registries);
        await using (target)
        await using (database)
        {
            var report = await target.GetRequiredService<ITransactionImport>().ImportAsync(entries.Content, series.Content);
            Assert.True(report.Succeeded, string.Join("; ", report.Issues.Select(x => $"L{x.Line} {x.Field}: {x.Message}")));

            var rebuilt = await target.GetRequiredService<IEntryCatalog>().GetAsync();
            Assert.Equal(new EntryReimbursement(seed.Person, 12.5m), rebuilt.Single(x => x.RecurringEntryExternalId is null).Reimbursement);
            Assert.All(rebuilt.Where(x => x.RecurringEntryExternalId is not null), x => Assert.Equal(new EntryReimbursement(seed.Person, 30m), x.Reimbursement));
            var seriesId = rebuilt.First(x => x.RecurringEntryExternalId is not null).RecurringEntryExternalId!.Value;
            Assert.Equal(new EntryReimbursement(seed.Person, 30m), (await target.GetRequiredService<IEntryCatalog>().GetRecurringAsync(seriesId))!.Template.Reimbursement);

            var again = await target.GetRequiredService<ITransactionImport>().ImportAsync(entries.Content, series.Content);
            Assert.True(again.Succeeded, string.Join("; ", again.Issues.Select(x => $"L{x.Line} {x.Field}: {x.Message}")));
            Assert.Equal(0, again.NewCount);
        }
    }

    [Theory]
    [InlineData(true, false, "reimbursement_percent")]
    [InlineData(false, true, "reimbursement_person_external_id")]
    public async Task PersonAndPercentMustComeTogether(bool hasPerson, bool hasPercent, string field)
    {
        var seed = await SeedAsync();
        var report = await Get<ITransactionImport>().ImportAsync(File(Row(seed, person: hasPerson ? seed.Person.ToString("D") : "", percent: hasPercent ? "50" : "")), null);
        Assert.False(report.Succeeded);
        Assert.Equal((3, field), (report.Issues[0].Line, report.Issues[0].Field));
        Assert.Empty(await Entries.GetAsync());
    }

    [Theory]
    [InlineData("100.01")]
    [InlineData("-1")]
    [InlineData("33.333")]
    public async Task InvalidPercentIsRejectedOnItsLine(string percent)
    {
        var seed = await SeedAsync();
        var report = await Get<ITransactionImport>().ImportAsync(File(Row(seed, person: seed.Person.ToString("D"), percent: percent)), null);
        Assert.Equal((3, "reimbursement_percent"), (report.Issues[0].Line, report.Issues[0].Field));
    }

    [Fact]
    public async Task IncomeWithPersonIsRejected()
    {
        var seed = await SeedAsync();
        var report = await Get<ITransactionImport>().ImportAsync(File(Row(seed, "Income", person: seed.Person.ToString("D"), percent: "50")), null);
        Assert.Contains(report.Issues, x => x.Field == "reimbursement_person_external_id");
    }

    [Fact]
    public async Task UnknownPersonIsRejected()
    {
        var seed = await SeedAsync();
        var report = await Get<ITransactionImport>().ImportAsync(File(Row(seed, person: Guid.CreateVersion7().ToString("D"), percent: "50")), null);
        Assert.Equal((3, "reimbursement_person_external_id"), (report.Issues[0].Line, report.Issues[0].Field));
        Assert.Empty(await Entries.GetAsync());
    }

    [Fact]
    public async Task StoredPercentIsKeptEvenIfTheDefaultOfThePersonIsDifferent()
    {
        var seed = await SeedAsync();
        Assert.True((await Get<ITransactionImport>().ImportAsync(File(Row(seed, person: seed.Person.ToString("D"), percent: "30")), null)).Succeeded);
        Assert.Equal(30m, Assert.Single(await Entries.GetAsync()).Reimbursement!.Percent);
    }

    [Theory]
    [InlineData("transactions")]
    [InlineData("recurring_series")]
    public async Task VersionOneOfTheEntryFilesIsRejected(string type)
    {
        var seed = await SeedAsync();
        var line = Row(seed);
        var bytes = Bytes($"ags-finance-csv,{type},1\r\n{string.Join(",", s_columns)}\r\n{line}\r\n");
        var report = type == "transactions" ? await Get<ITransactionImport>().ImportAsync(bytes, null) : await Get<ITransactionImport>().ImportAsync(File(line), bytes);
        Assert.False(report.Succeeded);
        Assert.Empty(await Entries.GetAsync());
    }

    [Fact]
    public async Task ExistingEntryWithDifferentPersonOrPercentIsAConflict()
    {
        var seed = await SeedAsync();
        var id = Guid.CreateVersion7();
        Assert.True((await Get<ITransactionImport>().ImportAsync(File(Row(seed, id: id, person: seed.Person.ToString("D"), percent: "30")), null)).Succeeded);

        var report = await Get<ITransactionImport>().ImportAsync(File(Row(seed, id: id, person: seed.Person.ToString("D"), percent: "40")), null);

        Assert.Equal("external_id", Assert.Single(report.Issues).Field);
        Assert.Equal(30m, Assert.Single(await Entries.GetAsync()).Reimbursement!.Percent);
    }

    [Fact]
    public async Task EntriesAreCreatedWithTheInformedAccountingMonthAndPreviewWritesNothing()
    {
        var seed = await SeedAsync();
        var id = Guid.CreateVersion7();
        var text = $"ags-finance-csv,transactions,2\r\n{string.Join(",", s_columns)}\r\n{id:D},2026-10-05,2027-02,Expense,12345,{seed.Merchant:D},{seed.Subcategory:D},{seed.Account:D},,,Mês manual,true,2026-10-05,,,,,,,\r\n";
        var preview = await Get<ITransactionImport>().PreviewAsync(Bytes(text), null);
        Assert.True(preview.Succeeded);
        Assert.Equal((1, 0, false), (preview.NewCount, preview.IdenticalCount, preview.Applied));
        Assert.Empty(await Entries.GetAsync());
        var applied = await Get<ITransactionImport>().ImportAsync(Bytes(text), null);
        Assert.True(applied.Applied);
        var entry = Assert.Single(await Entries.GetAsync());
        Assert.Equal((id, new AccountingMonth(2027, 2), new Money(-123.45m)), (entry.ExternalId, entry.Month, entry.Amount));
    }

    [Fact]
    public async Task TransfersAdjustmentsAndOtherLayoutsAreNotAccepted()
    {
        var seed = await SeedAsync();
        var transfer = $"ags-finance-csv,transactions,2\r\n{string.Join(",", s_columns)}\r\n{Guid.CreateVersion7():D},2026-10-05,2026-10,Transfer,100,{seed.Merchant:D},{seed.Subcategory:D},{seed.Account:D},,,x,true,2026-10-05,,,,,,,\r\n";
        var report = await Get<ITransactionImport>().ImportAsync(Bytes(transfer), null);
        Assert.False(report.Succeeded);
        Assert.Contains(report.Issues, x => x.Field == "kind");
        byte[][] others =
        [
            [0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x34, 0x0A, 0xE2],
            [0x50, 0x4B, 0x03, 0x04, 0x14, 0x00],
            Bytes("Data;Descricao;Valor\r\n05/10/2026;Padaria;10,00\r\n"),
        ];
        foreach (var bytes in others)
        {
            Assert.False((await Get<ITransactionImport>().ImportAsync(bytes, null)).Succeeded);
        }
        Assert.Empty(await Entries.GetAsync());
    }

    [Fact]
    public async Task ExampleFilesAreValidAgainstTheLayoutButNeedTheirReferences()
    {
        var export = Get<ITransactionExport>();
        var report = await Get<ITransactionImport>().PreviewAsync(export.Example(TransactionFileKind.Transactions).Content, export.Example(TransactionFileKind.RecurringSeries).Content);
        Assert.False(report.Succeeded);
        Assert.All(report.Issues, x => Assert.NotEmpty(x.Message));
    }
}
