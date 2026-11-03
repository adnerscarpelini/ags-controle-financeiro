using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

namespace Ags.Finance.Modules.Transactions.Infrastructure.Migrations;

[DbContext(typeof(TransactionsDbContext))]
[Migration("20261111020000_EntryRepetitions")]
internal sealed class EntryRepetitions : Migration
{
    private static readonly string[] s_repetitionIndex = ["RepetitionExternalId", "RepetitionNumber"];
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(name: "entry_repetitions", schema: "transactions", columns: table => new
        {
            Id = table.Column<long>(type: "bigint", nullable: false).Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
            ExternalId = table.Column<Guid>(type: "uuid", nullable: false),
            CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
            UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
            Count = table.Column<int>(type: "integer", nullable: false),
            Period = table.Column<int>(type: "integer", nullable: false)
        }, constraints: table => { table.PrimaryKey("PK_entry_repetitions", x => x.Id); table.UniqueConstraint("AK_entry_repetitions_ExternalId", x => x.ExternalId); });
        migrationBuilder.CreateIndex("IX_entry_repetitions_ExternalId", "entry_repetitions", "ExternalId", "transactions", unique: true);
        migrationBuilder.AddColumn<Guid>("RepetitionExternalId", "entries", "uuid", schema: "transactions", nullable: true);
        migrationBuilder.AddColumn<int>("RepetitionNumber", "entries", "integer", schema: "transactions", nullable: true);
        migrationBuilder.CreateIndex("IX_entries_RepetitionExternalId_RepetitionNumber", "entries", s_repetitionIndex, "transactions", unique: true);
        migrationBuilder.AddForeignKey("FK_entries_entry_repetitions_RepetitionExternalId", "entries", "RepetitionExternalId", "entry_repetitions", principalColumn: "ExternalId", schema: "transactions", principalSchema: "transactions", onDelete: ReferentialAction.Restrict);
    }
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey("FK_entries_entry_repetitions_RepetitionExternalId", "entries", "transactions");
        migrationBuilder.DropIndex("IX_entries_RepetitionExternalId_RepetitionNumber", "entries", "transactions");
        migrationBuilder.DropColumn("RepetitionExternalId", "entries", "transactions");
        migrationBuilder.DropColumn("RepetitionNumber", "entries", "transactions");
        migrationBuilder.DropTable("entry_repetitions", "transactions");
    }
}
