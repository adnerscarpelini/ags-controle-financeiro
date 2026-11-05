using Ags.Finance.Modules.Accounts.Contracts;
using Ags.Finance.Modules.Transactions.Contracts;
using Ags.Finance.SharedKernel;
using Microsoft.Extensions.DependencyInjection;

namespace Ags.Finance.Modules.Transactions.Tests;

public sealed partial class EntryTests
{
    private static readonly ReferenceMonth s_groupMonth = new(2026, 10);

    private async Task<Guid> CreateSecondCardAsync()
    {
        var result = await Provider.GetRequiredService<IAccountCatalog>().SaveCreditCardAsync(new(null, _accountId, "Cartão Segundo", CardBrand.Visa, new Money(10000), 3, 10, true, "green"));
        Assert.True(result.Succeeded, result.Error);
        return result.ExternalId!.Value;
    }

    private async Task AddCardEntriesAsync(Guid card, int count, decimal amount)
    {
        for (var day = 1; day <= count; day++)
        {
            var created = await Entries.CreateAsync(Request() with { CardExternalId = card, Date = new DateOnly(2026, 9, day + 4), Amount = new Money(amount) });
            Assert.True(created.Succeeded, created.Error);
        }
    }

    [Fact]
    public async Task GroupingSumsEachCardInOneRowAndKeepsTheOtherEntriesAndTheTotals()
    {
        var card = await CreateCardAsync();
        await AddCardEntriesAsync(card, 10, 10);
        await Entries.CreateAsync(Request() with { Date = new DateOnly(2026, 10, 20), Amount = new Money(30) });
        var plain = await Entries.QueryAsync(new(Month: s_groupMonth));

        var grouped = await Entries.QueryAsync(new(Month: s_groupMonth, GroupCardEntries: true));

        Assert.Equal(11, plain.TotalCount);
        Assert.Equal(2, grouped.TotalCount);
        Assert.Equal((plain.Income, plain.Expenses, plain.Balance), (grouped.Income, grouped.Expenses, grouped.Balance));
        var group = Assert.Single(grouped.Rows, x => x.CardGroup is not null).CardGroup!;
        Assert.Equal((card, "Cartão Exemplo", s_groupMonth, new DateOnly(2026, 9, 14), new Money(-100), 10),
            (group.CardExternalId, group.CardName, group.Month, group.LastDate, group.Amount, group.EntryCount));
        Assert.Equal(new Money(-30), Assert.Single(grouped.Entries).Amount);
    }

    [Fact]
    public async Task GroupingSeparatesCardsAndInvoiceMonths()
    {
        var first = await CreateCardAsync();
        var second = await CreateSecondCardAsync();
        await AddCardEntriesAsync(first, 2, 10);
        await AddCardEntriesAsync(second, 3, 10);
        Assert.True((await Entries.CreateAsync(Request() with { CardExternalId = first, Date = new DateOnly(2026, 10, 5), Amount = new Money(7) })).Succeeded);

        var grouped = await Entries.QueryAsync(new(GroupCardEntries: true));

        var groups = grouped.Rows.Select(x => x.CardGroup!).OrderBy(x => x.CardName).ThenBy(x => x.Month).ToList();
        Assert.Equal(
            [(first, new ReferenceMonth(2026, 10), new Money(-20)), (first, new ReferenceMonth(2026, 11), new Money(-7)), (second, new ReferenceMonth(2026, 10), new Money(-30))],
            groups.Select(x => (x.CardExternalId, x.Month, x.Amount)));
    }

    [Fact]
    public async Task TheGroupSumDoesNotDependOnThePageAndPagesHaveNoMissingRows()
    {
        var card = await CreateCardAsync();
        await AddCardEntriesAsync(card, 5, 10);
        for (var day = 1; day <= 5; day++) { await Entries.CreateAsync(Request() with { Date = new DateOnly(2026, 10, day), Amount = new Money(1) }); }

        var first = await Entries.QueryAsync(new(Month: s_groupMonth, GroupCardEntries: true, Page: 1, PageSize: 4));
        var second = await Entries.QueryAsync(new(Month: s_groupMonth, GroupCardEntries: true, Page: 2, PageSize: 4));

        Assert.Equal(6, first.TotalCount);
        Assert.Equal(2, first.TotalPages);
        Assert.Equal((4, 2), (first.Rows.Count, second.Rows.Count));
        Assert.Equal(new Money(-50), second.Rows[^1].CardGroup!.Amount);
    }

    [Fact]
    public async Task AnInvoiceAdjustmentEntersTheGroupOfItsCard()
    {
        var card = await CreateCardAsync();
        await AddCardEntriesAsync(card, 2, 50);
        Assert.True((await Entries.AdjustInvoiceAsync(new(Guid.CreateVersion7(), card, s_groupMonth, new Money(130)))).Succeeded);

        var grouped = await Entries.QueryAsync(new(Month: s_groupMonth, GroupCardEntries: true));

        var group = Assert.Single(grouped.Rows).CardGroup!;
        Assert.Equal((new Money(-130), 3), (group.Amount, group.EntryCount));
        Assert.Equal(new Money(-130), grouped.Expenses);
    }

    [Fact]
    public async Task FiltersAndSortingApplyBeforeAndAfterGrouping()
    {
        var card = await CreateCardAsync();
        await AddCardEntriesAsync(card, 2, 10);
        await Entries.CreateAsync(Request() with { Date = new DateOnly(2026, 10, 20), Amount = new Money(5) });
        await Entries.CreateAsync(Request() with { Date = new DateOnly(2026, 10, 21), Amount = new Money(50) });

        var byAmount = await Entries.QueryAsync(new(Month: s_groupMonth, GroupCardEntries: true, SortColumn: EntrySortColumn.Amount, SortDescending: false));
        var onlyCard = await Entries.QueryAsync(new(Month: s_groupMonth, GroupCardEntries: true, CardExternalId: card));

        Assert.Equal([new Money(-50), new Money(-20), new Money(-5)], byAmount.Rows.Select(x => x.CardGroup?.Amount ?? x.Entry!.Amount));
        Assert.Equal(new Money(-20), Assert.Single(onlyCard.Rows).CardGroup!.Amount);
    }

    [Fact]
    public async Task WithoutGroupingEveryEntryIsItsOwnRow()
    {
        var card = await CreateCardAsync();
        await AddCardEntriesAsync(card, 2, 10);

        var plain = await Entries.QueryAsync(new(Month: s_groupMonth));

        Assert.Null(plain.GroupedRows);
        Assert.Equal(plain.Entries, plain.Rows.Select(x => x.Entry));
    }

    [Fact]
    public async Task ThePendingViewIgnoresGroupingBecauseCardEntriesAreNotPending()
    {
        await Entries.CreateAsync(Request() with { Date = new DateOnly(2026, 10, 20), IsPaid = false });

        var pending = await Entries.QueryPendingAsync(new(Month: s_groupMonth, GroupCardEntries: true));

        Assert.Null(pending.GroupedRows);
        Assert.Single(pending.Rows);
    }
}
