namespace Ags.Finance.Web.Hosting;

internal static partial class StartupLog
{
    [LoggerMessage(Level = LogLevel.Critical, Message = "Não foi possível iniciar o servidor na porta {Port}. Verifique se ela está em uso e se o certificado HTTPS é válido.")]
    public static partial void StartFailed(ILogger logger, Exception exception, int port);
}
