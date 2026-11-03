using Ags.Finance.Modules.Transactions.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ags.Finance.Modules.Transactions.Infrastructure;

internal sealed class MonthlyPlanConfiguration : IEntityTypeConfiguration<MonthlyPlan>
{
    public void Configure(EntityTypeBuilder<MonthlyPlan> builder)
    {
        builder.ToTable("monthly_plans");
        builder.HasIndex(x => x.Month).IsUnique();
        builder.Property(x => x.SavingsPercent).HasColumnType("numeric");
        builder.Ignore(x => x.SpendingBudget);
        builder.Ignore(x => x.SavingsAmount);
    }
}

internal sealed class MonthlyPlanCategoryConfiguration : IEntityTypeConfiguration<MonthlyPlanCategory>
{
    public void Configure(EntityTypeBuilder<MonthlyPlanCategory> builder)
    {
        builder.ToTable("monthly_plan_categories");
        builder.HasOne<MonthlyPlan>().WithMany().HasForeignKey(x => x.MonthlyPlanExternalId).HasPrincipalKey(x => x.ExternalId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Category>().WithMany().HasForeignKey(x => x.CategoryExternalId).HasPrincipalKey(x => x.ExternalId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.MonthlyPlanExternalId, x.CategoryExternalId }).IsUnique().HasDatabaseName("IX_monthly_plan_categories_Plan_Category");
    }
}
