using Ags.Finance.Modules.Transactions;
using Ags.Finance.Modules.Transactions.Contracts;
using Ags.Finance.Tests.Support;
using Ags.Finance.Web.Components.Pages;
using Bunit;
using Microsoft.Extensions.DependencyInjection;

namespace Ags.Finance.Web.Tests;

public sealed class ClassificationPageTests
{
    [Fact]
    public async Task CreatesSubcategoryAndRequiresConfirmationBeforeDeactivation()
    {
        await using var database = await PostgresTestDatabase.CreateAsync();
        await using var context = new BunitContext();
        context.Services.AddTransactionsModule(database.ConnectionString);
        await context.Services.MigrateTransactionsModuleAsync();
        var catalog = context.Services.GetRequiredService<IClassificationCatalog>();
        var category = await catalog.SaveCategoryAsync(null, "Mercado", CategoryKind.Expense);
        var page = context.Render<Classifications>();
        page.WaitForAssertion(() => Assert.Contains("Mercado", page.Find("#subcategory-category").TextContent, StringComparison.Ordinal));
        page.Find("#subcategory-category").Change(category.ExternalId!.Value.ToString());
        page.Find("#subcategory-name").Change("Supermercado");
        await page.FindAll("form")[1].SubmitAsync(EventArgs.Empty);
        Assert.Contains("Mercado &gt; Supermercado", page.Markup, StringComparison.Ordinal);
        page.FindAll(".catalog-list")[1].QuerySelectorAll("button")[1].Click();
        Assert.Single(await catalog.GetSubcategoriesAsync());
        page.FindAll("[role=alertdialog] button")[1].Click();
        Assert.Single(await catalog.GetSubcategoriesAsync());
        page.FindAll(".catalog-list")[1].QuerySelectorAll("button")[1].Click();
        await page.Find("[role=alertdialog] button").ClickAsync(new());
        Assert.Empty(await catalog.GetSubcategoriesAsync());
        Assert.Single(await catalog.GetSubcategoriesAsync(false));
    }
}
