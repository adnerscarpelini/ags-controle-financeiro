using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

namespace Ags.Finance.Modules.Transactions.Infrastructure.Migrations;

[DbContext(typeof(TransactionsDbContext))]
[Migration("20261104220000_AccountMovements")]
internal sealed class AccountMovements : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(name: "account_movements", schema: "transactions", columns: table => new
        {
            Id = table.Column<long>(type: "bigint", nullable: false).Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
            ExternalId = table.Column<Guid>(type: "uuid", nullable: false),
            CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
            UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
            OperationExternalId = table.Column<Guid>(type: "uuid", nullable: false),
            AccountExternalId = table.Column<Guid>(type: "uuid", nullable: false),
            Date = table.Column<DateOnly>(type: "date", nullable: false),
            Amount = table.Column<long>(type: "bigint", nullable: false),
            Kind = table.Column<int>(type: "integer", nullable: false)
        }, constraints: table => table.PrimaryKey("PK_account_movements", x => x.Id));
        migrationBuilder.CreateIndex("IX_account_movements_ExternalId", "account_movements", "ExternalId", "transactions", unique: true);
        migrationBuilder.CreateIndex("IX_account_movements_OperationExternalId_AccountExternalId", "account_movements", ["OperationExternalId", "AccountExternalId"], "transactions", unique: true);
        migrationBuilder.CreateIndex("IX_account_movements_AccountExternalId_Date", "account_movements", ["AccountExternalId", "Date"], "transactions");
    }
    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropTable("account_movements", "transactions");
}
