using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

namespace Ags.Finance.Modules.Transactions.Infrastructure.Migrations;

[DbContext(typeof(TransactionsDbContext))]
[Migration("20261104180000_Installments")]
internal sealed class Installments : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(name: "installment_purchases", schema: "transactions", columns: table => new
        {
            Id = table.Column<long>(type: "bigint", nullable: false).Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
            ExternalId = table.Column<Guid>(type: "uuid", nullable: false),
            CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
            UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
            CardExternalId = table.Column<Guid>(type: "uuid", nullable: false),
            FirstMonth = table.Column<int>(type: "integer", nullable: false),
            Total = table.Column<long>(type: "bigint", nullable: false),
            Count = table.Column<int>(type: "integer", nullable: false),
        }, constraints: table =>
        {
            table.PrimaryKey("PK_installment_purchases", x => x.Id);
            table.UniqueConstraint("AK_installment_purchases_ExternalId", x => x.ExternalId);
        });
        migrationBuilder.CreateIndex("IX_installment_purchases_ExternalId", "installment_purchases", "ExternalId", "transactions", unique: true);
        migrationBuilder.AddColumn<Guid>("InstallmentPurchaseExternalId", "entries", "uuid", schema: "transactions", nullable: true);
        migrationBuilder.AddColumn<int>("InstallmentNumber", "entries", "integer", schema: "transactions", nullable: true);
        migrationBuilder.CreateIndex("IX_entries_InstallmentPurchaseExternalId_InstallmentNumber", "entries", ["InstallmentPurchaseExternalId", "InstallmentNumber"], "transactions", unique: true);
        migrationBuilder.AddForeignKey("FK_entries_installment_purchases_InstallmentPurchaseExternalId", "entries", "InstallmentPurchaseExternalId", "installment_purchases", schema: "transactions", principalSchema: "transactions", principalColumn: "ExternalId", onDelete: ReferentialAction.Restrict);
    }
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey("FK_entries_installment_purchases_InstallmentPurchaseExternalId", "entries", "transactions");
        migrationBuilder.DropIndex("IX_entries_InstallmentPurchaseExternalId_InstallmentNumber", "entries", "transactions");
        migrationBuilder.DropColumn("InstallmentNumber", "entries", "transactions");
        migrationBuilder.DropColumn("InstallmentPurchaseExternalId", "entries", "transactions");
        migrationBuilder.DropTable("installment_purchases", "transactions");
    }
}
