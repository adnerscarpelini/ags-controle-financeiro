using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Ags.Finance.Modules.Transactions.Infrastructure.Migrations;

[DbContext(typeof(TransactionsDbContext))]
[Migration("20261104210000_RecurringChanges")]
internal sealed class RecurringChanges : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateOnly[]>("ExcludedMonths", "recurring_entries", "date[]", schema: "transactions", nullable: false, defaultValue: Array.Empty<DateOnly>());
        migrationBuilder.AddColumn<int>("AccountingMonthOffset", "recurring_entries", "integer", schema: "transactions", nullable: false, defaultValue: 0);
    }
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn("ExcludedMonths", "recurring_entries", "transactions");
        migrationBuilder.DropColumn("AccountingMonthOffset", "recurring_entries", "transactions");
    }
}
