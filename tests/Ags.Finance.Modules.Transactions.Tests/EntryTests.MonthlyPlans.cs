using Ags.Finance.Modules.Transactions.Contracts;
using Ags.Finance.SharedKernel;
using Microsoft.Extensions.DependencyInjection;

namespace Ags.Finance.Modules.Transactions.Tests;

public sealed partial class EntryTests
{
    private IMonthlyPlanCatalog Plans => Provider.GetRequiredService<IMonthlyPlanCatalog>();
    private async Task<Guid> PlanCategoryAsync() => (await Provider.GetRequiredService<IClassificationCatalog>().GetSubcategoriesAsync()).Single(x => x.ExternalId == _subcategoryId).CategoryExternalId;

    [Fact]
    public async Task PreviewCalculatesBudgetAndCategorizationWithoutSavingAndCreateIsUniquePerMonth()
    {
        var month = new ReferenceMonth(2026, 11);
        var category = await PlanCategoryAsync();
        var request = new MonthlyPlanRequest(month, new Money(1000), Categories: [new(category, new Money(200))]);
        var preview = await Plans.PreviewAsync(request);
        Assert.True(preview.Succeeded, preview.Error);
        Assert.Equal((new Money(800), new Money(200), new Money(200), new Money(600)), (preview.Plan!.SpendingBudget, preview.Plan.SavingsAmount, preview.Plan.CategorizedTotal, preview.Plan.UnclassifiedBudget));
        Assert.Null(await Plans.GetAsync(month));
        Assert.True((await Plans.CreateAsync(request)).Succeeded);
        Assert.False((await Plans.CreateAsync(request)).Succeeded);
        Assert.Equal(20m, (await Plans.GetAsync(month))!.SavingsPercent);
        Assert.True((await Plans.SetCategoryAsync(month, category, new Money(900))).Succeeded);
        Assert.Equal(new Money(-100), (await Plans.GetAsync(month))!.UnclassifiedBudget);
        Assert.True((await Plans.RemoveCategoryAsync(month, category)).Succeeded);
        Assert.Empty((await Plans.GetAsync(month))!.Categories);
    }

    [Fact]
    public async Task CopyAcrossYearKeepsAllSettingsAndEditingOrDeletingTheCopyDoesNotChangeTheSource()
    {
        var category = await PlanCategoryAsync();
        var december = new ReferenceMonth(2026, 12);
        var january = new ReferenceMonth(2027, 1);
        Assert.False((await Plans.CopyPreviousAsync(december)).Succeeded);
        Assert.Null(await Plans.GetAsync(december));
        Assert.True((await Plans.CreateAsync(new(december, new Money(1000), 30m, MonthlyPlanCardView.PurchaseDate, [new(category, new Money(200))]))).Succeeded);
        Assert.True((await Plans.CopyPreviousAsync(january)).Succeeded);
        var copy = (await Plans.GetAsync(january))!;
        var source = (await Plans.GetAsync(december))!;
        Assert.NotEqual(source.ExternalId, copy.ExternalId);
        Assert.Equal((source.Income, source.SavingsPercent, source.CardView, source.CategorizedTotal), (copy.Income, copy.SavingsPercent, copy.CardView, copy.CategorizedTotal));
        Assert.True((await Plans.EditAsync(new(january, new Money(2000), 50m, MonthlyPlanCardView.InvoiceDate))).Succeeded);
        Assert.Equal(new Money(200), Assert.Single((await Plans.GetAsync(january))!.Categories).Target);
        Assert.True((await Plans.SetCategoryAsync(january, category, new Money(300))).Succeeded);
        Assert.Equal(new Money(200), Assert.Single((await Plans.GetAsync(december))!.Categories).Target);
        Assert.True((await Plans.DeleteAsync(january)).Succeeded);
        Assert.Null(await Plans.GetAsync(january));
        Assert.NotNull(await Plans.GetAsync(december));
    }

    [Theory]
    [InlineData(-1, 20, 0)]
    [InlineData(1000, -1, 0)]
    [InlineData(1000, 101, 0)]
    [InlineData(1000, 20, -1)]
    public async Task InvalidIncomePercentOrTargetDoesNotCreateAnything(int income, int percent, int target)
    {
        var month = new ReferenceMonth(2026, 11);
        var category = await PlanCategoryAsync();
        Assert.False((await Plans.CreateAsync(new(month, new Money(income), percent, Categories: [new(category, new Money(target))]))).Succeeded);
        Assert.Null(await Plans.GetAsync(month));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(100)]
    public async Task SavingsPercentLimitsAndZeroIncomeAreAllowed(int percent)
    {
        var month = new ReferenceMonth(2026, 11);
        Assert.True((await Plans.CreateAsync(new(month, Money.Zero, percent))).Succeeded);
        var plan = (await Plans.GetAsync(month))!;
        Assert.Equal(Money.Zero, plan.SpendingBudget);
        Assert.Equal(Money.Zero, plan.SavingsAmount);
    }

