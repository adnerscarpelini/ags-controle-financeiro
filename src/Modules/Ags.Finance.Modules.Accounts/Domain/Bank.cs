namespace Ags.Finance.Modules.Accounts.Domain;

internal sealed class Bank : Entity
{
    private Bank()
    {
    }
    public string Name { get; private set; } = string.Empty;
    public byte[]? LogoData { get; private set; }
    public Guid? LogoVersion { get; private set; }
    public string? SetLogo(byte[]? png)
    {
        if (png is { Length: 0 or > 1048576 }) { return "Logo final inválido."; }
        LogoData = png?.ToArray();
        LogoVersion = png is null ? null : Guid.CreateVersion7();
        return null;
    }
    public static Bank? Create(string name) => CatalogRules.IsValidName(name) ? new Bank
    {
        Name = name.Trim()
    } : null;
}
