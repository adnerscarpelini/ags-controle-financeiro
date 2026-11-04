namespace Ags.Finance.Modules.Accounts.Domain;

internal sealed class Bank : Entity
{
    private Bank()
    {
    }
    public string Name { get; private set; } = string.Empty;
    public string NormalizedName { get; private set; } = string.Empty;
    public bool IsActive { get; private set; } = true;
    public byte[]? LogoData { get; private set; }
    public Guid? LogoVersion { get; private set; }
    public string? SetLogo(byte[]? png)
    {
        if (png is { Length: 0 or > 1048576 }) { return "Logo final inválido."; }
        LogoData = png?.ToArray();
        LogoVersion = png is null ? null : Guid.CreateVersion7();
        return null;
    }
    public static Bank? Create(string name)
    {
        var bank = new Bank();
        return bank.Rename(name) ? bank : null;
    }
    public bool Rename(string name)
    {
        if (!CatalogRules.IsValidName(name)) { return false; }
        Name = name.Trim();
        NormalizedName = CatalogRules.NormalizeName(name);
        return true;
    }
    public void Deactivate() => IsActive = false;
    public void Reactivate() => IsActive = true;
}
