using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

namespace Ags.Finance.Modules.Invoices.Infrastructure.Migrations;

[DbContext(typeof(InvoicesDbContext))]
[Migration("20261126120000_CashbackReceipts")]
internal sealed class CashbackReceipts : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "cashback_receipts",
            schema: "invoices",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false).Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                ExternalId = table.Column<Guid>(type: "uuid", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                ConfirmationId = table.Column<Guid>(type: "uuid", nullable: false),
                InvoiceExternalId = table.Column<Guid>(type: "uuid", nullable: false),
                Date = table.Column<DateOnly>(type: "date", nullable: false),
                Expected = table.Column<long>(type: "bigint", nullable: false),
                Received = table.Column<long>(type: "bigint", nullable: false),
                Destination = table.Column<int>(type: "integer", nullable: false),
                AccountExternalId = table.Column<Guid>(type: "uuid", nullable: true),
                IncomeEntryExternalId = table.Column<Guid>(type: "uuid", nullable: true),
                TargetInvoiceExternalId = table.Column<Guid>(type: "uuid", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_cashback_receipts", x => x.Id);
                table.ForeignKey("FK_cashback_receipts_invoices_InvoiceExternalId", x => x.InvoiceExternalId, principalSchema: "invoices", principalTable: "invoices", principalColumn: "ExternalId", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_cashback_receipts_invoices_TargetInvoiceExternalId", x => x.TargetInvoiceExternalId, principalSchema: "invoices", principalTable: "invoices", principalColumn: "ExternalId", onDelete: ReferentialAction.Restrict);
            });
        migrationBuilder.CreateIndex("IX_cashback_receipts_ExternalId", "cashback_receipts", "ExternalId", schema: "invoices", unique: true);
        migrationBuilder.CreateIndex("IX_cashback_receipts_ConfirmationId", "cashback_receipts", "ConfirmationId", schema: "invoices", unique: true);
        migrationBuilder.CreateIndex("IX_cashback_receipts_InvoiceExternalId", "cashback_receipts", "InvoiceExternalId", schema: "invoices", unique: true);
        migrationBuilder.CreateIndex("IX_cashback_receipts_TargetInvoiceExternalId", "cashback_receipts", "TargetInvoiceExternalId", schema: "invoices");

        migrationBuilder.AddColumn<Guid>("CashbackReceiptExternalId", "invoice_payments", "uuid", schema: "invoices", nullable: true);
        migrationBuilder.CreateIndex("IX_invoice_payments_CashbackReceiptExternalId", "invoice_payments", "CashbackReceiptExternalId", schema: "invoices", unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex("IX_invoice_payments_CashbackReceiptExternalId", "invoice_payments", "invoices");
        migrationBuilder.DropColumn("CashbackReceiptExternalId", "invoice_payments", "invoices");
        migrationBuilder.DropTable("cashback_receipts", "invoices");
    }
}
