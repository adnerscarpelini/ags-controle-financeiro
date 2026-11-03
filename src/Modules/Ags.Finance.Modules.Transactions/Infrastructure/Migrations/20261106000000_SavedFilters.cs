using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

namespace Ags.Finance.Modules.Transactions.Infrastructure.Migrations;

[DbContext(typeof(TransactionsDbContext))]
[Migration("20261106000000_SavedFilters")]
internal sealed class SavedFilters : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(name: "saved_filters", schema: "transactions", columns: table => new
        {
            Id = table.Column<long>(type: "bigint", nullable: false).Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
            ExternalId = table.Column<Guid>(type: "uuid", nullable: false),
            CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
            UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
            Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
            NormalizedName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
            CategoryExternalIds = table.Column<Guid[]>(type: "uuid[]", nullable: false),
            SubcategoryExternalIds = table.Column<Guid[]>(type: "uuid[]", nullable: false),
            MerchantExternalIds = table.Column<Guid[]>(type: "uuid[]", nullable: false),
            AccountExternalIds = table.Column<Guid[]>(type: "uuid[]", nullable: false),
            CardExternalIds = table.Column<Guid[]>(type: "uuid[]", nullable: false),
            CardLastDigitsExternalIds = table.Column<Guid[]>(type: "uuid[]", nullable: false),
            Text = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
            IsEffective = table.Column<bool>(type: "boolean", nullable: true),
        }, constraints: table => table.PrimaryKey("PK_saved_filters", x => x.Id));
        migrationBuilder.CreateIndex("IX_saved_filters_ExternalId", "saved_filters", "ExternalId", "transactions", unique: true);
        migrationBuilder.CreateIndex("IX_saved_filters_NormalizedName", "saved_filters", "NormalizedName", "transactions", unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropTable("saved_filters", "transactions");
}
