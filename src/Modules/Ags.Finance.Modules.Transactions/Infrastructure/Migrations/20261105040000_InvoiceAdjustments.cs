using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

namespace Ags.Finance.Modules.Transactions.Infrastructure.Migrations;

[DbContext(typeof(TransactionsDbContext))]
[Migration("20261105040000_InvoiceAdjustments")]
internal sealed class InvoiceAdjustments : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(name: "invoice_adjustments", schema: "transactions", columns: table => new
        {
            Id = table.Column<long>(type: "bigint", nullable: false).Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
            ExternalId = table.Column<Guid>(type: "uuid", nullable: false),
            CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
            UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
            ConfirmationId = table.Column<Guid>(type: "uuid", nullable: false),
            CardExternalId = table.Column<Guid>(type: "uuid", nullable: false),
            AccountExternalId = table.Column<Guid>(type: "uuid", nullable: false),
            Month = table.Column<int>(type: "integer", nullable: false),
            Date = table.Column<DateOnly>(type: "date", nullable: false),
            ExpenseDifference = table.Column<long>(type: "bigint", nullable: false),
            CorrectTotal = table.Column<long>(type: "bigint", nullable: false),
        }, constraints: table => table.PrimaryKey("PK_invoice_adjustments", x => x.Id));
        migrationBuilder.CreateIndex("IX_invoice_adjustments_ExternalId", "invoice_adjustments", "ExternalId", "transactions", unique: true);
        migrationBuilder.CreateIndex("IX_invoice_adjustments_ConfirmationId", "invoice_adjustments", "ConfirmationId", "transactions", unique: true);
        migrationBuilder.CreateIndex("IX_invoice_adjustments_CardExternalId_Month", "invoice_adjustments", ["CardExternalId", "Month"], "transactions");
    }
    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropTable("invoice_adjustments", "transactions");
}
