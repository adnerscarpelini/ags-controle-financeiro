using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

namespace Ags.Finance.Modules.Invoices.Infrastructure.Migrations;

[DbContext(typeof(InvoicesDbContext))]
[Migration("20261104170000_InvoiceLifecycle")]
internal sealed class InvoiceLifecycle : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(name: "invoices", schema: "invoices", columns: table => new
        {
            Id = table.Column<long>(type: "bigint", nullable: false).Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
            ExternalId = table.Column<Guid>(type: "uuid", nullable: false),
            CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
            UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
            CardExternalId = table.Column<Guid>(type: "uuid", nullable: false),
            Month = table.Column<int>(type: "integer", nullable: false),
            Status = table.Column<int>(type: "integer", nullable: false),
            AutoCloseSuppressed = table.Column<bool>(type: "boolean", nullable: false),
            HasConfirmedPayment = table.Column<bool>(type: "boolean", nullable: false),
            ClosedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
        }, constraints: table => table.PrimaryKey("PK_invoices", x => x.Id));
        migrationBuilder.CreateIndex("IX_invoices_ExternalId", "invoices", "ExternalId", "invoices", unique: true);
        migrationBuilder.CreateIndex("IX_invoices_CardExternalId_Month", "invoices", ["CardExternalId", "Month"], "invoices", unique: true);
    }
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("invoices", "invoices");
    }
}
