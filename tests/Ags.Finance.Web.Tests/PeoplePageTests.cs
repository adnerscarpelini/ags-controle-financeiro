using Ags.Finance.Modules.Reimbursements.Contracts;
using Ags.Finance.Web.Components.Pages;
using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace Ags.Finance.Web.Tests;

public sealed class PeoplePageTests
{
    [Fact]
    public async Task CreatesEditsAndDeletesAPersonWithDefaultPercent()
    {
        await using var ui = await RegistryUi.CreateAsync();
        var catalog = ui.Context.Services.GetRequiredService<IPersonCatalog>();
        var page = ui.Context.Render<People>();
        page.WaitForAssertion(() => Assert.Contains("Nenhuma pessoa cadastrada", page.Find(".empty-state").TextContent, StringComparison.Ordinal));

        await page.Find("[data-registry-new]").ClickAsync(new MouseEventArgs());
        await ui.Dialogs.WaitForElement("#person-name").InputAsync("Pessoa Exemplo");
        await ui.Dialogs.Find("#person-percent").ChangeAsync("150");
        await ui.Dialogs.Find("#person-form").SubmitAsync(EventArgs.Empty);
        ui.Dialogs.WaitForAssertion(() => Assert.Contains("0 a 100", ui.Dialogs.Find("[data-entry-error]").TextContent, StringComparison.Ordinal));
        Assert.Equal("Pessoa Exemplo", ui.Dialogs.Find("#person-name").GetAttribute("value"));
        Assert.Empty(await catalog.GetAsync());

        await ui.Dialogs.Find("#person-percent").ChangeAsync("33.33");
        await ui.Dialogs.Find("#person-form").SubmitAsync(EventArgs.Empty);
        page.WaitForAssertion(() => Assert.Contains("33,33%", Assert.Single(RegistryUi.Rows(page)).TextContent, StringComparison.Ordinal));

        await RegistryUi.Rows(page)[0].QuerySelector("[data-action=edit]")!.ClickAsync(new MouseEventArgs());
        await ui.Dialogs.WaitForElement("#person-percent").ChangeAsync("50");
        await ui.Dialogs.Find("#person-form").SubmitAsync(EventArgs.Empty);
        page.WaitForAssertion(() => Assert.Contains("50%", RegistryUi.Rows(page)[0].TextContent, StringComparison.Ordinal));
        Assert.Equal(50m, Assert.Single(await catalog.GetAsync()).DefaultPercent);

        RegistryUi.Click(RegistryUi.Rows(page)[0].QuerySelector("[data-action=delete]")!);
        ui.WaitForConfirmation();
        await ui.Dialogs.Find("[data-confirm-cancel]").ClickAsync(new MouseEventArgs());
        Assert.Single(await catalog.GetAsync());

        RegistryUi.Click(RegistryUi.Rows(page)[0].QuerySelector("[data-action=delete]")!);
        ui.WaitForConfirmation();
        await ui.Dialogs.Find("[data-confirm-ok]").ClickAsync(new MouseEventArgs());
        page.WaitForAssertion(() => Assert.Empty(RegistryUi.Rows(page)));
        Assert.Empty(await catalog.GetAsync());
    }
}
