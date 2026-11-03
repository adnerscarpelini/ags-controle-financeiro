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
    private IReimbursementReceipts Receipts => Provider.GetRequiredService<IReimbursementReceipts>();
    private IReimbursementStatements Statements => Provider.GetRequiredService<IReimbursementStatements>();
    private IEntryCatalog Entries => Provider.GetRequiredService<IEntryCatalog>();

    private async Task<Guid> PersonWithDebtAsync(decimal debt = 1000m)
    {
        var person = (await Provider.GetRequiredService<IPersonCatalog>().SaveAsync(null, "Maria Exemplo", "Maria Pagadora")).ExternalId!.Value;
        await ExpenseAsync(new DateOnly(2026, 10, 5), debt);
        Assert.True((await Provider.GetRequiredService<ISharingRuleCatalog>().SaveAsync(null, person, await SaveFilterAsync(), 100m, s_october)).Succeeded);
        return person;
    }

    private async Task<Money> BalanceAsync() => (await Entries.GetBalanceAsync(AccountId, new DateOnly(2026, 12, 31)))!.EffectiveBalance;

    [Fact]
    public async Task FullReceiptZeroesTheBalanceAndCreatesOneEffectiveIncomeCreditedOnce()
    {
        var person = await PersonWithDebtAsync();
        var before = await BalanceAsync();
        var result = await Receipts.RegisterAsync(person, s_day, new Money(1000m), AccountId);
        Assert.True(result.Succeeded, result.Error);
        var statement = await Statements.GetAsync(person, s_october);
        Assert.Equal((new Money(1000m), new Money(1000m), Money.Zero), (statement.TotalDue, statement.TotalReceived, statement.Balance));
        var income = Assert.Single((await Entries.QueryAsync(new(Kind: CategoryKind.Income))).Entries);
        Assert.Equal((s_day, s_october, new Money(1000m), true, "Maria Pagadora", "Reembolsos > Reembolsos recebidos"), (income.Date, income.Month, income.Amount, income.IsEffective, income.MerchantName, income.Classification));
        Assert.Equal(s_day, income.EffectiveDate);
        Assert.Equal(before + new Money(1000m), await BalanceAsync());
        Assert.Empty((await Entries.QueryPendingAsync(new())).Entries);
        Assert.Equal(income.ExternalId, Assert.Single(statement.Receipts).EntryExternalId);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public async Task ZeroOrNegativeAmountIsRejectedWithoutCreatingAnything(int amount)
    {
        var person = await PersonWithDebtAsync();
        var before = await BalanceAsync();
        Assert.False((await Receipts.RegisterAsync(person, s_day, new Money(amount), AccountId)).Succeeded);
        Assert.Empty((await Entries.QueryAsync(new(Kind: CategoryKind.Income))).Entries);
        Assert.Equal(before, await BalanceAsync());
    }

    [Fact]
    public async Task ReceivingMoreThanDueLeavesNegativeBalanceAsCredit()
    {
        var person = await PersonWithDebtAsync(300m);
        Assert.True((await Receipts.RegisterAsync(person, s_day, new Money(350m), AccountId)).Succeeded);
        var statement = await Statements.GetAsync(person, s_october);
        Assert.Equal(new Money(-50m), statement.Balance);
        Assert.True(statement.HasCredit);
    }

    [Fact]
    public async Task SecondReceiptReusesTheCategoryAndThePayerDescription()
    {
        var person = await PersonWithDebtAsync();
        Assert.True((await Receipts.RegisterAsync(person, s_day, new Money(100m), AccountId)).Succeeded);
        Assert.True((await Receipts.RegisterAsync(person, s_day.AddDays(1), new Money(200m), AccountId)).Succeeded);
        Assert.Single(await Provider.GetRequiredService<IClassificationCatalog>().GetCategoriesAsync(true), x => x.Name == "Reembolsos");
        Assert.Single(await Provider.GetRequiredService<IMerchantCatalog>().FindAsync("Maria Pagadora", false));
        Assert.Equal(new Money(300m), (await Statements.GetAsync(person, s_october)).TotalReceived);
    }

    [Fact]
    public async Task UnknownPersonOrAccountIsRejectedAndAPersonWithReceiptsCannotBeDeleted()
    {
        var person = await PersonWithDebtAsync();
        Assert.False((await Receipts.RegisterAsync(Guid.NewGuid(), s_day, new Money(10m), AccountId)).Succeeded);
        Assert.False((await Receipts.RegisterAsync(person, s_day, new Money(10m), Guid.NewGuid())).Succeeded);
        Assert.True((await Receipts.RegisterAsync(person, s_day, new Money(10m), AccountId)).Succeeded);
        Assert.False((await Provider.GetRequiredService<IPersonCatalog>().DeleteAsync(person)).Succeeded);
    }

    [Fact]
    public async Task FailureAfterTheIncomeWasCreatedRollsTheIncomeBack()
    {
        var person = await PersonWithDebtAsync();
        var before = await BalanceAsync();
        await using var failing = Build(new ServiceCollection()).AddScoped<IReimbursementStore>(provider => new FailingStore(provider.GetRequiredService<IDbContextFactory<ReimbursementsDbContext>>())).BuildServiceProvider();
        await using var scope = failing.CreateAsyncScope();
        await Assert.ThrowsAsync<InvalidOperationException>(() => scope.ServiceProvider.GetRequiredService<IReimbursementReceipts>().RegisterAsync(person, s_day, new Money(500m), AccountId));
        Assert.Empty((await Entries.QueryAsync(new(Kind: CategoryKind.Income))).Entries);
        Assert.Equal(before, await BalanceAsync());
        Assert.Equal(Money.Zero, (await Statements.GetAsync(person, s_october)).TotalReceived);
    }

    private sealed class FailingStore(IDbContextFactory<ReimbursementsDbContext> factory) : IReimbursementStore
    {
        private readonly ReimbursementStore _inner = new(factory);
        public Task<IReadOnlyList<Person>> GetPeopleAsync(CancellationToken cancellationToken) => _inner.GetPeopleAsync(cancellationToken);
        public Task<bool> SavePersonAsync(Person person, CancellationToken cancellationToken) => _inner.SavePersonAsync(person, cancellationToken);
        public Task DeletePersonAsync(Person person, CancellationToken cancellationToken) => _inner.DeletePersonAsync(person, cancellationToken);
        public Task<IReadOnlyList<SharingRule>> GetRulesAsync(CancellationToken cancellationToken) => _inner.GetRulesAsync(cancellationToken);
        public Task<bool> SaveRuleAsync(SharingRule rule, CancellationToken cancellationToken) => _inner.SaveRuleAsync(rule, cancellationToken);
        public Task DeleteRuleAsync(SharingRule rule, CancellationToken cancellationToken) => _inner.DeleteRuleAsync(rule, cancellationToken);
        public Task<IReadOnlyList<ReimbursementReceipt>> GetReceiptsAsync(Guid personExternalId, CancellationToken cancellationToken) => _inner.GetReceiptsAsync(personExternalId, cancellationToken);
        public Task AddReceiptAsync(ReimbursementReceipt receipt, CancellationToken cancellationToken) => throw new InvalidOperationException("Falha simulada ao gravar o vínculo.");
    }
}
