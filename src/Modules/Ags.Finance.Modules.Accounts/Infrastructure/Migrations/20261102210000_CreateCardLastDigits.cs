using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Ags.Finance.Modules.Accounts.Infrastructure.Migrations;

[DbContext(typeof(AccountsDbContext))]
[Migration("20261102210000_CreateCardLastDigits")]
internal sealed class CreateCardLastDigits : Migration
{
    private static readonly string[] s_cardDigitsIndex = ["CardExternalId", "Digits"];

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "accounts_card_last_digits",
            columns: table => new
            {
                Id = table.Column<long>(type: "INTEGER", nullable: false).Annotation("Sqlite:Autoincrement", true),
                ExternalId = table.Column<Guid>(type: "TEXT", nullable: false),
                CardExternalId = table.Column<Guid>(type: "TEXT", nullable: false),
                Digits = table.Column<string>(type: "TEXT", maxLength: 4, nullable: false),
                Usage = table.Column<int>(type: "INTEGER", nullable: false),
                Label = table.Column<string>(type: "TEXT", nullable: false),
                IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
            },
            constraints: table => table.PrimaryKey("PK_accounts_card_last_digits", x => x.Id));
        migrationBuilder.CreateIndex("IX_accounts_card_last_digits_ExternalId", "accounts_card_last_digits", "ExternalId", unique: true);
        migrationBuilder.CreateIndex("IX_accounts_card_last_digits_CardExternalId_Digits", "accounts_card_last_digits", s_cardDigitsIndex, unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("accounts_card_last_digits");
    }
}
