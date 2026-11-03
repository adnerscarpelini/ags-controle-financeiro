using Ags.Finance.Modules.Transactions.Contracts;
using Ags.Finance.SharedKernel;

namespace Ags.Finance.Modules.Imports.Contracts;

/// <summary>Os dois arquivos da troca de lançamentos: os lançamentos e as definições das séries recorrentes que eles referenciam.</summary>
public enum TransactionFileKind { Transactions, RecurringSeries }

/// <summary>Critérios da exportação, os mesmos da consulta de Lançamentos (#15). Com <see cref="SavedFilterExternalId"/>, aplica o filtro salvo (#33) ao mês ou intervalo.</summary>
public sealed record TransactionExportFilter(AccountingMonth? Month = null, DateOnly? FromDate = null, DateOnly? ToDate = null, Guid? SavedFilterExternalId = null,
    Guid? AccountExternalId = null, Guid? CardExternalId = null, Guid? CardLastDigitsExternalId = null, Guid? CategoryExternalId = null, Guid? SubcategoryExternalId = null,
    string Text = "", bool? IsEffective = null, CategoryKind? Kind = null);

public sealed record CsvExportResult(CsvFile? File, string? Error)
{
    public bool Succeeded => Error is null;
}

public interface ITransactionExport
{
    /// <summary>
    /// Receitas e despesas do filtro, com vínculos explícitos de parcelas e séries e o estado Pendente/Efetivado. Ajustes de fatura, transferências e pagamentos
    /// de fatura não fazem parte do layout. Só consulta: não efetiva, não paga e não altera faturas.
    /// </summary>
    Task<CsvExportResult> ExportAsync(TransactionExportFilter filter, CancellationToken cancellationToken = default);

    /// <summary>Definições das séries recorrentes referenciadas pelos lançamentos do mesmo filtro.</summary>
    Task<CsvExportResult> ExportSeriesAsync(TransactionExportFilter filter, CancellationToken cancellationToken = default);

    CsvFile Template(TransactionFileKind kind);

    CsvFile Example(TransactionFileKind kind);
}
