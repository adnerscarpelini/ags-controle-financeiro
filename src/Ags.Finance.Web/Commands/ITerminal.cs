namespace Ags.Finance.Web.Commands;

/// <summary>Fronteira com o console, para os comandos poderem ser testados sem um terminal.</summary>
internal interface ITerminal
{
    /// <summary>Lê uma linha digitada. <c>null</c> quando a entrada acabou.</summary>
    string? ReadLine(string prompt);

    /// <summary>Lê uma senha sem ecoar o que é digitado. <c>null</c> quando a entrada acabou.</summary>
    string? ReadPassword(string prompt);

    void WriteLine(string message);

    void WriteError(string message);
}
