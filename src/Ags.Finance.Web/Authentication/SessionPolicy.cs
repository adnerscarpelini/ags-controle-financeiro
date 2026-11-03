using Microsoft.AspNetCore.Authentication;

namespace Ags.Finance.Web.Authentication;

/// <summary>Duração da sessão: a escolhida nas preferências (12 horas por padrão), ou 30 dias com "Manter conectado".</summary>
internal static class SessionPolicy
{
    public static readonly TimeSpan DefaultLifetime = TimeSpan.FromHours(12);

    public static readonly TimeSpan RememberedLifetime = TimeSpan.FromDays(30);

    /// <summary>
    /// Sem "Manter conectado" o cookie é de sessão do navegador e o ticket expira na duração das preferências (12 horas por padrão); com ele o cookie
    /// é persistente e vale 30 dias, mesmo depois de fechar e abrir o navegador. A duração é gravada no ticket no login:
    /// mudar a preferência depois não altera sessões já abertas.
    /// </summary>
    public static AuthenticationProperties CreateProperties(bool rememberMe, DateTimeOffset now, TimeSpan? sessionLifetime = null)
    {
        return new AuthenticationProperties
        {
            IsPersistent = rememberMe,
            IssuedUtc = now,
            ExpiresUtc = now + (rememberMe ? RememberedLifetime : sessionLifetime ?? DefaultLifetime),
            AllowRefresh = false,
        };
    }
}
