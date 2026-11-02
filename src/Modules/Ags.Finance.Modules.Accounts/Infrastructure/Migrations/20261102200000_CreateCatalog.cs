using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Ags.Finance.Modules.Accounts.Infrastructure.Migrations;

[DbContext(typeof(AccountsDbContext))]
[Migration("20261102200000_CreateCatalog")]
internal sealed class CreateCatalog : Migration
{
    private static readonly string[] s_accountNameIndex = ["BankExternalId", "NormalizedName"];
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "accounts_banks",
            columns: table => new
            {
                Id = table.Column<long>(type: "INTEGER", nullable: false).Annotation("Sqlite:Autoincrement", true),
                Name = table.Column<string>(type: "TEXT", nullable: false, maxLength: 100),
                ExternalId = table.Column<Guid>(type: "TEXT", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
            },
            constraints: table => table.PrimaryKey("PK_accounts_banks", x => x.Id));
        migrationBuilder.CreateIndex("IX_accounts_banks_ExternalId", "accounts_banks", "ExternalId", unique: true);
        migrationBuilder.CreateTable(
            name: "accounts_accounts",
            columns: table => new
            {
                Id = table.Column<long>(type: "INTEGER", nullable: false).Annotation("Sqlite:Autoincrement", true),
                BankExternalId = table.Column<Guid>(type: "TEXT", nullable: false),
                Name = table.Column<string>(type: "TEXT", nullable: false, maxLength: 100),
                NormalizedName = table.Column<string>(type: "TEXT", nullable: false, maxLength: 100),
                Kind = table.Column<int>(type: "INTEGER", nullable: false),
                Color = table.Column<string>(type: "TEXT", nullable: false, maxLength: 20),
                IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                ExternalId = table.Column<Guid>(type: "TEXT", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
            },
            constraints: table => table.PrimaryKey("PK_accounts_accounts", x => x.Id));
        migrationBuilder.CreateIndex("IX_accounts_accounts_ExternalId", "accounts_accounts", "ExternalId", unique: true);
        migrationBuilder.CreateTable(
            name: "accounts_credit_cards",
            columns: table => new
            {
                Id = table.Column<long>(type: "INTEGER", nullable: false).Annotation("Sqlite:Autoincrement", true),
                AccountExternalId = table.Column<Guid>(type: "TEXT", nullable: false),
                Name = table.Column<string>(type: "TEXT", nullable: false, maxLength: 100),
                Brand = table.Column<int>(type: "INTEGER", nullable: false),
                CreditLimit = table.Column<long>(type: "INTEGER", nullable: false),
                ClosingDay = table.Column<int>(type: "INTEGER", nullable: false),
                DueDay = table.Column<int>(type: "INTEGER", nullable: false),
                IncludesClosingDay = table.Column<bool>(type: "INTEGER", nullable: false),
                Color = table.Column<string>(type: "TEXT", nullable: false, maxLength: 20),
                IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                ExternalId = table.Column<Guid>(type: "TEXT", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
            },
            constraints: table => table.PrimaryKey("PK_accounts_credit_cards", x => x.Id));
        migrationBuilder.CreateIndex("IX_accounts_credit_cards_ExternalId", "accounts_credit_cards", "ExternalId", unique: true);
        migrationBuilder.CreateIndex("IX_accounts_accounts_BankExternalId_NormalizedName", "accounts_accounts", s_accountNameIndex, unique: true);
    }
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("accounts_credit_cards");
        migrationBuilder.DropTable("accounts_accounts");
        migrationBuilder.DropTable("accounts_banks");
    }
}
