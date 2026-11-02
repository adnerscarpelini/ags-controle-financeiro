using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Ags.Finance.Modules.Accounts.Infrastructure.Migrations;

[DbContext(typeof(AccountsDbContext))]
[Migration("20261104140000_OpeningBalance")]
internal sealed class OpeningBalance : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<long>("OpeningBalance", "accounts", "bigint", schema: "accounts", nullable: false, defaultValue: 0L);
        migrationBuilder.AddColumn<DateOnly>("OpeningDate", "accounts", "date", schema: "accounts", nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn("OpeningDate", "accounts", "accounts");
        migrationBuilder.DropColumn("OpeningBalance", "accounts", "accounts");
    }
}
