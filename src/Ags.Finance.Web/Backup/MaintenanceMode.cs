namespace Ags.Finance.Web.Backup;

/// <summary>Enquanto ativo, a aplicação só responde 503 (manutenção): ninguém grava durante uma restauração.</summary>
internal sealed class MaintenanceMode
{
    private int _active;

    public bool IsActive => Volatile.Read(ref _active) == 1;

    public bool TryEnter()
    {
        return Interlocked.CompareExchange(ref _active, 1, 0) == 0;
    }

    public void Exit()
    {
        Volatile.Write(ref _active, 0);
    }
}

internal static class MaintenanceMiddleware
{
    private const string Page =
        "<!DOCTYPE html><html lang=\"pt-BR\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">"
        + "<title>Em manutenção · AGS Finanças</title></head><body style=\"font-family:sans-serif;background:#0E1311;color:#E6EDE9;padding:2rem\">"
        + "<h1>Em manutenção</h1><p>Uma restauração de backup está em andamento. A aplicação volta sozinha em instantes; tente novamente em alguns minutos.</p></body></html>";

    /// <summary>Depois dos arquivos estáticos: páginas e endpoints respondem 503 durante a manutenção.</summary>
    public static IApplicationBuilder UseMaintenanceMode(this IApplicationBuilder app)
    {
        return app.Use(async (context, next) =>
        {
            if (!context.RequestServices.GetRequiredService<MaintenanceMode>().IsActive)
            {
                await next(context);
                return;
            }

            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            context.Response.Headers.RetryAfter = "30";
            context.Response.ContentType = "text/html; charset=utf-8";
            await context.Response.WriteAsync(Page, context.RequestAborted);
        });
    }
}
