using System.Text;
using Ags.Finance.Modules.Imports.Application.Csv;

namespace Ags.Finance.Modules.Imports.Tests;

public sealed class CsvCodecTests
{
    private static readonly string[] s_columns = ["external_id", "name"];

    private static CsvParseResult Read(byte[] bytes) => CsvReader.Parse(bytes, "banks", 1, s_columns);

    private static byte[] Write(params string[][] rows)
    {
        var writer = new CsvWriter("banks", 1, s_columns);
        foreach (var row in rows) { writer.Row(row); }
        return writer.ToBytes();
    }

    [Theory]
    [InlineData("Padaria São João")]
    [InlineData("Loja, Filial 2")]
    [InlineData("Ele disse \"oi\"")]
    [InlineData("Duas\r\nlinhas\nmesmo campo")]
    [InlineData("  espaços nas pontas  ")]
    [InlineData("=HYPERLINK(\"http://exemplo.invalid\")")]
    [InlineData("+1 + 2")]
    [InlineData("-cmd")]
    [InlineData("@usuario")]
    [InlineData("'=já começa com apóstrofo")]
    [InlineData("''@dois apóstrofos")]
    [InlineData("'texto comum com apóstrofo")]
    [InlineData("\t=tab")]
    public void TextSurvivesAWriteAndReadWithTheFormulaEscapeBeingSymmetric(string text)
    {
        var bytes = Write(["0190a000-0000-7000-8000-000000000001", CsvFormat.EscapeText(text)]);
        var table = Assert.IsType<CsvTable>(Read(bytes).Table);
        var row = Assert.Single(table.Rows);
        Assert.Equal(text, CsvFormat.UnescapeText(row.Fields[1]));
    }

    [Theory]
    [InlineData("=SOMA(A1)")]
    [InlineData("+55")]
    [InlineData("-1")]
    [InlineData("@x")]
    public void FormulaLikeTextIsNeverWrittenWithAFormulaTrigger(string text)
    {
        var escaped = CsvFormat.EscapeText(text);
        Assert.StartsWith("'", escaped, StringComparison.Ordinal);
        Assert.DoesNotContain(escaped[0], "=+-@\t\r");
    }

    [Fact]
    public void FileStartsWithAUtf8BomAndUsesCrlf()
    {
        var bytes = Write(["id", "Banco"]);
        Assert.Equal(Encoding.UTF8.GetPreamble(), bytes.Take(3));
        Assert.Contains("\r\n", Encoding.UTF8.GetString(bytes), StringComparison.Ordinal);
    }

    [Fact]
    public void ReaderAcceptsFilesWithOrWithoutBomAndWithLfOnlyLineEndings()
    {
        var text = "ags-finance-csv,banks,1\nexternal_id,name\nid,Banco\n";
        Assert.True(Read(Encoding.UTF8.GetBytes(text)).Succeeded);
        Assert.True(Read([.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(text)]).Succeeded);
    }

    [Theory]
    [InlineData("", "arquivo está vazio")]
    [InlineData("nome,valor\r\nx,1\r\n", "não é um arquivo no layout CSV oficial")]
    [InlineData("ags-finance-csv,accounts,1\r\nexternal_id,name\r\n", "tipo \"accounts\" diferente")]
    [InlineData("ags-finance-csv,banks,2\r\nexternal_id,name\r\n", "versão \"2\"")]
    [InlineData("ags-finance-csv,banks,1\r\nexternal_id\r\n", "cabeçalho diferente")]
    [InlineData("ags-finance-csv,banks,1\r\nexternal_id,name,extra\r\n", "cabeçalho diferente")]
    [InlineData("ags-finance-csv,banks,1\r\nname,external_id\r\n", "cabeçalho diferente")]
    [InlineData("ags-finance-csv,banks,1\r\nexternal_id;name\r\n", "cabeçalho diferente")]
    [InlineData("ags-finance-csv,banks,1\r\nexternal_id,name\r\nsó-uma-coluna\r\n", "Linha 3: 1 colunas")]
    [InlineData("ags-finance-csv,banks,1\r\nexternal_id,name\r\n\r\nx,y\r\n", "linhas em branco")]
    [InlineData("ags-finance-csv,banks,1\r\nexternal_id,name\r\nx,\"aberto\r\n", "não foi fechado")]
    [InlineData("ags-finance-csv,banks,1\r\nexternal_id,name\r\nx,ab\"c\r\n", "aspas fora do lugar")]
    [InlineData("ags-finance-csv,banks,1\r\nexternal_id,name\r\nx,\"ab\"c\r\n", "aspas fora do lugar")]
    public void ReaderRejectsAnythingOutsideTheOfficialLayoutWithAClearMessage(string text, string expected)
    {
        var result = Read(Encoding.UTF8.GetBytes(text));
        Assert.False(result.Succeeded);
        Assert.Contains(expected, result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void ReaderRejectsBytesThatAreNotUtf8AndBinaryFormats()
    {
        Assert.Contains("UTF-8", Read([0x61, 0xE9, 0x62]).Error, StringComparison.Ordinal);
        Assert.False(Read([0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x34, 0x00, 0xFF]).Succeeded);
        Assert.False(Read([0x50, 0x4B, 0x03, 0x04, 0x14, 0x00]).Succeeded);
    }

    [Fact]
    public void ReaderReportsTheFileLineOfAMalformedRowAfterMultilineFields()
    {
        var text = "ags-finance-csv,banks,1\r\nexternal_id,name\r\nx,\"duas\r\nlinhas\"\r\ny\r\n";
        var result = Read(Encoding.UTF8.GetBytes(text));
        Assert.Contains("Linha 5", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void ReaderRejectsFilesAboveTheRowAndSizeLimits()
    {
        var builder = new StringBuilder("ags-finance-csv,banks,1\r\nexternal_id,name\r\n");
        for (var index = 0; index <= CsvReader.MaxRows; index++) { builder.Append("i,n\r\n"); }
        Assert.Contains("excede", Read(Encoding.UTF8.GetBytes(builder.ToString())).Error, StringComparison.Ordinal);
        Assert.Contains("MB", Read(new byte[CsvReader.MaxBytes + 1]).Error, StringComparison.Ordinal);
    }
}
