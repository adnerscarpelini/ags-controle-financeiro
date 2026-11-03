using Ags.Finance.Modules.Transactions.Contracts;
using Ags.Finance.Web.Components.Pages;
using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace Ags.Finance.Web.Tests;

public sealed class MerchantPageTests
{
    [Fact]
    public async Task SuggestsEquivalentDescriptionBeforeSaveAndConfirmsMerge()
    {
        await using var ui = await RegistryUi.CreateAsync();
        var catalog = ui.Context.Services.GetRequiredService<IMerchantCatalog>();
        var original = await catalog.SaveAsync(null, "Padaria São Exemplo", null);
        var target = await catalog.SaveAsync(null, "Padaria Nova", null);
        var page = ui.Context.Render<Merchants>();
        page.WaitForAssertion(() => Assert.Equal(2, RegistryUi.Rows(page).Count));
        Assert.Empty(page.FindAll("form"));

        await page.Find("[data-registry-new]").ClickAsync(new MouseEventArgs());
        await ui.Dialogs.WaitForElement("#merchant-name").InputAsync("Padaria Sao Exemplo");
        ui.Dialogs.WaitForAssertion(() => Assert.Contains("Padaria São Exemplo", ui.Dialogs.Find("[aria-label=Sugestões]").TextContent, StringComparison.Ordinal));
        await ui.Dialogs.Find("#merchant-form").SubmitAsync(EventArgs.Empty);
        ui.Dialogs.WaitForAssertion(() => Assert.Contains("já existe", ui.Dialogs.Find("[data-entry-error]").TextContent, StringComparison.Ordinal));
        Assert.Equal("Padaria Sao Exemplo", ui.Dialogs.Find("#merchant-name").GetAttribute("value"));
        Assert.Equal(2, (await catalog.FindAsync()).Count);
        await ui.Dialogs.Find("[data-dialog-cancel]").ClickAsync(new MouseEventArgs());

        await RegistryUi.Row(page, "Padaria São Exemplo").QuerySelector("[data-action=merge]")!.ClickAsync(new MouseEventArgs());
        ui.Dialogs.WaitForAssertion(() => Assert.Contains("Padaria Nova", ui.Dialogs.Find("#merge-target").TextContent, StringComparison.Ordinal));
        Assert.Equal("Padaria São Exemplo", ui.Dialogs.Find("[data-merge-source]").TextContent);
        Assert.DoesNotContain("Padaria São Exemplo", ui.Dialogs.Find("#merge-target").TextContent, StringComparison.Ordinal);
        Assert.True(ui.Dialogs.Find("[data-confirm-ok]").HasAttribute("disabled"));
        await ui.Dialogs.Find("#merge-target").ChangeAsync(target.ExternalId!.Value.ToString());
        Assert.Equal(2, (await catalog.FindAsync()).Count);
        await ui.Dialogs.Find("[data-confirm-ok]").ClickAsync(new MouseEventArgs());

        Assert.Equal(target.ExternalId, Assert.Single(await catalog.FindAsync()).ExternalId);
        Assert.DoesNotContain(await catalog.FindAsync(activeOnly: false), x => x.ExternalId == original.ExternalId);
        page.WaitForAssertion(() => Assert.Single(RegistryUi.Rows(page)));
    }

    [Fact]
    public async Task TheSearchFiltersAndDeactivationAsksForConfirmation()
    {
        await using var ui = await RegistryUi.CreateAsync();
        var catalog = ui.Context.Services.GetRequiredService<IMerchantCatalog>();
        await catalog.SaveAsync(null, "Padaria Exemplo", null);
        await catalog.SaveAsync(null, "Mercado Exemplo", null);
        var page = ui.Context.Render<Merchants>();
        page.WaitForAssertion(() => Assert.Equal(2, RegistryUi.Rows(page).Count));

        await page.Find("#registry-search").InputAsync("Padaria");
        page.WaitForAssertion(() => Assert.Single(RegistryUi.Rows(page)));

        RegistryUi.Click(RegistryUi.Rows(page)[0].QuerySelector("[data-action=delete]")!);
        ui.WaitForConfirmation();
        Assert.Contains("preserva os lançamentos", ui.Dialogs.Find(".confirm-message").TextContent, StringComparison.Ordinal);
        await ui.Dialogs.Find("[data-confirm-ok]").ClickAsync(new MouseEventArgs());

        page.WaitForAssertion(() => Assert.Contains("Desativada", RegistryUi.Rows(page)[0].TextContent, StringComparison.Ordinal));
        Assert.Single(await catalog.FindAsync());
    }

    [Fact]
    public async Task AnEmptyRegistryGuidesToCreateTheFirst()
    {
        await using var ui = await RegistryUi.CreateAsync();

        var page = ui.Context.Render<Merchants>();

        page.WaitForAssertion(() => Assert.Contains("Cadastre a primeira", page.Find(".empty-state").TextContent, StringComparison.Ordinal));
    }
}
