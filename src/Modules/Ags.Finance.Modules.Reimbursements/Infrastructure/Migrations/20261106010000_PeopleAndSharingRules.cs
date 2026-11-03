using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

namespace Ags.Finance.Modules.Reimbursements.Infrastructure.Migrations;

[DbContext(typeof(ReimbursementsDbContext))]
[Migration("20261106010000_PeopleAndSharingRules")]
internal sealed class PeopleAndSharingRules : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(name: "people", schema: "reimbursements", columns: table => new
        {
            Id = table.Column<long>(type: "bigint", nullable: false).Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
            ExternalId = table.Column<Guid>(type: "uuid", nullable: false),
            CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
            UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
            Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
            NormalizedName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
            PayerDescription = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
        }, constraints: table => table.PrimaryKey("PK_people", x => x.Id));
        migrationBuilder.CreateIndex("IX_people_ExternalId", "people", "ExternalId", "reimbursements", unique: true);
        migrationBuilder.CreateIndex("IX_people_NormalizedName", "people", "NormalizedName", "reimbursements", unique: true);

        migrationBuilder.CreateTable(name: "sharing_rules", schema: "reimbursements", columns: table => new
        {
            Id = table.Column<long>(type: "bigint", nullable: false).Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
            ExternalId = table.Column<Guid>(type: "uuid", nullable: false),
            CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
            UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
            PersonExternalId = table.Column<Guid>(type: "uuid", nullable: false),
            SavedFilterExternalId = table.Column<Guid>(type: "uuid", nullable: false),
            Percent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
            StartMonth = table.Column<int>(type: "integer", nullable: false),
        }, constraints: table =>
        {
            table.PrimaryKey("PK_sharing_rules", x => x.Id);
            table.ForeignKey("FK_sharing_rules_people_PersonExternalId", x => x.PersonExternalId, "people", "ExternalId", "reimbursements", onDelete: ReferentialAction.Restrict);
        });
        migrationBuilder.CreateIndex("IX_sharing_rules_ExternalId", "sharing_rules", "ExternalId", "reimbursements", unique: true);
        migrationBuilder.CreateIndex("IX_sharing_rules_PersonExternalId_SavedFilterExternalId_StartMonth", "sharing_rules", ["PersonExternalId", "SavedFilterExternalId", "StartMonth"], "reimbursements", unique: true);
        migrationBuilder.CreateIndex("IX_sharing_rules_SavedFilterExternalId", "sharing_rules", "SavedFilterExternalId", "reimbursements");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("sharing_rules", "reimbursements");
        migrationBuilder.DropTable("people", "reimbursements");
    }
}
