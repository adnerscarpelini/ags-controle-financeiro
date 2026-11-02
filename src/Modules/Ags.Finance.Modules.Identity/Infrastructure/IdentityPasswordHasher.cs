using Ags.Finance.Modules.Identity.Application;
using Ags.Finance.Modules.Identity.Domain;
using Microsoft.AspNetCore.Identity;

namespace Ags.Finance.Modules.Identity.Infrastructure;

/// <summary>Hash PBKDF2 do <see cref="PasswordHasher{TUser}"/> do ASP.NET Core Identity, com sal e custo no próprio hash.</summary>
internal sealed class IdentityPasswordHasher : Application.IPasswordHasher
{
    private readonly PasswordHasher<User> _hasher = new();

    public string Hash(string password)
    {
        // O PasswordHasher não usa o usuário; a assinatura genérica é herança do Identity.
        return _hasher.HashPassword(null!, password);
    }

    public PasswordVerification Verify(string passwordHash, string password)
    {
        return _hasher.VerifyHashedPassword(null!, passwordHash, password) switch
        {
            PasswordVerificationResult.Success => PasswordVerification.Success,
            PasswordVerificationResult.SuccessRehashNeeded => PasswordVerification.SuccessRehashNeeded,
            _ => PasswordVerification.Failed,
        };
    }
}
