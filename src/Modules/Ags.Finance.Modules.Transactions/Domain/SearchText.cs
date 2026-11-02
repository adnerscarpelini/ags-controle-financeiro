using System.Globalization;
using System.Text;

namespace Ags.Finance.Modules.Transactions.Domain;

internal static class SearchText
{
    public static string Normalize(string value) => string.Concat(value.Normalize(NormalizationForm.FormD)
        .Where(x => CharUnicodeInfo.GetUnicodeCategory(x) != UnicodeCategory.NonSpacingMark)).ToUpperInvariant();
}
