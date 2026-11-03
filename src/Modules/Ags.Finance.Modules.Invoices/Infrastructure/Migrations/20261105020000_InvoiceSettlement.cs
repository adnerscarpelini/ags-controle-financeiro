using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Ags.Finance.Modules.Invoices.Infrastructure.Migrations;

[DbContext(typeof(InvoicesDbContext))]
[Migration("20261105020000_InvoiceSettlement")]
internal sealed class InvoiceSettlement : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
        => migrationBuilder.AddColumn<DateTimeOffset>("PaidAt", "invoices", "timestamp with time zone", schema: "invoices", nullable: true);
    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.DropColumn("PaidAt", "invoices", "invoices");
}
