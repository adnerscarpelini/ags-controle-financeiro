using Ags.Finance.Modules.Accounts.Contracts;
using Ags.Finance.Modules.Reimbursements.Application;
using Ags.Finance.Modules.Reimbursements.Contracts;
using Ags.Finance.Modules.Reimbursements.Domain;
using Ags.Finance.Modules.Reimbursements.Infrastructure;
using Ags.Finance.Modules.Transactions.Contracts;
using Ags.Finance.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Ags.Finance.Modules.Reimbursements.Tests;

public sealed class ReimbursementReceiptTests : ReimbursementFixture
{
    private static readonly AccountingMonth s_october = new(2026, 10);
    private static readonly DateOnly s_day = new(2026, 10, 20);

    private IEntryCatalog Entries => Provider.GetRequiredService<IEntryCatalog>();
    private IReimbursementReceipts Receipts => Provider.GetRequiredService<IReimbursementReceipts>();
    private IReimbursementOverviews Overviews => Provider.GetRequiredService<IReimbursementOverviews>();

    private async Task<Guid> PersonAsync(string name = "Ana Exemplo")
        => (await Provider.GetRequiredService<IPersonCatalog>().SaveAsync(null, name, 50m)).ExternalId!.Value;

    private async Task<Guid> ExpenseAsync(decimal amount, Guid person, decimal percent = 100m)
    {
        var result = await Entries.CreateAsync(new(new DateOnly(2026, 10, 5), MerchantId, SubcategoryId, AccountId, new Money(amount), Reimbursement: new(person, percent)));
        Assert.True(result.Succeeded, result.Error);
        return result.ExternalId!.Value;
    }

    private static ReceiveReimbursementItem Item(Guid entry, decimal expected, decimal? received = null) => new(entry, new Money(expected), new Money(received ?? expected));

    private ReceiveReimbursementsRequest Request(params ReceiveReimbursementItem[] items) => new(s_day, AccountId, items);

    private static CreateEntryRequest Change(EntryDetails entry) => new(entry.Date, entry.MerchantExternalId, entry.SubcategoryExternalId, entry.AccountExternalId,
        new Money(Math.Abs(entry.Amount.Amount)), entry.Details, entry.Month, entry.CardExternalId, entry.CardLastDigitsExternalId, entry.Reimbursement);

    private async Task<IReadOnlyList<EntryDetails>> IncomesAsync() => (await Entries.QueryAsync(new(Kind: CategoryKind.Income))).Entries;

    private async Task<Money> BalanceAsync() => (await Entries.GetBalanceAsync(AccountId, new DateOnly(2026, 12, 31)))!.EffectiveBalance;

    [Fact]
    public async Task ItemsOfOnePersonBecomeOneIncomeAndAreReceived()
    {
        var person = await PersonAsync();
        var first = await ExpenseAsync(100m, person);
        var second = await ExpenseAsync(200m, person);
        var before = await BalanceAsync();

        var result = await Receipts.ReceiveAsync(Request(Item(first, 100m), Item(second, 200m)));

        Assert.True(result.Succeeded, result.Error);
        var income = Assert.Single(await IncomesAsync());
        Assert.Equal((new Money(300m), s_day, s_october, true, "Ana Exemplo", "Reembolsos > Reembolsos recebidos", (Guid?)null),
            (income.Amount, income.Date, income.Month, income.IsEffective, income.MerchantName, income.Classification, income.CardExternalId));
        Assert.Equal(before + new Money(300m), await BalanceAsync());
        var overview = await Overviews.GetAsync(new(s_october));
        Assert.All(overview.Items, x => Assert.Equal(ReimbursementStatus.Received, x.Status));
        Assert.Equal((new Money(300m), new Money(300m), Money.Zero), (overview.Expected, overview.Received, overview.Pending));
    }

    [Fact]
    public async Task ItemsOfTwoPeopleBecomeOneIncomeEach()
    {
        var first = await ExpenseAsync(100m, await PersonAsync());
        var second = await ExpenseAsync(200m, await PersonAsync("Bia Exemplo"));

        var result = await Receipts.ReceiveAsync(Request(Item(first, 100m), Item(second, 200m)));

        Assert.Equal(2, result.ReceiptExternalIds.Count);
        var incomes = await IncomesAsync();
        Assert.Equal([new Money(100m), new Money(200m)], incomes.OrderBy(x => x.Amount.Amount).Select(x => x.Amount));
        Assert.Equal(2, incomes.Select(x => x.MerchantExternalId).Distinct().Count());
        Assert.Single(incomes.Select(x => x.SubcategoryExternalId).Distinct());
    }

