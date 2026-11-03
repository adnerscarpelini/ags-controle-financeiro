using Ags.Finance.Modules.Transactions.Contracts;
using Ags.Finance.SharedKernel;

namespace Ags.Finance.Modules.Transactions.Tests;

public sealed partial class EntryTests
{
    [Fact]
    public async Task AdjustmentAndPurchasePaginationHasNoMissingOrDuplicateRowsAndRespectsFilters()
    {
        var identifiers = new HashSet<Guid>();
        for (var day = 1; day <= 30; day++)
        {
            var created = await Entries.CreateAsync(Request() with { Date = new DateOnly(2026, 10, day), Amount = new Money(1) });
            identifiers.Add(created.ExternalId!.Value);
        }
        var card = await CreateCardAsync();
        foreach (var total in new[] { 100, 102, 101 })
        {
            var created = await Entries.AdjustInvoiceAsync(new(Guid.CreateVersion7(), card, new ReferenceMonth(2026, 10), new Money(total)));
            Assert.True(created.Succeeded, created.Error);
            identifiers.Add(created.ExternalId!.Value);
        }
        var found = new List<Guid>();
        for (var page = 1; page <= 5; page++)
        {
            var result = await Entries.QueryAsync(new(Month: new ReferenceMonth(2026, 10), Page: page, PageSize: 7));
            Assert.Equal(33, result.TotalCount);
            Assert.Equal(new Money(-131), result.Expenses);
            Assert.Equal(Money.Zero, result.Income);
            found.AddRange(result.Entries.Select(x => x.ExternalId));
        }
        Assert.Equal(33, found.Count);
        Assert.True(identifiers.SetEquals(found));
        Assert.Equal(33, found.Distinct().Count());
        Assert.Equal(3, (await Entries.QueryAsync(new(Text: "ajuste de fatura", CardExternalId: card))).TotalCount);
        Assert.Equal(3, (await Entries.QueryAsync(new(Text: "AJUSTE-DE-FATURA", CardExternalId: card))).TotalCount);
        Assert.Equal(30, (await Entries.QueryAsync(new(SubcategoryExternalId: _subcategoryId))).TotalCount);
        Assert.Equal(0, (await Entries.QueryPendingAsync(new())).TotalCount);
        Assert.Equal(0, (await Entries.QueryAsync(new(Kind: CategoryKind.Income))).TotalCount);
    }
}
