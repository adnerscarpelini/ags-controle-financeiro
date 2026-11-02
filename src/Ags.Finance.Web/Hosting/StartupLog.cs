namespace Ags.Finance.Web.Hosting;

internal static partial class StartupLog
{
    [LoggerMessage(Level = LogLevel.Critical, Message = "Não foi possível iniciar o servidor na porta {Port}. Verifique se ela está em uso e se o certificado HTTPS é válido.")]
    public static partial void StartFailed(ILogger logger, Exception exception, int port);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Aguardando o PostgreSQL em {Target} ficar disponível.")]
    public static partial void WaitingForDatabase(ILogger logger, string target);

    [LoggerMessage(Level = LogLevel.Critical, Message = "Não foi possível preparar o banco de dados; a aplicação não vai atender.")]
    public static partial void DatabaseFailed(ILogger logger, Exception exception);
}