    [Fact]
    public async Task RoundingPreservesIncomeAndInvalidEditLeavesThePreviousPlanIntact()
    {
        var month = new ReferenceMonth(2026, 11);
        var category = await PlanCategoryAsync();
        Assert.True((await Plans.CreateAsync(new(month, new Money(0.01m), 50m))).Succeeded);
        var plan = (await Plans.GetAsync(month))!;
        Assert.Equal(new Money(0.01m), plan.SpendingBudget);
        Assert.Equal(Money.Zero, plan.SavingsAmount);
        Assert.Equal(plan.Income, plan.SpendingBudget + plan.SavingsAmount);
        Assert.False((await Plans.EditAsync(new(month, new Money(2000), Categories: [new(category, new Money(-1))]))).Succeeded);
        var unchanged = (await Plans.GetAsync(month))!;
        Assert.Equal((plan.ExternalId, plan.Income, plan.SavingsPercent, plan.SpendingBudget), (unchanged.ExternalId, unchanged.Income, unchanged.SavingsPercent, unchanged.SpendingBudget));
        Assert.Empty(unchanged.Categories);
        Assert.False((await Plans.SetCategoryAsync(month, Guid.NewGuid(), new Money(100))).Succeeded);
        Assert.False((await Plans.CreateAsync(new(new ReferenceMonth(2026, 12), new Money(1000), Categories: [new(category, new Money(100)), new(category, new Money(100))]))).Succeeded);
        Assert.False((await Plans.CopyPreviousAsync(new ReferenceMonth(1, 1))).Succeeded);
    }

    [Fact]
    public async Task FailureWhileWritingCategoryRollsBackTheParentPlan()
    {
        var month = new ReferenceMonth(2026, 11);
        var category = await PlanCategoryAsync();
        await (_database ?? throw new InvalidOperationException()).ExecuteAsync("ALTER TABLE transactions.monthly_plan_categories ADD CONSTRAINT test_reject_plan CHECK (\"Target\" = 0)");
        await Assert.ThrowsAsync<Microsoft.EntityFrameworkCore.DbUpdateException>(() => Plans.CreateAsync(new(month, new Money(1000), Categories: [new(category, new Money(200))])));
        Assert.Null(await Plans.GetAsync(month));
    }
    [Fact]
    public async Task MissingPlansInvalidEnumsAndStorageLimitsReturnErrorsWithoutPartialWrites()
    {
        var month = new ReferenceMonth(2026, 11);
        var category = await PlanCategoryAsync();
        Assert.False((await Plans.EditAsync(new(month, Money.Zero))).Succeeded);
        Assert.False((await Plans.SetCategoryAsync(month, category, Money.Zero)).Succeeded);
        Assert.False((await Plans.DeleteAsync(month)).Succeeded);
        Assert.False((await Plans.CreateAsync(new(default, Money.Zero))).Succeeded);
        Assert.False((await Plans.CreateAsync(new(month, Money.Zero, CardView: (MonthlyPlanCardView)99))).Succeeded);
        Assert.False((await Plans.CreateAsync(new(month, new Money(92233720368547758.08m)))).Succeeded);
        Assert.False((await Plans.CreateAsync(new(month, Money.Zero, Categories: [new(category, new Money(92233720368547758.08m))]))).Succeeded);
        Assert.Null(await Plans.GetAsync(month));
        Assert.True((await Plans.CreateAsync(new(month, new Money(92233720368547758.07m), 0m, Categories: [new(category, new Money(92233720368547758.07m))]))).Succeeded);
        var plan = (await Plans.GetAsync(month))!;
        Assert.Equal(plan.Income, plan.SpendingBudget);
        Assert.Equal(Money.Zero, plan.SavingsAmount);
        Assert.True((await Plans.EditAsync(new(month, plan.Income, 100m))).Succeeded);
        plan = (await Plans.GetAsync(month))!;
        Assert.Equal(Money.Zero, plan.SpendingBudget);
        Assert.Equal(plan.Income, plan.SavingsAmount);
        Assert.True((await Plans.CopyPreviousAsync(month.AddMonths(1))).Succeeded);
        Assert.False((await Plans.CopyPreviousAsync(month.AddMonths(1))).Succeeded);
    }
}
