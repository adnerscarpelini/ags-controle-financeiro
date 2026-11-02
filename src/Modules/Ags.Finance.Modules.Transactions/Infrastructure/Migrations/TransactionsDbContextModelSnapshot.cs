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
            b.HasAlternateKey("ExternalId");
            b.HasIndex("ExternalId").IsUnique();
            b.ToTable("subcategories", "transactions");
        });
        modelBuilder.Entity("Ags.Finance.Modules.Transactions.Domain.Merchant", b =>
        {
            b.Property<long>("Id").ValueGeneratedOnAdd().HasColumnType("bigint").HasAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);
            b.Property<Guid>("ExternalId").HasColumnType("uuid");
            b.Property<DateTimeOffset>("CreatedAt").HasColumnType("timestamp with time zone");
            b.Property<DateTimeOffset>("UpdatedAt").HasColumnType("timestamp with time zone");
            b.Property<string>("Name").IsRequired().HasMaxLength(100).HasColumnType("character varying(100)");
            b.Property<string>("NormalizedName").IsRequired().HasMaxLength(100).HasColumnType("character varying(100)");
            b.Property<Guid?>("DefaultSubcategoryExternalId").HasColumnType("uuid");
            b.Property<bool>("IsActive").HasColumnType("boolean");
            b.HasKey("Id");
            b.HasIndex("ExternalId").IsUnique();
            b.HasAlternateKey("ExternalId");
            b.HasIndex("NormalizedName").IsUnique();
            b.HasIndex("DefaultSubcategoryExternalId");
            b.ToTable("merchants", "transactions");
        });
        modelBuilder.Entity("Ags.Finance.Modules.Transactions.Domain.Merchant", b =>
        {
            b.HasOne("Ags.Finance.Modules.Transactions.Domain.Subcategory", null).WithMany().HasForeignKey("DefaultSubcategoryExternalId").HasPrincipalKey("ExternalId").OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity("Ags.Finance.Modules.Transactions.Domain.Subcategory", b =>
        {
            b.HasOne("Ags.Finance.Modules.Transactions.Domain.Category", null).WithMany().HasForeignKey("CategoryExternalId").HasPrincipalKey("ExternalId").OnDelete(DeleteBehavior.Restrict).IsRequired();
        });
        modelBuilder.Entity("Ags.Finance.Modules.Transactions.Domain.TransactionEntry", b =>
        {
            b.Property<long>("Id").ValueGeneratedOnAdd().HasColumnType("bigint").HasAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);
            b.Property<Guid>("ExternalId").HasColumnType("uuid");
            b.Property<DateTimeOffset>("CreatedAt").HasColumnType("timestamp with time zone");
            b.Property<DateTimeOffset>("UpdatedAt").HasColumnType("timestamp with time zone");
            b.Property<DateOnly>("Date").HasColumnType("date");
            b.Property<int>("Month").HasColumnType("integer");
            b.Property<Guid>("MerchantExternalId").HasColumnType("uuid");
            b.Property<Guid>("SubcategoryExternalId").HasColumnType("uuid");
            b.Property<Guid>("AccountExternalId").HasColumnType("uuid");
            b.Property<Guid?>("InstallmentPurchaseExternalId").HasColumnType("uuid");
            b.Property<int?>("InstallmentNumber").HasColumnType("integer");
            b.Property<Guid?>("CardExternalId").HasColumnType("uuid");
            b.Property<Guid?>("CardLastDigitsExternalId").HasColumnType("uuid");
            b.Property<int>("Kind").HasColumnType("integer");
            b.Property<long>("Amount").HasColumnType("bigint");
            b.Property<string>("Details").IsRequired().HasMaxLength(1000).HasColumnType("character varying(1000)");
            b.Property<bool>("IsEffective").HasColumnType("boolean");
            b.Property<DateOnly?>("EffectiveDate").HasColumnType("date");
            b.Property<DateTimeOffset?>("EffectiveAt").HasColumnType("timestamp with time zone");
            b.HasKey("Id");
            b.HasIndex("ExternalId").IsUnique();
            b.HasIndex("Month");
            b.HasIndex("AccountExternalId", "EffectiveDate");
            b.HasIndex("InstallmentPurchaseExternalId", "InstallmentNumber").IsUnique();
            b.HasIndex("CardExternalId", "Month");
            b.HasIndex("MerchantExternalId");
            b.HasIndex("SubcategoryExternalId");
            b.ToTable("entries", "transactions");
        });
        modelBuilder.Entity("Ags.Finance.Modules.Transactions.Domain.TransactionEntry", b =>
        {
            b.HasOne("Ags.Finance.Modules.Transactions.Domain.Merchant", null).WithMany().HasForeignKey("MerchantExternalId").HasPrincipalKey("ExternalId").OnDelete(DeleteBehavior.Restrict).IsRequired();
            b.HasOne("Ags.Finance.Modules.Transactions.Domain.Subcategory", null).WithMany().HasForeignKey("SubcategoryExternalId").HasPrincipalKey("ExternalId").OnDelete(DeleteBehavior.Restrict).IsRequired();
        });
        modelBuilder.Entity("Ags.Finance.Modules.Transactions.Domain.InstallmentPurchase", b =>
        {
            b.Property<long>("Id").ValueGeneratedOnAdd().HasColumnType("bigint").HasAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);
            b.Property<Guid>("ExternalId").HasColumnType("uuid");
            b.Property<DateTimeOffset>("CreatedAt").HasColumnType("timestamp with time zone");
            b.Property<DateTimeOffset>("UpdatedAt").HasColumnType("timestamp with time zone");
            b.Property<Guid>("CardExternalId").HasColumnType("uuid");
            b.Property<int>("FirstMonth").HasColumnType("integer");
            b.Property<long>("Total").HasColumnType("bigint");
            b.Property<int>("Count").HasColumnType("integer");
            b.HasKey("Id");
            b.HasAlternateKey("ExternalId");
            b.HasIndex("ExternalId").IsUnique();
            b.ToTable("installment_purchases", "transactions");
        });
        modelBuilder.Entity("Ags.Finance.Modules.Transactions.Domain.TransactionEntry", b =>
        {
            b.HasOne("Ags.Finance.Modules.Transactions.Domain.InstallmentPurchase", null).WithMany().HasForeignKey("InstallmentPurchaseExternalId").HasPrincipalKey("ExternalId").OnDelete(DeleteBehavior.Restrict);
        });
    }
}
