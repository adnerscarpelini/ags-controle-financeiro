using System.Globalization;

namespace Ags.Finance.Modules.Imports.Application.Csv;

/// <summary>Formatação do contrato CSV: independe da cultura do sistema ou do programa que abrir o arquivo.</summary>
internal static class CsvFormat
{
    public const string Marker = "ags-finance-csv";

    public static string Bool(bool value) => value ? "true" : "false";

    public static string Date(DateOnly value) => value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public static string Number(long value) => value.ToString(CultureInfo.InvariantCulture);

    public static string Id(Guid? value) => value?.ToString("D") ?? string.Empty;

    /// <summary>Percentual com ponto decimal e sem zeros supérfluos (<c>33.33</c>, <c>50</c>).</summary>
    public static string Percent(decimal value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    public static bool TryPercent(string text, out decimal value)
        => decimal.TryParse(text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out value) && value <= 100 && decimal.Round(value, 2) == value;

    public static bool TryBool(string text, out bool value)
    {
        value = text == "true";
        return text is "true" or "false";
    }

    public static bool TryDate(string text, out DateOnly value)
        => DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out value);

    public static bool TryInteger(string text, out long value)
        => long.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out value) && text.Length <= 19 && !text.StartsWith('+');

    public static bool TryGuid(string text, out Guid value)
        => Guid.TryParseExact(text, "D", out value);

    /// <summary>
    /// Impede que planilhas interpretem texto como fórmula. Se o texto começa com zero ou mais apóstrofos seguidos de <c>= + - @</c>, TAB ou CR,
    /// acrescenta um apóstrofo; <see cref="UnescapeText"/> remove exatamente um, então o texto original volta intacto.
    /// </summary>
    public static string EscapeText(string value) => StartsWithTrigger(value) ? "'" + value : value;

    public static string UnescapeText(string value) => value.StartsWith('\'') && StartsWithTrigger(value) ? value[1..] : value;

    private static bool StartsWithTrigger(string value)
    {
        var index = 0;
        while (index < value.Length && value[index] == '\'') { index++; }
        return index < value.Length && value[index] is '=' or '+' or '-' or '@' or '\t' or '\r';
    }
}
