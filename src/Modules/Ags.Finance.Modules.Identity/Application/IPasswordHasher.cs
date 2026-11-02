namespace Ags.Finance.Modules.Identity.Application;

internal enum PasswordVerification
{
    Failed,
    Success,
    SuccessRehashNeeded,
}

/// <summary>Fronteira com o algoritmo de hash de senha.</summary>
internal interface IPasswordHasher
{
    string Hash(string password);

    PasswordVerification Verify(string passwordHash, string password);
}
