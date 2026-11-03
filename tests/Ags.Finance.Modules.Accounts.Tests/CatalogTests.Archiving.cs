using Ags.Finance.Modules.Accounts.Contracts;

namespace Ags.Finance.Modules.Accounts.Tests;

public sealed partial class CatalogTests
{
    [Fact]
    public async Task RestorePreservesIdentityAndIndividualCardArchiveSurvivesAccountRestore()
    {
        var account = await CreateAccountAsync();
        var first = await Catalog.SaveCreditCardAsync(Card(account));
        var second = await Catalog.SaveCreditCardAsync(Card(account) with { Name = "Outro cartão" });
        var before = Assert.Single(await Catalog.GetAccountsAsync());
        await Catalog.DeactivateCreditCardAsync(second.ExternalId!.Value);
        await Catalog.DeactivateAccountAsync(account);
        Assert.Empty(await Catalog.GetAccountsAsync());
        Assert.Empty(await Catalog.GetCreditCardsAsync());
        var unavailable = (await Catalog.GetCreditCardsAsync(false)).Single(x => x.ExternalId == first.ExternalId);
        Assert.True(unavailable.IsActive);
        Assert.False(unavailable.IsAccountActive);
        Assert.False(unavailable.IsAvailable);
        Assert.True((await Catalog.ReactivateAccountAsync(account)).Succeeded);
        Assert.True((await Catalog.ReactivateAccountAsync(account)).Succeeded);
        Assert.Equal(before, Assert.Single(await Catalog.GetAccountsAsync()));
        Assert.Equal(first.ExternalId, Assert.Single(await Catalog.GetCreditCardsAsync()).ExternalId);
        Assert.False((await Catalog.GetCreditCardsAsync(false)).Single(x => x.ExternalId == second.ExternalId).IsActive);
        Assert.True((await Catalog.ReactivateCreditCardAsync(second.ExternalId.Value)).Succeeded);
        Assert.True((await Catalog.ReactivateCreditCardAsync(second.ExternalId.Value)).Succeeded);
        Assert.Equal(2, (await Catalog.GetCreditCardsAsync()).Count);
    }

    [Fact]
    public async Task RestoringCardUnderArchivedAccountKeepsItUnavailableAndMissingIdsFail()
    {
        var account = await CreateAccountAsync();
        var created = await Catalog.SaveCreditCardAsync(Card(account));
        await Catalog.DeactivateCreditCardAsync(created.ExternalId!.Value);
        await Catalog.DeactivateAccountAsync(account);
        Assert.True((await Catalog.ReactivateCreditCardAsync(created.ExternalId.Value)).Succeeded);
        Assert.True(Assert.Single(await Catalog.GetCreditCardsAsync(false)).IsActive);
        Assert.False(Assert.Single(await Catalog.GetCreditCardsAsync(false)).IsAvailable);
        Assert.Empty(await Catalog.GetCreditCardsAsync());
        Assert.False((await Catalog.ReactivateAccountAsync(Guid.NewGuid())).Succeeded);
        Assert.False((await Catalog.ReactivateCreditCardAsync(Guid.NewGuid())).Succeeded);
    }
}
