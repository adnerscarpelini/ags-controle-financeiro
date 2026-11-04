using Ags.Finance.Modules.Accounts;
using Ags.Finance.Modules.Accounts.Contracts;
using Ags.Finance.Modules.Invoices;
using Ags.Finance.Modules.Preferences;
using Ags.Finance.Modules.Preferences.Contracts;
using Ags.Finance.Modules.Reports;
using Ags.Finance.Modules.Transactions;
using Ags.Finance.Modules.Transactions.Contracts;
using Ags.Finance.SharedKernel;
using Ags.Finance.Tests.Support;
using Ags.Finance.Web.Components.Dashboard;
using Bunit;
using Microsoft.Extensions.DependencyInjection;

namespace Ags.Finance.Web.Tests;

public sealed class InvoiceForecastPageTests
{
    /// <summary>Hoje é 20/09/2026; a fatura de outubro tem R$ 100,00 de compra e uma recorrente de R$ 50,00 em 27/09, ainda futura.</summary>
    private static async Task SetUpAsync(BunitContext context, PostgresTestDatabase database, RecurringInvoiceMode mode)
    {
        context.Services.AddSingleton<TimeProvider>(new FixedClock(new DateTimeOffset(2026, 9, 20, 12, 0, 0, TimeSpan.Zero)))
            .AddAccountsModule(database.ConnectionString).AddTransactionsModule(database.ConnectionString)
            .AddInvoicesModule(database.ConnectionString).AddPreferencesModule(database.ConnectionString).AddReportsModule();
        await context.Services.MigrateAccountsModuleAsync();
        await context.Services.MigrateTransactionsModuleAsync();
        await context.Services.MigrateInvoicesModuleAsync();
        await context.Services.MigratePreferencesModuleAsync();
        Assert.True((await context.Services.GetRequiredService<IPreferences>().SaveAsync(AppPreferences.Default with { RecurringInvoices = mode })).Succeeded);

        var accounts = context.Services.GetRequiredService<IAccountCatalog>();
        var bank = await accounts.CreateBankAsync("Banco Exemplo");
        var account = await accounts.SaveAccountAsync(new(null, bank.ExternalId!.Value, "Conta Exemplo", AccountKind.Checking, "green"));
        var card = await accounts.SaveCreditCardAsync(new(null, account.ExternalId!.Value, "Cartão Exemplo", CardBrand.Visa, new Money(10000), 3, 10, true, "green"));
        var classifications = context.Services.GetRequiredService<IClassificationCatalog>();
        var category = await classifications.SaveCategoryAsync(null, "Casa", CategoryKind.Expense);
        var subcategory = await classifications.SaveSubcategoryAsync(null, category.ExternalId!.Value, "Assinaturas");
        var merchant = await context.Services.GetRequiredService<IMerchantCatalog>().SaveAsync(null, "Serviço Exemplo", subcategory.ExternalId!.Value);
        var entries = context.Services.GetRequiredService<IEntryCatalog>();
        CreateEntryRequest Purchase(decimal amount, DateOnly date) => new(date, merchant.ExternalId!.Value, subcategory.ExternalId!.Value, account.ExternalId!.Value, new Money(amount), CardExternalId: card.ExternalId);
        Assert.True((await entries.CreateAsync(Purchase(100, new DateOnly(2026, 9, 14)))).Succeeded);
        Assert.True((await entries.CreateRecurringAsync(new(Purchase(50, new DateOnly(2026, 9, 27))))).Succeeded);
    }

    [Theory]
    [InlineData(RecurringInvoiceMode.SumOnDate, "R$ 100,00", "R$ 50,00")]
    [InlineData(RecurringInvoiceMode.SumUpfront, "R$ 150,00", null)]
    public async Task DashboardInvoiceCardShowsTheForecastLeftOutOfTheTotal(RecurringInvoiceMode mode, string invoice, string? forecast)
    {
        await using var database = await PostgresTestDatabase.CreateAsync();
        await using var context = new BunitContext().UseAppUi();
        await SetUpAsync(context, database, mode);

        var section = context.Render<CardInvoicesSummary>(parameters => parameters.Add(x => x.Month, new ReferenceMonth(2026, 10)));

        section.WaitForAssertion(() => Assert.Equal(invoice, section.Find("[data-invoice-total]").TextContent));
        if (forecast is null)
        {
            Assert.Empty(section.FindAll("[data-invoice-forecast]"));
        }
        else
        {
            Assert.Equal(forecast, section.Find("[data-invoice-forecast]").TextContent);
        }
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }
}
