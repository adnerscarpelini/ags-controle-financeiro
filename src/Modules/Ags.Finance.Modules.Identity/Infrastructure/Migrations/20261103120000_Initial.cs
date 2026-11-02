using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

namespace Ags.Finance.Modules.Identity.Infrastructure.Migrations;

/// <summary>Cria o schema e a tabela do usuário único.</summary>
[DbContext(typeof(IdentityDbContext))]
[Migration("20261103120000_Initial")]
internal sealed class Initial : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(name: "identity");

        migrationBuilder.CreateTable(
            name: "users",
            schema: "identity",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false).Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                UserName = table.Column<string>(type: "text", nullable: false),
                PasswordHash = table.Column<string>(type: "text", nullable: false),
                SecurityStamp = table.Column<string>(type: "text", nullable: false),
                FailedSignInCount = table.Column<int>(type: "integer", nullable: false),
                LockoutEnd = table.Column<DateTimeOffset?>(type: "timestamp with time zone", nullable: true),
                ExternalId = table.Column<Guid>(type: "uuid", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
            },
            constraints: table => table.PrimaryKey("PK_users", x => x.Id));
        migrationBuilder.CreateIndex(name: "IX_users_ExternalId", schema: "identity", table: "users", column: "ExternalId", unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "users", schema: "identity");
    }
}
