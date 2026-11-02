using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

namespace Ags.Finance.Modules.Transactions.Infrastructure.Migrations;

[DbContext(typeof(TransactionsDbContext))]
[Migration("20261104120000_Classifications")]
internal sealed class Classifications : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(name: "categories", schema: "transactions", columns: table => new
        {
            Id = table.Column<long>(type: "bigint", nullable: false).Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
            ExternalId = table.Column<Guid>(type: "uuid", nullable: false),
            CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
            UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
            Name = table.Column<string>(type: "character varying(100)", nullable: false, maxLength: 100),
            Kind = table.Column<int>(type: "integer", nullable: false),
            IsActive = table.Column<bool>(type: "boolean", nullable: false),
        }, constraints: table => { table.PrimaryKey("PK_categories", x => x.Id); table.UniqueConstraint("AK_categories_ExternalId", x => x.ExternalId); });
        migrationBuilder.CreateIndex(name: "IX_categories_ExternalId", schema: "transactions", table: "categories", column: "ExternalId", unique: true);
        migrationBuilder.CreateTable(name: "subcategories", schema: "transactions", columns: table => new
        {
            Id = table.Column<long>(type: "bigint", nullable: false).Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
            ExternalId = table.Column<Guid>(type: "uuid", nullable: false),
            CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
            UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
            CategoryExternalId = table.Column<Guid>(type: "uuid", nullable: false),
            Name = table.Column<string>(type: "character varying(100)", nullable: false, maxLength: 100),
            NormalizedName = table.Column<string>(type: "character varying(100)", nullable: false, maxLength: 100),
            IsActive = table.Column<bool>(type: "boolean", nullable: false),
        }, constraints: table => { table.PrimaryKey("PK_subcategories", x => x.Id); table.ForeignKey("FK_subcategories_categories_CategoryExternalId", x => x.CategoryExternalId, principalSchema: "transactions", principalTable: "categories", principalColumn: "ExternalId", onDelete: ReferentialAction.Restrict); });
        migrationBuilder.CreateIndex(name: "IX_subcategories_ExternalId", schema: "transactions", table: "subcategories", column: "ExternalId", unique: true);
        migrationBuilder.CreateIndex(name: "IX_subcategories_CategoryExternalId_NormalizedName", schema: "transactions", table: "subcategories", columns: ["CategoryExternalId", "NormalizedName"], unique: true);
    }
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("subcategories", "transactions");
        migrationBuilder.DropTable("categories", "transactions");
    }
}
