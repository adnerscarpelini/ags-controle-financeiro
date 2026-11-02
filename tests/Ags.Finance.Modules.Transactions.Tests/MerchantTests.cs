using Ags.Finance.Modules.Transactions.Contracts;
using Ags.Finance.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace Ags.Finance.Modules.Transactions.Tests;

public sealed class MerchantTests : IAsyncLifetime
{
    private PostgresTestDatabase? _database;
    private ServiceProvider? _provider;
    private IMerchantCatalog Catalog => (_provider ?? throw new InvalidOperationException()).GetRequiredService<IMerchantCatalog>();

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

    [Theory]
    [InlineData("Loja Exemplo", "LOJAEXEMPLO")]
    [InlineData("Loja Exemplo", "loja-exemplo")]
    [InlineData("Padaria São Exemplo", " PADARIA SAO EXEMPLO ")]
    public async Task EquivalentNamesAreRejectedAndIdentifyExistingMerchant(string original, string duplicate)
    {
        var saved = await Catalog.SaveAsync(null, original, null);
        var result = await Catalog.SaveAsync(null, duplicate, null);
        Assert.True(saved.Succeeded, saved.Error);
        Assert.False(result.Succeeded);
        Assert.Equal(saved.ExternalId, result.ExternalId);
        Assert.Contains(original, result.Error, StringComparison.Ordinal);
        Assert.Single(await Catalog.FindAsync());
    }

    [Fact]
    public async Task SearchesByAccentInsensitiveSubstringAndPreservesOptionalDefault()
    {
        var classifications = (_provider ?? throw new InvalidOperationException()).GetRequiredService<IClassificationCatalog>();
        var category = await classifications.SaveCategoryAsync(null, "Mercado", CategoryKind.Expense);
        var subcategory = await classifications.SaveSubcategoryAsync(null, category.ExternalId!.Value, "Feira");
        var saved = await Catalog.SaveAsync(null, "Padaria São Exemplo", subcategory.ExternalId);
        var suggestion = Assert.Single(await Catalog.FindAsync("SAO"));
        Assert.Equal(saved.ExternalId, suggestion.ExternalId);
        Assert.Equal(subcategory.ExternalId, suggestion.DefaultSubcategoryExternalId);
        Assert.True((await Catalog.SaveAsync(saved.ExternalId, "Padaria Novo Exemplo", null)).Succeeded);
        Assert.Empty(await Catalog.FindAsync("SAO"));
        Assert.Equal(saved.ExternalId, Assert.Single(await Catalog.FindAsync("NOVO")).ExternalId);
    }

    [Fact]
    public async Task DeactivationKeepsIdentityAndPreventsRecreatingEquivalentName()
    {
        var saved = await Catalog.SaveAsync(null, "Loja Exemplo", null);
        Assert.True((await Catalog.DeactivateAsync(saved.ExternalId!.Value)).Succeeded);
        Assert.Empty(await Catalog.FindAsync());
        Assert.Equal(saved.ExternalId, Assert.Single(await Catalog.FindAsync(activeOnly: false)).ExternalId);
        Assert.False((await Catalog.SaveAsync(null, "LOJAEXEMPLO", null)).Succeeded);
    }

    [Fact]
    public async Task MergePreservesTargetAndRejectsMissingInactiveOrSameTarget()
    {
        var source = await Catalog.SaveAsync(null, "Descrição antiga", null);
        var target = await Catalog.SaveAsync(null, "Descrição mantida", null);
        Assert.False((await Catalog.MergeAsync(source.ExternalId!.Value, source.ExternalId.Value)).Succeeded);
        Assert.False((await Catalog.MergeAsync(source.ExternalId.Value, Guid.NewGuid())).Succeeded);
        Assert.Equal(2, (await Catalog.FindAsync()).Count);
        Assert.True((await Catalog.MergeAsync(source.ExternalId.Value, target.ExternalId!.Value)).Succeeded);
        Assert.Equal(target.ExternalId, Assert.Single(await Catalog.FindAsync()).ExternalId);
        var another = await Catalog.SaveAsync(null, "Outra descrição", null);
        await Catalog.DeactivateAsync(target.ExternalId.Value);
        Assert.False((await Catalog.MergeAsync(another.ExternalId!.Value, target.ExternalId.Value)).Succeeded);
        Assert.Equal(2, (await Catalog.FindAsync(activeOnly: false)).Count);
    }

    [Fact]
    public async Task RejectsInvalidDataAndUnknownDefaultWithoutSaving()
    {
        Assert.False((await Catalog.SaveAsync(null, "...", null)).Succeeded);
        Assert.False((await Catalog.SaveAsync(null, new string('a', 101), null)).Succeeded);
        Assert.False((await Catalog.SaveAsync(null, "Loja", Guid.NewGuid())).Succeeded);
        Assert.False((await Catalog.SaveAsync(Guid.NewGuid(), "Loja", null)).Succeeded);
        Assert.Empty(await Catalog.FindAsync(activeOnly: false));
    }

    [Fact]
    public async Task ConcurrentEquivalentCreatesPersistOnlyOneMerchant()
    {
        var results = await Task.WhenAll(Catalog.SaveAsync(null, "Loja Exemplo", null), Catalog.SaveAsync(null, "LOJA-EXEMPLO", null));
        Assert.Single(results, x => x.Succeeded);
        Assert.Single(await Catalog.FindAsync());
    }
}
