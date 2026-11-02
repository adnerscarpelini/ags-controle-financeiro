using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Ags.Finance.Modules.Accounts.Infrastructure.Migrations;

[DbContext(typeof(AccountsDbContext))]
internal sealed class AccountsDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
        modelBuilder.HasAnnotation("ProductVersion", "10.0.14");
        modelBuilder.Entity("Ags.Finance.Modules.Accounts.Domain.CardLastDigits", b =>
        {
            b.Property<long>("Id").ValueGeneratedOnAdd().HasColumnType("INTEGER");
            b.Property<Guid>("ExternalId").HasColumnType("TEXT");
            b.Property<Guid>("CardExternalId").HasColumnType("TEXT");
            b.Property<string>("Digits").IsRequired().HasMaxLength(4).HasColumnType("TEXT");
            b.Property<int>("Usage").HasColumnType("INTEGER");
            b.Property<string>("Label").IsRequired().HasColumnType("TEXT");
            b.Property<bool>("IsActive").HasColumnType("INTEGER");
            b.Property<DateTimeOffset>("CreatedAt").HasColumnType("TEXT");
            b.Property<DateTimeOffset>("UpdatedAt").HasColumnType("TEXT");
            b.HasKey("Id");
            b.HasIndex("ExternalId").IsUnique();
            b.HasIndex("CardExternalId", "Digits").IsUnique();
            b.ToTable("accounts_card_last_digits");
        });
        modelBuilder.Entity("Ags.Finance.Modules.Accounts.Domain.Bank", b =>
        {
            b.Property<long>("Id").ValueGeneratedOnAdd().HasColumnType("INTEGER");
            b.Property<string>("Name").IsRequired().HasMaxLength(100).HasColumnType("TEXT");
            b.Property<Guid>("ExternalId").HasColumnType("TEXT");
            b.Property<DateTimeOffset>("CreatedAt").HasColumnType("TEXT");
            b.Property<DateTimeOffset>("UpdatedAt").HasColumnType("TEXT");
            b.HasKey("Id");
            b.HasIndex("ExternalId").IsUnique();
            b.ToTable("accounts_banks");
        });
        modelBuilder.Entity("Ags.Finance.Modules.Accounts.Domain.Account", b =>
        {
            b.Property<long>("Id").ValueGeneratedOnAdd().HasColumnType("INTEGER");
            b.Property<Guid>("BankExternalId").HasColumnType("TEXT");
            b.Property<string>("Name").IsRequired().HasMaxLength(100).HasColumnType("TEXT");
            b.Property<string>("NormalizedName").IsRequired().HasMaxLength(100).HasColumnType("TEXT");
            b.Property<int>("Kind").HasColumnType("INTEGER");
            b.Property<string>("Color").IsRequired().HasMaxLength(20).HasColumnType("TEXT");
            b.Property<bool>("IsActive").HasColumnType("INTEGER");
            b.Property<Guid>("ExternalId").HasColumnType("TEXT");
            b.Property<DateTimeOffset>("CreatedAt").HasColumnType("TEXT");
            b.Property<DateTimeOffset>("UpdatedAt").HasColumnType("TEXT");
            b.HasKey("Id");
            b.HasIndex("ExternalId").IsUnique();
            b.HasIndex("BankExternalId", "NormalizedName").IsUnique();
            b.ToTable("accounts_accounts");
        });
        modelBuilder.Entity("Ags.Finance.Modules.Accounts.Domain.CreditCard", b =>
        {
            b.Property<long>("Id").ValueGeneratedOnAdd().HasColumnType("INTEGER");
            b.Property<Guid>("AccountExternalId").HasColumnType("TEXT");
            b.Property<string>("Name").IsRequired().HasMaxLength(100).HasColumnType("TEXT");
            b.Property<int>("Brand").HasColumnType("INTEGER");
            b.Property<long>("CreditLimit").HasColumnType("INTEGER");
            b.Property<int>("ClosingDay").HasColumnType("INTEGER");
            b.Property<int>("DueDay").HasColumnType("INTEGER");
            b.Property<bool>("IncludesClosingDay").HasColumnType("INTEGER");
            b.Property<string>("Color").IsRequired().HasMaxLength(20).HasColumnType("TEXT");
            b.Property<bool>("IsActive").HasColumnType("INTEGER");
            b.Property<Guid>("ExternalId").HasColumnType("TEXT");
            b.Property<DateTimeOffset>("CreatedAt").HasColumnType("TEXT");
            b.Property<DateTimeOffset>("UpdatedAt").HasColumnType("TEXT");
            b.HasKey("Id");
            b.HasIndex("ExternalId").IsUnique();
            b.ToTable("accounts_credit_cards");
        });
    }
}
