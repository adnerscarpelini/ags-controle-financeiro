using Ags.Finance.Modules.Accounts.Contracts;
using Ags.Finance.Modules.Reimbursements.Contracts;
using Ags.Finance.Modules.Transactions.Contracts;
using Ags.Finance.SharedKernel;
using Microsoft.Extensions.DependencyInjection;

namespace Ags.Finance.Modules.Reimbursements.Tests;

public sealed class EntryReimbursementTests : ReimbursementFixture
{
    private static readonly DateOnly s_day = new(2026, 10, 5);

    private IEntryCatalog Entries => Provider.GetRequiredService<IEntryCatalog>();
    private IPersonCatalog People => Provider.GetRequiredService<IPersonCatalog>();

    private async Task<Guid> PersonAsync(decimal defaultPercent = 50m, string name = "Pessoa Exemplo")
        => (await People.SaveAsync(null, name, defaultPercent)).ExternalId!.Value;

    private CreateEntryRequest Expense(decimal amount = 100m, EntryReimbursement? reimbursement = null)
        => new(s_day, MerchantId, SubcategoryId, AccountId, new Money(amount), Reimbursement: reimbursement);

    private static CreateEntryRequest Change(EntryDetails entry) => new(entry.Date, entry.MerchantExternalId, entry.SubcategoryExternalId, entry.AccountExternalId,
        new Money(Math.Abs(entry.Amount.Amount)), entry.Details, entry.Month, entry.CardExternalId, entry.CardLastDigitsExternalId, entry.Reimbursement);

    [Fact]
    public async Task ExpenseKeepsThePersonAndTheChosenPercentIndependentlyOfTheDefault()
    {
        var person = await PersonAsync();
        Assert.True((await Entries.CreateAsync(Expense(reimbursement: new(person, 30m)))).Succeeded);
        Assert.Equal(new EntryReimbursement(person, 30m), Assert.Single(await Entries.GetAsync()).Reimbursement);

        Assert.True((await People.SaveAsync(person, "Pessoa Exemplo", 70m)).Succeeded);
        Assert.Equal(30m, Assert.Single(await Entries.GetAsync()).Reimbursement!.Percent);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(100)]
    public async Task LimitPercentsAreAccepted(decimal percent)
        => Assert.True((await Entries.CreateAsync(Expense(reimbursement: new(await PersonAsync(), percent)))).Succeeded);

    [Theory]
    [InlineData(-0.01)]
    [InlineData(100.01)]
    [InlineData(33.333)]
    public async Task InvalidPercentIsRejectedAndNothingIsSaved(decimal percent)
    {
        Assert.False((await Entries.CreateAsync(Expense(reimbursement: new(await PersonAsync(), percent)))).Succeeded);
        Assert.Empty(await Entries.GetAsync());
    }

    [Fact]
    public async Task UnknownPersonIsRejected()
    {
        Assert.Contains("pessoa cadastrada", (await Entries.CreateAsync(Expense(reimbursement: new(Guid.CreateVersion7(), 50m)))).Error);
        Assert.Empty(await Entries.GetAsync());
    }

    [Fact]
    public async Task IncomeCannotHaveAReimbursement()
    {
        var classifications = Provider.GetRequiredService<IClassificationCatalog>();
        var category = await classifications.SaveCategoryAsync(null, "Receitas", CategoryKind.Income);
        var income = (await classifications.SaveSubcategoryAsync(null, category.ExternalId!.Value, "Salário")).ExternalId!.Value;
        var result = await Entries.CreateAsync(Expense(reimbursement: new(await PersonAsync(), 50m)) with { SubcategoryExternalId = income });
        Assert.Contains("despesas", result.Error);
        Assert.Empty(await Entries.GetAsync());
    }

    [Fact]
    public async Task ChangingAnExpenseToIncomeRequiresRemovingThePerson()
    {
        var classifications = Provider.GetRequiredService<IClassificationCatalog>();
        var category = await classifications.SaveCategoryAsync(null, "Receitas", CategoryKind.Income);
        var income = (await classifications.SaveSubcategoryAsync(null, category.ExternalId!.Value, "Salário")).ExternalId!.Value;
        Assert.True((await Entries.CreateAsync(Expense(reimbursement: new(await PersonAsync(), 50m)))).Succeeded);
        var entry = Assert.Single(await Entries.GetAsync());

        Assert.False((await Entries.EditAsync(entry.ExternalId, Change(entry) with { SubcategoryExternalId = income }, EntryChangeScope.OnlyThis)).Succeeded);
        Assert.True((await Entries.EditAsync(entry.ExternalId, Change(entry) with { SubcategoryExternalId = income, Reimbursement = null }, EntryChangeScope.OnlyThis)).Succeeded);

        var changed = Assert.Single(await Entries.GetAsync());
        Assert.Equal(CategoryKind.Income, changed.Kind);
        Assert.Null(changed.Reimbursement);
    }

