using Ags.Finance.Modules.Identity.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Ags.Finance.Modules.Identity.Infrastructure.Migrations;

[DbContext(typeof(IdentityDbContext))]
internal sealed class IdentityDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
        modelBuilder.HasAnnotation("ProductVersion", "10.0.14");

        modelBuilder.Entity("Ags.Finance.Modules.Identity.Domain.User", b =>
        {
            b.Property<long>("Id")
                .ValueGeneratedOnAdd()
                .HasColumnType("INTEGER");

            b.Property<DateTimeOffset>("CreatedAt")
                .HasColumnType("TEXT");

            b.Property<Guid>("ExternalId")
                .HasColumnType("TEXT");

            b.Property<int>("FailedSignInCount")
                .HasColumnType("INTEGER");

            b.Property<DateTimeOffset?>("LockoutEnd")
                .HasColumnType("TEXT");

            b.Property<string>("PasswordHash")
                .IsRequired()
                .HasColumnType("TEXT");

            b.Property<string>("SecurityStamp")
                .IsRequired()
                .HasColumnType("TEXT");

            b.Property<DateTimeOffset>("UpdatedAt")
                .HasColumnType("TEXT");

            b.Property<string>("UserName")
                .IsRequired()
                .HasColumnType("TEXT");

            b.HasKey("Id");

            b.HasIndex("ExternalId")
                .IsUnique();

            b.ToTable("identity_users");
        });
    }
}
