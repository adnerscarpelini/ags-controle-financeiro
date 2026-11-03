using System.Text;
using Ags.Finance.Modules.Accounts;
using Ags.Finance.Modules.Accounts.Contracts;
using Ags.Finance.Modules.Attachments;
using Ags.Finance.Modules.Attachments.Contracts;
using Ags.Finance.Modules.Reimbursements.Contracts;
using Ags.Finance.Modules.Transactions;
using Ags.Finance.Modules.Transactions.Contracts;
using Ags.Finance.SharedKernel;
using Ags.Finance.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace Ags.Finance.Modules.Reimbursements.Tests;

public sealed class ReimbursementReceiptAttachmentTests
{
    [Fact]
    public async Task UndoingSendsTheAttachmentsOfTheIncomeToTheTrash()
    {
        var directory = Path.Combine(Path.GetTempPath(), "ags-reimbursement-attachments-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            await using var database = await PostgresTestDatabase.CreateAsync();
            await using var provider = new ServiceCollection().AddAttachmentsModule(database.ConnectionString, new(directory)).AddAccountsModule(database.ConnectionString)
                .AddTransactionsModule(database.ConnectionString).AddReimbursementsModule(database.ConnectionString).BuildServiceProvider();
            await provider.MigrateAttachmentsModuleAsync();
            await provider.MigrateAccountsModuleAsync();
            await provider.MigrateTransactionsModuleAsync();
            await provider.MigrateReimbursementsModuleAsync();
            var accounts = provider.GetRequiredService<IAccountCatalog>();
            var bank = await accounts.CreateBankAsync("Banco Exemplo");
            var account = (await accounts.SaveAccountAsync(new(null, bank.ExternalId!.Value, "Conta Exemplo", AccountKind.Checking, "green"))).ExternalId!.Value;
            var classifications = provider.GetRequiredService<IClassificationCatalog>();
            var category = await classifications.SaveCategoryAsync(null, "Casa", CategoryKind.Expense);
            var subcategory = (await classifications.SaveSubcategoryAsync(null, category.ExternalId!.Value, "Internet")).ExternalId!.Value;
            var merchant = (await provider.GetRequiredService<IMerchantCatalog>().SaveAsync(null, "Provedor Exemplo", subcategory)).ExternalId!.Value;
            var person = (await provider.GetRequiredService<IPersonCatalog>().SaveAsync(null, "Pessoa Exemplo", 100m)).ExternalId!.Value;
            var entries = provider.GetRequiredService<IEntryCatalog>();
            var expense = await entries.CreateAsync(new(new DateOnly(2026, 10, 5), merchant, subcategory, account, new Money(100), Reimbursement: new(person, 100m)));
            var receipts = provider.GetRequiredService<IReimbursementReceipts>();
            var received = await receipts.ReceiveAsync(new(new DateOnly(2026, 10, 6), account, [new(expense.ExternalId!.Value, new Money(100), new Money(100))]));
            var income = Assert.Single((await entries.QueryAsync(new(Kind: CategoryKind.Income))).Entries);
            var documents = provider.GetRequiredService<IEntryAttachmentCatalog>();
            using var content = new MemoryStream(Encoding.ASCII.GetBytes("%PDF-1.7\n1 0 obj<</Type /Catalog>>endobj\ntrailer<</Root 1 0 R>>\n%%EOF\n"));
            Assert.True((await documents.UploadAsync(income.ExternalId, AttachmentKind.Receipt, "fictitious.pdf", content)).Succeeded);

            Assert.True((await receipts.UndoAsync(Assert.Single(received.ReceiptExternalIds))).Succeeded);

            Assert.NotNull(Assert.Single(await documents.ListAsync()).DeletedAt);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }
}
