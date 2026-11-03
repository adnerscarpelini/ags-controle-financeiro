using Ags.Finance.Modules.Accounts.Contracts;
using Ags.Finance.SharedKernel;
using Ags.Finance.Web.Components.Pages;
using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace Ags.Finance.Web.Tests;

/// <summary>Tela de Bancos (#101).</summary>
public sealed class BanksPageTests
{
    [Fact]
    public async Task ABankIsCreatedInTheWindowAndAppearsWithItsInitialsAndLinkedAccounts()
    {
        await using var ui = await RegistryUi.CreateAsync();
        var catalog = ui.Context.Services.GetRequiredService<IAccountCatalog>();
        var page = ui.Context.Render<Banks>();
        page.WaitForAssertion(() => Assert.Contains("Cadastre o primeiro", page.Find(".empty-state").TextContent, StringComparison.Ordinal));

        await page.Find("[data-registry-new]").ClickAsync(new MouseEventArgs());
        await ui.Dialogs.WaitForElement("#registry-name").InputAsync("Banco Exemplo");
        await ui.Dialogs.Find("#name-form").SubmitAsync(EventArgs.Empty);

        page.WaitForAssertion(() => Assert.Contains("Banco Exemplo", Assert.Single(RegistryUi.Rows(page)).TextContent, StringComparison.Ordinal));
        var bank = Assert.Single(await catalog.GetBanksAsync());
        Assert.Equal("BE", page.Find(".bank-logo-initials").TextContent);
        Assert.Contains("0", RegistryUi.Rows(page)[0].TextContent, StringComparison.Ordinal);

        await catalog.SaveAccountAsync(new(null, bank.ExternalId, "Conta Exemplo", AccountKind.Checking, "green", Money.Zero, new DateOnly(2026, 1, 1)));
        var reloaded = ui.Context.Render<Banks>();
        reloaded.WaitForAssertion(() => Assert.Contains("1", RegistryUi.Rows(reloaded)[0].TextContent, StringComparison.Ordinal));
    }

    [Fact]
    public async Task AnInvalidNameStaysInTheWindowAndSaveAndCreateNewKeepsItOpen()
    {
        await using var ui = await RegistryUi.CreateAsync();
        await ui.Context.Services.GetRequiredService<IAccountCatalog>().CreateBankAsync("Banco Exemplo");
        var page = ui.Context.Render<Banks>();
        page.WaitForAssertion(() => Assert.Single(RegistryUi.Rows(page)));
        await page.Find("[data-registry-new]").ClickAsync(new MouseEventArgs());
        var invalid = new string('x', 101);
        await ui.Dialogs.WaitForElement("#registry-name").InputAsync(invalid);

        await ui.Dialogs.Find("#name-form").SubmitAsync(EventArgs.Empty);
        ui.Dialogs.WaitForAssertion(() => Assert.Contains("até 100 caracteres", ui.Dialogs.Find("[data-entry-error]").TextContent, StringComparison.Ordinal));
        Assert.Equal(invalid, ui.Dialogs.Find("#registry-name").GetAttribute("value"));

        await ui.Dialogs.Find("#registry-name").InputAsync("Outro Banco");
        await ui.Dialogs.Find("[data-save-new]").ClickAsync(new MouseEventArgs());
        ui.Dialogs.WaitForAssertion(() => Assert.Contains("Cadastro salvo", ui.Dialogs.Find("[data-entry-success]").TextContent, StringComparison.Ordinal));
        page.WaitForAssertion(() => Assert.Equal(2, RegistryUi.Rows(page).Count));
    }

    [Fact]
    public async Task TheLogoEditorOpensInAWindow()
    {
        await using var ui = await RegistryUi.CreateAsync();
        await ui.Context.Services.GetRequiredService<IAccountCatalog>().CreateBankAsync("Banco Exemplo");
        var page = ui.Context.Render<Banks>();
        page.WaitForAssertion(() => Assert.Single(RegistryUi.Rows(page)));

        await RegistryUi.Rows(page)[0].QuerySelector("[data-action=logo]")!.ClickAsync(new MouseEventArgs());

        ui.Dialogs.WaitForElement("#bank-logo-file");
    }
}
