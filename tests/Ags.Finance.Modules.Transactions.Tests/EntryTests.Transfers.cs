using Ags.Finance.Modules.Accounts.Contracts;
using Ags.Finance.Modules.Transactions.Contracts;
using Ags.Finance.SharedKernel;
using Microsoft.Extensions.DependencyInjection;

namespace Ags.Finance.Modules.Transactions.Tests;

public sealed partial class EntryTests
{
    private IAccountMovementCatalog Movements => Provider.GetRequiredService<IAccountMovementCatalog>();
    private async Task<Guid> DestinationAsync(AccountKind kind = AccountKind.Investment, decimal opening = 200)
    {
        var accounts = Provider.GetRequiredService<IAccountCatalog>();
        var bank = Assert.Single(await accounts.GetBanksAsync());
        var result = await accounts.SaveAccountAsync(new(null, bank.ExternalId, "Reserva Exemplo", kind, "green", new Money(opening), new DateOnly(2026, 10, 1)));
        Assert.True(result.Succeeded, result.Error);
        return result.ExternalId!.Value;
    }

    [Theory]
    [InlineData(AccountKind.Investment)]
    [InlineData(AccountKind.Savings)]
    public async Task TransferHasTwoAtomicLegsPreservesTotalAndDoesNotEnterBudget(AccountKind kind)
    {
        var destination = await DestinationAsync(kind);
        var request = new TransferRequest(Guid.CreateVersion7(), _accountId, destination, new DateOnly(2026, 11, 1), new Money(300));
        var results = await Task.WhenAll(Movements.TransferAsync(request), Movements.TransferAsync(request));
        Assert.All(results, x => Assert.True(x.Succeeded, x.Error));
        Assert.Equal(new Money(700), (await Entries.GetBalanceAsync(_accountId, request.Date))?.EffectiveBalance);
        Assert.Equal(new Money(500), (await Entries.GetBalanceAsync(destination, request.Date))?.EffectiveBalance);
        var legs = await Movements.GetAsync();
        Assert.Equal(2, legs.Count);
        Assert.All(legs, x => Assert.Equal(request.ConfirmationId, x.OperationExternalId));
        Assert.Equal(0, legs.Sum(x => x.Amount.Amount));
        Assert.Empty(await Entries.GetAsync());
        var budget = await Entries.QueryAsync(new(Month: new ReferenceMonth(2026, 11)));
        Assert.Equal(Money.Zero, budget.Income);
        Assert.Equal(Money.Zero, budget.Expenses);
        Assert.False((await Movements.TransferAsync(request with { Amount = new Money(301) })).Succeeded);
    }

    [Fact]
    public async Task TransferPermitsNegativeSourceAndKeepsEarlierBalanceUnchanged()
    {
        var destination = await DestinationAsync();
        var date = new DateOnly(2026, 11, 1);
        Assert.True((await Movements.TransferAsync(new(Guid.CreateVersion7(), _accountId, destination, date, new Money(1150)))).Succeeded);
        Assert.Equal(new Money(-150), (await Entries.GetBalanceAsync(_accountId, date))?.EffectiveBalance);
        Assert.Equal(new Money(1350), (await Entries.GetBalanceAsync(destination, date))?.EffectiveBalance);
        Assert.Equal(new Money(1000), (await Entries.GetBalanceAsync(_accountId, date.AddDays(-1)))?.EffectiveBalance);
    }

    [Theory]
    [InlineData(AccountKind.Investment)]
    [InlineData(AccountKind.Savings)]
    public async Task RedemptionFromInvestmentOrSavingsMovesBothBalancesAndStaysOutOfTheBudget(AccountKind kind)
    {
        var reserve = await DestinationAsync(kind);
        var date = new DateOnly(2026, 11, 1);

        var result = await Movements.TransferAsync(new(Guid.CreateVersion7(), reserve, _accountId, date, new Money(150)));

        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(new Money(50), (await Entries.GetBalanceAsync(reserve, date))?.EffectiveBalance);
        Assert.Equal(new Money(1150), (await Entries.GetBalanceAsync(_accountId, date))?.EffectiveBalance);
        var budget = await Entries.QueryAsync(new(Month: new ReferenceMonth(2026, 11)));
        Assert.Equal(Money.Zero, budget.Income);
        Assert.Equal(Money.Zero, budget.Expenses);
    }

    [Fact]
    public async Task InvalidOrInactiveAccountsNeverProduceOneLeg()
    {
        var destination = await DestinationAsync();
        var request = new TransferRequest(Guid.CreateVersion7(), _accountId, destination, new DateOnly(2026, 11, 1), new Money(300));
        foreach (var invalid in new[] { request with { Amount = Money.Zero }, request with { Amount = new Money(-1) }, request with { DestinationAccountExternalId = _accountId }, request with { SourceAccountExternalId = Guid.NewGuid() } })
        {
            Assert.False((await Movements.TransferAsync(invalid)).Succeeded);
        }
        await Provider.GetRequiredService<IAccountCatalog>().DeactivateAccountAsync(destination);
        Assert.False((await Movements.TransferAsync(request)).Succeeded);
        Assert.False((await Movements.TransferAsync(request with { SourceAccountExternalId = destination, DestinationAccountExternalId = _accountId })).Succeeded);
        Assert.Empty(await Movements.GetAsync());
    }

    [Fact]
    public async Task FailureOnIncomingLegRollsBackTheOutgoingLeg()
    {
        var destination = await DestinationAsync();
        await (_database ?? throw new InvalidOperationException()).ExecuteAsync("ALTER TABLE transactions.account_movements ADD CONSTRAINT test_reject_incoming CHECK (\"Amount\" < 0)");
        await Assert.ThrowsAsync<Microsoft.EntityFrameworkCore.DbUpdateException>(() => Movements.TransferAsync(new(Guid.CreateVersion7(), _accountId, destination, new DateOnly(2026, 11, 1), new Money(300))));
        Assert.Empty(await Movements.GetAsync());
        Assert.Equal(new Money(1000), (await Entries.GetBalanceAsync(_accountId, new DateOnly(2026, 11, 1)))?.EffectiveBalance);
    }
}
