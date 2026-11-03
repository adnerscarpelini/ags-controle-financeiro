using System.Globalization;
using System.Text;

namespace Ags.Finance.Modules.Imports.Application.Csv;

/// <summary>Uma linha de dados com o número da linha do arquivo, para as mensagens de erro.</summary>
internal sealed record CsvRow(int Line, IReadOnlyList<string> Fields);

internal sealed record CsvTable(string Type, int Version, IReadOnlyList<string> Columns, IReadOnlyList<CsvRow> Rows);

internal sealed record CsvParseResult(CsvTable? Table, string? Error)
{
    public bool Succeeded => Error is null;
}

/// <summary>
/// Leitor estrito do contrato CSV. Não infere nada: rejeita BOM diferente de UTF-8, bytes inválidos, aspas fora do lugar, marcador/tipo/versão
/// desconhecidos, cabeçalho diferente do oficial (ausente, extra, fora de ordem) e linhas com número de colunas diferente.
/// </summary>
internal static class CsvReader
{
    public const int MaxBytes = 10 * 1024 * 1024;
    public const int MaxRows = 50_000;

    public static CsvParseResult Parse(byte[] content, string expectedType, int expectedVersion, IReadOnlyList<string> expectedColumns)
    {
        if (content.Length == 0) { return Fail("O arquivo está vazio."); }
        if (content.Length > MaxBytes) { return Fail($"O arquivo excede {MaxBytes / (1024 * 1024)} MB."); }
        string text;
        try
        {
            var offset = content.AsSpan().StartsWith(Encoding.UTF8.GetPreamble()) ? Encoding.UTF8.GetPreamble().Length : 0;
            text = new UTF8Encoding(false, true).GetString(content, offset, content.Length - offset);
        }
        catch (DecoderFallbackException)
        {
            return Fail("O arquivo não está em UTF-8.");
        }
        var rows = new List<CsvRow>();
        var error = Split(text, rows);
        if (error is not null) { return Fail(error); }
        if (rows.Count < 2) { return Fail("O arquivo precisa ter a linha de versão e o cabeçalho do layout oficial."); }

        var marker = rows[0].Fields;
        if (marker.Count != 3 || marker[0] != CsvFormat.Marker) { return Fail("Linha 1: não é um arquivo no layout CSV oficial do sistema."); }
        if (marker[1] != expectedType) { return Fail($"Linha 1: tipo \"{marker[1]}\" diferente do esperado \"{expectedType}\"."); }
        if (!int.TryParse(marker[2], NumberStyles.None, CultureInfo.InvariantCulture, out var version) || version != expectedVersion)
        {
            return Fail($"Linha 1: versão \"{marker[2]}\" do layout não é suportada (esperado {expectedVersion}).");
        }
        if (!rows[1].Fields.SequenceEqual(expectedColumns))
        {
            return Fail($"Linha 2: cabeçalho diferente do layout oficial. Esperado: {string.Join(",", expectedColumns)}.");
        }
        var data = rows.Skip(2).ToList();
        if (data.Count > MaxRows) { return Fail($"O arquivo excede {MaxRows} registros."); }
        foreach (var row in data.Where(x => x.Fields.Count != expectedColumns.Count))
        {
            return Fail($"Linha {row.Line}: {row.Fields.Count} colunas; o layout tem {expectedColumns.Count}.");
        }
        return new(new(expectedType, version, expectedColumns, data), null);
    }

    private static CsvParseResult Fail(string error) => new(null, error);

    /// <summary>Divide em linhas e campos. Retorna a mensagem de erro, ou <c>null</c>.</summary>
    private static string? Split(string text, List<CsvRow> rows)
    {
        var fields = new List<string>();
        var field = new StringBuilder();
        var line = 1;
        var rowLine = 1;
        var inQuotes = false;
        var fieldWasQuoted = false;
        var index = 0;
        while (index < text.Length)
        {
            var current = text[index];
            if (inQuotes)
            {
                if (current == '"' && index + 1 < text.Length && text[index + 1] == '"') { field.Append('"'); index += 2; continue; }
                if (current == '"') { inQuotes = false; index++; if (index < text.Length && text[index] is not (',' or '\r' or '\n')) { return $"Linha {line}: aspas fora do lugar."; } continue; }
                if (current == '\n') { line++; }
                field.Append(current);
                index++;
                continue;
            }
            switch (current)
            {
                case '"' when field.Length == 0 && !fieldWasQuoted:
                    inQuotes = true; fieldWasQuoted = true; index++; break;
                case '"':
                    return $"Linha {line}: aspas fora do lugar.";
                case ',':
                    fields.Add(field.ToString()); field.Clear(); fieldWasQuoted = false; index++; break;
                case '\r' or '\n':
                    if (current == '\r' && index + 1 < text.Length && text[index + 1] == '\n') { index++; }
                    index++;
                    fields.Add(field.ToString()); field.Clear(); fieldWasQuoted = false;
                    rows.Add(new(rowLine, fields.ToArray())); fields.Clear();
                    line++; rowLine = line;
                    break;
                default:
                    field.Append(current); index++; break;
            }
        }
        if (inQuotes) { return $"Linha {rowLine}: campo entre aspas não foi fechado."; }
        if (field.Length > 0 || fields.Count > 0 || fieldWasQuoted)
        {
            fields.Add(field.ToString());
            rows.Add(new(rowLine, fields.ToArray()));
        }
        return rows.Any(x => x.Fields.Count == 1 && x.Fields[0].Length == 0) ? "Há linhas em branco no arquivo." : null;
    }
}
