using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Ags.Finance.Modules.Accounts.Contracts;
using Ags.Finance.Modules.Attachments.Contracts;
using Ags.Finance.Modules.Invoices.Contracts;
using Ags.Finance.Modules.Transactions.Contracts;
using Ags.Finance.SharedKernel;
using Ags.Finance.Web.Attachments;
using Ags.Finance.Web.Backup;
using Ags.Finance.Web.Commands;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using AttachmentPage = Ags.Finance.Web.Components.Pages.Attachments;

namespace Ags.Finance.Web.Tests;

public sealed class AttachmentManagementTests
{
    private static byte[] Pdf => Encoding.ASCII.GetBytes("%PDF-1.7\n1 0 obj<</Type /Catalog>>endobj\ntrailer<</Root 1 0 R>>\n%%EOF\n");
    private static async Task<(Guid Card, Guid Account, AttachmentDetails Row)> InvoiceAsync(IServiceProvider services)
    {
        var accounts = services.GetRequiredService<IAccountCatalog>();
        var bank = await accounts.CreateBankAsync("Banco Fictício");
        var account = await accounts.SaveAccountAsync(new(null, bank.ExternalId!.Value, "Conta Fictícia", AccountKind.Checking, "green"));
        var card = await accounts.SaveCreditCardAsync(new(null, account.ExternalId!.Value, "Cartão Fictício", CardBrand.Visa, new Money(1000), 3, 10, true, "green"));
        using var source = new MemoryStream(Pdf);
        var uploaded = await services.GetRequiredService<IInvoiceAttachmentCatalog>().UploadInvoiceAsync(card.ExternalId!.Value, new(2027, 1), AttachmentKind.Invoice, "fatura fictícia.pdf", source);
        Assert.True(uploaded.Succeeded, uploaded.Error);
        return (card.ExternalId.Value, account.ExternalId.Value, Assert.Single(await services.GetRequiredService<IAttachmentCatalog>().ListAsync()));
    }
    [Fact]
    public async Task AuthenticatedDownloadPreservesBytesFriendlyNameAndNosniffAndMaintenanceBlocksAccess()
    {
        using var factory = new AppFactory();
        using var browser = await factory.CreateSignedInBrowserAsync();
        await using var scope = factory.Services.CreateAsyncScope();
        var (_, _, row) = await InvoiceAsync(scope.ServiceProvider);
        using var opened = await browser.GetAsync($"/attachments/{row.ExternalId}");
        Assert.Equal(HttpStatusCode.OK, opened.StatusCode);
        Assert.Equal("application/pdf", opened.Content.Headers.ContentType?.MediaType);
        Assert.Equal("nosniff", Assert.Single(opened.Headers.GetValues("X-Content-Type-Options")));
        Assert.Equal(Pdf, await opened.Content.ReadAsByteArrayAsync());
        using var downloaded = await browser.GetAsync($"/attachments/{row.ExternalId}?download=true");
        Assert.Equal("attachment", downloaded.Content.Headers.ContentDisposition?.DispositionType);
        Assert.Equal(row.OriginalFileName, downloaded.Content.Headers.ContentDisposition?.FileNameStar);
        var maintenance = factory.Services.GetRequiredService<MaintenanceMode>();
        Assert.True(maintenance.TryEnter());
        using var blocked = await browser.GetAsync($"/attachments/{row.ExternalId}");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, blocked.StatusCode);
        using var source = new MemoryStream(Pdf);
        var catalog = scope.ServiceProvider.GetRequiredService<IAttachmentCatalog>();
        Assert.False((await catalog.UploadAsync(row.Owner, AttachmentKind.Other, "another.pdf", source)).Succeeded);
        Assert.Null(await catalog.OpenAsync(row.ExternalId));
        maintenance.Exit();
    }
    [Fact]
    public async Task ReconciliationReportsMissingCorruptAndOrphanFilesAndOnlyCleansExplicitOrphans()
    {
        using var factory = new AppFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var (_, _, row) = await InvoiceAsync(scope.ServiceProvider);
        var options = scope.ServiceProvider.GetRequiredService<AttachmentStorageOptions>();
        var path = Path.Combine(options.Directory, row.StorageKey);
        var orphan = Path.Combine(options.Directory, "orphan.pdf");
        await File.WriteAllBytesAsync(orphan, Pdf);
        await File.WriteAllBytesAsync(path, new byte[Pdf.Length]);
        var inventory = scope.ServiceProvider.GetRequiredService<AttachmentInventory>();
        var reported = await inventory.VerifyAsync(false, default);
        Assert.Contains(reported, x => x.Status == "corrompido");
        Assert.Contains(reported, x => x.Status == "sem registro");
        Assert.True(File.Exists(orphan));
        await inventory.VerifyAsync(true, default);
        Assert.False(File.Exists(orphan));
        Assert.True(File.Exists(path));
        File.Delete(path);
        Assert.Contains(await inventory.VerifyAsync(false, default), x => x.Status == "ausente");
        Assert.Single(await scope.ServiceProvider.GetRequiredService<IAttachmentCatalog>().ListAsync());
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TrashPageRestoresExistingOwnersAndRefusesDeletedOwners(bool deleteOwner)
    {
        using var factory = new AppFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var (_, _, row) = await InvoiceAsync(scope.ServiceProvider);
        var catalog = scope.ServiceProvider.GetRequiredService<IAttachmentCatalog>();
        await catalog.TrashAsync(row.Owner, row.ExternalId);
        if (deleteOwner)
        {
            await using var connection = await scope.ServiceProvider.GetRequiredService<NpgsqlDataSource>().OpenConnectionAsync();
            await using var command = new NpgsqlCommand("DELETE FROM invoices.invoices WHERE \"ExternalId\" = @id", connection);
            command.Parameters.AddWithValue("id", row.Owner.ExternalId);
            await command.ExecuteNonQueryAsync();
        }
        await using var context = new BunitContext();
        context.Services.AddSingleton(catalog).AddSingleton(scope.ServiceProvider.GetRequiredService<IEntryAttachmentCatalog>())
            .AddSingleton(scope.ServiceProvider.GetRequiredService<IInvoiceAttachmentCatalog>()).AddSingleton(scope.ServiceProvider.GetRequiredService<AttachmentInventory>())
            .AddSingleton(TimeProvider.System);
        var page = context.Render<AttachmentPage>();
        await page.InvokeAsync(() => page.Find("input[type=checkbox]").Change(true));
        page.WaitForAssertion(() => Assert.Contains(page.FindAll("button"), x => x.TextContent == "Restaurar"));
        await page.FindAll("button").Single(x => x.TextContent == "Restaurar").ClickAsync(new());
        Assert.Equal(deleteOwner, Assert.Single(await catalog.ListAsync()).DeletedAt is not null);
        if (deleteOwner) { Assert.Contains("dono", page.Find("[role=status]").TextContent, StringComparison.Ordinal); }
    }
    [Fact]
    public async Task ZipUsesFinancialDatesExcludesTrashAndEveryChecksumIncludingTheIndexMatches()
    {
        using var factory = new AppFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var (card, account, invoice) = await InvoiceAsync(services);
        var payments = services.GetRequiredService<IInvoicePaymentCatalog>();
        var payment = await payments.ConfirmPaymentAsync(new(Guid.CreateVersion7(), card, new(2027, 1), account, new(2026, 11, 3), new Money(25)));
        using (var source = new MemoryStream(Pdf))
        { Assert.True((await services.GetRequiredService<IInvoiceAttachmentCatalog>().UploadPaymentAsync(payment.ExternalId!.Value, AttachmentKind.Receipt, "payment.pdf", source)).Succeeded); }
        var export = services.GetRequiredService<AttachmentExport>();
        var plan = await export.PlanAsync(new(new(2027, 1, 15), new(2027, 1, 20)), default);
        Assert.Null(plan.Error);
        Assert.Equal(2, plan.Items.Count);
        Assert.All(plan.Items, x => Assert.StartsWith("faturas/cartao-ficticio/2027-01/", x.Name, StringComparison.Ordinal));
        await services.GetRequiredService<IInvoiceAttachmentCatalog>().TrashAsync(invoice.Owner, invoice.ExternalId);
        plan = await export.PlanAsync(new(new(2027, 1, 1), new(2027, 1, 31), AttachmentKind.Receipt), default);
        Assert.Single(plan.Items);
        using var output = new MemoryStream();
        await export.WriteAsync(plan, output, default);
        output.Position = 0;
        using var zip = new ZipArchive(output, ZipArchiveMode.Read);
        var checksumEntry = zip.GetEntry("SHA256SUMS");
        Assert.NotNull(checksumEntry);
        using var sums = new StreamReader(checksumEntry.Open());
        foreach (var line in (await sums.ReadToEndAsync()).Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var name = line[66..];
            var entry = zip.GetEntry(name);
            Assert.NotNull(entry);
            await using var content = entry.Open();
            Assert.Equal(line[..64], Convert.ToHexStringLower(await SHA256.HashDataAsync(content)));
        }
        Assert.Equal(3, zip.Entries.Count);
        using var browser = await factory.CreateSignedInBrowserAsync();
        using var response = await browser.GetAsync("/attachments/export?from=2027-01-01&to=2027-01-31&kind=Receipt");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/zip", response.Content.Headers.ContentType?.MediaType);
        using var downloaded = new MemoryStream(await response.Content.ReadAsByteArrayAsync());
        using var httpZip = new ZipArchive(downloaded, ZipArchiveMode.Read);
        Assert.Equal(3, httpZip.Entries.Count);
        Assert.DoesNotContain(zip.Entries, x => x.Name.Contains(invoice.ExternalId.ToString("N")[^8..], StringComparison.Ordinal));
        Assert.NotNull((await export.PlanAsync(new(new(2020, 1, 1), new(2020, 1, 31)), default)).Error);
        var options = services.GetRequiredService<AttachmentStorageOptions>();
        await File.WriteAllBytesAsync(Path.Combine(options.Directory, plan.Items[0].Attachment.StorageKey), new byte[Pdf.Length]);
        using var corrupt = await browser.GetAsync("/attachments/export?from=2027-01-01&to=2027-01-31&kind=Receipt");
        Assert.Equal(HttpStatusCode.BadRequest, corrupt.StatusCode);
        Assert.Contains("integridade", await corrupt.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.NotEqual("application/zip", corrupt.Content.Headers.ContentType?.MediaType);
    }
    [Fact]
    public async Task EmptyExportReturnsAMessageAndDoesNotCreateAZip()
    {
        using var factory = new AppFactory();
        using var browser = await factory.CreateSignedInBrowserAsync();
        using var response = await browser.GetAsync("/attachments/export?from=2020-01-01&to=2020-01-31");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Nenhum anexo", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        await using var scope = factory.Services.CreateAsyncScope();
        var terminal = new RecordingTerminal();
        var commands = new AttachmentCommands(scope.ServiceProvider.GetRequiredService<AttachmentInventory>(), scope.ServiceProvider.GetRequiredService<AttachmentExport>(), terminal);
        var file = Path.Combine(Path.GetTempPath(), "ags-empty-" + Guid.NewGuid().ToString("N") + ".zip");
        Assert.Equal(1, await commands.RunAsync(["export", "--from", "2020-01-01", "--to", "2020-01-31", "--output", file], default));
        Assert.False(File.Exists(file));
        Assert.Contains("Nenhum anexo", terminal.Error, StringComparison.Ordinal);
    }
    [Fact]
    public async Task EntryExportUsesItsEditedDateWhileTheStoredPathRemainsUnchanged()
    {
        using var factory = new AppFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var (_, account, _) = await InvoiceAsync(services);
        var classifications = services.GetRequiredService<IClassificationCatalog>();
        var category = await classifications.SaveCategoryAsync(null, "Categoria Fictícia", CategoryKind.Expense);
        var subcategory = await classifications.SaveSubcategoryAsync(null, category.ExternalId!.Value, "Subcategoria Fictícia");
        var merchant = await services.GetRequiredService<IMerchantCatalog>().SaveAsync(null, "Descrição Fictícia", subcategory.ExternalId);
        var request = new CreateEntryRequest(new(2026, 11, 3), merchant.ExternalId!.Value, subcategory.ExternalId!.Value, account, new Money(12));
        var entries = services.GetRequiredService<IEntryCatalog>();
        var created = await entries.CreateAsync(request);
        Assert.True(created.Succeeded, created.Error);
        using var source = new MemoryStream(Pdf);
        Assert.True((await services.GetRequiredService<IEntryAttachmentCatalog>().UploadAsync(created.ExternalId!.Value, AttachmentKind.Receipt, "fictitious.pdf", source)).Succeeded);
        var row = (await services.GetRequiredService<IAttachmentCatalog>().ListAsync()).Single(x => x.Owner.Type == AttachmentOwnerType.Entry);
        var export = services.GetRequiredService<AttachmentExport>();
        var before = await export.PlanAsync(new(new(2026, 11, 3), new(2026, 11, 3), AttachmentKind.Receipt), default);
        Assert.StartsWith("lancamentos/2026/11/2026-11-03_comprovante_descricao-ficticia_", Assert.Single(before.Items).Name, StringComparison.Ordinal);
        Assert.True((await entries.EditAsync(created.ExternalId.Value, request with { Date = new(2026, 12, 3) }, EntryChangeScope.OnlyThis)).Succeeded);
        Assert.NotNull((await export.PlanAsync(new(new(2026, 11, 3), new(2026, 11, 3), AttachmentKind.Receipt), default)).Error);
        var after = await export.PlanAsync(new(new(2026, 12, 3), new(2026, 12, 3), AttachmentKind.Receipt), default);
        Assert.StartsWith("lancamentos/2026/12/2026-12-03_comprovante_descricao-ficticia_", Assert.Single(after.Items).Name, StringComparison.Ordinal);
        Assert.Equal(row.StorageKey, Assert.Single(await services.GetRequiredService<IEntryAttachmentCatalog>().ListAsync()).StorageKey);
    }
    private sealed class RecordingTerminal : ITerminal
    {
        public string Error { get; private set; } = string.Empty;
        public string? ReadLine(string prompt) => null;
        public string? ReadPassword(string prompt) => null;
        public void WriteLine(string message) { }
        public void WriteError(string message) => Error = message;
    }
}
