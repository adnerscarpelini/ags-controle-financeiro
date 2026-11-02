namespace Ags.Finance.Modules.Identity.Domain;

/// <summary>Regras de senha do usuário único.</summary>
internal static class PasswordPolicy
{
    public const int MinimumLength = 8;

    public static bool IsValid(string? password)
    {
        return password is not null && password.Length >= MinimumLength;
    }
}