    [Fact]
    public async Task EditingChangesAndRemovesTheReimbursement()
    {
        var person = await PersonAsync();
        var other = await PersonAsync(20m, "Outra Pessoa");
        Assert.True((await Entries.CreateAsync(Expense(reimbursement: new(person, 50m)))).Succeeded);
        var entry = Assert.Single(await Entries.GetAsync());

        Assert.True((await Entries.EditAsync(entry.ExternalId, Change(entry) with { Reimbursement = new(other, 25m) }, EntryChangeScope.OnlyThis)).Succeeded);
        Assert.Equal(new EntryReimbursement(other, 25m), Assert.Single(await Entries.GetAsync()).Reimbursement);

        entry = Assert.Single(await Entries.GetAsync());
        Assert.True((await Entries.EditAsync(entry.ExternalId, Change(entry) with { Reimbursement = null }, EntryChangeScope.OnlyThis)).Succeeded);
        Assert.Null(Assert.Single(await Entries.GetAsync()).Reimbursement);
    }

    [Fact]
    public async Task DuplicatingKeepsPersonAndPercent()
    {
        var person = await PersonAsync();
        var created = await Entries.CreateAsync(Expense(reimbursement: new(person, 30m)));
        Assert.True((await Entries.DuplicateAsync(created.ExternalId!.Value)).Succeeded);
        Assert.All(await Entries.GetAsync(), x => Assert.Equal(new EntryReimbursement(person, 30m), x.Reimbursement));
    }

    [Fact]
    public async Task InstallmentsReplicateThePersonAndThisAndFollowingChangesOnlyTheFuture()
    {
        var person = await PersonAsync();
        var card = await Provider.GetRequiredService<IAccountCatalog>().SaveCreditCardAsync(new(null, AccountId, "Cartão Exemplo", CardBrand.Mastercard, new Money(10000), 3, 10, true, "green"));
        var purchase = Expense(reimbursement: new(person, 50m)) with { CardExternalId = card.ExternalId };
        Assert.True((await Entries.CreateInstallmentsAsync(purchase, 3)).Succeeded);
        var parts = (await Entries.GetAsync()).OrderBy(x => x.InstallmentNumber).ToList();
        Assert.All(parts, x => Assert.Equal(new EntryReimbursement(person, 50m), x.Reimbursement));

        Assert.True((await Entries.EditAsync(parts[1].ExternalId, Change(parts[1]) with { Reimbursement = new(person, 25m) }, EntryChangeScope.ThisAndFollowing)).Succeeded);

        var changed = (await Entries.GetAsync()).OrderBy(x => x.InstallmentNumber).Select(x => x.Reimbursement!.Percent).ToList();
        Assert.Equal([50m, 25m, 25m], changed);
    }

    [Fact]
    public async Task ThisAndFollowingKeepsAnExceptionWhenAnotherFieldChanges()
    {
        var person = await PersonAsync();
        var card = await Provider.GetRequiredService<IAccountCatalog>().SaveCreditCardAsync(new(null, AccountId, "Cartão Exemplo", CardBrand.Mastercard, new Money(10000), 3, 10, true, "green"));
        Assert.True((await Entries.CreateInstallmentsAsync(Expense(reimbursement: new(person, 50m)) with { CardExternalId = card.ExternalId }, 3)).Succeeded);
        var parts = (await Entries.GetAsync()).OrderBy(x => x.InstallmentNumber).ToList();
        Assert.True((await Entries.EditAsync(parts[2].ExternalId, Change(parts[2]) with { Reimbursement = new(person, 10m) }, EntryChangeScope.OnlyThis)).Succeeded);

        Assert.True((await Entries.EditAsync(parts[0].ExternalId, Change(parts[0]) with { Details = "Alterado" }, EntryChangeScope.ThisAndFollowing)).Succeeded);

        var changed = (await Entries.GetAsync()).OrderBy(x => x.InstallmentNumber).ToList();
        Assert.All(changed, x => Assert.Equal("Alterado", x.Details));
        Assert.Equal([50m, 50m, 10m], changed.Select(x => x.Reimbursement!.Percent));
    }

    [Fact]
    public async Task PersonInUseCannotBeDeletedUntilRemovedFromTheEntries()
    {
        var person = await PersonAsync();
        Assert.True((await Entries.CreateAsync(Expense(reimbursement: new(person, 50m)))).Succeeded);

        Assert.Contains("não pode ser excluída", (await People.DeleteAsync(person)).Error);
        Assert.Single(await People.GetAsync());

        var entry = Assert.Single(await Entries.GetAsync());
        Assert.True((await Entries.EditAsync(entry.ExternalId, Change(entry) with { Reimbursement = null }, EntryChangeScope.OnlyThis)).Succeeded);
        Assert.True((await People.DeleteAsync(person)).Succeeded);
    }
}
