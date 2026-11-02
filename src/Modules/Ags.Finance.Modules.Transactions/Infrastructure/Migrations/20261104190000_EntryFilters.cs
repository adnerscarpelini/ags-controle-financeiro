using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Ags.Finance.Modules.Transactions.Infrastructure.Migrations;

[DbContext(typeof(TransactionsDbContext))]
[Migration("20261104190000_EntryFilters")]
internal sealed class EntryFilters : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>("NormalizedDetails", "entries", "text", schema: "transactions", nullable: true);
        migrationBuilder.CreateIndex("IX_entries_Date_Id", "entries", ["Date", "Id"], "transactions");
    }
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex("IX_entries_Date_Id", "entries", "transactions");
        migrationBuilder.DropColumn("NormalizedDetails", "entries", "transactions");
    }
}