    [Theory]
    [InlineData(80)]
    [InlineData(120)]
    public async Task ReceivedDifferentFromExpectedClosesTheItemWithoutResidue(decimal received)
    {
        var entry = await ExpenseAsync(100m, await PersonAsync());

        Assert.True((await Receipts.ReceiveAsync(Request(Item(entry, 100m, received)))).Succeeded);

        var overview = await Overviews.GetAsync(new(s_october));
        var item = Assert.Single(overview.Items);
        Assert.Equal((ReimbursementStatus.Received, new Money(100m), new Money(received), s_day), (item.Status, item.Expected, item.Received!.Value, item.ReceivedDate!.Value));
        Assert.Equal(Money.Zero, overview.Pending);
        Assert.Equal(new Money(received), Assert.Single(await IncomesAsync()).Amount);
    }

    [Fact]
    public async Task ZeroReceivedClosesTheItemsWithoutIncomeOrAccount()
    {
        var entry = await ExpenseAsync(100m, await PersonAsync());
        var before = await BalanceAsync();

        var result = await Receipts.ReceiveAsync(new(s_day, null, [Item(entry, 100m, 0m)]));

        Assert.True(result.Succeeded, result.Error);
        Assert.Empty(await IncomesAsync());
        Assert.Equal(before, await BalanceAsync());
        Assert.Equal(ReimbursementStatus.Received, Assert.Single((await Overviews.GetAsync(new(s_october))).Items).Status);
    }

    [Fact]
    public async Task OnlyPeopleWithAPositiveTotalGenerateIncome()
    {
        var first = await ExpenseAsync(100m, await PersonAsync());
        var second = await ExpenseAsync(200m, await PersonAsync("Bia Exemplo"));

        Assert.True((await Receipts.ReceiveAsync(Request(Item(first, 100m, 0m), Item(second, 200m)))).Succeeded);

        Assert.Equal("Bia Exemplo", Assert.Single(await IncomesAsync()).MerchantName);
    }

    [Fact]
    public async Task NegativeReceivedIsRejected()
    {
        var entry = await ExpenseAsync(100m, await PersonAsync());
        Assert.False((await Receipts.ReceiveAsync(Request(new ReceiveReimbursementItem(entry, new Money(100m), new Money(-1m))))).Succeeded);
        Assert.Empty(await IncomesAsync());
        Assert.Equal(ReimbursementStatus.Pending, Assert.Single((await Overviews.GetAsync(new(s_october))).Items).Status);
    }

    [Fact]
    public async Task AccountIsRequiredAndMustBeAnActiveCheckingOne()
    {
        var entry = await ExpenseAsync(100m, await PersonAsync());
        var accounts = Provider.GetRequiredService<IAccountCatalog>();
        var bank = (await accounts.GetBanksAsync())[0].ExternalId;
        var savings = (await accounts.SaveAccountAsync(new(null, bank, "Poupança Exemplo", AccountKind.Savings, "green"))).ExternalId!.Value;
        var archived = (await accounts.SaveAccountAsync(new(null, bank, "Conta Arquivada", AccountKind.Checking, "green"))).ExternalId!.Value;
        Assert.True((await accounts.DeactivateAccountAsync(archived)).Succeeded);

        foreach (var account in new Guid?[] { null, savings, archived, Guid.CreateVersion7() })
        {
            Assert.Contains("conta corrente", (await Receipts.ReceiveAsync(new(s_day, account, [Item(entry, 100m)]))).Error);
        }
        Assert.Empty(await IncomesAsync());
    }

    [Fact]
    public async Task EmptySelectionIsRejected() => Assert.False((await Receipts.ReceiveAsync(Request())).Succeeded);

