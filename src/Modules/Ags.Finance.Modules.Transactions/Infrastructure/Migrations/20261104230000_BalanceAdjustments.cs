using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Ags.Finance.Modules.Transactions.Infrastructure.Migrations;

[DbContext(typeof(TransactionsDbContext))]
[Migration("20261104230000_BalanceAdjustments")]
internal sealed class BalanceAdjustments : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
        => migrationBuilder.AddColumn<long>("TargetBalance", "account_movements", "bigint", schema: "transactions", nullable: false, defaultValue: 0L);
    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.DropColumn("TargetBalance", "account_movements", "transactions");
}
