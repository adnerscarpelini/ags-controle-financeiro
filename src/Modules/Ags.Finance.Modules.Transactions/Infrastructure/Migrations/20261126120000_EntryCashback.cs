using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Ags.Finance.Modules.Transactions.Infrastructure.Migrations;

[DbContext(typeof(TransactionsDbContext))]
[Migration("20261126120000_EntryCashback")]
internal sealed class EntryCashback : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Compras já lançadas ficam com 0%: o percentual da fatura Aberta atual é preenchido fora das migrations, após configurar os cartões.
        migrationBuilder.AddColumn<decimal>("CashbackPercent", "entries", "numeric(5,2)", schema: "transactions", precision: 5, scale: 2, nullable: false, defaultValue: 0m);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn("CashbackPercent", "entries", "transactions");
    }
}
