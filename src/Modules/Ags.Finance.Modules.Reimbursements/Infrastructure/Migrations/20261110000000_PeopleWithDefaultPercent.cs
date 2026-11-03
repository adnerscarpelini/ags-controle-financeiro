using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Ags.Finance.Modules.Reimbursements.Infrastructure.Migrations;

/// <summary>Reembolso passa a ser indicado na despesa (#78). As regras e os recebimentos antigos são descartados, sem migração de dados.</summary>
[DbContext(typeof(ReimbursementsDbContext))]
[Migration("20261110000000_PeopleWithDefaultPercent")]
internal sealed class PeopleWithDefaultPercent : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("sharing_rules", "reimbursements");
        migrationBuilder.DropTable("receipts", "reimbursements");
        migrationBuilder.DropColumn("PayerDescription", "people", "reimbursements");
        migrationBuilder.AddColumn<decimal>("DefaultPercent", "people", type: "numeric(5,2)", precision: 5, scale: 2, nullable: false, defaultValue: 0m, schema: "reimbursements");
        migrationBuilder.AlterColumn<decimal>("DefaultPercent", "people", type: "numeric(5,2)", precision: 5, scale: 2, nullable: false, oldClrType: typeof(decimal), oldType: "numeric(5,2)", oldPrecision: 5, oldScale: 2, oldDefaultValue: 0m, schema: "reimbursements");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
        => throw new NotSupportedException("As regras de rateio e os recebimentos antigos foram descartados; recrie o banco de desenvolvimento.");
}
