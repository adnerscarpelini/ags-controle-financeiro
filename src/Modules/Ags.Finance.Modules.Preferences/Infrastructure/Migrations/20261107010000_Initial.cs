using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

namespace Ags.Finance.Modules.Preferences.Infrastructure.Migrations;

[DbContext(typeof(PreferencesDbContext))]
[Migration("20261107010000_Initial")]
internal sealed class Initial : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(name: "preferences");
        migrationBuilder.CreateTable(name: "user_preferences", schema: "preferences", columns: table => new
        {
            Id = table.Column<long>(type: "bigint", nullable: false).Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
            ExternalId = table.Column<Guid>(type: "uuid", nullable: false),
            CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
            UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
            Theme = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
            StartScreen = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
            RecurringMode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
            PendingWindowDays = table.Column<int>(type: "integer", nullable: false),
            TopCategories = table.Column<int>(type: "integer", nullable: false),
            ShowSummary = table.Column<bool>(type: "boolean", nullable: false),
            ShowPending = table.Column<bool>(type: "boolean", nullable: false),
            ShowCategories = table.Column<bool>(type: "boolean", nullable: false),
            ShowInvoices = table.Column<bool>(type: "boolean", nullable: false),
            ShowHeatMap = table.Column<bool>(type: "boolean", nullable: false),
            PageSize = table.Column<int>(type: "integer", nullable: false),
            SessionHours = table.Column<int>(type: "integer", nullable: false),
        }, constraints: table => table.PrimaryKey("PK_user_preferences", x => x.Id));
        migrationBuilder.CreateIndex("IX_user_preferences_ExternalId", "user_preferences", "ExternalId", "preferences", unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("user_preferences", "preferences");
    }
}
