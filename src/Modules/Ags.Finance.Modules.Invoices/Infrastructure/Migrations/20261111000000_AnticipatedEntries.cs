using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Ags.Finance.Modules.Invoices.Infrastructure.Migrations;

[DbContext(typeof(InvoicesDbContext))]
[Migration("20261111000000_AnticipatedEntries")]
internal sealed class AnticipatedEntries : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
        => migrationBuilder.AddColumn<Guid[]>(name: "EntryExternalIds", schema: "invoices", table: "installment_anticipations", type: "uuid[]", nullable: false, defaultValue: Array.Empty<Guid>());
    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.DropColumn(name: "EntryExternalIds", schema: "invoices", table: "installment_anticipations");
}
