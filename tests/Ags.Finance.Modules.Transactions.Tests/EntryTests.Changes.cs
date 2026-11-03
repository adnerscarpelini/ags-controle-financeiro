using Ags.Finance.Modules.Accounts.Contracts;
using Ags.Finance.Modules.Transactions.Contracts;
using Ags.Finance.Modules.Transactions.Domain;
using Ags.Finance.Modules.Transactions.Infrastructure;
using Ags.Finance.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Ags.Finance.Modules.Transactions.Tests;

public sealed partial class EntryTests
{
    private static CreateEntryRequest Change(EntryDetails entry) => new(entry.Date, entry.MerchantExternalId, entry.SubcategoryExternalId,
        entry.AccountExternalId, new Money(Math.Abs(entry.Amount.Amount)), entry.Details, entry.Month, entry.CardExternalId, entry.CardLastDigitsExternalId, entry.Reimbursement);

    [Fact]
    public async Task EditingAndDeletingEffectiveCashRecalculatesBalanceAndPreservesIdentity()
    {
        await Entries.CreateAsync(Request());
        var original = Assert.Single(await Entries.GetAsync());
        Assert.True((await Entries.EditAsync(original.ExternalId, Change(original) with { Amount = new Money(200) }, EntryChangeScope.OnlyThis)).Succeeded);
        var edited = Assert.Single(await Entries.GetAsync());
        Assert.Equal(original.ExternalId, edited.ExternalId);
        Assert.Equal(original.EffectiveAt, edited.EffectiveAt);
        Assert.Equal(new Money(800), (await Entries.GetBalanceAsync(_accountId, original.Date))?.EffectiveBalance);
        Assert.True((await Entries.DeleteAsync(original.ExternalId, EntryChangeScope.OnlyThis)).Succeeded);
        Assert.Empty(await Entries.GetAsync());
        Assert.Equal(new Money(1000), (await Entries.GetBalanceAsync(_accountId, original.Date))?.EffectiveBalance);
    }

    [Fact]
    public async Task EditingFollowingMerchantDoesNotOverwriteDifferentInstallmentCents()
    {
        var card = await CreateCardAsync();
        await Entries.CreateInstallmentsAsync(Request() with { CardExternalId = card, Amount = new Money(1000), Month = new AccountingMonth(2026, 10) }, 3);
        var original = (await Entries.GetAsync()).OrderBy(x => x.InstallmentNumber).ToList();
        var target = await Provider.GetRequiredService<IMerchantCatalog>().SaveAsync(null, "Outra Loja", null);
        var result = await Entries.EditAsync(original[0].ExternalId, Change(original[0]) with { MerchantExternalId = target.ExternalId!.Value }, EntryChangeScope.ThisAndFollowing);
        Assert.True(result.Succeeded, result.Error);
        var changed = (await Entries.GetAsync()).OrderBy(x => x.InstallmentNumber).ToList();
        Assert.Equal(original.Select(x => x.Amount), changed.Select(x => x.Amount));
        Assert.Equal(original.Select(x => x.ExternalId), changed.Select(x => x.ExternalId));
        Assert.All(changed, x => Assert.Equal(target.ExternalId, x.MerchantExternalId));
    }

    [Fact]
    public async Task DeletingThirdAndFollowingPreservesFirstTwoAndRemovesEmptyPurchaseOnlyAfterLastEntry()
    {
        await Entries.CreateInstallmentsAsync(Request() with { CardExternalId = await CreateCardAsync(), Month = new AccountingMonth(2026, 10) }, 10);
        var entries = (await Entries.GetAsync()).OrderBy(x => x.InstallmentNumber).ToList();
        Assert.True((await Entries.DeleteAsync(entries[2].ExternalId, EntryChangeScope.ThisAndFollowing)).Succeeded);
        var remaining = (await Entries.GetAsync()).OrderBy(x => x.InstallmentNumber).ToList();
        Assert.Equal([1, 2], remaining.Select(x => x.InstallmentNumber!.Value));
        Assert.All(remaining, x => Assert.Equal(10, x.InstallmentCount));
        await Entries.DeleteAsync(remaining[0].ExternalId, EntryChangeScope.OnlyThis);
        await Entries.DeleteAsync(remaining[1].ExternalId, EntryChangeScope.OnlyThis);
        Assert.Empty(await Entries.GetAsync());
        using var scope = Provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TransactionsDbContext>();
        Assert.False(await context.Set<InstallmentPurchase>().AnyAsync());
    }

    [Fact]
    public async Task MovingOnlyOneInstallmentKeepsOtherMonthsDateAndValue()
    {
        await Entries.CreateInstallmentsAsync(Request() with { CardExternalId = await CreateCardAsync(), Month = new AccountingMonth(2026, 10) }, 3);
        var entries = (await Entries.GetAsync()).OrderBy(x => x.InstallmentNumber).ToList();
        var selected = entries[1];
        Assert.True((await Entries.EditAsync(selected.ExternalId, Change(selected) with { Month = new AccountingMonth(2027, 1) }, EntryChangeScope.OnlyThis)).Succeeded);
        var moved = (await Entries.GetAsync()).OrderBy(x => x.InstallmentNumber).ToList();
        Assert.Equal(entries[0].Month, moved[0].Month);
        Assert.Equal(entries[2].Month, moved[2].Month);
        Assert.Equal(new AccountingMonth(2027, 1), moved[1].Month);
        Assert.Equal(selected.Date, moved[1].Date);
        Assert.Equal(selected.Amount, moved[1].Amount);
    }

    [Fact]
    public async Task DuplicatingCashUsesTodayAndKeepsSavedFieldsWithANewIdentity()
    {
        await Entries.CreateAsync(Request() with { Month = new AccountingMonth(2027, 1), Details = "Detalhe Exemplo" });
        var source = Assert.Single(await Entries.GetAsync());
        var duplicate = await Entries.DuplicateAsync(source.ExternalId);
        Assert.True(duplicate.Succeeded, duplicate.Error);
        var copied = (await Entries.GetAsync()).Single(x => x.ExternalId == duplicate.ExternalId);
        Assert.NotEqual(source.ExternalId, copied.ExternalId);
        Assert.Equal(DateOnly.FromDateTime(_clock.GetLocalNow().DateTime), copied.Date);
        Assert.Equal(source.Month, copied.Month);
        Assert.Equal(source.Amount, copied.Amount);
        Assert.Equal(source.MerchantExternalId, copied.MerchantExternalId);
        Assert.Equal(source.Details, copied.Details);
    }

    [Fact]
    public async Task UnchangedInactiveReferencesCanBeKeptWhenEditingHistoricalDetails()
    {
        await Entries.CreateAsync(Request());
        var entry = Assert.Single(await Entries.GetAsync());
        await Provider.GetRequiredService<IMerchantCatalog>().DeactivateAsync(_merchantId);
        await Provider.GetRequiredService<IClassificationCatalog>().DeactivateAsync(_subcategoryId, true);
        await Provider.GetRequiredService<IAccountCatalog>().DeactivateAccountAsync(_accountId);
        var result = await Entries.EditAsync(entry.ExternalId, Change(entry) with { Details = "Detalhe corrigido" }, EntryChangeScope.OnlyThis);
        Assert.True(result.Succeeded, result.Error);
        Assert.Equal("Detalhe corrigido", Assert.Single(await Entries.GetAsync()).Details);
    }
}
