using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

namespace Ags.Finance.Modules.Transactions.Infrastructure.Migrations;

[DbContext(typeof(TransactionsDbContext))]
[Migration("20261104150000_Entries")]
internal sealed class Entries : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddUniqueConstraint("AK_merchants_ExternalId", "merchants", "ExternalId", "transactions");
        migrationBuilder.CreateTable(name: "entries", schema: "transactions", columns: table => new
        {
            Id = table.Column<long>(type: "bigint", nullable: false).Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
            ExternalId = table.Column<Guid>(type: "uuid", nullable: false),
            CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
            UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
            Date = table.Column<DateOnly>(type: "date", nullable: false),
            Month = table.Column<int>(type: "integer", nullable: false),
            MerchantExternalId = table.Column<Guid>(type: "uuid", nullable: false),
            SubcategoryExternalId = table.Column<Guid>(type: "uuid", nullable: false),
            AccountExternalId = table.Column<Guid>(type: "uuid", nullable: false),
            Kind = table.Column<int>(type: "integer", nullable: false),
            Amount = table.Column<long>(type: "bigint", nullable: false),
            Details = table.Column<string>(type: "character varying(1000)", nullable: false, maxLength: 1000),
            IsEffective = table.Column<bool>(type: "boolean", nullable: false),
            EffectiveDate = table.Column<DateOnly>(type: "date", nullable: true),
            EffectiveAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
        }, constraints: table =>
        {
            table.PrimaryKey("PK_entries", x => x.Id);
            table.ForeignKey("FK_entries_merchants_MerchantExternalId", x => x.MerchantExternalId, principalSchema: "transactions", principalTable: "merchants", principalColumn: "ExternalId", onDelete: ReferentialAction.Restrict);
            table.ForeignKey("FK_entries_subcategories_SubcategoryExternalId", x => x.SubcategoryExternalId, principalSchema: "transactions", principalTable: "subcategories", principalColumn: "ExternalId", onDelete: ReferentialAction.Restrict);
        });
        migrationBuilder.CreateIndex(name: "IX_entries_ExternalId", schema: "transactions", table: "entries", column: "ExternalId", unique: true);
        migrationBuilder.CreateIndex(name: "IX_entries_Month", schema: "transactions", table: "entries", column: "Month");
        migrationBuilder.CreateIndex(name: "IX_entries_AccountExternalId_EffectiveDate", schema: "transactions", table: "entries", columns: ["AccountExternalId", "EffectiveDate"]);
        migrationBuilder.CreateIndex(name: "IX_entries_MerchantExternalId", schema: "transactions", table: "entries", column: "MerchantExternalId");
        migrationBuilder.CreateIndex(name: "IX_entries_SubcategoryExternalId", schema: "transactions", table: "entries", column: "SubcategoryExternalId");
    }
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("entries", "transactions");
        migrationBuilder.DropUniqueConstraint("AK_merchants_ExternalId", "merchants", "transactions");
    }
}
