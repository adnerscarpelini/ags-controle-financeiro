using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Ags.Finance.Modules.Accounts.Infrastructure.Migrations;

[DbContext(typeof(AccountsDbContext))]
[Migration("20261106090000_BankNamesAndArchiving")]
internal sealed class BankNamesAndArchiving : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>("NormalizedName", "banks", "character varying(100)", maxLength: 100, schema: "accounts", nullable: false, defaultValue: string.Empty);
        migrationBuilder.AddColumn<bool>("IsActive", "banks", "boolean", schema: "accounts", nullable: false, defaultValue: true);

        // Nomes repetidos já gravados não podem impedir a atualização: o Id no sufixo torna o nome único sem mudar vínculos,
        // que usam o ExternalId, e o usuário pode renomear depois.
        migrationBuilder.Sql("""
            WITH ranked AS (
                SELECT "Id", row_number() OVER (PARTITION BY upper(btrim("Name")) ORDER BY "Id") AS position
                FROM accounts.banks)
            UPDATE accounts.banks AS bank
            SET "Name" = left(btrim(bank."Name"), 70) || ' (' || bank."Id" || ')'
            FROM ranked
            WHERE bank."Id" = ranked."Id" AND ranked.position > 1;
            UPDATE accounts.banks SET "Name" = btrim("Name"), "NormalizedName" = upper(btrim("Name"));
            """);

        migrationBuilder.AlterColumn<string>("NormalizedName", "banks", "character varying(100)", maxLength: 100, schema: "accounts", nullable: false, oldClrType: typeof(string), oldType: "character varying(100)", oldMaxLength: 100, oldDefaultValue: string.Empty);
        migrationBuilder.AlterColumn<bool>("IsActive", "banks", "boolean", schema: "accounts", nullable: false, oldClrType: typeof(bool), oldType: "boolean", oldDefaultValue: true);
        migrationBuilder.CreateIndex("IX_banks_NormalizedName", "banks", "NormalizedName", "accounts", unique: true);
    }
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex("IX_banks_NormalizedName", "banks", "accounts");
        migrationBuilder.DropColumn("IsActive", "banks", "accounts");
        migrationBuilder.DropColumn("NormalizedName", "banks", "accounts");
    }
}
