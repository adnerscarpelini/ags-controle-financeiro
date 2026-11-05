using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Ags.Finance.Modules.Preferences.Infrastructure.Migrations;

[DbContext(typeof(PreferencesDbContext))]
[Migration("20261112010000_RemoveHeatMapSection")]
internal sealed class RemoveHeatMapSection : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "ShowHeatMap", table: "user_preferences", schema: "preferences");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(name: "ShowHeatMap", table: "user_preferences", schema: "preferences", type: "boolean", nullable: false, defaultValue: true);
    }
}
