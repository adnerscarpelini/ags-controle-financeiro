namespace Ags.Finance.Modules.Imports.Contracts;

/// <summary>Resumo da prévia/importação de lançamentos. Problemas do arquivo de séries têm a coluna prefixada por <c>series:</c>. Com qualquer problema nada é gravado.</summary>
public sealed record TransactionImportReport(int NewCount, int IdenticalCount, IReadOnlyList<ImportIssue> Issues, bool Applied)
{
    public bool Succeeded => Issues.Count == 0;
}

public interface ITransactionImport
{
    /// <summary>Valida o(s) arquivo(s) inteiro(s) pelas regras reais de Transactions e descarta o resultado; nada é gravado.</summary>
    Task<TransactionImportReport> PreviewAsync(byte[] transactions, byte[]? recurringSeries, CancellationToken cancellationToken = default);

    /// <summary>Grava tudo ou nada. Os conteúdos não são guardados depois do processamento.</summary>
    Task<TransactionImportReport> ImportAsync(byte[] transactions, byte[]? recurringSeries, CancellationToken cancellationToken = default);
}
