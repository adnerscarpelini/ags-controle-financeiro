using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

namespace Ags.Finance.Modules.Transactions.Infrastructure.Migrations;

[DbContext(typeof(TransactionsDbContext))]
[Migration("20261112010000_RecurringTransfers")]
internal sealed class RecurringTransfers : Migration
{
    private static readonly string[] s_occurrenceIndex = ["RecurringTransferExternalId", "OccurrenceMonth"];

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(name: "recurring_transfers", schema: "transactions", columns: table => new
        {
            Id = table.Column<long>(type: "bigint", nullable: false).Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
            ExternalId = table.Column<Guid>(type: "uuid", nullable: false),
            CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
            UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
            StartDate = table.Column<DateOnly>(type: "date", nullable: false),
            Day = table.Column<int>(type: "integer", nullable: false),
            EndMonth = table.Column<DateOnly>(type: "date", nullable: true),
            SourceAccountExternalId = table.Column<Guid>(type: "uuid", nullable: false),
            DestinationAccountExternalId = table.Column<Guid>(type: "uuid", nullable: false),
            Amount = table.Column<long>(type: "bigint", nullable: false),
            IsActive = table.Column<bool>(type: "boolean", nullable: false),
            ExcludedMonths = table.Column<DateOnly[]>(type: "date[]", nullable: false),
        }, constraints: table =>
        {
            table.PrimaryKey("PK_recurring_transfers", x => x.Id);
            table.UniqueConstraint("AK_recurring_transfers_ExternalId", x => x.ExternalId);
        });
        migrationBuilder.CreateIndex("IX_recurring_transfers_ExternalId", "recurring_transfers", "ExternalId", "transactions", unique: true);

        migrationBuilder.CreateTable(name: "transfer_occurrences", schema: "transactions", columns: table => new
        {
            Id = table.Column<long>(type: "bigint", nullable: false).Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
            ExternalId = table.Column<Guid>(type: "uuid", nullable: false),
            CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
            UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
            RecurringTransferExternalId = table.Column<Guid>(type: "uuid", nullable: false),
            OccurrenceMonth = table.Column<DateOnly>(type: "date", nullable: false),
            Date = table.Column<DateOnly>(type: "date", nullable: false),
            SourceAccountExternalId = table.Column<Guid>(type: "uuid", nullable: false),
            DestinationAccountExternalId = table.Column<Guid>(type: "uuid", nullable: false),
            Amount = table.Column<long>(type: "bigint", nullable: false),
            OperationExternalId = table.Column<Guid>(type: "uuid", nullable: true),
        }, constraints: table =>
        {
            table.PrimaryKey("PK_transfer_occurrences", x => x.Id);
            table.ForeignKey("FK_transfer_occurrences_recurring_transfers_RecurringTransferExternalId", x => x.RecurringTransferExternalId, principalSchema: "transactions", principalTable: "recurring_transfers", principalColumn: "ExternalId", onDelete: ReferentialAction.Restrict);
        });
        migrationBuilder.CreateIndex("IX_transfer_occurrences_ExternalId", "transfer_occurrences", "ExternalId", "transactions", unique: true);
        migrationBuilder.CreateIndex("IX_transfer_occurrences_RecurringTransferExternalId_OccurrenceMonth", "transfer_occurrences", s_occurrenceIndex, "transactions", unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("transfer_occurrences", "transactions");
        migrationBuilder.DropTable("recurring_transfers", "transactions");
    }
}
