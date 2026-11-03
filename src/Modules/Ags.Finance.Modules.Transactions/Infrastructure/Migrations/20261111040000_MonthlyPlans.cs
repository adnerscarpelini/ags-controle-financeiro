using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

namespace Ags.Finance.Modules.Transactions.Infrastructure.Migrations;

[DbContext(typeof(TransactionsDbContext))]
[Migration("20261111040000_MonthlyPlans")]
internal sealed class MonthlyPlans : Migration
{
    private static readonly string[] s_categoryIndex = ["MonthlyPlanExternalId", "CategoryExternalId"];
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(name: "monthly_plans", schema: "transactions", columns: table => new
        {
            Id = table.Column<long>(type: "bigint", nullable: false).Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
            ExternalId = table.Column<Guid>(type: "uuid", nullable: false),
            CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
            UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
            Month = table.Column<int>(type: "integer", nullable: false),
            Income = table.Column<long>(type: "bigint", nullable: false),
            SavingsPercent = table.Column<decimal>(type: "numeric", nullable: false),
            CardView = table.Column<int>(type: "integer", nullable: false)
        }, constraints: table => { table.PrimaryKey("PK_monthly_plans", x => x.Id); table.UniqueConstraint("AK_monthly_plans_ExternalId", x => x.ExternalId); });
        migrationBuilder.CreateIndex("IX_monthly_plans_ExternalId", "monthly_plans", "ExternalId", "transactions", unique: true);
        migrationBuilder.CreateIndex("IX_monthly_plans_Month", "monthly_plans", "Month", "transactions", unique: true);
        migrationBuilder.CreateTable(name: "monthly_plan_categories", schema: "transactions", columns: table => new
        {
            Id = table.Column<long>(type: "bigint", nullable: false).Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
            ExternalId = table.Column<Guid>(type: "uuid", nullable: false),
            CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
            UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
            MonthlyPlanExternalId = table.Column<Guid>(type: "uuid", nullable: false),
            CategoryExternalId = table.Column<Guid>(type: "uuid", nullable: false),
            Target = table.Column<long>(type: "bigint", nullable: false)
        }, constraints: table =>
        {
            table.PrimaryKey("PK_monthly_plan_categories", x => x.Id);
            table.ForeignKey("FK_monthly_plan_categories_monthly_plans_MonthlyPlanExternalId", x => x.MonthlyPlanExternalId, principalSchema: "transactions", principalTable: "monthly_plans", principalColumn: "ExternalId", onDelete: ReferentialAction.Cascade);
            table.ForeignKey("FK_monthly_plan_categories_categories_CategoryExternalId", x => x.CategoryExternalId, principalSchema: "transactions", principalTable: "categories", principalColumn: "ExternalId", onDelete: ReferentialAction.Restrict);
        });
        migrationBuilder.CreateIndex("IX_monthly_plan_categories_ExternalId", "monthly_plan_categories", "ExternalId", "transactions", unique: true);
        migrationBuilder.CreateIndex("IX_monthly_plan_categories_CategoryExternalId", "monthly_plan_categories", "CategoryExternalId", "transactions");
        migrationBuilder.CreateIndex("IX_monthly_plan_categories_Plan_Category", "monthly_plan_categories", s_categoryIndex, "transactions", unique: true);
    }
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("monthly_plan_categories", "transactions");
        migrationBuilder.DropTable("monthly_plans", "transactions");
    }
}
