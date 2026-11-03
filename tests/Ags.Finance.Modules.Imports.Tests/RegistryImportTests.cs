using System.Text;
using Ags.Finance.Modules.Accounts;
using Ags.Finance.Modules.Accounts.Contracts;
using Ags.Finance.Modules.Imports.Application.Csv;
using Ags.Finance.Modules.Imports.Application.Registries;
using Ags.Finance.Modules.Imports.Contracts;
using Ags.Finance.Modules.Reimbursements;
using Ags.Finance.Modules.Transactions;
using Ags.Finance.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace Ags.Finance.Modules.Imports.Tests;

public sealed class RegistryImportTests : ImportsFixture
{
    private static readonly RegistryKind[] s_order = Enum.GetValues<RegistryKind>();

    /// <summary>Um segundo ambiente vazio, com o mesmo conjunto de módulos, para reconstruir cadastros a partir dos arquivos.</summary>
    private static async Task<(PostgresTestDatabase Database, ServiceProvider Provider)> EmptyEnvironmentAsync()
    {
        var database = await PostgresTestDatabase.CreateAsync();
        var provider = Build(database.ConnectionString).BuildServiceProvider();
        await provider.MigrateAccountsModuleAsync();
        await provider.MigrateTransactionsModuleAsync();
        await provider.MigrateReimbursementsModuleAsync();
        return (database, provider);
    }

    private static async Task<Dictionary<RegistryKind, CsvFile>> ExportAllAsync(IServiceProvider provider)
    {
        var export = provider.GetRequiredService<IRegistryExport>();
        var files = new Dictionary<RegistryKind, CsvFile>();
        foreach (var kind in s_order) { files[kind] = await export.ExportAsync(kind); }
        return files;
    }

    private static HashSet<string> Rows(CsvFile file, RegistryKind kind) => Parse(file, kind).Rows.Select(x => string.Join("\u001f", x.Fields)).ToHashSet(StringComparer.Ordinal);

    [Fact]
    public async Task ExportedFilesRebuildTheRegistriesWithTheSamePublicIdentitiesInAnEmptyEnvironment()
    {
        var seed = await SeedAsync();
        Assert.True((await Get<IAccountCatalog>().DeactivateCreditCardAsync(seed.Card)).Succeeded);
        var files = await ExportAllAsync(Provider);
        var (database, target) = await EmptyEnvironmentAsync();
        await using (target)
        await using (database)
        {
            var import = target.GetRequiredService<IRegistryImport>();
            foreach (var kind in s_order)
            {
                var report = await import.ImportAsync(kind, files[kind].Content);
                Assert.True(report.Succeeded, $"{kind}: {string.Join("; ", report.Issues.Select(x => $"L{x.Line} {x.Field}: {x.Message}"))}");
                Assert.True(report.Applied);
                Assert.Equal(Parse(files[kind], kind).Rows.Count, report.NewCount);
            }
            var rebuilt = await ExportAllAsync(target);
            foreach (var kind in s_order) { Assert.Equal(Rows(files[kind], kind), Rows(rebuilt[kind], kind)); }
            var archived = (await target.GetRequiredService<IAccountCatalog>().GetCreditCardsAsync(false)).Single();
            Assert.Equal((seed.Card, false), (archived.ExternalId, archived.IsActive));
            Assert.Equal(2, (await target.GetRequiredService<IAccountCatalog>().GetAccountsAsync(false)).Count);
        }
    }

    [Fact]
    public async Task ImportingTheSameFilesAgainCreatesNothingAndChangesNothing()
    {
        await SeedAsync();
        var files = await ExportAllAsync(Provider);
        var import = Get<IRegistryImport>();
        foreach (var kind in s_order)
        {
            var report = await import.ImportAsync(kind, files[kind].Content);
            Assert.True(report.Succeeded, string.Join("; ", report.Issues.Select(x => x.Message)));
            Assert.Equal(0, report.NewCount);
            Assert.Equal(Parse(files[kind], kind).Rows.Count, report.IdenticalCount);
        }
        var after = await ExportAllAsync(Provider);
        foreach (var kind in s_order) { Assert.Equal(Rows(files[kind], kind), Rows(after[kind], kind)); }
    }

    [Fact]
    public async Task PreviewReportsNewAndIdenticalRecordsWithoutWritingAnything()
    {
        var seed = await SeedAsync();
        var bankFile = await Get<IRegistryExport>().ExportAsync(RegistryKind.Banks);
        var extra = Encoding.UTF8.GetString(bankFile.Content, 3, bankFile.Content.Length - 3) + $"{Guid.CreateVersion7():D},Outro Banco Fictício\r\n";
        var preview = await Get<IRegistryImport>().PreviewAsync(RegistryKind.Banks, Encoding.UTF8.GetBytes(extra));
        Assert.True(preview.Succeeded);
        Assert.Equal((1, 1, false), (preview.NewCount, preview.IdenticalCount, preview.Applied));
        Assert.Equal(seed.Bank, Assert.Single(await Get<IAccountCatalog>().GetBanksAsync()).ExternalId);
        var applied = await Get<IRegistryImport>().ImportAsync(RegistryKind.Banks, Encoding.UTF8.GetBytes(extra));
        Assert.True(applied.Applied);
        Assert.Equal(2, (await Get<IAccountCatalog>().GetBanksAsync()).Count);
    }

