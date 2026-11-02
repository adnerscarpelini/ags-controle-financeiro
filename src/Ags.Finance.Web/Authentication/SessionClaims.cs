using System.Security.Claims;
using Ags.Finance.Modules.Identity.Contracts;

namespace Ags.Finance.Web.Authentication;

/// <summary>Converte o usuário autenticado na identidade guardada no cookie, e de volta.</summary>
internal static class SessionClaims
{
    /// <summary>Carimbo de segurança do usuário; quando a senha muda, o cookie antigo deixa de valer.</summary>
    public const string SecurityStamp = "ags:security-stamp";

    public static ClaimsPrincipal ToPrincipal(AuthenticatedUser user, string authenticationType)
    {
        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
                new Claim(ClaimTypes.Name, user.UserName),
                new Claim(SecurityStamp, user.SecurityStamp),
            ],
            authenticationType);
        return new ClaimsPrincipal(identity);
    }

    public static bool TryRead(ClaimsPrincipal? principal, out Guid userId, out string securityStamp)
    {
        var id = principal?.FindFirstValue(ClaimTypes.NameIdentifier);
        var stamp = principal?.FindFirstValue(SecurityStamp);

        userId = Guid.Empty;
        securityStamp = stamp ?? string.Empty;
        return Guid.TryParse(id, out userId) && !string.IsNullOrEmpty(stamp);
    }
}
