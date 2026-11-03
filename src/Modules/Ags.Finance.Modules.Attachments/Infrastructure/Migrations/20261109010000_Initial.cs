using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

namespace Ags.Finance.Modules.Attachments.Infrastructure.Migrations;

[DbContext(typeof(AttachmentsDbContext))]
[Migration("20261109010000_Initial")]
internal sealed class Initial : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema("attachments");
        migrationBuilder.CreateTable(name: "documents", schema: "attachments", columns: table => new
        {
            Id = table.Column<long>(type: "bigint", nullable: false).Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
            ExternalId = table.Column<Guid>(type: "uuid", nullable: false),
            CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
            UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
            OwnerType = table.Column<string>(type: "character varying(30)", nullable: false, maxLength: 30),
            OwnerExternalId = table.Column<Guid>(type: "uuid", nullable: false),
            Kind = table.Column<string>(type: "character varying(20)", nullable: false, maxLength: 20),
            OriginalFileName = table.Column<string>(type: "character varying(255)", nullable: false, maxLength: 255),
            ContentType = table.Column<string>(type: "character varying(50)", nullable: false, maxLength: 50),
            SizeBytes = table.Column<long>(type: "bigint", nullable: false),
            Sha256 = table.Column<string>(type: "character varying(64)", nullable: false, maxLength: 64),
            StorageKey = table.Column<string>(type: "character varying(200)", nullable: false, maxLength: 200),
            DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
        }, constraints: table => table.PrimaryKey("PK_documents", x => x.Id));
        migrationBuilder.CreateIndex("IX_documents_ExternalId", "documents", "ExternalId", "attachments", unique: true);
        migrationBuilder.CreateIndex("IX_documents_OwnerType_OwnerExternalId_Sha256", "documents", ["OwnerType", "OwnerExternalId", "Sha256"], "attachments", unique: true);
    }
    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropTable("documents", "attachments");
}
