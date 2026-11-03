namespace Ags.Finance.Modules.Imports.Contracts;

/// <summary>Tipos de cadastro do layout CSV oficial. A ordem dos valores é a ordem de importação (#28): quem referencia vem depois de quem é referenciado.</summary>
public enum RegistryKind
{
    Banks,
    Accounts,
    Cards,
    CardDigits,
    Categories,
    Subcategories,
    Merchants,
    People
}

public sealed record CsvFile(string FileName, byte[] Content);

public interface IRegistryExport
{
    /// <summary>Exporta o cadastro atual (ativos e arquivados). Somente leitura.</summary>
    Task<CsvFile> ExportAsync(RegistryKind kind, CancellationToken cancellationToken = default);

    /// <summary>Modelo vazio: versão e cabeçalhos do layout oficial, sem registros.</summary>
    CsvFile Template(RegistryKind kind);

    /// <summary>Exemplo com dados somente fictícios, no layout oficial.</summary>
    CsvFile Example(RegistryKind kind);
}
