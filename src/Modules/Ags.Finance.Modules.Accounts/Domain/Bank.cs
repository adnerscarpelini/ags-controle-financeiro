namespace Ags.Finance.Modules.Accounts.Domain;

internal sealed class Bank : Entity
{
    private Bank()
    {
    }
    public string Name { get; private set; } = string.Empty;
    public static Bank? Create(string name) => CatalogRules.IsValidName(name) ? new Bank
    {
        Name = name.Trim()
    } : null;
}
