using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

namespace Ags.Finance.Modules.Transactions.Infrastructure.Migrations;

[DbContext(typeof(TransactionsDbContext))]
[Migration("20261104200000_Recurrences")]
internal sealed class Recurrences : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(name: "recurring_entries", schema: "transactions", columns: table => new
        {
            Id = table.Column<long>(type: "bigint", nullable: false).Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
            ExternalId = table.Column<Guid>(type: "uuid", nullable: false),
            CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
            UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
            StartDate = table.Column<DateOnly>(type: "date", nullable: false),
            Day = table.Column<int>(type: "integer", nullable: false),
            EndMonth = table.Column<DateOnly>(type: "date", nullable: true),
            MerchantExternalId = table.Column<Guid>(type: "uuid", nullable: false),
            SubcategoryExternalId = table.Column<Guid>(type: "uuid", nullable: false),
            AccountExternalId = table.Column<Guid>(type: "uuid", nullable: false),
            CardExternalId = table.Column<Guid>(type: "uuid", nullable: true),
            CardLastDigitsExternalId = table.Column<Guid>(type: "uuid", nullable: true),
            Amount = table.Column<long>(type: "bigint", nullable: false),
            Kind = table.Column<int>(type: "integer", nullable: false),
            Details = table.Column<string>(type: "character varying(1000)", nullable: false, maxLength: 1000),
            IsActive = table.Column<bool>(type: "boolean", nullable: false),
        }, constraints: table =>
        {
            table.PrimaryKey("PK_recurring_entries", x => x.Id);
            table.UniqueConstraint("AK_recurring_entries_ExternalId", x => x.ExternalId);
            table.ForeignKey("FK_recurring_entries_merchants_MerchantExternalId", x => x.MerchantExternalId, principalSchema: "transactions", principalTable: "merchants", principalColumn: "ExternalId", onDelete: ReferentialAction.Restrict);
            table.ForeignKey("FK_recurring_entries_subcategories_SubcategoryExternalId", x => x.SubcategoryExternalId, principalSchema: "transactions", principalTable: "subcategories", principalColumn: "ExternalId", onDelete: ReferentialAction.Restrict);
        });
        migrationBuilder.CreateIndex("IX_recurring_entries_ExternalId", "recurring_entries", "ExternalId", "transactions", unique: true);
        migrationBuilder.CreateIndex("IX_recurring_entries_MerchantExternalId", "recurring_entries", "MerchantExternalId", "transactions");
        migrationBuilder.CreateIndex("IX_recurring_entries_SubcategoryExternalId", "recurring_entries", "SubcategoryExternalId", "transactions");
        migrationBuilder.AddColumn<Guid>("RecurringEntryExternalId", "entries", "uuid", schema: "transactions", nullable: true);
        migrationBuilder.AddColumn<DateOnly>("OccurrenceMonth", "entries", "date", schema: "transactions", nullable: true);
        migrationBuilder.CreateIndex("IX_entries_RecurringEntryExternalId_OccurrenceMonth", "entries", ["RecurringEntryExternalId", "OccurrenceMonth"], "transactions", unique: true);
        migrationBuilder.AddForeignKey("FK_entries_recurring_entries_RecurringEntryExternalId", "entries", "RecurringEntryExternalId", "recurring_entries", schema: "transactions", principalSchema: "transactions", principalColumn: "ExternalId", onDelete: ReferentialAction.Restrict);
    }
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey("FK_entries_recurring_entries_RecurringEntryExternalId", "entries", "transactions");
        migrationBuilder.DropIndex("IX_entries_RecurringEntryExternalId_OccurrenceMonth", "entries", "transactions");
        migrationBuilder.DropColumn("OccurrenceMonth", "entries", "transactions");
        migrationBuilder.DropColumn("RecurringEntryExternalId", "entries", "transactions");
        migrationBuilder.DropTable("recurring_entries", "transactions");
    }
}
