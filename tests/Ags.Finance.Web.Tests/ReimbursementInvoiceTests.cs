using Ags.Finance.Modules.Accounts;
using Ags.Finance.Modules.Accounts.Contracts;
using Ags.Finance.Modules.Invoices;
using Ags.Finance.Modules.Invoices.Contracts;
using Ags.Finance.Modules.Preferences;
using Ags.Finance.Modules.Reimbursements;
using Ags.Finance.Modules.Reimbursements.Contracts;
using Ags.Finance.Modules.Transactions;
using Ags.Finance.Modules.Transactions.Contracts;
using Ags.Finance.SharedKernel;
using Ags.Finance.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace Ags.Finance.Web.Tests;

/// <summary>Reimbursements e Invoices não se conhecem: só o host reúne os dois módulos.</summary>
public sealed class ReimbursementInvoiceTests
{
    [Fact]
    public async Task ReceivingAPurchaseOfAPaidInvoiceKeepsTheInvoiceAsItWas()
    {
        await using var database = await PostgresTestDatabase.CreateAsync();
        await using var provider = new ServiceCollection().AddSingleton<TimeProvider>(new FixedClock()).AddAccountsModule(database.ConnectionString).AddTransactionsModule(database.ConnectionString)
            .AddReimbursementsModule(database.ConnectionString).AddInvoicesModule(database.ConnectionString).AddPreferencesModule(database.ConnectionString).BuildServiceProvider();
        await provider.MigrateAccountsModuleAsync();
        await provider.MigrateTransactionsModuleAsync();
        await provider.MigrateReimbursementsModuleAsync();
        await provider.MigrateInvoicesModuleAsync();
        await provider.MigratePreferencesModuleAsync();
        var accounts = provider.GetRequiredService<IAccountCatalog>();
        var bank = await accounts.CreateBankAsync("Banco Exemplo");
        var account = (await accounts.SaveAccountAsync(new(null, bank.ExternalId!.Value, "Conta Exemplo", AccountKind.Checking, "green", new Money(1000), new DateOnly(2026, 1, 1)))).ExternalId!.Value;
        var card = (await accounts.SaveCreditCardAsync(new(null, account, "Cartão Exemplo", CardBrand.Visa, new Money(10000), 3, 10, true, "green"))).ExternalId!.Value;
        var classifications = provider.GetRequiredService<IClassificationCatalog>();
        var category = await classifications.SaveCategoryAsync(null, "Casa", CategoryKind.Expense);
        var subcategory = (await classifications.SaveSubcategoryAsync(null, category.ExternalId!.Value, "Internet")).ExternalId!.Value;
        var merchant = (await provider.GetRequiredService<IMerchantCatalog>().SaveAsync(null, "Provedor Exemplo", subcategory)).ExternalId!.Value;
        var person = (await provider.GetRequiredService<IPersonCatalog>().SaveAsync(null, "Pessoa Exemplo", 50m)).ExternalId!.Value;
        var month = new AccountingMonth(2026, 10);
        var entries = provider.GetRequiredService<IEntryCatalog>();
        var created = await entries.CreateAsync(new(new DateOnly(2026, 9, 14), merchant, subcategory, account, new Money(100), Month: month, CardExternalId: card, Reimbursement: new(person, 50m)));
        Assert.True(created.Succeeded, created.Error);
        Assert.True((await provider.GetRequiredService<IInvoiceLifecycle>().CloseAsync(card, month)).Succeeded);
        var payment = await provider.GetRequiredService<IInvoicePaymentCatalog>().ConfirmPaymentAsync(new(Guid.CreateVersion7(), card, month, account, new DateOnly(2026, 9, 20), new Money(100), true));
        Assert.True(payment.Succeeded, payment.Error);
        var catalog = provider.GetRequiredService<IInvoiceCatalog>();
        var before = Assert.Single(await catalog.GetMonthAsync(month));
        Assert.Equal(InvoiceStatus.Paid, before.Status);

        var result = await provider.GetRequiredService<IReimbursementReceipts>().ReceiveAsync(new(new DateOnly(2026, 9, 20), account, [new(created.ExternalId!.Value, new Money(50), new Money(50))]));
        Assert.True(result.Succeeded, result.Error);

        var after = Assert.Single(await catalog.GetMonthAsync(month));
        Assert.Equal((before.Status, before.Total, before.RemainingBalance), (after.Status, after.Total, after.RemainingBalance));
        Assert.Equal(new Money(50), (await entries.QueryAsync(new(Kind: CategoryKind.Income))).Income);
    }

    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 9, 20, 12, 0, 0, TimeSpan.Zero);
    }
}
