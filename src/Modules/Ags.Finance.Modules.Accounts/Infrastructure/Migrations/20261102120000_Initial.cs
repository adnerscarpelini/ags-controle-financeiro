using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Ags.Finance.Modules.Accounts.Infrastructure.Migrations;

/// <summary>Linha de base do módulo: cria o histórico de migrations. As tabelas chegam com as entidades.</summary>
[DbContext(typeof(AccountsDbContext))]
[Migration("20261102120000_Initial")]
internal sealed class Initial : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
    }
}
