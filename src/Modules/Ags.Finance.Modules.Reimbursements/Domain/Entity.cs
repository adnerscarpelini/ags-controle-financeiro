namespace Ags.Finance.Modules.Reimbursements.Domain;

/// <summary>
/// Base de toda entidade com tabela própria. <see cref="Id"/> é a chave interna e nunca sai do módulo;
/// <see cref="ExternalId"/> é a identidade pública. Os carimbos de data são preenchidos pela Infrastructure.
/// </summary>
internal abstract class Entity
{
    protected Entity()
    {
        ExternalId = Guid.CreateVersion7();
    }

    public long Id { get; private set; }

    public Guid ExternalId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>Preserva a identidade pública vinda de uma importação CSV oficial. Só vale em entidade ainda não gravada.</summary>
    public void UseExternalId(Guid externalId)
    {
        if (Id != 0 || externalId == Guid.Empty)
        {
            throw new InvalidOperationException("A identidade pública só pode ser definida em uma entidade nova.");
        }
        ExternalId = externalId;
    }
}
