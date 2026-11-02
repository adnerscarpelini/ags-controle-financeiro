using Microsoft.AspNetCore.Authentication;

namespace Ags.Finance.Web.Authentication;

/// <summary>Duração da sessão: 12 horas por padrão, 30 dias com "Manter conectado".</summary>
internal static class SessionPolicy
{
    public static readonly TimeSpan DefaultLifetime = TimeSpan.FromHours(12);

    public static readonly TimeSpan RememberedLifetime = TimeSpan.FromDays(30);

    /// <summary>
    /// Sem "Manter conectado" o cookie é de sessão do navegador e o ticket expira em 12 horas; com ele o cookie
    /// é persistente e vale 30 dias, mesmo depois de fechar e abrir o navegador.
    /// </summary>
    public static AuthenticationProperties CreateProperties(bool rememberMe, DateTimeOffset now)
    {
        return new AuthenticationProperties
        {
            IsPersistent = rememberMe,
            IssuedUtc = now,
            ExpiresUtc = now + (rememberMe ? RememberedLifetime : DefaultLifetime),
            AllowRefresh = false,
        };
    }
}
