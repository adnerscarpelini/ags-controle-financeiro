using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Ags.Finance.Modules.Transactions.Infrastructure.Migrations;

[DbContext(typeof(TransactionsDbContext))]
[Migration("20261104160000_CardEntries")]
internal sealed class CardEntries : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>("CardExternalId", "entries", "uuid", schema: "transactions", nullable: true);
        migrationBuilder.AddColumn<Guid>("CardLastDigitsExternalId", "entries", "uuid", schema: "transactions", nullable: true);
        migrationBuilder.CreateIndex("IX_entries_CardExternalId_Month", "entries", ["CardExternalId", "Month"], "transactions");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex("IX_entries_CardExternalId_Month", "entries", "transactions");
        migrationBuilder.DropColumn("CardLastDigitsExternalId", "entries", "transactions");
        migrationBuilder.DropColumn("CardExternalId", "entries", "transactions");
    }
}
