namespace Ags.Finance.Modules.Attachments.Domain;

/// <summary>
/// Base for persisted entities. <see cref="Id"/> remains internal to the module;
/// <see cref="ExternalId"/> is the public identity. Infrastructure supplies audit timestamps.
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
}
