using System.Net;
using Ags.Finance.Modules.Accounts;
using Ags.Finance.Modules.Accounts.Contracts;
using Ags.Finance.Tests.Support;
using Ags.Finance.Web.Components;
using Bunit;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;
using SkiaSharp;

namespace Ags.Finance.Web.Tests;

public sealed class BankLogoPageTests
{
    private static byte[] LogoContent()
    {
        using var bitmap = new SKBitmap(400, 200);
        bitmap.Erase(SKColors.Green);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    [Fact]
    public async Task LogoEndpointRequiresLoginAndServesStoredPngWithVersion()
    {
        using var factory = new AppFactory();
        using var anonymous = factory.CreateBrowser();
        using var denied = await anonymous.GetAsync($"/bank-logos/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.Redirect, denied.StatusCode);
        using var browser = await factory.CreateSignedInBrowserAsync();
        using var scope = factory.Services.CreateScope();
        var bank = await scope.ServiceProvider.GetRequiredService<IAccountCatalog>().CreateBankAsync("Banco Exemplo");
        var logos = scope.ServiceProvider.GetRequiredService<IBankLogoCatalog>();
        await logos.SaveAsync(bank.ExternalId!.Value, LogoContent(), new());
        var stored = await logos.GetAsync(bank.ExternalId.Value);
        using var response = await browser.GetAsync($"/bank-logos/{bank.ExternalId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/png", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(stored?.Png, await response.Content.ReadAsByteArrayAsync());
        Assert.NotNull(response.Headers.ETag);
        await logos.RemoveAsync(bank.ExternalId.Value);
        using var missing = await browser.GetAsync($"/bank-logos/{bank.ExternalId}");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task CropPreviewCanBeCancelledSavedAndRemovedWithConfirmation()
    {
        await using var database = await PostgresTestDatabase.CreateAsync();
        await using var context = new BunitContext();
        context.Services.AddAccountsModule(database.ConnectionString);
        await context.Services.MigrateAccountsModuleAsync();
        var accounts = context.Services.GetRequiredService<IAccountCatalog>();
        var created = await accounts.CreateBankAsync("Banco Exemplo");
        var bank = Assert.Single(await accounts.GetBanksAsync());
        var logos = context.Services.GetRequiredService<IBankLogoCatalog>();
        var cancelled = false;
        var editor = context.Render<BankLogoEditor>(parameters => parameters.Add(x => x.Bank, bank).Add(x => x.Cancelled, () => cancelled = true));
        editor.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromBinary(LogoContent(), "logo.png", contentType: "image/png"));
        editor.WaitForElement(".bank-logo-preview");
        await editor.FindAll("button").Single(x => x.TextContent == "Cancelar").ClickAsync(new());
        Assert.True(cancelled);
        Assert.Null(await logos.GetAsync(created.ExternalId!.Value));
        await editor.InvokeAsync(() => editor.Find("#logo-zoom").Change("2"));
        await editor.FindAll("button").Single(x => x.TextContent == "Salvar recorte").ClickAsync(new());
        Assert.NotNull(await logos.GetAsync(created.ExternalId.Value));
        bank = Assert.Single(await accounts.GetBanksAsync());
        editor = context.Render<BankLogoEditor>(parameters => parameters.Add(x => x.Bank, bank));
        await editor.FindAll("button").Single(x => x.TextContent == "Remover logo").ClickAsync(new());
        await editor.FindAll("[aria-label='Remover logo'] button")[1].ClickAsync(new());
        Assert.NotNull(await logos.GetAsync(created.ExternalId.Value));
        await editor.FindAll("button").Single(x => x.TextContent == "Remover logo").ClickAsync(new());
        await editor.Find("[aria-label='Remover logo'] button").ClickAsync(new());
        Assert.Null(await logos.GetAsync(created.ExternalId.Value));
        var withoutLogo = Assert.Single(await accounts.GetBanksAsync());
        var fallback = context.Render<BankLogo>(parameters => parameters.Add(x => x.Bank, withoutLogo));
        Assert.Empty(fallback.FindAll("img"));
        Assert.Equal("BE", fallback.Find("span").TextContent);
    }
}
