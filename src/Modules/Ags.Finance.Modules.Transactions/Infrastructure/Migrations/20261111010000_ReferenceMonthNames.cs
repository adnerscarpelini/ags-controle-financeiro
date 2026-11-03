using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Ags.Finance.Modules.Transactions.Infrastructure.Migrations;

[DbContext(typeof(TransactionsDbContext))]
[Migration("20261111010000_ReferenceMonthNames")]
internal sealed class ReferenceMonthNames : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
        => migrationBuilder.RenameColumn(name: "AccountingMonthOffset", schema: "transactions", table: "recurring_entries", newName: "ReferenceMonthOffset");
    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.RenameColumn(name: "ReferenceMonthOffset", schema: "transactions", table: "recurring_entries", newName: "AccountingMonthOffset");
}
