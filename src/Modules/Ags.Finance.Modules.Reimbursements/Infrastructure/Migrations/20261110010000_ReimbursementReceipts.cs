using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

namespace Ags.Finance.Modules.Reimbursements.Infrastructure.Migrations;

[DbContext(typeof(ReimbursementsDbContext))]
[Migration("20261110010000_ReimbursementReceipts")]
internal sealed class ReimbursementReceipts : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(name: "reimbursement_receipts", schema: "reimbursements", columns: table => new
        {
            Id = table.Column<long>(type: "bigint", nullable: false).Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
            ExternalId = table.Column<Guid>(type: "uuid", nullable: false),
            CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
            UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
            PersonExternalId = table.Column<Guid>(type: "uuid", nullable: false),
            Date = table.Column<DateOnly>(type: "date", nullable: false),
            AccountExternalId = table.Column<Guid>(type: "uuid", nullable: true),
            IncomeEntryExternalId = table.Column<Guid>(type: "uuid", nullable: true),
            Total = table.Column<long>(type: "bigint", nullable: false),
        }, constraints: table =>
        {
            table.PrimaryKey("PK_reimbursement_receipts", x => x.Id);
            table.UniqueConstraint("AK_reimbursement_receipts_ExternalId", x => x.ExternalId);
            table.ForeignKey("FK_reimbursement_receipts_people_PersonExternalId", x => x.PersonExternalId, "people", "ExternalId", "reimbursements", onDelete: ReferentialAction.Restrict);
        });
        migrationBuilder.CreateIndex("IX_reimbursement_receipts_ExternalId", "reimbursement_receipts", "ExternalId", "reimbursements", unique: true);
        migrationBuilder.CreateIndex("IX_reimbursement_receipts_PersonExternalId", "reimbursement_receipts", "PersonExternalId", "reimbursements");
        migrationBuilder.CreateIndex("IX_reimbursement_receipts_IncomeEntryExternalId", "reimbursement_receipts", "IncomeEntryExternalId", "reimbursements", unique: true);
        migrationBuilder.CreateTable(name: "reimbursement_receipt_items", schema: "reimbursements", columns: table => new
        {
            Id = table.Column<long>(type: "bigint", nullable: false).Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
            ExternalId = table.Column<Guid>(type: "uuid", nullable: false),
            CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
            UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
            ReceiptExternalId = table.Column<Guid>(type: "uuid", nullable: false),
            ExpenseEntryExternalId = table.Column<Guid>(type: "uuid", nullable: false),
            ExpenseAmount = table.Column<long>(type: "bigint", nullable: false),
            Percent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
            Expected = table.Column<long>(type: "bigint", nullable: false),
            Received = table.Column<long>(type: "bigint", nullable: false),
        }, constraints: table =>
        {
            table.PrimaryKey("PK_reimbursement_receipt_items", x => x.Id);
            table.ForeignKey("FK_reimbursement_receipt_items_reimbursement_receipts_ReceiptExternalId", x => x.ReceiptExternalId, "reimbursement_receipts", "ExternalId", "reimbursements", onDelete: ReferentialAction.Cascade);
        });
        migrationBuilder.CreateIndex("IX_reimbursement_receipt_items_ExternalId", "reimbursement_receipt_items", "ExternalId", "reimbursements", unique: true);
        migrationBuilder.CreateIndex("IX_reimbursement_receipt_items_ReceiptExternalId", "reimbursement_receipt_items", "ReceiptExternalId", "reimbursements");
        migrationBuilder.CreateIndex("IX_reimbursement_receipt_items_ExpenseEntryExternalId", "reimbursement_receipt_items", "ExpenseEntryExternalId", "reimbursements", unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("reimbursement_receipt_items", "reimbursements");
        migrationBuilder.DropTable("reimbursement_receipts", "reimbursements");
    }
}
