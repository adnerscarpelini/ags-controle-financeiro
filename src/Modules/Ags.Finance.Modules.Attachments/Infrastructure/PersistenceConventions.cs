using Ags.Finance.Modules.Attachments.Domain;
using Microsoft.EntityFrameworkCore;

namespace Ags.Finance.Modules.Attachments.Infrastructure;

/// <summary>Persistence conventions shared by the module entities.</summary>
internal static class PersistenceConventions
{
    /// <summary>Keeps the <see cref="Entity"/> base out of the model before applying configurations.</summary>
    public static void IgnoreEntityBase(ModelBuilder modelBuilder)
    {
        modelBuilder.Ignore<Entity>();
    }

    /// <summary>Applies the primary key and unique <see cref="Entity.ExternalId"/> index after configurations.</summary>
    public static void ApplyEntityRules(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes().ToList())
        {
            if (typeof(Entity).IsAssignableFrom(entityType.ClrType))
            {
                modelBuilder.Entity(entityType.ClrType, entity =>
                {
                    entity.HasKey(nameof(Entity.Id));
                    entity.HasIndex(nameof(Entity.ExternalId)).IsUnique();
                });
            }
        }
    }
}
