using System.Diagnostics;
using Ags.Finance.Modules.Accounts.Contracts;
using Ags.Finance.Modules.Transactions.Contracts;
using Ags.Finance.Modules.Transactions.Domain;
using Ags.Finance.Modules.Transactions.Infrastructure;
using Ags.Finance.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using CategoryKind = Ags.Finance.Modules.Transactions.Contracts.CategoryKind;

namespace Ags.Finance.Modules.Transactions.Tests;

public sealed partial class EntryTests
{
    private readonly Xunit.Abstractions.ITestOutputHelper _output;
    public EntryTests(Xunit.Abstractions.ITestOutputHelper output) => _output = output;

    private async Task<Guid> IncomeSubcategoryAsync()
    {
        var catalog = Provider.GetRequiredService<IClassificationCatalog>();
        var category = await catalog.SaveCategoryAsync(null, "Receitas", CategoryKind.Income);
        return (await catalog.SaveSubcategoryAsync(null, category.ExternalId!.Value, "Recebimentos")).ExternalId!.Value;
    }

    [Fact]
    public async Task MonthFilterTotalsOnlyMatchingEntriesAndPreservesSignedBalance()
    {
        await Entries.CreateAsync(Request());
        await Entries.CreateAsync(Request() with { Amount = new Money(75), Month = new AccountingMonth(2026, 11) });
        await Entries.CreateAsync(Request() with { SubcategoryExternalId = await IncomeSubcategoryAsync(), Amount = new Money(500) });
        var result = await Entries.QueryAsync(new(Month: new AccountingMonth(2026, 10)));
        Assert.Equal(2, result.TotalCount);
        Assert.Equal(new Money(500), result.Income);
        Assert.Equal(new Money(-150), result.Expenses);
        Assert.Equal(new Money(350), result.Balance);
        Assert.All(result.Entries, x => Assert.Equal(new AccountingMonth(2026, 10), x.Month));
    }

    [Fact]
    public async Task CombinedCardFinalCategoryAccountAndDateFiltersUseIntersection()
    {
        var card = await CreateCardAsync();
        var final = await Provider.GetRequiredService<ICardLastDigitsCatalog>().SaveAsync(new(null, card, "1234", CardUsage.Own, "Próprio"));
        await Entries.CreateAsync(Request() with { CardExternalId = card, CardLastDigitsExternalId = final.ExternalId });
        await Entries.CreateAsync(Request());
        var subcategory = (await Provider.GetRequiredService<IClassificationCatalog>().GetSubcategoriesAsync()).Single();
        var filter = new EntryFilter(FromDate: new DateOnly(2026, 10, 1), ToDate: new DateOnly(2026, 10, 31), AccountExternalId: _accountId,
            CardExternalId: card, CardLastDigitsExternalId: final.ExternalId, CategoryExternalId: subcategory.CategoryExternalId, SubcategoryExternalId: _subcategoryId);
        Assert.Single((await Entries.QueryAsync(filter)).Entries);
        Assert.Empty((await Entries.QueryAsync(filter with { CategoryExternalId = Guid.NewGuid() })).Entries);
        Assert.Empty((await Entries.QueryAsync(filter with { ToDate = new DateOnly(2026, 10, 4) })).Entries);
    }

    [Fact]
    public async Task TextSearchIgnoresCaseAndAccentsInDescriptionOrDetailsIncludingLegacyRows()
    {
        var merchant = await Provider.GetRequiredService<IMerchantCatalog>().SaveAsync(null, "Padaria São Exemplo", null);
        var saved = await Entries.CreateAsync(Request() with { MerchantExternalId = merchant.ExternalId!.Value, Details = "Entrega de pão e café *extra*" });
        Assert.Single((await Entries.QueryAsync(new(Text: "PADARIA SAO"))).Entries);
        Assert.Single((await Entries.QueryAsync(new(Text: "PAO E CAFE"))).Entries);
        Assert.Single((await Entries.QueryAsync(new(Text: "*"))).Entries);
        using var scope = Provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TransactionsDbContext>();
        await context.Database.ExecuteSqlInterpolatedAsync($"UPDATE transactions.entries SET \"NormalizedDetails\" = NULL WHERE \"ExternalId\" = {saved.ExternalId}");
        Assert.Single((await Entries.QueryAsync(new(Text: "pao e cafe"))).Entries);
        Assert.Null((await context.Set<TransactionEntry>().AsNoTracking().SingleAsync()).NormalizedDetails);
    }

    [Fact]
    public async Task PaginationDoesNotLimitTotalsAndEffectiveFilterComposesWithOtherFields()
    {
        for (var index = 0; index < 5; index++) { await Entries.CreateAsync(Request()); }
        var first = await Entries.QueryAsync(new(PageSize: 2, IsEffective: true));
        Assert.Equal(2, first.Entries.Count);
        Assert.Equal(5, first.TotalCount);
        Assert.Equal(new Money(-750), first.Expenses);
        var last = await Entries.QueryAsync(new(Page: int.MaxValue, PageSize: 2));
        Assert.Equal(3, last.Page);
        Assert.Single(last.Entries);
        Assert.Equal(first.Expenses, last.Expenses);
        Assert.Empty((await Entries.QueryAsync(new(IsEffective: false))).Entries);
        Assert.NotNull((await Entries.QueryAsync(new(FromDate: new DateOnly(2026, 11, 1), ToDate: new DateOnly(2026, 10, 1)))).Error);
    }

    [Fact]
    public async Task TenThousandEntriesFilterReturnsPageAndFullTotalsUnderOneSecond()
    {
        using (var scope = Provider.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<TransactionsDbContext>();
            for (var index = 0; index < 10000; index++)
            {
                var entry = new TransactionEntry();
                Assert.Null(entry.Update(new DateOnly(2026, 10, 5), new AccountingMonth(2026, index % 2 == 0 ? 10 : 11), _merchantId, _subcategoryId,
                    _accountId, Domain.CategoryKind.Expense, new Money(1), index % 2 == 0 ? "Filtro Exemplo" : "Outro Exemplo"));
                entry.SetEffective(entry.Date, _clock.GetUtcNow());
                context.Add(entry);
            }
            await context.SaveChangesAsync();
        }
        await Entries.QueryAsync(new());
        var watch = Stopwatch.StartNew();
        var result = await Entries.QueryAsync(new(Month: new AccountingMonth(2026, 10), Text: "filtro"));
        watch.Stop();
        _output.WriteLine($"Consulta de 10.000 lançamentos: {watch.Elapsed.TotalMilliseconds:F1} ms.");
        Assert.Equal(5000, result.TotalCount);
        Assert.Equal(100, result.Entries.Count);
        Assert.Equal(new Money(-5000), result.Expenses);
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(1), $"A consulta levou {watch.Elapsed.TotalMilliseconds:F1} ms.");
    }
}
