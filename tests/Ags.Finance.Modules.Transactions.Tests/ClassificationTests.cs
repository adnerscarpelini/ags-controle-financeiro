using Ags.Finance.Modules.Transactions.Contracts;
using Ags.Finance.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace Ags.Finance.Modules.Transactions.Tests;

public sealed class ClassificationTests : IAsyncLifetime
{
    private PostgresTestDatabase? _database;
    private ServiceProvider? _provider;
    private IClassificationCatalog Catalog => (_provider ?? throw new InvalidOperationException()).GetRequiredService<IClassificationCatalog>();

    public async Task InitializeAsync()
    {
        _database = await PostgresTestDatabase.CreateAsync();
        _provider = new ServiceCollection().AddTransactionsModule(_database.ConnectionString).BuildServiceProvider();
        await _provider.MigrateTransactionsModuleAsync();
    }

    public async Task DisposeAsync()
    {
        if (_provider is not null) { await _provider.DisposeAsync(); }
        if (_database is not null) { await _database.DisposeAsync(); }
    }

    [Fact]
    public async Task SavesEditsAndFormatsClassificationWithoutChangingIdentity()
    {
        var category = await Catalog.SaveCategoryAsync(null, "Mercado", CategoryKind.Expense);
        Assert.True(category.Succeeded, category.Error);
        var subcategory = await Catalog.SaveSubcategoryAsync(null, category.ExternalId!.Value, "Supermercado");
        Assert.True(subcategory.Succeeded, subcategory.Error);
        Assert.Equal("Mercado > Supermercado", Assert.Single(await Catalog.GetSubcategoriesAsync()).DisplayName);
        Assert.True((await Catalog.SaveSubcategoryAsync(subcategory.ExternalId, category.ExternalId.Value, "Feira")).Succeeded);
        var saved = Assert.Single(await Catalog.GetSubcategoriesAsync());
        Assert.Equal(subcategory.ExternalId, saved.ExternalId);
        Assert.Equal("Mercado > Feira", saved.DisplayName);
    }

    [Fact]
    public async Task RejectsDuplicateWithinParentAndAllowsSameNameInAnotherCategory()
    {
        var first = await Catalog.SaveCategoryAsync(null, "Mercado", CategoryKind.Expense);
        var second = await Catalog.SaveCategoryAsync(null, "Outros", CategoryKind.Expense);
        Assert.True((await Catalog.SaveSubcategoryAsync(null, first.ExternalId!.Value, "Feira")).Succeeded);
        Assert.False((await Catalog.SaveSubcategoryAsync(null, first.ExternalId.Value, " FEIRA ")).Succeeded);
        Assert.True((await Catalog.SaveSubcategoryAsync(null, second.ExternalId!.Value, "Feira")).Succeeded);
        Assert.Equal(2, (await Catalog.GetSubcategoriesAsync()).Count);
    }

    [Fact]
    public async Task DeactivatingParentHidesChildrenFromSelectionsAndKeepsHistory()
    {
        var category = await Catalog.SaveCategoryAsync(null, "Mercado", CategoryKind.Expense);
        await Catalog.SaveSubcategoryAsync(null, category.ExternalId!.Value, "Feira");
        Assert.True((await Catalog.DeactivateAsync(category.ExternalId.Value, false)).Succeeded);
        Assert.Empty(await Catalog.GetCategoriesAsync());
        Assert.Empty(await Catalog.GetSubcategoriesAsync());
        Assert.Single(await Catalog.GetCategoriesAsync(false));
        Assert.False(Assert.Single(await Catalog.GetSubcategoriesAsync(false)).IsActive);
        Assert.False((await Catalog.SaveSubcategoryAsync(null, category.ExternalId.Value, "Outra")).Succeeded);
    }

    [Fact]
    public async Task InvalidDataDoesNotPersistAndDeactivationPreservesSubcategory()
    {
        Assert.False((await Catalog.SaveCategoryAsync(null, " ", CategoryKind.Income)).Succeeded);
        Assert.False((await Catalog.SaveCategoryAsync(null, "Exemplo", (CategoryKind)99)).Succeeded);
        Assert.False((await Catalog.SaveSubcategoryAsync(null, Guid.NewGuid(), "Exemplo")).Succeeded);
        var category = await Catalog.SaveCategoryAsync(null, "Recebimentos", CategoryKind.Income);
        var subcategory = await Catalog.SaveSubcategoryAsync(null, category.ExternalId!.Value, "Exemplo");
        await Catalog.DeactivateAsync(subcategory.ExternalId!.Value, true);
        Assert.Empty(await Catalog.GetSubcategoriesAsync());
        Assert.Equal(subcategory.ExternalId, Assert.Single(await Catalog.GetSubcategoriesAsync(false)).ExternalId);
    }
}
