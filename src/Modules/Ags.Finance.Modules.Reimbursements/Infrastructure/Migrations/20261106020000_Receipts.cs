using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

namespace Ags.Finance.Modules.Reimbursements.Infrastructure.Migrations;

[DbContext(typeof(ReimbursementsDbContext))]
[Migration("20261106020000_Receipts")]
internal sealed class Receipts : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(name: "receipts", schema: "reimbursements", columns: table => new
        {
            Id = table.Column<long>(type: "bigint", nullable: false).Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
            ExternalId = table.Column<Guid>(type: "uuid", nullable: false),
            CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
            UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
            PersonExternalId = table.Column<Guid>(type: "uuid", nullable: false),
            EntryExternalId = table.Column<Guid>(type: "uuid", nullable: false),
            AccountExternalId = table.Column<Guid>(type: "uuid", nullable: false),
            Date = table.Column<DateOnly>(type: "date", nullable: false),
            Month = table.Column<int>(type: "integer", nullable: false),
            Amount = table.Column<long>(type: "bigint", nullable: false),
        }, constraints: table =>
        {
            table.PrimaryKey("PK_receipts", x => x.Id);
            table.ForeignKey("FK_receipts_people_PersonExternalId", x => x.PersonExternalId, "people", "ExternalId", "reimbursements", onDelete: ReferentialAction.Restrict);
        });
        migrationBuilder.CreateIndex("IX_receipts_ExternalId", "receipts", "ExternalId", "reimbursements", unique: true);
        migrationBuilder.CreateIndex("IX_receipts_EntryExternalId", "receipts", "EntryExternalId", "reimbursements", unique: true);
        migrationBuilder.CreateIndex("IX_receipts_PersonExternalId_Month", "receipts", ["PersonExternalId", "Month"], "reimbursements");
    }

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropTable("receipts", "reimbursements");
}
