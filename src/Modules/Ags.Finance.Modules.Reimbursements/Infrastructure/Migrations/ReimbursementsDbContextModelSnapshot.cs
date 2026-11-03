using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

namespace Ags.Finance.Modules.Reimbursements.Infrastructure.Migrations;

[DbContext(typeof(ReimbursementsDbContext))]
internal sealed class ReimbursementsDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("reimbursements");
        modelBuilder.HasAnnotation("ProductVersion", "10.0.14");
        modelBuilder.HasAnnotation("Relational:MaxIdentifierLength", 63);
        NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);
        modelBuilder.Entity("Ags.Finance.Modules.Reimbursements.Domain.Person", b =>
        {
            b.Property<long>("Id").ValueGeneratedOnAdd().HasColumnType("bigint").HasAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);
            b.Property<Guid>("ExternalId").HasColumnType("uuid");
            b.Property<DateTimeOffset>("CreatedAt").HasColumnType("timestamp with time zone");
            b.Property<DateTimeOffset>("UpdatedAt").HasColumnType("timestamp with time zone");
            b.Property<string>("Name").IsRequired().HasMaxLength(100).HasColumnType("character varying(100)");
            b.Property<string>("NormalizedName").IsRequired().HasMaxLength(100).HasColumnType("character varying(100)");
            b.Property<decimal>("DefaultPercent").HasPrecision(5, 2).HasColumnType("numeric(5,2)");
            b.HasKey("Id");
            b.HasIndex("ExternalId").IsUnique();
            b.HasIndex("NormalizedName").IsUnique();
            b.ToTable("people", "reimbursements");
        });
        modelBuilder.Entity("Ags.Finance.Modules.Reimbursements.Domain.ReimbursementReceipt", b =>
        {
            b.Property<long>("Id").ValueGeneratedOnAdd().HasColumnType("bigint").HasAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);
            b.Property<Guid>("ExternalId").HasColumnType("uuid");
            b.Property<DateTimeOffset>("CreatedAt").HasColumnType("timestamp with time zone");
            b.Property<DateTimeOffset>("UpdatedAt").HasColumnType("timestamp with time zone");
            b.Property<Guid>("PersonExternalId").HasColumnType("uuid");
            b.Property<DateOnly>("Date").HasColumnType("date");
            b.Property<Guid?>("AccountExternalId").HasColumnType("uuid");
            b.Property<Guid?>("IncomeEntryExternalId").HasColumnType("uuid");
            b.Property<long>("Total").HasColumnType("bigint");
            b.HasKey("Id");
            b.HasAlternateKey("ExternalId");
            b.HasIndex("ExternalId").IsUnique();
            b.HasIndex("PersonExternalId");
            b.HasIndex("IncomeEntryExternalId").IsUnique();
            b.ToTable("reimbursement_receipts", "reimbursements");
            b.HasOne("Ags.Finance.Modules.Reimbursements.Domain.Person", null).WithMany().HasForeignKey("PersonExternalId").HasPrincipalKey("ExternalId").OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity("Ags.Finance.Modules.Reimbursements.Domain.ReimbursementReceiptItem", b =>
        {
            b.Property<long>("Id").ValueGeneratedOnAdd().HasColumnType("bigint").HasAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);
            b.Property<Guid>("ExternalId").HasColumnType("uuid");
            b.Property<DateTimeOffset>("CreatedAt").HasColumnType("timestamp with time zone");
            b.Property<DateTimeOffset>("UpdatedAt").HasColumnType("timestamp with time zone");
            b.Property<Guid>("ReceiptExternalId").HasColumnType("uuid");
            b.Property<Guid>("ExpenseEntryExternalId").HasColumnType("uuid");
            b.Property<long>("ExpenseAmount").HasColumnType("bigint");
            b.Property<decimal>("Percent").HasPrecision(5, 2).HasColumnType("numeric(5,2)");
            b.Property<long>("Expected").HasColumnType("bigint");
            b.Property<long>("Received").HasColumnType("bigint");
            b.HasKey("Id");
            b.HasIndex("ExternalId").IsUnique();
            b.HasIndex("ReceiptExternalId");
            b.HasIndex("ExpenseEntryExternalId").IsUnique();
            b.ToTable("reimbursement_receipt_items", "reimbursements");
            b.HasOne("Ags.Finance.Modules.Reimbursements.Domain.ReimbursementReceipt", null).WithMany().HasForeignKey("ReceiptExternalId").HasPrincipalKey("ExternalId").OnDelete(DeleteBehavior.Cascade);
        });
    }
}
