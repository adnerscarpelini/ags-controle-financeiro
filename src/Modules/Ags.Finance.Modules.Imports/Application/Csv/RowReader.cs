using Ags.Finance.Modules.Imports.Contracts;
using Ags.Finance.SharedKernel;

namespace Ags.Finance.Modules.Imports.Application.Csv;

/// <summary>Lê os campos tipados de uma linha e registra os problemas por coluna; nada é corrigido nem inferido.</summary>
internal sealed class RowReader(CsvRow row, IReadOnlyList<string> columns, List<ImportIssue> issues)
{
    private int _problems;

    public int Line => row.Line;

    public bool Valid => _problems == 0;

    public void Problem(string column, string message)
    {
        _problems++;
        issues.Add(new(row.Line, column, message));
    }

    public string Raw(string column) => row.Fields[columns.ToList().IndexOf(column)];

    /// <summary>Texto obrigatório, com o escape de fórmula desfeito.</summary>
    public string Text(string column)
    {
        var value = CsvFormat.UnescapeText(Raw(column));
        if (value.Length == 0) { Problem(column, "Campo obrigatório."); }
        return value;
    }

    /// <summary>Texto que pode ser vazio.</summary>
    public string OptionalText(string column) => CsvFormat.UnescapeText(Raw(column));

    public Guid Id(string column)
    {
        if (!CsvFormat.TryGuid(Raw(column), out var id) || id == Guid.Empty) { Problem(column, "Informe um identificador no formato 00000000-0000-0000-0000-000000000000."); }
        return id;
    }

    public Guid? OptionalId(string column)
    {
        if (Raw(column).Length == 0) { return null; }
        return Id(column);
    }

    public bool Bool(string column)
    {
        if (!CsvFormat.TryBool(Raw(column), out var value)) { Problem(column, "Use true ou false."); }
        return value;
    }

    public long Integer(string column, long min, long max)
    {
        if (!CsvFormat.TryInteger(Raw(column), out var value) || value < min || value > max)
        {
            Problem(column, $"Informe um número inteiro de {min} a {max}, sem separadores.");
            return 0;
        }
        return value;
    }

    public DateOnly Date(string column)
    {
        if (!CsvFormat.TryDate(Raw(column), out var value)) { Problem(column, "Use a data no formato yyyy-MM-dd."); }
        return value;
    }

    public DateOnly? OptionalDate(string column) => Raw(column).Length == 0 ? null : Date(column);

    public long? OptionalInteger(string column, long min, long max) => Raw(column).Length == 0 ? null : Integer(column, min, max);

    /// <summary>Mês contábil no formato yyyy-MM.</summary>
    public AccountingMonth Month(string column)
    {
        if (!DateOnly.TryParseExact(Raw(column) + "-01", "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var date) || Raw(column).Length != 7)
        {
            Problem(column, "Use o mês no formato yyyy-MM.");
            return default;
        }
        return AccountingMonth.FromDate(date);
    }

    public AccountingMonth? OptionalMonth(string column) => Raw(column).Length == 0 ? null : Month(column);

    public T Enum<T>(string column)
        where T : struct, Enum
    {
        var text = Raw(column);
        if (text.Length == 0 || !char.IsLetter(text[0]) || !System.Enum.TryParse<T>(text, false, out var value) || !System.Enum.IsDefined(value))
        {
            Problem(column, $"Valores aceitos: {string.Join(", ", System.Enum.GetNames<T>())}.");
            return default;
        }
        return value;
    }
}
