namespace Ags.Finance.Modules.Imports.Contracts;

/// <summary>Um problema que impede a importação, com a linha do arquivo (1 = versão, 2 = cabeçalho) e a coluna. Linha 0 = o arquivo como um todo.</summary>
public sealed record ImportIssue(int Line, string Field, string Message);

/// <summary>Resumo de uma prévia ou importação. Com qualquer <see cref="Issues"/> nada é gravado. <see cref="Applied"/> só é verdadeiro após a gravação confirmada.</summary>
public sealed record RegistryImportReport(RegistryKind Kind, int NewCount, int IdenticalCount, IReadOnlyList<ImportIssue> Issues, bool Applied)
{
    public bool Succeeded => Issues.Count == 0;
}

public interface IRegistryImport
{
    /// <summary>Valida o arquivo inteiro e informa novos, idênticos e problemas, sem gravar nada.</summary>
    Task<RegistryImportReport> PreviewAsync(RegistryKind kind, byte[] content, CancellationToken cancellationToken = default);

    /// <summary>Grava tudo ou nada. O conteúdo não é guardado depois do processamento.</summary>
    Task<RegistryImportReport> ImportAsync(RegistryKind kind, byte[] content, CancellationToken cancellationToken = default);
}
