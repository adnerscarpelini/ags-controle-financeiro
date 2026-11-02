using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Ags.Finance.Modules.Transactions.Infrastructure.Migrations;

[DbContext(typeof(TransactionsDbContext))]
[Migration("20261105000000_RecurringDateOffset")]
internal sealed class RecurringDateOffset : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
        => migrationBuilder.AddColumn<int>("DateMonthOffset", "recurring_entries", "integer", schema: "transactions", nullable: false, defaultValue: 0);
    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.DropColumn("DateMonthOffset", "recurring_entries", "transactions");
}
