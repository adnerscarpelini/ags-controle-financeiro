using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Ags.Finance.Modules.Transactions.Infrastructure.Migrations;

[DbContext(typeof(TransactionsDbContext))]
[Migration("20261110010000_RecurringReimbursement")]
internal sealed class RecurringReimbursement : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>("ReimbursementPersonExternalId", "recurring_entries", type: "uuid", nullable: true, schema: "transactions");
        migrationBuilder.AddColumn<decimal>("ReimbursementPercent", "recurring_entries", type: "numeric(5,2)", precision: 5, scale: 2, nullable: true, schema: "transactions");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn("ReimbursementPercent", "recurring_entries", "transactions");
        migrationBuilder.DropColumn("ReimbursementPersonExternalId", "recurring_entries", "transactions");
    }
}
