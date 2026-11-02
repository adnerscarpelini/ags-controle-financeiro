namespace Ags.Finance.Modules.Accounts.Domain;

internal enum AccountKind
{
    Checking,
    Investment,
    Savings
}
internal enum CardBrand
{
    Visa,
    Mastercard,
    Elo
}

internal static class CatalogRules
{
    public static bool IsValidName(string? name) => !string.IsNullOrWhiteSpace(name) && name.Trim().Length <= 100;
    public static bool IsValidColor(string? color) => color is "green" or "violet" or "orange" or "rose" or "slate";
    public static string NormalizeName(string name) => name.Trim().ToUpperInvariant();
}
