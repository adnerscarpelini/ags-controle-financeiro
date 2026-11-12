using Ags.Finance.Modules.Accounts.Contracts;
using Ags.Finance.Modules.Transactions.Contracts;
using Ags.Finance.SharedKernel;
using Microsoft.Extensions.DependencyInjection;

namespace Ags.Finance.Modules.Transactions.Tests;

/// <summary>Transferências e ajustes de saldo na consulta de lançamentos (#141).</summary>
public sealed partial class EntryTests
{
    private static readonly ReferenceMonth s_movementMonth = new(2026, 11);

    private static EntryFilter MovementFilter(int pageSize = 100) => new(Month: s_movementMonth, PageSize: pageSize, IncludeAccountMovements: true);

    private static List<EntryMovement> MovementsOf(EntryQueryResult result) => result.Rows.Select(x => x.Movement).OfType<EntryMovement>().ToList();

    /// <summary>Uma despesa de R$ 150 e um resgate de R$ 100 da Poupança Exemplo para a Conta Exemplo, ambos em novembro.</summary>
    private async Task<Guid> RedemptionScenarioAsync()
    {
        Assert.True((await Entries.CreateAsync(Request() with { Date = new DateOnly(2026, 11, 1) })).Succeeded);
        var savings = await DestinationAsync(AccountKind.Savings);
        Assert.True((await Movements.TransferAsync(new(Guid.CreateVersion7(), savings, _accountId, new DateOnly(2026, 11, 10), new Money(100)))).Succeeded);
        return savings;
    }

    [Fact]
    public async Task TransferAppearsAsTwoLegsAndLeavesTheTotalsUnchanged()
    {
        var savings = await RedemptionScenarioAsync();
        var plain = await Entries.QueryAsync(new(Month: s_movementMonth));

        var result = await Entries.QueryAsync(MovementFilter());

        Assert.Null(result.Error);
        Assert.Equal(3, result.TotalCount);
        Assert.Single(result.Entries);
        var legs = MovementsOf(result);
        var outgoing = Assert.Single(legs, x => x.Kind == EntryMovementKind.TransferOut);
        var incoming = Assert.Single(legs, x => x.Kind == EntryMovementKind.TransferIn);
        Assert.Equal((savings, new Money(-100), "Transferência para Conta Exemplo"), (outgoing.AccountExternalId, outgoing.Amount, outgoing.Description));
        Assert.Equal((_accountId, new Money(100), "Transferência de Reserva Exemplo"), (incoming.AccountExternalId, incoming.Amount, incoming.Description));
        Assert.All(legs, x => Assert.True(x.IsEffective));
        Assert.All(legs, x => Assert.Equal("Transferência", x.Classification));
        Assert.Equal((plain.Income, plain.Expenses, plain.Balance), (result.Income, result.Expenses, result.Balance));
        Assert.Equal(new DateOnly(2026, 11, 10), result.Rows[0].Movement?.Date);
    }

    [Fact]
    public async Task OtherConsumersOfTheQueryNeverReceiveMovements()
    {
        await RedemptionScenarioAsync();

        var result = await Entries.QueryAsync(new(Month: s_movementMonth));

        Assert.Equal(1, result.TotalCount);
        Assert.Empty(MovementsOf(result));
    }

    [Fact]
    public async Task MovementsOnlyListsTransfersAndBalanceAdjustmentsWithZeroTotals()
    {
        await RedemptionScenarioAsync();
        var date = new DateOnly(2026, 11, 12);
        var before = (await Entries.GetBalanceAsync(_accountId, date))?.EffectiveBalance ?? throw new InvalidOperationException();
        Assert.True((await Movements.AdjustBalanceAsync(new(Guid.CreateVersion7(), _accountId, date, new Money(1000)))).Succeeded);

        var result = await Entries.QueryAsync(MovementFilter() with { AccountMovementsOnly = true });

        Assert.Empty(result.Entries);
        Assert.Equal(3, result.TotalCount);
        var adjustment = Assert.Single(MovementsOf(result), x => x.Kind == EntryMovementKind.BalanceAdjustment);
        Assert.Equal((_accountId, new Money(1000) - before, "Ajuste de saldo"), (adjustment.AccountExternalId, adjustment.Amount, adjustment.Classification));
        Assert.Equal((Money.Zero, Money.Zero, Money.Zero), (result.Income, result.Expenses, result.Balance));
    }

