using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

namespace Ags.Finance.Modules.Invoices.Infrastructure.Migrations;

[DbContext(typeof(InvoicesDbContext))]
[Migration("20261105030000_InstallmentAnticipations")]
internal sealed class InstallmentAnticipations : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(name: "installment_anticipations", schema: "invoices", columns: table => new
        {
            Id = table.Column<long>(type: "bigint", nullable: false).Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
            ExternalId = table.Column<Guid>(type: "uuid", nullable: false),
            CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
            UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
            ConfirmationId = table.Column<Guid>(type: "uuid", nullable: false),
            EntryExternalId = table.Column<Guid>(type: "uuid", nullable: false),
            PurchaseExternalId = table.Column<Guid>(type: "uuid", nullable: false),
            CardExternalId = table.Column<Guid>(type: "uuid", nullable: false),
            Month = table.Column<int>(type: "integer", nullable: false),
            Count = table.Column<int>(type: "integer", nullable: false),
            Total = table.Column<long>(type: "bigint", nullable: false),
            Discount = table.Column<long>(type: "bigint", nullable: false),
        }, constraints: table => table.PrimaryKey("PK_installment_anticipations", x => x.Id));
        migrationBuilder.CreateIndex("IX_installment_anticipations_ExternalId", "installment_anticipations", "ExternalId", "invoices", unique: true);
        migrationBuilder.CreateIndex("IX_installment_anticipations_ConfirmationId", "installment_anticipations", "ConfirmationId", "invoices", unique: true);
    }
    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropTable("installment_anticipations", "invoices");
}
