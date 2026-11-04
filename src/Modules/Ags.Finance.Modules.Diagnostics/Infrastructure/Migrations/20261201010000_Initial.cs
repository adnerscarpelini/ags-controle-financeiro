using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

namespace Ags.Finance.Modules.Diagnostics.Infrastructure.Migrations;

[DbContext(typeof(DiagnosticsDbContext))]
[Migration("20261201010000_Initial")]
internal sealed class Initial : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(name: "diagnostics");
        migrationBuilder.CreateTable(name: "error_records", schema: "diagnostics", columns: table => new
        {
            Id = table.Column<long>(type: "bigint", nullable: false).Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
            ExternalId = table.Column<Guid>(type: "uuid", nullable: false),
            CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
            UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
            Fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
            ExceptionType = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
            Source = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
            StackTrace = table.Column<string>(type: "text", nullable: false),
            SqlState = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: true),
            FirstOccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
            LastOccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
            Occurrences = table.Column<long>(type: "bigint", nullable: false),
        }, constraints: table => table.PrimaryKey("PK_error_records", x => x.Id));
        migrationBuilder.CreateIndex("IX_error_records_ExternalId", "error_records", "ExternalId", "diagnostics", unique: true);
        migrationBuilder.CreateIndex("IX_error_records_Fingerprint", "error_records", "Fingerprint", "diagnostics", unique: true);
        migrationBuilder.CreateIndex("IX_error_records_LastOccurredAt", "error_records", "LastOccurredAt", "diagnostics");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("error_records", "diagnostics");
    }
}
