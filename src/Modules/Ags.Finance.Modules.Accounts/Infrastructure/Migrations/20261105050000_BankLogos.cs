using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Ags.Finance.Modules.Accounts.Infrastructure.Migrations;

[DbContext(typeof(AccountsDbContext))]
[Migration("20261105050000_BankLogos")]
internal sealed class BankLogos : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<byte[]>("LogoData", "banks", "bytea", schema: "accounts", nullable: true);
        migrationBuilder.AddColumn<Guid>("LogoVersion", "banks", "uuid", schema: "accounts", nullable: true);
    }
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn("LogoData", "banks", "accounts");
        migrationBuilder.DropColumn("LogoVersion", "banks", "accounts");
    }
}
