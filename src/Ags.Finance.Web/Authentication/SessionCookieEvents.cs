using Ags.Finance.Modules.Identity.Contracts;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace Ags.Finance.Web.Authentication;

/// <summary>
/// A cada requisição confere se o carimbo de segurança do cookie ainda é o do usuário. Trocar a senha muda o
/// carimbo, então as demais sessões abertas caem na próxima requisição.
/// </summary>
internal sealed class SessionCookieEvents : CookieAuthenticationEvents
{
    public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
    {
        if (!SessionClaims.TryRead(context.Principal, out var userId, out var securityStamp))
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(context.Scheme.Name);
            return;
        }

        var validateSession = context.HttpContext.RequestServices.GetRequiredService<IValidateSession>();
        if (!await validateSession.ExecuteAsync(userId, securityStamp, context.HttpContext.RequestAborted))
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(context.Scheme.Name);
        }
    }
}