    [Fact]
    public async Task FailureInTheSecondPersonRollsEverythingBack()
    {
        var first = await ExpenseAsync(100m, await PersonAsync());
        var second = await ExpenseAsync(200m, await PersonAsync("Bia Exemplo"));
        var before = await BalanceAsync();
        await using var failing = Build(new ServiceCollection()).AddScoped<IReimbursementStore>(provider => new FailingStore(provider.GetRequiredService<IDbContextFactory<ReimbursementsDbContext>>())).BuildServiceProvider();
        await using var scope = failing.CreateAsyncScope();

        await Assert.ThrowsAsync<InvalidOperationException>(() => scope.ServiceProvider.GetRequiredService<IReimbursementReceipts>().ReceiveAsync(Request(Item(first, 100m), Item(second, 200m))));

        Assert.Empty(await IncomesAsync());
        Assert.Equal(before, await BalanceAsync());
        Assert.All((await Overviews.GetAsync(new(s_october))).Items, x => Assert.Equal(ReimbursementStatus.Pending, x.Status));
    }

    [Fact]
    public async Task ReceivingTheSameItemAgainIsRejectedWithoutNewIncome()
    {
        var entry = await ExpenseAsync(100m, await PersonAsync());
        Assert.True((await Receipts.ReceiveAsync(Request(Item(entry, 100m)))).Succeeded);

        Assert.False((await Receipts.ReceiveAsync(Request(Item(entry, 100m)))).Succeeded);

        Assert.Single(await IncomesAsync());
    }

    [Fact]
    public async Task ExpectedChangedAfterOpeningTheScreenIsRejected()
    {
        var entry = await ExpenseAsync(100m, await PersonAsync());
        var details = Assert.Single(await Entries.GetAsync());
        Assert.True((await Entries.EditAsync(entry, Change(details) with { Amount = new Money(150m) }, EntryChangeScope.OnlyThis)).Succeeded);

        Assert.Contains("Confira", (await Receipts.ReceiveAsync(Request(Item(entry, 100m)))).Error);

        Assert.Empty(await IncomesAsync());
    }

    [Fact]
    public async Task DeactivatedDescriptionBlocksWithoutCreatingADuplicate()
    {
        var entry = await ExpenseAsync(100m, await PersonAsync());
        var merchants = Provider.GetRequiredService<IMerchantCatalog>();
        var existing = (await merchants.SaveAsync(null, "Ana Exemplo", SubcategoryId)).ExternalId!.Value;
        Assert.True((await merchants.DeactivateAsync(existing)).Succeeded);

        Assert.Contains("desativada", (await Receipts.ReceiveAsync(Request(Item(entry, 100m)))).Error);

        Assert.Single(await merchants.FindAsync("Ana Exemplo", false));
    }

    [Fact]
    public async Task SecondReceiptReusesTheClassificationAndTheDescription()
    {
        var person = await PersonAsync();
        var first = await ExpenseAsync(100m, person);
        var second = await ExpenseAsync(200m, person);
        Assert.True((await Receipts.ReceiveAsync(Request(Item(first, 100m)))).Succeeded);
        Assert.True((await Receipts.ReceiveAsync(Request(Item(second, 200m)))).Succeeded);

        var incomes = await IncomesAsync();
        Assert.Single(incomes.Select(x => x.MerchantExternalId).Distinct());
        Assert.Single(incomes.Select(x => x.SubcategoryExternalId).Distinct());
    }

    [Fact]
    public async Task ReceivedExpenseBlocksProtectedChangesAndAllowsTheOthers()
    {
        var person = await PersonAsync();
        var other = await PersonAsync("Bia Exemplo");
        var entry = await ExpenseAsync(100m, person);
        Assert.True((await Receipts.ReceiveAsync(Request(Item(entry, 100m)))).Succeeded);
        var details = (await Entries.GetAsync()).Single(x => x.ExternalId == entry);

        Assert.Contains("desfaça o recebimento", (await Entries.EditAsync(entry, Change(details) with { Amount = new Money(150m) }, EntryChangeScope.OnlyThis)).Error);
        Assert.False((await Entries.EditAsync(entry, Change(details) with { Reimbursement = new(other, 100m) }, EntryChangeScope.OnlyThis)).Succeeded);
        Assert.False((await Entries.EditAsync(entry, Change(details) with { Reimbursement = new(person, 50m) }, EntryChangeScope.OnlyThis)).Succeeded);
        Assert.False((await Entries.EditAsync(entry, Change(details) with { Reimbursement = null }, EntryChangeScope.OnlyThis)).Succeeded);
        Assert.False((await Entries.DeleteAsync(entry, EntryChangeScope.OnlyThis)).Succeeded);

        Assert.True((await Entries.EditAsync(entry, Change(details) with { Details = "Observação", Month = new AccountingMonth(2026, 11) }, EntryChangeScope.OnlyThis)).Succeeded);
        Assert.Empty((await Overviews.GetAsync(new(s_october))).Items);
        Assert.Equal(ReimbursementStatus.Received, Assert.Single((await Overviews.GetAsync(new(new AccountingMonth(2026, 11)))).Items).Status);
    }

