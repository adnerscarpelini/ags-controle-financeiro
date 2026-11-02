using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

namespace Ags.Finance.Modules.Transactions.Infrastructure.Migrations;

[DbContext(typeof(TransactionsDbContext))]
[Migration("20261104130000_Merchants")]
internal sealed class Merchants : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddUniqueConstraint("AK_subcategories_ExternalId", "subcategories", "ExternalId", "transactions");
        migrationBuilder.CreateTable(name: "merchants", schema: "transactions", columns: table => new
        {
            Id = table.Column<long>(type: "bigint", nullable: false).Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
            ExternalId = table.Column<Guid>(type: "uuid", nullable: false),
            CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
            UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
            Name = table.Column<string>(type: "character varying(100)", nullable: false, maxLength: 100),
            NormalizedName = table.Column<string>(type: "character varying(100)", nullable: false, maxLength: 100),
            DefaultSubcategoryExternalId = table.Column<Guid>(type: "uuid", nullable: true),
            IsActive = table.Column<bool>(type: "boolean", nullable: false),
        }, constraints: table =>
        {
            table.PrimaryKey("PK_merchants", x => x.Id);
            table.ForeignKey("FK_merchants_subcategories_DefaultSubcategoryExternalId", x => x.DefaultSubcategoryExternalId, principalSchema: "transactions", principalTable: "subcategories", principalColumn: "ExternalId", onDelete: ReferentialAction.Restrict);
        });
        migrationBuilder.CreateIndex(name: "IX_merchants_ExternalId", schema: "transactions", table: "merchants", column: "ExternalId", unique: true);
        migrationBuilder.CreateIndex(name: "IX_merchants_NormalizedName", schema: "transactions", table: "merchants", column: "NormalizedName", unique: true);
        migrationBuilder.CreateIndex(name: "IX_merchants_DefaultSubcategoryExternalId", schema: "transactions", table: "merchants", column: "DefaultSubcategoryExternalId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("merchants", "transactions");
        migrationBuilder.DropUniqueConstraint("AK_subcategories_ExternalId", "subcategories", "transactions");
    }
}
