namespace Ags.Finance.Web.Hosting;

internal static partial class StartupLog
{
    [LoggerMessage(Level = LogLevel.Critical, Message = "Não foi possível iniciar o servidor na porta {Port}. Verifique se ela já está em uso.")]
    public static partial void StartFailed(ILogger logger, Exception exception, int port);
}
