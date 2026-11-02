using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

namespace Ags.Finance.Modules.Transactions.Infrastructure.Migrations;

[DbContext(typeof(TransactionsDbContext))]
internal sealed class TransactionsDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("transactions");
        modelBuilder.HasAnnotation("ProductVersion", "10.0.14");
        modelBuilder.HasAnnotation("Relational:MaxIdentifierLength", 63);
        NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);
        modelBuilder.Entity("Ags.Finance.Modules.Transactions.Domain.Category", b =>
        {
            b.Property<long>("Id").ValueGeneratedOnAdd().HasColumnType("bigint").HasAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);
            b.Property<Guid>("ExternalId").HasColumnType("uuid");
            b.Property<DateTimeOffset>("CreatedAt").HasColumnType("timestamp with time zone");
            b.Property<DateTimeOffset>("UpdatedAt").HasColumnType("timestamp with time zone");
            b.Property<string>("Name").IsRequired().HasMaxLength(100).HasColumnType("character varying(100)");
            b.Property<int>("Kind").HasColumnType("integer");
            b.Property<bool>("IsActive").HasColumnType("boolean");
            b.HasKey("Id");
            b.HasAlternateKey("ExternalId");
            b.HasIndex("ExternalId").IsUnique();
            b.ToTable("categories", "transactions");
        });
        modelBuilder.Entity("Ags.Finance.Modules.Transactions.Domain.Subcategory", b =>
        {
            b.Property<long>("Id").ValueGeneratedOnAdd().HasColumnType("bigint").HasAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);
            b.Property<Guid>("ExternalId").HasColumnType("uuid");
            b.Property<DateTimeOffset>("CreatedAt").HasColumnType("timestamp with time zone");
            b.Property<DateTimeOffset>("UpdatedAt").HasColumnType("timestamp with time zone");
            b.Property<Guid>("CategoryExternalId").HasColumnType("uuid");
            b.Property<string>("Name").IsRequired().HasMaxLength(100).HasColumnType("character varying(100)");
            b.Property<string>("NormalizedName").IsRequired().HasMaxLength(100).HasColumnType("character varying(100)");
            b.Property<bool>("IsActive").HasColumnType("boolean");
            b.HasKey("Id");
            b.HasIndex("CategoryExternalId", "NormalizedName").IsUnique();
            b.HasIndex("ExternalId").IsUnique();
            b.ToTable("subcategories", "transactions");
        });
        modelBuilder.Entity("Ags.Finance.Modules.Transactions.Domain.Subcategory", b =>
        {
            b.HasOne("Ags.Finance.Modules.Transactions.Domain.Category", null).WithMany().HasForeignKey("CategoryExternalId").HasPrincipalKey("ExternalId").OnDelete(DeleteBehavior.Restrict).IsRequired();
        });
    }
}
