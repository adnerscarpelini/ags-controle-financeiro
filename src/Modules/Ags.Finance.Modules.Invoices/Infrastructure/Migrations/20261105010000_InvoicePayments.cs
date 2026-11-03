using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

namespace Ags.Finance.Modules.Invoices.Infrastructure.Migrations;

[DbContext(typeof(InvoicesDbContext))]
[Migration("20261105010000_InvoicePayments")]
internal sealed class InvoicePayments : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddUniqueConstraint("AK_invoices_ExternalId", "invoices", "ExternalId", "invoices");
        migrationBuilder.CreateTable(name: "invoice_payments", schema: "invoices", columns: table => new
        {
            Id = table.Column<long>(type: "bigint", nullable: false).Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
            ExternalId = table.Column<Guid>(type: "uuid", nullable: false),
            CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
            UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
            ConfirmationId = table.Column<Guid>(type: "uuid", nullable: false),
            InvoiceExternalId = table.Column<Guid>(type: "uuid", nullable: false),
            Date = table.Column<DateOnly>(type: "date", nullable: false),
            Amount = table.Column<long>(type: "bigint", nullable: false),
            Kind = table.Column<int>(type: "integer", nullable: false),
            AccountExternalId = table.Column<Guid>(type: "uuid", nullable: true),
            Details = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false)
        }, constraints: table =>
        {
            table.PrimaryKey("PK_invoice_payments", x => x.Id);
            table.ForeignKey("FK_invoice_payments_invoices_InvoiceExternalId", x => x.InvoiceExternalId, principalSchema: "invoices", principalTable: "invoices", principalColumn: "ExternalId", onDelete: ReferentialAction.Restrict);
        });
        migrationBuilder.CreateIndex("IX_invoice_payments_ExternalId", "invoice_payments", "ExternalId", "invoices", unique: true);
        migrationBuilder.CreateIndex("IX_invoice_payments_ConfirmationId", "invoice_payments", "ConfirmationId", "invoices", unique: true);
        migrationBuilder.CreateIndex("IX_invoice_payments_InvoiceExternalId", "invoice_payments", "InvoiceExternalId", "invoices");
    }
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("invoice_payments", "invoices");
        migrationBuilder.DropUniqueConstraint("AK_invoices_ExternalId", "invoices", "invoices");
    }
}
