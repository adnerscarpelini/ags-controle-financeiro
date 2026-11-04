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
    public async Task ABankIsRenamedInTheWindow()
    {
        await using var ui = await RegistryUi.CreateAsync();
        await ui.Context.Services.GetRequiredService<IAccountCatalog>().CreateBankAsync("Banco Exemplo");
        var page = ui.Context.Render<Banks>();
        page.WaitForAssertion(() => Assert.Single(RegistryUi.Rows(page)));

        await RegistryUi.Rows(page)[0].QuerySelector("[data-action=edit]")!.ClickAsync(new MouseEventArgs());
        var name = ui.Dialogs.WaitForElement("#registry-name");
        Assert.Equal("Banco Exemplo", name.GetAttribute("value"));
        Assert.Empty(ui.Dialogs.FindAll("[data-save-new]"));
        await name.InputAsync("Banco Renomeado");
        await ui.Dialogs.Find("#name-form").SubmitAsync(EventArgs.Empty);

        page.WaitForAssertion(() => Assert.Contains("Banco Renomeado", RegistryUi.Rows(page)[0].TextContent, StringComparison.Ordinal));
    }

    [Fact]
    public async Task AnUnusedBankOffersDeleteAndDisappearsAfterConfirming()
    {
        await using var ui = await RegistryUi.CreateAsync();
        var catalog = ui.Context.Services.GetRequiredService<IAccountCatalog>();
        await catalog.CreateBankAsync("Banco Exemplo");
        var page = ui.Context.Render<Banks>();
        page.WaitForAssertion(() => Assert.Single(RegistryUi.Rows(page)));
        Assert.Null(RegistryUi.Rows(page)[0].QuerySelector("[data-action=archive]"));

        RegistryUi.Click(RegistryUi.Rows(page)[0].QuerySelector("[data-action=delete]")!);
        await ui.Dialogs.WaitForElement("[data-confirm-ok]").ClickAsync(new MouseEventArgs());

        page.WaitForAssertion(() => Assert.Contains("Cadastre o primeiro", page.Find(".empty-state").TextContent, StringComparison.Ordinal));
        Assert.Empty(await catalog.GetBanksAsync());
    }

    [Fact]
    public async Task ABankWithAnAccountOffersArchiveAndCanBeRestored()
    {
        await using var ui = await RegistryUi.CreateAsync();
        var catalog = ui.Context.Services.GetRequiredService<IAccountCatalog>();
        var bank = (await catalog.CreateBankAsync("Banco Exemplo")).ExternalId!.Value;
        await catalog.CreateBankAsync("Banco Livre");
        await catalog.SaveAccountAsync(new(null, bank, "Conta Exemplo", AccountKind.Investment, "green"));
        var page = ui.Context.Render<Banks>();
        page.WaitForAssertion(() => Assert.Equal(2, RegistryUi.Rows(page).Count));
        var row = RegistryUi.Row(page, "Banco Exemplo");
        Assert.Null(row.QuerySelector("[data-action=delete]"));
        Assert.Empty(page.FindAll("[data-toggle-archived]"));

        RegistryUi.Click(row.QuerySelector("[data-action=archive]")!);
        await ui.Dialogs.WaitForElement("[data-confirm-ok]").ClickAsync(new MouseEventArgs());

        page.WaitForAssertion(() => Assert.DoesNotContain("Banco Exemplo", Assert.Single(RegistryUi.Rows(page)).TextContent, StringComparison.Ordinal));
        await page.Find("[data-toggle-archived]").ClickAsync(new MouseEventArgs());
        page.WaitForAssertion(() => Assert.Contains("Arquivado", RegistryUi.Row(page, "Banco Exemplo").TextContent, StringComparison.Ordinal));

        RegistryUi.Click(RegistryUi.Row(page, "Banco Exemplo").QuerySelector("[data-action=restore]")!);
        await ui.Dialogs.WaitForElement("[data-confirm-ok]").ClickAsync(new MouseEventArgs());

        page.WaitForAssertion(() => Assert.Empty(page.FindAll("[data-bank-archived]")));
        Assert.All(await catalog.GetBanksAsync(), item => Assert.True(item.IsActive));
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
