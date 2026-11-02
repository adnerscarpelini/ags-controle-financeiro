using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

namespace Ags.Finance.Modules.Accounts.Infrastructure.Migrations;

/// <summary>Cria o schema e as tabelas de bancos, contas, cartões e finais de cartão.</summary>
[DbContext(typeof(AccountsDbContext))]
[Migration("20261103120000_Initial")]
internal sealed class Initial : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(name: "accounts");

        migrationBuilder.CreateTable(
            name: "banks",
            schema: "accounts",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false).Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                Name = table.Column<string>(type: "character varying(100)", nullable: false, maxLength: 100),
                ExternalId = table.Column<Guid>(type: "uuid", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
            },
            constraints: table => table.PrimaryKey("PK_banks", x => x.Id));
        migrationBuilder.CreateIndex(name: "IX_banks_ExternalId", schema: "accounts", table: "banks", column: "ExternalId", unique: true);

        migrationBuilder.CreateTable(
            name: "accounts",
            schema: "accounts",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false).Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                BankExternalId = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(type: "character varying(100)", nullable: false, maxLength: 100),
                NormalizedName = table.Column<string>(type: "character varying(100)", nullable: false, maxLength: 100),
                Kind = table.Column<int>(type: "integer", nullable: false),
                Color = table.Column<string>(type: "character varying(20)", nullable: false, maxLength: 20),
                IsActive = table.Column<bool>(type: "boolean", nullable: false),
                ExternalId = table.Column<Guid>(type: "uuid", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
            },
            constraints: table => table.PrimaryKey("PK_accounts", x => x.Id));
        migrationBuilder.CreateIndex(name: "IX_accounts_ExternalId", schema: "accounts", table: "accounts", column: "ExternalId", unique: true);
        migrationBuilder.CreateIndex(name: "IX_accounts_BankExternalId_NormalizedName", schema: "accounts", table: "accounts", columns: ["BankExternalId", "NormalizedName"], unique: true);

        migrationBuilder.CreateTable(
            name: "credit_cards",
            schema: "accounts",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false).Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                AccountExternalId = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(type: "character varying(100)", nullable: false, maxLength: 100),
                Brand = table.Column<int>(type: "integer", nullable: false),
                CreditLimit = table.Column<long>(type: "bigint", nullable: false),
                ClosingDay = table.Column<int>(type: "integer", nullable: false),
                DueDay = table.Column<int>(type: "integer", nullable: false),
                IncludesClosingDay = table.Column<bool>(type: "boolean", nullable: false),
                Color = table.Column<string>(type: "character varying(20)", nullable: false, maxLength: 20),
                IsActive = table.Column<bool>(type: "boolean", nullable: false),
                ExternalId = table.Column<Guid>(type: "uuid", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
            },
            constraints: table => table.PrimaryKey("PK_credit_cards", x => x.Id));
        migrationBuilder.CreateIndex(name: "IX_credit_cards_ExternalId", schema: "accounts", table: "credit_cards", column: "ExternalId", unique: true);

        migrationBuilder.CreateTable(
            name: "card_last_digits",
            schema: "accounts",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false).Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                CardExternalId = table.Column<Guid>(type: "uuid", nullable: false),
                Digits = table.Column<string>(type: "character varying(4)", nullable: false, maxLength: 4),
                Usage = table.Column<int>(type: "integer", nullable: false),
                Label = table.Column<string>(type: "text", nullable: false),
                IsActive = table.Column<bool>(type: "boolean", nullable: false),
                ExternalId = table.Column<Guid>(type: "uuid", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
            },
            constraints: table => table.PrimaryKey("PK_card_last_digits", x => x.Id));
        migrationBuilder.CreateIndex(name: "IX_card_last_digits_ExternalId", schema: "accounts", table: "card_last_digits", column: "ExternalId", unique: true);
        migrationBuilder.CreateIndex(name: "IX_card_last_digits_CardExternalId_Digits", schema: "accounts", table: "card_last_digits", columns: ["CardExternalId", "Digits"], unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "card_last_digits", schema: "accounts");
        migrationBuilder.DropTable(name: "credit_cards", schema: "accounts");
        migrationBuilder.DropTable(name: "accounts", schema: "accounts");
        migrationBuilder.DropTable(name: "banks", schema: "accounts");
    }
}
