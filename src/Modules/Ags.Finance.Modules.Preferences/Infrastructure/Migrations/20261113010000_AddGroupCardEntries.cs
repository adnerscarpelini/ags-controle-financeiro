using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Ags.Finance.Modules.Preferences.Infrastructure.Migrations;

[DbContext(typeof(PreferencesDbContext))]
[Migration("20261113010000_AddGroupCardEntries")]
internal sealed class AddGroupCardEntries : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(name: "GroupCardEntries", table: "user_preferences", schema: "preferences", type: "boolean", nullable: false, defaultValue: false);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "GroupCardEntries", table: "user_preferences", schema: "preferences");
    }
}