    [Fact]
    public async Task ReceivedOccurrenceInTheScopeOfAnEditRejectsTheWholeOperation()
    {
        var person = await PersonAsync();
        Assert.True((await Entries.CreateRecurringAsync(new(new(new DateOnly(2026, 10, 5), MerchantId, SubcategoryId, AccountId, new Money(100), Reimbursement: new(person, 100m))))).Succeeded);
        Assert.True((await Entries.EnsureRecurringMonthAsync(new AccountingMonth(2026, 11))).Succeeded);
        var occurrences = (await Entries.GetAsync()).OrderBy(x => x.Month).ToList();
        Assert.True((await Receipts.ReceiveAsync(Request(Item(occurrences[1].ExternalId, 100m)))).Succeeded);

        var edited = await Entries.EditRecurringAsync(occurrences[0].ExternalId, new(Change(occurrences[0]) with { Amount = new Money(200m) }, RecurringChangeScope.All));
        Assert.Contains("05/11/2026", edited.Error);
        Assert.False((await Entries.DeleteRecurringAsync(occurrences[0].ExternalId, RecurringChangeScope.ThisAndFollowing)).Succeeded);

        var after = (await Entries.GetAsync()).Where(x => x.RecurringEntryExternalId is not null).OrderBy(x => x.Month).ToList();
        Assert.Equal([new Money(-100m), new Money(-100m)], after.Select(x => x.Amount));

        Assert.True((await Entries.EditRecurringAsync(occurrences[0].ExternalId, new(Change(occurrences[0]) with { Details = "Alterado" }, RecurringChangeScope.All))).Succeeded);
    }

    [Fact]
    public async Task ReceivedPendingExpenseCanStillBeConfirmed()
    {
        var person = await PersonAsync();
        Assert.True((await Entries.CreateRecurringAsync(new(new(new DateOnly(2026, 10, 5), MerchantId, SubcategoryId, AccountId, new Money(100), Reimbursement: new(person, 100m))))).Succeeded);
        var occurrence = Assert.Single(await Entries.GetAsync());
        Assert.False(occurrence.IsEffective);
        Assert.True((await Receipts.ReceiveAsync(Request(Item(occurrence.ExternalId, 100m)))).Succeeded);

        Assert.True((await Entries.ConfirmEffectiveAsync(occurrence.ExternalId, occurrence.Date)).Succeeded);

        var item = Assert.Single((await Overviews.GetAsync(new(s_october))).Items);
        Assert.Equal((ReimbursementStatus.Received, true), (item.Status, item.IsExpenseEffective));
    }

    [Fact]
    public async Task GeneratedIncomeCannotBeEditedOrDeleted()
    {
        var entry = await ExpenseAsync(100m, await PersonAsync());
        Assert.True((await Receipts.ReceiveAsync(Request(Item(entry, 100m)))).Succeeded);
        var income = Assert.Single(await IncomesAsync());

        Assert.Contains("desfaça o recebimento", (await Entries.EditAsync(income.ExternalId, Change(income) with { Details = "Alterado" }, EntryChangeScope.OnlyThis)).Error);
        Assert.Contains("desfaça o recebimento", (await Entries.DeleteAsync(income.ExternalId, EntryChangeScope.OnlyThis)).Error);
        Assert.Single(await IncomesAsync());
    }

