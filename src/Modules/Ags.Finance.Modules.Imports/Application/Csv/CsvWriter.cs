using System.Text;

namespace Ags.Finance.Modules.Imports.Application.Csv;

/// <summary>
/// Escreve o contrato CSV do sistema: UTF-8 com BOM (para planilhas reconhecerem os acentos), separador vírgula, fim de linha CRLF e
/// campos entre aspas quando contêm vírgula, aspas ou quebra de linha (aspas duplicadas dentro do campo).
/// </summary>
internal sealed class CsvWriter
{
    private readonly StringBuilder _text = new();

    public CsvWriter(string type, int version, IReadOnlyList<string> columns)
    {
        Row([CsvFormat.Marker, type, version.ToString(System.Globalization.CultureInfo.InvariantCulture)]);
        Row(columns);
    }

    public void Row(IReadOnlyList<string> fields)
    {
        for (var index = 0; index < fields.Count; index++)
        {
            if (index > 0) { _text.Append(','); }
            _text.Append(Quote(fields[index]));
        }
        _text.Append("\r\n");
    }

    public byte[] ToBytes() => [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(_text.ToString())];

    private static string Quote(string field)
        => field.AsSpan().IndexOfAny(",\"\r\n") >= 0 ? "\"" + field.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"" : field;
}
