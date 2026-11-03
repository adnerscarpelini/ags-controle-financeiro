using System.Globalization;
using System.Text;

namespace Ags.Finance.Modules.Reimbursements.Domain;

internal sealed class Person : Entity
{
    public string Name { get; private set; } = string.Empty;
    public string NormalizedName { get; private set; } = string.Empty;

    /// <summary>Só pré-preenche novas seleções em despesas. Alterar não muda lançamentos ou séries já gravados.</summary>
    public decimal DefaultPercent { get; private set; }

    public string? Update(string name, decimal defaultPercent)
    {
        var normalized = Normalize(name ?? string.Empty);
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 100 || normalized.Length == 0)
        {
            return "Informe um nome com letras ou números e até 100 caracteres.";
        }
        if (defaultPercent is < 0 or > 100 || decimal.Round(defaultPercent, 2) != defaultPercent)
        {
            return "Informe um percentual de 0 a 100, com até duas casas decimais.";
        }
        Name = name.Trim();
        NormalizedName = normalized;
        DefaultPercent = defaultPercent;
        return null;
    }

    public static string Normalize(string value)
    {
        var decomposed = value.Normalize(NormalizationForm.FormD);
        return string.Concat(decomposed.Where(x => CharUnicodeInfo.GetUnicodeCategory(x) != UnicodeCategory.NonSpacingMark && char.IsLetterOrDigit(x))).ToUpperInvariant();
    }
}
