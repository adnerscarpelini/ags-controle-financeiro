using Ags.Finance.Modules.Transactions.Contracts;
using Ags.Finance.Web.Components.Pages;
using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace Ags.Finance.Web.Tests;

/// <summary>Categorias (#101): tabela primeiro, alternância por tipo, subcategorias expansíveis e janelas.</summary>
public sealed class ClassificationPageTests
{
    private static IClassificationCatalog Catalog(RegistryUi ui) => ui.Context.Services.GetRequiredService<IClassificationCatalog>();

    [Fact]
    public async Task TheFirstThingSeenIsTheTableWithoutAnOpenForm()
    {
        await using var ui = await RegistryUi.CreateAsync();
        await Catalog(ui).SaveCategoryAsync(null, "Mercado", CategoryKind.Expense);

        var page = ui.Context.Render<Classifications>();

        page.WaitForAssertion(() => Assert.Single(RegistryUi.Rows(page)));
        Assert.Empty(page.FindAll("form"));
        Assert.Empty(ui.Dialogs.FindAll("form"));
        Assert.Equal("Categorias", page.Find("h1").TextContent);
        Assert.Contains("Nova categoria", page.Find("[data-registry-new]").TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnEmptyRegistryGuidesToCreateTheFirst()
    {
        await using var ui = await RegistryUi.CreateAsync();

        var page = ui.Context.Render<Classifications>();

        page.WaitForAssertion(() => Assert.Contains("Cadastre a primeira", page.Find(".empty-state").TextContent, StringComparison.Ordinal));
        await page.Find(".empty-state button").ClickAsync(new MouseEventArgs());
        ui.Dialogs.WaitForElement("#category-form");
    }

    [Fact]
    public async Task TheTabsAlternateBetweenExpensesAndIncomes()
    {
        await using var ui = await RegistryUi.CreateAsync();
        await Catalog(ui).SaveCategoryAsync(null, "Mercado", CategoryKind.Expense);
        await Catalog(ui).SaveCategoryAsync(null, "Salário", CategoryKind.Income);
        var page = ui.Context.Render<Classifications>();
        page.WaitForAssertion(() => Assert.Contains("Mercado", Assert.Single(RegistryUi.Rows(page)).TextContent, StringComparison.Ordinal));
        Assert.NotNull(RegistryUi.Rows(page)[0].QuerySelector(".kind-expense"));

        await page.Find($"[data-kind-tab='{CategoryKind.Income}']").ClickAsync(new MouseEventArgs());

        Assert.Contains("Salário", Assert.Single(RegistryUi.Rows(page)).TextContent, StringComparison.Ordinal);
        Assert.NotNull(RegistryUi.Rows(page)[0].QuerySelector(".kind-income"));
        Assert.Empty(page.FindAll($"[data-kind-tab='{CategoryKind.Transfer}']"));
    }

    [Fact]
    public async Task TheSearchFiltersByName()
    {
        await using var ui = await RegistryUi.CreateAsync();
        await Catalog(ui).SaveCategoryAsync(null, "Mercado", CategoryKind.Expense);
        await Catalog(ui).SaveCategoryAsync(null, "Casa", CategoryKind.Expense);
        var page = ui.Context.Render<Classifications>();
        page.WaitForAssertion(() => Assert.Equal(2, RegistryUi.Rows(page).Count));

        await page.Find("#registry-search").InputAsync("merc");

        Assert.Contains("Mercado", Assert.Single(RegistryUi.Rows(page)).TextContent, StringComparison.Ordinal);
        await page.Find("#registry-search").InputAsync("nada parecido");
        Assert.Contains("Nenhum registro", page.Find(".empty-state").TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NewSavesInTheWindowAndTheLineAppearsAndAnInvalidNameKeepsTheData()
    {
        await using var ui = await RegistryUi.CreateAsync();
        await Catalog(ui).SaveCategoryAsync(null, "Mercado", CategoryKind.Expense);
        var page = ui.Context.Render<Classifications>();
        page.WaitForAssertion(() => Assert.Single(RegistryUi.Rows(page)));

        await page.Find("[data-registry-new]").ClickAsync(new MouseEventArgs());
        var invalid = new string('x', 101);
        await ui.Dialogs.WaitForElement("#category-name").InputAsync(invalid);
        await ui.Dialogs.Find("#category-form").SubmitAsync(EventArgs.Empty);
        ui.Dialogs.WaitForAssertion(() => Assert.NotEmpty(ui.Dialogs.Find("[data-entry-error]").TextContent));
        Assert.Equal(invalid, ui.Dialogs.Find("#category-name").GetAttribute("value"));

        await ui.Dialogs.Find("#category-name").InputAsync("Lazer");
        await ui.Dialogs.Find("#category-form").SubmitAsync(EventArgs.Empty);

        ui.Dialogs.WaitForAssertion(() => Assert.Empty(ui.Dialogs.FindAll("#category-form")));
        page.WaitForAssertion(() => Assert.Equal(2, RegistryUi.Rows(page).Count));
    }

    [Fact]
    public async Task SaveAndCreateNewKeepsTheWindowOpen()
    {
        await using var ui = await RegistryUi.CreateAsync();
        var page = ui.Context.Render<Classifications>();
        page.WaitForElement(".empty-state");
        await page.Find("[data-registry-new]").ClickAsync(new MouseEventArgs());
        await ui.Dialogs.WaitForElement("#category-name").InputAsync("Lazer");

        await ui.Dialogs.Find("[data-save-new]").ClickAsync(new MouseEventArgs());

        ui.Dialogs.WaitForAssertion(() => Assert.Contains("Categoria salva", ui.Dialogs.Find("[data-entry-success]").TextContent, StringComparison.Ordinal));
        Assert.Equal(string.Empty, ui.Dialogs.Find("#category-name").GetAttribute("value") ?? string.Empty);
        page.WaitForAssertion(() => Assert.Single(RegistryUi.Rows(page)));
    }

    [Fact]
    public async Task NewSubcategoryOpensWithTheCategorySelectedAndExpandingShowsIt()
    {
        await using var ui = await RegistryUi.CreateAsync();
        var category = await Catalog(ui).SaveCategoryAsync(null, "Mercado", CategoryKind.Expense);
        var page = ui.Context.Render<Classifications>();
        page.WaitForAssertion(() => Assert.Single(RegistryUi.Rows(page)));
        Assert.Contains("0 subcategorias", RegistryUi.Rows(page)[0].TextContent, StringComparison.Ordinal);

        await RegistryUi.Rows(page)[0].QuerySelector("[data-action='new-subcategory']")!.ClickAsync(new MouseEventArgs());
        ui.Dialogs.WaitForAssertion(() => Assert.Equal(category.ExternalId.ToString(), ui.Dialogs.Find("#subcategory-category").GetAttribute("value")));
        await ui.Dialogs.Find("#subcategory-name").InputAsync("Supermercado");
        await ui.Dialogs.Find("#subcategory-form").SubmitAsync(EventArgs.Empty);

        page.WaitForAssertion(() => Assert.NotEmpty(page.FindAll("[data-subcategory='Supermercado']")));
        Assert.Contains("1 subcategoria", RegistryUi.Row(page, "Mercado").TextContent, StringComparison.Ordinal);

        await RegistryUi.Row(page, "Mercado").QuerySelector("[data-expand]")!.ClickAsync(new MouseEventArgs());
        Assert.Empty(page.FindAll("[data-subcategory='Supermercado']"));
        await RegistryUi.Row(page, "Mercado").QuerySelector("[data-expand]")!.ClickAsync(new MouseEventArgs());
        Assert.NotEmpty(page.FindAll("[data-subcategory='Supermercado']"));
    }

    [Fact]
    public async Task DeactivationAsksForConfirmationExplainingTheEffect()
    {
        await using var ui = await RegistryUi.CreateAsync();
        var category = await Catalog(ui).SaveCategoryAsync(null, "Mercado", CategoryKind.Expense);
        await Catalog(ui).SaveSubcategoryAsync(null, category.ExternalId!.Value, "Supermercado");
        var page = ui.Context.Render<Classifications>();
        page.WaitForAssertion(() => Assert.Single(RegistryUi.Rows(page)));
        await RegistryUi.Rows(page)[0].QuerySelector("[data-expand]")!.ClickAsync(new MouseEventArgs());
        var delete = "[data-subcategory='Supermercado'] [data-action='delete']";

        RegistryUi.Click(page.Find(delete));
        ui.WaitForConfirmation();
        Assert.Contains("preserva o histórico", ui.Dialogs.Find(".confirm-message").TextContent, StringComparison.Ordinal);
        await ui.Dialogs.Find("[data-confirm-cancel]").ClickAsync(new MouseEventArgs());
        Assert.Single(await Catalog(ui).GetSubcategoriesAsync());

        RegistryUi.Click(page.Find(delete));
        ui.WaitForConfirmation();
        await ui.Dialogs.Find("[data-confirm-ok]").ClickAsync(new MouseEventArgs());

        page.WaitForAssertion(() => Assert.Contains("Desativada", page.Find("[data-subcategory='Supermercado']").TextContent, StringComparison.Ordinal));
        Assert.Empty(await Catalog(ui).GetSubcategoriesAsync());
        Assert.Single(await Catalog(ui).GetSubcategoriesAsync(false));
    }

    [Fact]
    public async Task TheMenuOffersImportAndExportCsv()
    {
        await using var ui = await RegistryUi.CreateAsync();
        var page = ui.Context.Render<Classifications>();
        page.WaitForElement(".empty-state");

        await page.Find("[data-registry-menu] button").ClickAsync(new MouseEventArgs());

        ui.Popovers.WaitForAssertion(() =>
            Assert.Equal(["Importar CSV", "Exportar CSV de categorias", "Exportar CSV de subcategorias"], ui.Popovers.FindAll(".mud-popover-open .mud-menu-item").Select(x => x.TextContent.Trim())));
    }
}
