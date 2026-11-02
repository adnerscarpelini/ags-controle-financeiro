using Ags.Finance.Modules.Transactions;
using Ags.Finance.Modules.Transactions.Contracts;
using Ags.Finance.Tests.Support;
using Ags.Finance.Web.Components.Pages;
using Bunit;
using Microsoft.Extensions.DependencyInjection;

namespace Ags.Finance.Web.Tests;

public sealed class MerchantPageTests
{
    [Fact]
    public async Task SuggestsEquivalentDescriptionBeforeSaveAndConfirmsMerge()
    {
        await using var database = await PostgresTestDatabase.CreateAsync();
        await using var context = new BunitContext();
        context.Services.AddTransactionsModule(database.ConnectionString);
        await context.Services.MigrateTransactionsModuleAsync();
        var catalog = context.Services.GetRequiredService<IMerchantCatalog>();
        var original = await catalog.SaveAsync(null, "Padaria São Exemplo", null);
        var target = await catalog.SaveAsync(null, "Padaria Nova", null);
        var page = context.Render<Merchants>();
        page.WaitForAssertion(() => Assert.Equal(2, page.FindAll(".catalog-list li").Count));
        await page.Find("#merchant-name").InputAsync("Padaria Sao Exemplo");
        Assert.Contains("Padaria São Exemplo", page.Find("[aria-label=Sugestões]").TextContent, StringComparison.Ordinal);
        await page.Find("form").SubmitAsync(EventArgs.Empty);
        Assert.Contains("já existe", page.Find("[role=status]").TextContent, StringComparison.Ordinal);
        Assert.Equal(2, (await catalog.FindAsync()).Count);
        var sourceRow = page.FindAll(".catalog-list li").Single(x => x.TextContent.Contains("Padaria São Exemplo", StringComparison.Ordinal));
        sourceRow.QuerySelectorAll("button")[2].Click();
        page.Find("#merge-target").Change(target.ExternalId!.Value.ToString());
        page.FindAll("button").Single(x => x.TextContent == "Mesclar descrições").Click();
        Assert.Equal(2, (await catalog.FindAsync()).Count);
        await page.Find("[role=alertdialog] button").ClickAsync(new());
        Assert.Equal(target.ExternalId, Assert.Single(await catalog.FindAsync()).ExternalId);
        Assert.DoesNotContain(await catalog.FindAsync(activeOnly: false), x => x.ExternalId == original.ExternalId);
    }
}