    [Fact]
    public async Task PersonWithAReceiptCannotBeDeleted()
    {
        var person = await PersonAsync();
        var entry = await ExpenseAsync(100m, person);
        Assert.True((await Receipts.ReceiveAsync(Request(Item(entry, 100m)))).Succeeded);

        Assert.False((await Provider.GetRequiredService<IPersonCatalog>().DeleteAsync(person)).Succeeded);
    }

    [Fact]
    public async Task ChangingTheDefaultPercentDoesNotChangeAReceivedItem()
    {
        var person = await PersonAsync();
        var entry = await ExpenseAsync(100m, person, 40m);
        Assert.True((await Receipts.ReceiveAsync(Request(Item(entry, 40m)))).Succeeded);
        Assert.True((await Provider.GetRequiredService<IPersonCatalog>().SaveAsync(person, "Ana Exemplo", 90m)).Succeeded);

        var item = Assert.Single((await Overviews.GetAsync(new(s_october))).Items);
        Assert.Equal((new Money(100m), 40m, new Money(40m), new Money(40m)), (item.ExpenseAmount, item.Percent, item.Expected, item.Received!.Value));
    }

    [Fact]
    public async Task UndoingRemovesTheIncomeAndReturnsAllItemsToPending()
    {
        var person = await PersonAsync();
        var first = await ExpenseAsync(100m, person);
        var second = await ExpenseAsync(200m, person);
        var before = await BalanceAsync();
        var receipt = Assert.Single((await Receipts.ReceiveAsync(Request(Item(first, 100m), Item(second, 200m)))).ReceiptExternalIds);

        var details = await Receipts.FindAsync(receipt);
        Assert.Equal(("Ana Exemplo", s_day, (Guid?)AccountId, new Money(300m), 2), (details!.PersonName, details.Date, details.AccountExternalId, details.Total, details.Items.Count));

        var result = await Receipts.UndoAsync(receipt);

        Assert.True(result.Succeeded, result.Error);
        Assert.Empty(await IncomesAsync());
        Assert.Equal(before, await BalanceAsync());
        var overview = await Overviews.GetAsync(new(s_october));
        Assert.Equal(2, overview.Items.Count);
        Assert.All(overview.Items, x => Assert.Equal((ReimbursementStatus.Pending, (Money?)null), (x.Status, x.Received)));
        Assert.Null(await Receipts.FindAsync(receipt));
    }

    [Fact]
    public async Task UndoingOnePersonKeepsTheOtherReceipt()
    {
        var first = await ExpenseAsync(100m, await PersonAsync());
        var second = await ExpenseAsync(200m, await PersonAsync("Bia Exemplo"));
        var receipts = (await Receipts.ReceiveAsync(Request(Item(first, 100m), Item(second, 200m)))).ReceiptExternalIds;

        Assert.True((await Receipts.UndoAsync(receipts[0])).Succeeded);

        Assert.Equal("Bia Exemplo", Assert.Single(await IncomesAsync()).MerchantName);
        var items = (await Overviews.GetAsync(new(s_october))).Items;
        Assert.Equal(ReimbursementStatus.Pending, items.Single(x => x.EntryExternalId == first).Status);
        Assert.Equal(ReimbursementStatus.Received, items.Single(x => x.EntryExternalId == second).Status);
    }

    [Fact]
    public async Task AfterUndoingTheExpenseIsFreeAndExpectedFollowsIt()
    {
        var entry = await ExpenseAsync(100m, await PersonAsync());
        var receipt = Assert.Single((await Receipts.ReceiveAsync(Request(Item(entry, 100m, 80m)))).ReceiptExternalIds);
        Assert.True((await Receipts.UndoAsync(receipt)).Succeeded);
        var details = Assert.Single(await Entries.GetAsync());

        Assert.True((await Entries.EditAsync(entry, Change(details) with { Amount = new Money(150m) }, EntryChangeScope.OnlyThis)).Succeeded);

        Assert.Equal(new Money(150m), Assert.Single((await Overviews.GetAsync(new(s_october))).Items).Expected);
    }

    [Fact]
    public async Task UndoneReceiptCanBeReceivedAgainWithoutDuplicates()
    {
        var entry = await ExpenseAsync(100m, await PersonAsync());
        var receipt = Assert.Single((await Receipts.ReceiveAsync(Request(Item(entry, 100m)))).ReceiptExternalIds);
        Assert.True((await Receipts.UndoAsync(receipt)).Succeeded);

        Assert.True((await Receipts.ReceiveAsync(Request(Item(entry, 100m, 90m)))).Succeeded);

        Assert.Equal(new Money(90m), Assert.Single(await IncomesAsync()).Amount);
    }

