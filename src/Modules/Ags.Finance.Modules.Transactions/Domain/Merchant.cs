using System.Globalization;
using System.Text;

namespace Ags.Finance.Modules.Transactions.Domain;

internal sealed class Merchant : Entity
{
    public string Name { get; private set; } = string.Empty;
    public string NormalizedName { get; private set; } = string.Empty;
    public Guid? DefaultSubcategoryExternalId { get; private set; }
    public bool IsActive { get; private set; } = true;

    public string? Update(string name, Guid? defaultSubcategoryExternalId)
    {
        var normalized = Normalize(name);
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 100 || normalized.Length == 0)
        {
            return "Informe uma descrição com letras ou números e até 100 caracteres.";
        }
        Name = name.Trim();
        NormalizedName = normalized;
        DefaultSubcategoryExternalId = defaultSubcategoryExternalId;
        return null;
    }

    public void Deactivate() => IsActive = false;

    public static string Normalize(string value)
    {
        var decomposed = value.Normalize(NormalizationForm.FormD);
        return string.Concat(decomposed.Where(x => CharUnicodeInfo.GetUnicodeCategory(x) != UnicodeCategory.NonSpacingMark && char.IsLetterOrDigit(x))).ToUpperInvariant();
    }
}
