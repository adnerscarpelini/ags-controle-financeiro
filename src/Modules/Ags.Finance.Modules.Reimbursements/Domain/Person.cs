using System.Globalization;
using System.Text;

namespace Ags.Finance.Modules.Reimbursements.Domain;

internal sealed class Person : Entity
{
    public string Name { get; private set; } = string.Empty;
    public string NormalizedName { get; private set; } = string.Empty;

    /// <summary>Descrição (pagador) usada no lançamento de receita dos reembolsos recebidos desta pessoa.</summary>
    public string PayerDescription { get; private set; } = string.Empty;

    public string? Update(string name, string payerDescription)
    {
        var normalized = Normalize(name ?? string.Empty);
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 100 || normalized.Length == 0)
        {
            return "Informe um nome com letras ou números e até 100 caracteres.";
        }
        payerDescription = string.IsNullOrWhiteSpace(payerDescription) ? name : payerDescription;
        if (payerDescription.Trim().Length > 100 || Normalize(payerDescription).Length == 0)
        {
            return "Informe uma descrição do pagador com letras ou números e até 100 caracteres.";
        }
        Name = name.Trim();
        NormalizedName = normalized;
        PayerDescription = payerDescription.Trim();
        return null;
    }

    public static string Normalize(string value)
    {
        var decomposed = value.Normalize(NormalizationForm.FormD);
        return string.Concat(decomposed.Where(x => CharUnicodeInfo.GetUnicodeCategory(x) != UnicodeCategory.NonSpacingMark && char.IsLetterOrDigit(x))).ToUpperInvariant();
    }
}
