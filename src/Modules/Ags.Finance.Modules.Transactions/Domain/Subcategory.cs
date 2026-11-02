namespace Ags.Finance.Modules.Transactions.Domain;

internal sealed class Subcategory : Entity
{
    public Guid CategoryExternalId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string NormalizedName { get; private set; } = string.Empty;
    public bool IsActive { get; private set; } = true;

    public string? Update(Guid categoryExternalId, string name)
    {
        if (categoryExternalId == Guid.Empty || string.IsNullOrWhiteSpace(name) || name.Trim().Length > 100)
        {
            return "Selecione uma categoria e informe nome com até 100 caracteres.";
        }
        CategoryExternalId = categoryExternalId;
        Name = name.Trim();
        NormalizedName = Name.ToUpperInvariant();
        return null;
    }

    public void Deactivate() => IsActive = false;
}
