using Ags.Finance.Modules.Accounts;
using Ags.Finance.Modules.Accounts.Contracts;
using Ags.Finance.Tests.Support;
using Ags.Finance.Web.Components;
using Bunit;
using Microsoft.Extensions.DependencyInjection;

namespace Ags.Finance.Web.Tests;

/// <summary>Seletor de cor por amostra (#112): a lista da tela, os tokens e a validação do módulo não podem divergir.</summary>
public sealed class CatalogColorTests
{
    [Fact]
    public async Task EveryOptionIsAcceptedByTheAccountsModuleAndHasItsTokens()
    {
        await using var database = await PostgresTestDatabase.CreateAsync();
        var services = new ServiceCollection();
        services.AddSingleton(TimeProvider.System).AddAccountsModule(database.ConnectionString);
        await using var provider = services.BuildServiceProvider();
        await provider.MigrateAccountsModuleAsync();
        var accounts = provider.GetRequiredService<IAccountCatalog>();
        var bank = await accounts.CreateBankAsync("Banco Exemplo");
        var tokens = ReadTokens();

        foreach (var option in CatalogColor.Options)
        {
            var result = await accounts.SaveAccountAsync(new(null, bank.ExternalId!.Value, $"Conta {option.Key}", AccountKind.Savings, option.Key));
            Assert.True(result.Error is null, $"{option.Key}: {result.Error}");
            Assert.Contains($"--color-cat-{option.Key}:", tokens, StringComparison.Ordinal);
            Assert.Contains($"--swatch-{option.Key}:", tokens, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task AColorOutsideThePaletteIsRejected()
    {
        await using var database = await PostgresTestDatabase.CreateAsync();
        var services = new ServiceCollection();
        services.AddSingleton(TimeProvider.System).AddAccountsModule(database.ConnectionString);
        await using var provider = services.BuildServiceProvider();
        await provider.MigrateAccountsModuleAsync();
        var accounts = provider.GetRequiredService<IAccountCatalog>();
        var bank = await accounts.CreateBankAsync("Banco Exemplo");

        var result = await accounts.SaveAccountAsync(new(null, bank.ExternalId!.Value, "Conta Exemplo", AccountKind.Savings, "fuchsia"));

        Assert.NotNull(result.Error);
    }

    [Fact]
    public async Task ChoosingFromAllTheColorsKeepsTheChosenOneVisibleInTheShortList()
    {
        await using var context = new BunitContext().UseAppUi();
        var chosen = string.Empty;
        var picker = context.Render<CatalogColor>(parameters => parameters.Add(x => x.Id, "account-color").Add(x => x.Value, string.Empty).Add(x => x.ValueChanged, value => chosen = value));

        Assert.Equal(5, picker.FindAll("[data-color]").Count);
        Assert.Empty(picker.FindAll("[data-color='wine']"));

        await picker.Find("[data-more-colors]").ClickAsync(new());
        Assert.Equal(CatalogColor.Options.Count, picker.FindAll("[data-color]").Count);
        await picker.Find("[data-color='wine']").ClickAsync(new());
        Assert.Equal("wine", chosen);

        picker.Render(parameters => parameters.Add(x => x.Value, "wine"));
        await picker.Find("[data-more-colors]").ClickAsync(new());
        Assert.Equal(6, picker.FindAll("[data-color]").Count);
        Assert.Equal("true", picker.Find("[data-color='wine']").GetAttribute("aria-checked"));
    }

    private static string ReadTokens()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Ags.Finance.slnx"))) { directory = directory.Parent; }
        Assert.NotNull(directory);
        return File.ReadAllText(Path.Combine(directory.FullName, "src", "Ags.Finance.Web", "wwwroot", "css", "tokens.css"));
    }
}
