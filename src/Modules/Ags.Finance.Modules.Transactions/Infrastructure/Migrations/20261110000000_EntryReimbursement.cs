using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Ags.Finance.Modules.Transactions.Infrastructure.Migrations;

[DbContext(typeof(TransactionsDbContext))]
[Migration("20261110000000_EntryReimbursement")]
internal sealed class EntryReimbursement : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>("ReimbursementPersonExternalId", "entries", type: "uuid", nullable: true, schema: "transactions");
        migrationBuilder.AddColumn<decimal>("ReimbursementPercent", "entries", type: "numeric(5,2)", precision: 5, scale: 2, nullable: true, schema: "transactions");
        migrationBuilder.CreateIndex("IX_entries_ReimbursementPersonExternalId", "entries", "ReimbursementPersonExternalId", "transactions");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex("IX_entries_ReimbursementPersonExternalId", "entries", "transactions");
        migrationBuilder.DropColumn("ReimbursementPercent", "entries", "transactions");
        migrationBuilder.DropColumn("ReimbursementPersonExternalId", "entries", "transactions");
    }
}
