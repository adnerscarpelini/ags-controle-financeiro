namespace Ags.Finance.Modules.Transactions.Domain;

internal enum CategoryKind
{
    Expense,
    Income,
    Transfer
}

internal sealed class Category : Entity
{
    public string Name { get; private set; } = string.Empty;
    public CategoryKind Kind { get; private set; }
    public bool IsActive { get; private set; } = true;

    public string? Update(string name, CategoryKind kind)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 100 || !Enum.IsDefined(kind))
        {
            return "Informe nome com até 100 caracteres e um tipo válido.";
        }
        Name = name.Trim();
        Kind = kind;
        return null;
    }

    public void Deactivate() => IsActive = false;
}
