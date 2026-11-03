using Ags.Finance.Modules.Accounts;
using Ags.Finance.Modules.Accounts.Contracts;
using Ags.Finance.Modules.Attachments;
using Ags.Finance.Modules.Attachments.Contracts;
using Ags.Finance.Modules.Invoices;
using Ags.Finance.Modules.Invoices.Contracts;
using Ags.Finance.Modules.Preferences;
using Ags.Finance.Modules.Transactions;
using Ags.Finance.Modules.Transactions.Contracts;
using Ags.Finance.SharedKernel;
using Ags.Finance.Tests.Support;
using Ags.Finance.Web.Components;
using Bunit;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;

namespace Ags.Finance.Web.Tests;

public sealed class InvoicePaymentAttachmentTests
{
    [Fact]
    public async Task FailedDocumentShowsRetryWithoutConfirmingThePaymentAgain()
    {
        var root = Path.Combine(Path.GetTempPath(), "ags-payment-document-" + Guid.NewGuid().ToString("N"));
        try
        {
            await using var database = await PostgresTestDatabase.CreateAsync();
            await using var context = new BunitContext();
            context.Services.AddAttachmentsModule(database.ConnectionString, new(root)).AddAccountsModule(database.ConnectionString)
                .AddTransactionsModule(database.ConnectionString).AddInvoicesModule(database.ConnectionString).AddPreferencesModule(database.ConnectionString);
            await context.Services.MigrateAttachmentsModuleAsync();
            await context.Services.MigrateAccountsModuleAsync();
            await context.Services.MigrateTransactionsModuleAsync();
            await context.Services.MigrateInvoicesModuleAsync();
            await context.Services.MigratePreferencesModuleAsync();
            var accounts = context.Services.GetRequiredService<IAccountCatalog>();
            var bank = await accounts.CreateBankAsync("Banco Fictício");
            var account = await accounts.SaveAccountAsync(new(null, bank.ExternalId!.Value, "Conta Fictícia", AccountKind.Checking, "green"));
            var card = await accounts.SaveCreditCardAsync(new(null, account.ExternalId!.Value, "Cartão Fictício", CardBrand.Visa, new Money(1000), 3, 10, true, "green"));
            var month = new ReferenceMonth(2027, 1);
            var completed = false;
            var recorded = 0;
            var dialog = context.Render<InvoicePaymentDialog>(parameters => parameters
                .Add(x => x.Invoice, new InvoiceDetails(card.ExternalId!.Value, "Cartão Fictício", month, new Money(100), []))
                .Add(x => x.PaymentRecorded, () => recorded++)
                .Add(x => x.Confirmed, () => completed = true));
            dialog.WaitForAssertion(() => Assert.False(dialog.Find("#invoice-payment-form button").HasAttribute("disabled")));
            dialog.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromText("%PDF-1.7\n1 0 obj<</Type /Catalog>>endobj\ntrailer<</Root 1 0 R>>\n%%EOF\n", "fictitious.pdf"));
            await dialog.Find("#invoice-payment-form").SubmitAsync(EventArgs.Empty);
            await dialog.FindAll("button").Single(x => x.TextContent == "Confirmar pagamento").ClickAsync(new());
            Assert.False(completed);
            Assert.Equal(1, recorded);
            Assert.Contains("Pagamento confirmado. O anexo falhou", dialog.Markup, StringComparison.Ordinal);
            Assert.DoesNotContain(dialog.FindAll("button"), x => x.TextContent == "Confirmar pagamento");
            var payments = context.Services.GetRequiredService<IInvoicePaymentCatalog>();
            Assert.Single(await payments.GetHistoryAsync());
            Assert.Single(await context.Services.GetRequiredService<IAccountMovementCatalog>().GetAsync());
            Directory.CreateDirectory(root);
            await dialog.FindAll("button").Single(x => x.TextContent == "Reenviar somente anexos").ClickAsync(new());
            Assert.True(completed);
            Assert.Equal(1, recorded);
            Assert.Single(await payments.GetHistoryAsync());
            Assert.Single(await context.Services.GetRequiredService<IAccountMovementCatalog>().GetAsync());
            var row = Assert.Single(await context.Services.GetRequiredService<IAttachmentCatalog>().ListAsync());
            Assert.Equal(Assert.Single(await payments.GetHistoryAsync()).ExternalId, row.Owner.ExternalId);
        }
        finally { if (Directory.Exists(root)) { Directory.Delete(root, true); } }
    }
}
