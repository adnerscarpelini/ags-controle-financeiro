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
    private static readonly HashSet<string> s_colors = ["green", "blue", "violet", "orange", "rose", "red", "sky", "lilac", "lime", "amber", "coral", "ice", "lavender", "pistachio", "sand", "blush", "charcoal", "graphite", "slate", "silver", "indigo", "teal", "forest", "yellow", "olive", "magenta", "caramel", "wine", "steel", "black", "pine"];

    public static bool IsValidColor(string? color) => color is not null && s_colors.Contains(color);
    public static string NormalizeName(string name) => name.Trim().ToUpperInvariant();
}
