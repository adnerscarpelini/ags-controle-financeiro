using Ags.Finance.Modules.Reimbursements.Contracts;
using Ags.Finance.Modules.Reimbursements.Domain;
using Ags.Finance.Modules.Reimbursements.Infrastructure;
using Ags.Finance.Modules.Transactions.Contracts;
using Ags.Finance.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Ags.Finance.Modules.Reimbursements.Tests;

public sealed class ReimbursementStatementTests : ReimbursementFixture
{
    private static readonly AccountingMonth s_october = new(2026, 10);
    private IReimbursementStatements Statements => Provider.GetRequiredService<IReimbursementStatements>();
    private ISharingRuleCatalog Rules => Provider.GetRequiredService<ISharingRuleCatalog>();

    private async Task<Guid> PersonAsync(string name = "Pessoa Exemplo") => (await Provider.GetRequiredService<IPersonCatalog>().SaveAsync(null, name, "")).ExternalId!.Value;

    private async Task RuleAsync(Guid person, Guid filter, decimal percent, AccountingMonth start)
        => Assert.True((await Rules.SaveAsync(null, person, filter, percent, start)).Succeeded);

    [Fact]
    public async Task HalfOfAnInternetBillIsDueForTheMonth()
    {
        var person = await PersonAsync();
        await ExpenseAsync(new DateOnly(2026, 10, 5), 142.90m);
        await RuleAsync(person, await SaveFilterAsync(), 50m, s_october);
        var statement = await Statements.GetAsync(person, s_october);
        Assert.Null(statement.Error);
        var item = Assert.Single(statement.Items);
        Assert.Equal((new Money(142.90m), 50m, new Money(71.45m)), (item.Total, item.Percent, item.Due));
        Assert.Equal((new Money(71.45m), Money.Zero, new Money(71.45m)), (statement.TotalDue, statement.TotalReceived, statement.Balance));
    }

    [Fact]
    public async Task FullAmountIsDueForEntriesOnACardFinal()
    {
        var person = await PersonAsync();
        var (card, final) = await CardWithFinalAsync();
        await ExpenseAsync(new DateOnly(2026, 10, 1), 100m, card: card, final: final);
        await ExpenseAsync(new DateOnly(2026, 10, 2), 200m, card: card, final: final);
        await ExpenseAsync(new DateOnly(2026, 10, 3), 80m);
        await RuleAsync(person, await SaveFilterAsync("Final 1234", SavedFilterCriteria.Empty with { CardLastDigitsExternalIds = [final] }), 100m, s_october);
        var statement = await Statements.GetAsync(person, s_october);
        Assert.Equal(new Money(300m), statement.TotalDue);
        Assert.Equal(2, statement.Items.Count);
    }

    [Fact]
    public async Task EntryMatchingTwoRulesIsCountedOnceByTheHigherPercent()
    {
        var person = await PersonAsync();
        await ExpenseAsync(new DateOnly(2026, 10, 5), 100m);
        await RuleAsync(person, await SaveFilterAsync("Casa"), 30m, s_october);
        await RuleAsync(person, await SaveFilterAsync("Internet", SavedFilterCriteria.Empty with { Text = "provedor" }), 60m, s_october);
        var item = Assert.Single((await Statements.GetAsync(person, s_october)).Items);
        Assert.Equal((60m, new Money(60m)), (item.Percent, item.Due));
    }

    [Fact]
    public async Task RuleStartingInNovemberDoesNotAffectOctoberAndANewerRuleOfTheSameFilterSupersedesTheOlder()
    {
        var person = await PersonAsync();
        await ExpenseAsync(new DateOnly(2026, 10, 5), 100m);
        await ExpenseAsync(new DateOnly(2026, 11, 5), 100m);
        var filter = await SaveFilterAsync();
        await RuleAsync(person, filter, 50m, new AccountingMonth(2026, 11));
        Assert.Equal(Money.Zero, (await Statements.GetAsync(person, s_october)).TotalDue);
        await RuleAsync(person, filter, 20m, new AccountingMonth(2026, 12));
        Assert.Equal(new Money(50m), (await Statements.GetAsync(person, new AccountingMonth(2026, 11))).TotalDue);
        await ExpenseAsync(new DateOnly(2026, 12, 5), 100m);
        Assert.Equal(new Money(20m), (await Statements.GetAsync(person, new AccountingMonth(2026, 12))).TotalDue);
    }

    [Fact]
    public async Task EditingTheSavedFilterChangesTheAmountDueEvenForPastMonths()
    {
        var person = await PersonAsync();
        await ExpenseAsync(new DateOnly(2026, 10, 5), 100m);
        var water = await SubcategoryAsync("Água");
        await ExpenseAsync(new DateOnly(2026, 10, 6), 40m, water);
        var filter = await SaveFilterAsync();
        await RuleAsync(person, filter, 50m, s_october);
        Assert.Equal(new Money(50m), (await Statements.GetAsync(person, s_october)).TotalDue);
        await Provider.GetRequiredService<ISavedFilterCatalog>().SaveAsync(filter, "Despesas da casa", SavedFilterCriteria.Empty with { SubcategoryExternalIds = [SubcategoryId, water] });
        Assert.Equal(new Money(70m), (await Statements.GetAsync(person, s_october)).TotalDue);
    }

    [Fact]
    public async Task ReceivedReimbursementsReduceTheBalanceAndExcessBecomesCredit()
    {
        var person = await PersonAsync();
        await ExpenseAsync(new DateOnly(2026, 10, 5), 1000m);
        await RuleAsync(person, await SaveFilterAsync(), 100m, s_october);
        await AddReceiptAsync(person, 200m);
        var partial = await Statements.GetAsync(person, s_october);
        Assert.Equal((new Money(1000m), new Money(200m), new Money(800m)), (partial.TotalDue, partial.TotalReceived, partial.Balance));
        Assert.False(partial.HasCredit);
        await AddReceiptAsync(person, 900m);
        var credit = await Statements.GetAsync(person, s_october);
        Assert.Equal(new Money(-100m), credit.Balance);
        Assert.True(credit.HasCredit);
        Assert.Equal(Money.Zero, (await Statements.GetAsync(person, new AccountingMonth(2026, 11))).TotalReceived);
    }

    [Fact]
    public async Task UnknownPersonOrInvalidMonthReturnsAnError()
    {
        Assert.NotNull((await Statements.GetAsync(Guid.NewGuid(), s_october)).Error);
        Assert.NotNull((await Statements.GetAsync(await PersonAsync(), default)).Error);
    }

    private async Task AddReceiptAsync(Guid person, decimal amount)
    {
        var receipt = ReimbursementReceipt.Create(person, Guid.NewGuid(), AccountId, new DateOnly(2026, 10, 20), s_october, new Money(amount));
        var factory = Provider.GetRequiredService<IDbContextFactory<ReimbursementsDbContext>>();
        await using var context = await factory.CreateDbContextAsync();
        context.Add(receipt!);
        await context.SaveChangesAsync();
    }
}