    [Fact]
    public async Task ZeroTotalReceiptCanBeUndoneWithoutAccountEffect()
    {
        var entry = await ExpenseAsync(100m, await PersonAsync());
        var before = await BalanceAsync();
        var receipt = Assert.Single((await Receipts.ReceiveAsync(new(s_day, null, [Item(entry, 100m, 0m)]))).ReceiptExternalIds);

        Assert.True((await Receipts.UndoAsync(receipt)).Succeeded);

        Assert.Equal(ReimbursementStatus.Pending, Assert.Single((await Overviews.GetAsync(new(s_october))).Items).Status);
        Assert.Equal(before, await BalanceAsync());
    }

    [Fact]
    public async Task FailureDeletingTheIncomeKeepsTheReceiptIntact()
    {
        var entry = await ExpenseAsync(100m, await PersonAsync());
        var receipt = Assert.Single((await Receipts.ReceiveAsync(Request(Item(entry, 100m)))).ReceiptExternalIds);
        var income = Assert.Single(await IncomesAsync());
        await Database.ScalarAsync($"DELETE FROM transactions.entries WHERE \"ExternalId\" = '{income.ExternalId}' RETURNING 1");

        Assert.False((await Receipts.UndoAsync(receipt)).Succeeded);

        Assert.NotNull(await Receipts.FindAsync(receipt));
        Assert.Equal(ReimbursementStatus.Received, Assert.Single((await Overviews.GetAsync(new(s_october))).Items).Status);
    }

    [Fact]
    public async Task UndoingAgainIsRejected()
    {
        var entry = await ExpenseAsync(100m, await PersonAsync());
        var receipt = Assert.Single((await Receipts.ReceiveAsync(Request(Item(entry, 100m)))).ReceiptExternalIds);
        Assert.True((await Receipts.UndoAsync(receipt)).Succeeded);

        Assert.False((await Receipts.UndoAsync(receipt)).Succeeded);
    }

    private sealed class FailingStore(IDbContextFactory<ReimbursementsDbContext> factory) : IReimbursementStore
    {
        private readonly ReimbursementStore _inner = new(factory);
        private int _added;
        public Task<IReadOnlyList<Person>> GetPeopleAsync(CancellationToken cancellationToken) => _inner.GetPeopleAsync(cancellationToken);
        public Task<bool> SavePersonAsync(Person person, CancellationToken cancellationToken) => _inner.SavePersonAsync(person, cancellationToken);
        public Task DeletePersonAsync(Person person, CancellationToken cancellationToken) => _inner.DeletePersonAsync(person, cancellationToken);
        public Task<IReadOnlyList<ReimbursementReceipt>> GetReceiptsAsync(CancellationToken cancellationToken) => _inner.GetReceiptsAsync(cancellationToken);
        public Task<IReadOnlyList<ReimbursementReceiptItem>> GetReceiptItemsAsync(CancellationToken cancellationToken) => _inner.GetReceiptItemsAsync(cancellationToken);
        public Task DeleteReceiptAsync(ReimbursementReceipt receipt, CancellationToken cancellationToken) => _inner.DeleteReceiptAsync(receipt, cancellationToken);
        public Task<bool> IsIncomeOfReceiptAsync(Guid entryExternalId, CancellationToken cancellationToken) => _inner.IsIncomeOfReceiptAsync(entryExternalId, cancellationToken);
        public Task<bool> IsReceivedExpenseAsync(Guid entryExternalId, CancellationToken cancellationToken) => _inner.IsReceivedExpenseAsync(entryExternalId, cancellationToken);
        public Task AddReceiptAsync(ReimbursementReceipt receipt, IReadOnlyList<ReimbursementReceiptItem> items, CancellationToken cancellationToken)
            => ++_added == 2 ? throw new InvalidOperationException("Falha simulada ao gravar o recebimento.") : _inner.AddReceiptAsync(receipt, items, cancellationToken);
    }
}
