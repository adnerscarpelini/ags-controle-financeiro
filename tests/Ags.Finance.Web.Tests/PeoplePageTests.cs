using Ags.Finance.Modules.Accounts;
using Ags.Finance.Modules.Reimbursements;
using Ags.Finance.Modules.Reimbursements.Contracts;
using Ags.Finance.Modules.Transactions;
using Ags.Finance.Tests.Support;
using Ags.Finance.Web.Components.Pages;
using Bunit;
using Microsoft.Extensions.DependencyInjection;

namespace Ags.Finance.Web.Tests;

public sealed class PeoplePageTests
{
    [Fact]
    public async Task CreatesEditsAndDeletesAPersonWithDefaultPercent()
    {
        await using var database = await PostgresTestDatabase.CreateAsync();
        await using var context = new BunitContext();
        context.Services.AddAccountsModule(database.ConnectionString).AddTransactionsModule(database.ConnectionString).AddReimbursementsModule(database.ConnectionString);
        await context.Services.MigrateAccountsModuleAsync();
        await context.Services.MigrateTransactionsModuleAsync();
        await context.Services.MigrateReimbursementsModuleAsync();
        var catalog = context.Services.GetRequiredService<IPersonCatalog>();
        var page = context.Render<People>();

        page.Find("#person-name").Change("Pessoa Exemplo");
        page.Find("#person-percent").Change("150");
        await page.Find("form").SubmitAsync(EventArgs.Empty);
        Assert.Contains("0 a 100", page.Find("[role=status]").TextContent, StringComparison.Ordinal);
        Assert.Empty(await catalog.GetAsync());

        page.Find("#person-percent").Change("33.33");
        await page.Find("form").SubmitAsync(EventArgs.Empty);
        page.WaitForAssertion(() => Assert.Contains("33,33%", page.Find(".catalog-list li").TextContent, StringComparison.Ordinal));

        page.FindAll(".catalog-list li button")[0].Click();
        page.Find("#person-percent").Change("50");
        await page.Find("form").SubmitAsync(EventArgs.Empty);
        page.WaitForAssertion(() => Assert.Contains("50%", page.Find(".catalog-list li").TextContent, StringComparison.Ordinal));
        Assert.Equal(50m, Assert.Single(await catalog.GetAsync()).DefaultPercent);

        await page.FindAll(".catalog-list li button")[1].ClickAsync(new());
        page.WaitForAssertion(() => Assert.Empty(page.FindAll(".catalog-list li")));
        Assert.Empty(await catalog.GetAsync());
    }
}