    [Fact]
    public async Task DifferentDataForAnExistingIdentityIsAConflictAndNothingIsOverwritten()
    {
        var seed = await SeedAsync();
        var text = $"ags-finance-csv,banks,1\r\nexternal_id,name\r\n{seed.Bank:D},Nome Alterado\r\n{Guid.CreateVersion7():D},Banco Válido\r\n";
        var report = await Get<IRegistryImport>().ImportAsync(RegistryKind.Banks, Encoding.UTF8.GetBytes(text));
        Assert.False(report.Succeeded);
        var issue = Assert.Single(report.Issues);
        Assert.Equal((3, "external_id"), (issue.Line, issue.Field));
        var banks = await Get<IAccountCatalog>().GetBanksAsync();
        Assert.Equal("Banco \"São João\", Filial", Assert.Single(banks).Name);
    }

    [Fact]
    public async Task AnyErrorRejectsTheWholeFileWithoutPartialWrites()
    {
        var good = Guid.CreateVersion7();
        var text = $"ags-finance-csv,accounts,1\r\nexternal_id,bank_external_id,name,kind,color,is_active,opening_balance_cents,opening_date\r\n" +
            $"{good:D},{Guid.NewGuid():D},Conta A,Checking,green,true,0,2026-01-01\r\n" +
            $"{Guid.CreateVersion7():D},{Guid.NewGuid():D},Conta B,Poupanca,green,talvez,1,25/12/2026\r\n";
        var bank = (await Get<IAccountCatalog>().CreateBankAsync("Banco Exemplo")).ExternalId!.Value;
        var withBank = text.Replace(text.Split("\r\n")[2].Split(',')[1], bank.ToString("D"), StringComparison.Ordinal);
        var report = await Get<IRegistryImport>().ImportAsync(RegistryKind.Accounts, Encoding.UTF8.GetBytes(withBank));
        Assert.False(report.Succeeded);
        Assert.Contains(report.Issues, x => x is { Line: 4, Field: "kind" });
        Assert.Contains(report.Issues, x => x is { Line: 4, Field: "is_active" });
        Assert.Contains(report.Issues, x => x is { Line: 4, Field: "opening_date" });
        Assert.Empty(await Get<IAccountCatalog>().GetAccountsAsync(false));
    }

    [Fact]
    public async Task MissingReferenceIsReportedWithLineAndFieldAndNothingIsWritten()
    {
        var text = $"ags-finance-csv,accounts,1\r\nexternal_id,bank_external_id,name,kind,color,is_active,opening_balance_cents,opening_date\r\n" +
            $"{Guid.CreateVersion7():D},{Guid.NewGuid():D},Conta A,Checking,green,true,0,2026-01-01\r\n";
        var report = await Get<IRegistryImport>().ImportAsync(RegistryKind.Accounts, Encoding.UTF8.GetBytes(text));
        Assert.False(report.Succeeded);
        Assert.Equal(3, Assert.Single(report.Issues).Line);
        Assert.Empty(await Get<IAccountCatalog>().GetAccountsAsync(false));
    }

    [Fact]
    public async Task ArchivedRecordIsNeverReactivatedByAReimport()
    {
        var seed = await SeedAsync();
        var file = await Get<IRegistryExport>().ExportAsync(RegistryKind.Accounts);
        var text = Encoding.UTF8.GetString(file.Content, 3, file.Content.Length - 3).Replace(",false,0,2025-01-01", ",true,0,2025-01-01", StringComparison.Ordinal);
        var report = await Get<IRegistryImport>().ImportAsync(RegistryKind.Accounts, Encoding.UTF8.GetBytes(text));
        Assert.False(report.Succeeded);
        Assert.Equal("external_id", Assert.Single(report.Issues).Field);
        Assert.False((await Get<IAccountCatalog>().GetAccountsAsync(false)).Single(x => x.ExternalId == seed.ArchivedAccount).IsActive);
    }

    [Theory]
    [InlineData("nome,valor\r\nx,1\r\n")]
    [InlineData("ags-finance-csv,banks,9\r\nexternal_id,name\r\n")]
    [InlineData("ags-finance-csv,banks,1\r\nid;nome\r\n")]
    [InlineData("ags-finance-csv,banks,1\r\nexternal_id,name,extra\r\n")]
    public async Task OutOfLayoutFilesAreRejectedWithAFileLevelMessage(string text)
    {
        var report = await Get<IRegistryImport>().ImportAsync(RegistryKind.Banks, Encoding.UTF8.GetBytes(text));
        Assert.False(report.Succeeded);
        Assert.Equal(0, Assert.Single(report.Issues).Line);
        Assert.Empty(await Get<IAccountCatalog>().GetBanksAsync());
    }

    [Fact]
    public async Task PdfAndSpreadsheetBytesAreNotSupported()
    {
        foreach (var bytes in new[] { new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x34, 0x0A, 0xE2, 0xE3 }, [0x50, 0x4B, 0x03, 0x04, 0x14, 0x00, 0x06, 0x00] })
        {
            Assert.False((await Get<IRegistryImport>().ImportAsync(RegistryKind.Banks, bytes)).Succeeded);
        }
        Assert.Empty(await Get<IAccountCatalog>().GetBanksAsync());
    }

    [Fact]
    public async Task ExampleFilesFollowTheDocumentedOrderOfReferences()
    {
        var export = Get<IRegistryExport>();
        var import = Get<IRegistryImport>();
        foreach (var kind in s_order)
        {
            var report = await import.ImportAsync(kind, export.Example(kind).Content);
            Assert.True(report.Succeeded, $"{kind}: {string.Join("; ", report.Issues.Select(x => $"L{x.Line} {x.Field}: {x.Message}"))}");
            Assert.Equal(1, report.NewCount);
        }
        Assert.Equal(RegistryLayout.All.Count, s_order.Length);
    }
}
