using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

namespace Ags.Finance.Modules.Invoices.Infrastructure.Migrations;

/// <summary>Cria o schema do módulo; as tabelas chegam com as entidades.</summary>
[DbContext(typeof(InvoicesDbContext))]
[Migration("20261103120000_Initial")]
internal sealed class Initial : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(name: "invoices");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
    }
}
