using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Ags.Finance.Modules.Accounts.Infrastructure.Migrations;

[DbContext(typeof(AccountsDbContext))]
[Migration("20261126120000_CreditCardCashback")]
internal sealed class CreditCardCashback : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<decimal>("CashbackPercent", "credit_cards", "numeric(5,2)", schema: "accounts", precision: 5, scale: 2, nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn("CashbackPercent", "credit_cards", "accounts");
    }
}
