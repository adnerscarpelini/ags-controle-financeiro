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
            b.Property<string>("PayerDescription").IsRequired().HasMaxLength(100).HasColumnType("character varying(100)");
            b.HasKey("Id");
            b.HasIndex("ExternalId").IsUnique();
            b.HasIndex("NormalizedName").IsUnique();
            b.ToTable("people", "reimbursements");
        });
        modelBuilder.Entity("Ags.Finance.Modules.Reimbursements.Domain.SharingRule", b =>
        {
            b.Property<long>("Id").ValueGeneratedOnAdd().HasColumnType("bigint").HasAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);
            b.Property<Guid>("ExternalId").HasColumnType("uuid");
            b.Property<DateTimeOffset>("CreatedAt").HasColumnType("timestamp with time zone");
            b.Property<DateTimeOffset>("UpdatedAt").HasColumnType("timestamp with time zone");
            b.Property<Guid>("PersonExternalId").HasColumnType("uuid");
            b.Property<Guid>("SavedFilterExternalId").HasColumnType("uuid");
            b.Property<decimal>("Percent").HasPrecision(5, 2).HasColumnType("numeric(5,2)");
            b.Property<int>("StartMonth").HasColumnType("integer");
            b.HasKey("Id");
            b.HasIndex("ExternalId").IsUnique();
            b.HasIndex("PersonExternalId", "SavedFilterExternalId", "StartMonth").IsUnique();
            b.HasIndex("SavedFilterExternalId");
            b.ToTable("sharing_rules", "reimbursements");
            b.HasOne("Ags.Finance.Modules.Reimbursements.Domain.Person", null).WithMany().HasForeignKey("PersonExternalId").HasPrincipalKey("ExternalId").OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity("Ags.Finance.Modules.Reimbursements.Domain.ReimbursementReceipt", b =>
        {
            b.Property<long>("Id").ValueGeneratedOnAdd().HasColumnType("bigint").HasAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);
            b.Property<Guid>("ExternalId").HasColumnType("uuid");
            b.Property<DateTimeOffset>("CreatedAt").HasColumnType("timestamp with time zone");
            b.Property<DateTimeOffset>("UpdatedAt").HasColumnType("timestamp with time zone");
            b.Property<Guid>("PersonExternalId").HasColumnType("uuid");
            b.Property<Guid>("EntryExternalId").HasColumnType("uuid");
            b.Property<Guid>("AccountExternalId").HasColumnType("uuid");
            b.Property<DateOnly>("Date").HasColumnType("date");
            b.Property<int>("Month").HasColumnType("integer");
            b.Property<long>("Amount").HasColumnType("bigint");
            b.HasKey("Id");
            b.HasIndex("ExternalId").IsUnique();
            b.HasIndex("EntryExternalId").IsUnique();
            b.HasIndex("PersonExternalId", "Month");
            b.ToTable("receipts", "reimbursements");
            b.HasOne("Ags.Finance.Modules.Reimbursements.Domain.Person", null).WithMany().HasForeignKey("PersonExternalId").HasPrincipalKey("ExternalId").OnDelete(DeleteBehavior.Restrict);
        });
    }
}
