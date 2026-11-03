using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Ags.Finance.Modules.Invoices.Infrastructure.Migrations;

[DbContext(typeof(InvoicesDbContext))]
[Migration("20261111030000_UndoneInvoicePayments")]
internal sealed class UndoneInvoicePayments : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
        => migrationBuilder.AddColumn<DateTimeOffset>("UndoneAt", "invoice_payments", "timestamp with time zone", schema: "invoices", nullable: true);
    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.DropColumn("UndoneAt", "invoice_payments", "invoices");
}