    [Fact]
    public async Task AccountFilterKeepsOnlyTheLegOfThatAccount()
    {
        var savings = await RedemptionScenarioAsync();

        var savingsOnly = await Entries.QueryAsync(MovementFilter() with { AccountExternalId = savings });

        var leg = Assert.Single(savingsOnly.Rows);
        Assert.Equal(EntryMovementKind.TransferOut, leg.Movement?.Kind);
    }

    [Fact]
    public async Task EntryOnlyFiltersHideTheMovements()
    {
        await RedemptionScenarioAsync();
        var category = Assert.Single(await Provider.GetRequiredService<IClassificationCatalog>().GetCategoriesAsync(false));

        foreach (var filter in new[] { MovementFilter() with { Kind = CategoryKind.Expense }, MovementFilter() with { CategoryExternalId = category.ExternalId },
            MovementFilter() with { SubcategoryExternalId = _subcategoryId }, MovementFilter() with { CardExternalId = Guid.NewGuid() } })
        {
            Assert.Empty(MovementsOf(await Entries.QueryAsync(filter)));
        }
    }

    [Fact]
    public async Task TextSearchFindsTheMovementsIgnoringAccents()
    {
        await RedemptionScenarioAsync();

        var result = await Entries.QueryAsync(MovementFilter() with { Text = "transferencia de reserva" });

        Assert.Equal(EntryMovementKind.TransferIn, Assert.Single(result.Rows).Movement?.Kind);
    }

    [Fact]
    public async Task PendingRecurringTransferAppearsAsPendingLegsUntilConfirmed()
    {
        var savings = await DestinationAsync(AccountKind.Savings);
        Assert.True((await Transfers.CreateAsync(new(savings, _accountId, new DateOnly(2026, 11, 20), new Money(80), null, false))).Succeeded);
        Assert.True((await Movements.TransferAsync(new(Guid.CreateVersion7(), savings, _accountId, new DateOnly(2026, 11, 10), new Money(100)))).Succeeded);

        var pending = await Entries.QueryPendingAsync(MovementFilter());

        var legs = MovementsOf(pending);
        Assert.Equal(2, legs.Count);
        Assert.All(legs, x => Assert.False(x.IsEffective));
        Assert.All(legs, x => Assert.Equal("Recorrente", x.Details));
        var occurrence = legs[0].RecurringOccurrence ?? throw new InvalidOperationException();
        Assert.True((await Transfers.ConfirmAsync(occurrence.ExternalId, occurrence.Date, occurrence.Amount)).Succeeded);
        Assert.Empty(MovementsOf(await Entries.QueryPendingAsync(MovementFilter())));
        var confirmed = MovementsOf(await Entries.QueryAsync(MovementFilter() with { IsEffective = true })).Where(x => x.RecurringOccurrence is not null).ToList();
        Assert.Equal(2, confirmed.Count);
        Assert.All(confirmed, x => Assert.True(x.RecurringOccurrence?.IsConfirmed));
    }

    [Fact]
    public async Task PagesInterleaveMovementsAndEntriesByDateWithoutLosingRows()
    {
        foreach (var day in new[] { 3, 9, 15 })
        {
            Assert.True((await Entries.CreateAsync(Request() with { Date = new DateOnly(2026, 11, day) })).Succeeded);
        }

        var savings = await DestinationAsync(AccountKind.Savings);
        Assert.True((await Movements.TransferAsync(new(Guid.CreateVersion7(), savings, _accountId, new DateOnly(2026, 11, 10), new Money(100)))).Succeeded);

        var first = await Entries.QueryAsync(MovementFilter(pageSize: 2));
        var rows = new List<EntryRow>(first.Rows);
        for (var page = 2; page <= first.TotalPages; page++)
        {
            rows.AddRange((await Entries.QueryAsync(MovementFilter(pageSize: 2) with { Page = page })).Rows);
        }

        Assert.Equal(5, first.TotalCount);
        Assert.Equal([15, 10, 10, 9, 3], rows.Select(x => (x.Entry?.Date ?? x.Movement?.Date ?? default).Day));
    }

    [Fact]
    public async Task CardGroupingKeepsTheMovementsAsTheirOwnRows()
    {
        await RedemptionScenarioAsync();

        var result = await Entries.QueryAsync(MovementFilter() with { GroupCardEntries = true });

        Assert.Equal(3, result.TotalCount);
        Assert.Equal(2, MovementsOf(result).Count);
    }
}
