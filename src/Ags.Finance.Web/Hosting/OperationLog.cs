namespace Ags.Finance.Web.Hosting;

/// <summary>Falha inesperada que o chamador transforma em mensagem: a exceção vai ao log para não se perder.</summary>
internal static partial class OperationLog
{
    [LoggerMessage(Level = LogLevel.Error, Message = "{Operation} falhou.")]
    public static partial void Failed(ILogger logger, string operation, Exception exception);
}
