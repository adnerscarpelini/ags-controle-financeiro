using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

namespace Ags.Finance.Modules.Identity.Infrastructure.Migrations;

[DbContext(typeof(IdentityDbContext))]
internal sealed class IdentityDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("identity");
        modelBuilder.HasAnnotation("ProductVersion", "10.0.14");
        modelBuilder.HasAnnotation("Relational:MaxIdentifierLength", 63);
        NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);
        modelBuilder.Entity("Ags.Finance.Modules.Identity.Domain.User", b =>
        {
            b.Property<long>("Id").ValueGeneratedOnAdd().HasColumnType("bigint")
                .HasAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);
            b.Property<string>("UserName").IsRequired().HasColumnType("text");
            b.Property<string>("PasswordHash").IsRequired().HasColumnType("text");
            b.Property<string>("SecurityStamp").IsRequired().HasColumnType("text");
            b.Property<int>("FailedSignInCount").HasColumnType("integer");
            b.Property<DateTimeOffset?>("LockoutEnd").HasColumnType("timestamp with time zone");
            b.Property<Guid>("ExternalId").HasColumnType("uuid");
            b.Property<DateTimeOffset>("CreatedAt").HasColumnType("timestamp with time zone");
            b.Property<DateTimeOffset>("UpdatedAt").HasColumnType("timestamp with time zone");
            b.HasKey("Id");
            b.HasIndex("ExternalId").IsUnique();
            b.ToTable("users", "identity");
        });
    }
}
