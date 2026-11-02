using Ags.Finance.Modules.Accounts.Domain;
using Ags.Finance.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace Ags.Finance.Modules.Accounts.Infrastructure;

/// <summary>Convenções de persistência comuns a todas as entidades do módulo.</summary>
internal static class PersistenceConventions
{
    public static void ConfigureValueConversions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<Money>().HaveConversion<MoneyCentsConverter>();
        configurationBuilder.Properties<AccountingMonth>().HaveConversion<AccountingMonthConverter>();
    }

    /// <summary>Impede que a base <see cref="Entity"/> vire tabela. Chamar antes de aplicar os mapeamentos.</summary>
    public static void IgnoreEntityBase(ModelBuilder modelBuilder)
    {
        modelBuilder.Ignore<Entity>();
    }

    /// <summary>
    /// Chave, índice único de <see cref="Entity.ExternalId"/> e prefixo de tabela do módulo. Chamar depois de
    /// aplicar os mapeamentos, que informam o nome da tabela sem o prefixo.
    /// </summary>
    public static void ApplyEntityRules(ModelBuilder modelBuilder, string tablePrefix)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes().ToList())
        {
            var tableName = entityType.GetTableName();
            if (tableName is not null && !tableName.StartsWith(tablePrefix, StringComparison.Ordinal))
            {
                entityType.SetTableName(tablePrefix + tableName);
            }

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
