using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

namespace Ags.Finance.Modules.Accounts.Infrastructure.Migrations;

[DbContext(typeof(AccountsDbContext))]
internal sealed class AccountsDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("accounts");
        modelBuilder.HasAnnotation("ProductVersion", "10.0.14");
        modelBuilder.HasAnnotation("Relational:MaxIdentifierLength", 63);
        NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);
        modelBuilder.Entity("Ags.Finance.Modules.Accounts.Domain.Bank", b =>
        {
            b.Property<long>("Id").ValueGeneratedOnAdd().HasColumnType("bigint")
                .HasAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);
            b.Property<string>("Name").IsRequired().HasMaxLength(100).HasColumnType("character varying(100)");
            b.Property<Guid>("ExternalId").HasColumnType("uuid");
            b.Property<DateTimeOffset>("CreatedAt").HasColumnType("timestamp with time zone");
            b.Property<DateTimeOffset>("UpdatedAt").HasColumnType("timestamp with time zone");
            b.Property<byte[]>("LogoData").HasColumnType("bytea");
            b.Property<Guid?>("LogoVersion").HasColumnType("uuid");
            b.HasKey("Id");
            b.HasIndex("ExternalId").IsUnique();
            b.ToTable("banks", "accounts");
        });
        modelBuilder.Entity("Ags.Finance.Modules.Accounts.Domain.Account", b =>
        {
            b.Property<long>("Id").ValueGeneratedOnAdd().HasColumnType("bigint")
                .HasAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);
            b.Property<Guid>("BankExternalId").HasColumnType("uuid");
            b.Property<long>("OpeningBalance").HasColumnType("bigint");
            b.Property<DateOnly?>("OpeningDate").HasColumnType("date");
            b.Property<string>("Name").IsRequired().HasMaxLength(100).HasColumnType("character varying(100)");
            b.Property<string>("NormalizedName").IsRequired().HasMaxLength(100).HasColumnType("character varying(100)");
            b.Property<int>("Kind").HasColumnType("integer");
            b.Property<string>("Color").IsRequired().HasMaxLength(20).HasColumnType("character varying(20)");
            b.Property<bool>("IsActive").HasColumnType("boolean");
            b.Property<Guid>("ExternalId").HasColumnType("uuid");
            b.Property<DateTimeOffset>("CreatedAt").HasColumnType("timestamp with time zone");
            b.Property<DateTimeOffset>("UpdatedAt").HasColumnType("timestamp with time zone");
            b.HasKey("Id");
            b.HasIndex("ExternalId").IsUnique();
            b.HasIndex("BankExternalId", "NormalizedName").IsUnique();
            b.ToTable("accounts", "accounts");
        });
        modelBuilder.Entity("Ags.Finance.Modules.Accounts.Domain.CreditCard", b =>
        {
            b.Property<long>("Id").ValueGeneratedOnAdd().HasColumnType("bigint")
                .HasAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);
            b.Property<Guid>("AccountExternalId").HasColumnType("uuid");
            b.Property<string>("Name").IsRequired().HasMaxLength(100).HasColumnType("character varying(100)");
            b.Property<int>("Brand").HasColumnType("integer");
            b.Property<long>("CreditLimit").HasColumnType("bigint");
            b.Property<int>("ClosingDay").HasColumnType("integer");
            b.Property<int>("DueDay").HasColumnType("integer");
            b.Property<bool>("IncludesClosingDay").HasColumnType("boolean");
            b.Property<string>("Color").IsRequired().HasMaxLength(20).HasColumnType("character varying(20)");
            b.Property<bool>("IsActive").HasColumnType("boolean");
            b.Property<Guid>("ExternalId").HasColumnType("uuid");
            b.Property<DateTimeOffset>("CreatedAt").HasColumnType("timestamp with time zone");
            b.Property<DateTimeOffset>("UpdatedAt").HasColumnType("timestamp with time zone");
            b.HasKey("Id");
            b.HasIndex("ExternalId").IsUnique();
            b.ToTable("credit_cards", "accounts");
        });
        modelBuilder.Entity("Ags.Finance.Modules.Accounts.Domain.CardLastDigits", b =>
        {
            b.Property<long>("Id").ValueGeneratedOnAdd().HasColumnType("bigint")
                .HasAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);
            b.Property<Guid>("CardExternalId").HasColumnType("uuid");
            b.Property<string>("Digits").IsRequired().HasMaxLength(4).HasColumnType("character varying(4)");
            b.Property<int>("Usage").HasColumnType("integer");
            b.Property<string>("Label").IsRequired().HasColumnType("text");
            b.Property<bool>("IsActive").HasColumnType("boolean");
            b.Property<Guid>("ExternalId").HasColumnType("uuid");
            b.Property<DateTimeOffset>("CreatedAt").HasColumnType("timestamp with time zone");
            b.Property<DateTimeOffset>("UpdatedAt").HasColumnType("timestamp with time zone");
            b.HasKey("Id");
            b.HasIndex("ExternalId").IsUnique();
            b.HasIndex("CardExternalId", "Digits").IsUnique();
            b.ToTable("card_last_digits", "accounts");
        });
    }
}
