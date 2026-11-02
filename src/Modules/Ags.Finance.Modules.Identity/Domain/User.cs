namespace Ags.Finance.Modules.Identity.Domain;

/// <summary>
/// O único usuário da aplicação. Guarda só o hash da senha. O <see cref="SecurityStamp"/> muda quando a senha
/// muda, o que encerra as sessões abertas; o bloqueio temporário após tentativas erradas também é do usuário.
/// </summary>
internal sealed class User : Entity
{
    public const int MaxFailedSignIns = 5;

    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(5);

    private User()
    {
        UserName = string.Empty;
        PasswordHash = string.Empty;
        SecurityStamp = string.Empty;
    }

    private User(string userName, string passwordHash)
    {
        UserName = userName;
        PasswordHash = passwordHash;
        SecurityStamp = NewSecurityStamp();
    }

    public string UserName { get; private set; }

    public string PasswordHash { get; private set; }

    public string SecurityStamp { get; private set; }

    public int FailedSignInCount { get; private set; }

    public DateTimeOffset? LockoutEnd { get; private set; }

    /// <summary>Devolve o usuário, ou <c>null</c> quando o nome é vazio.</summary>
    public static User? Create(string? userName, string passwordHash)
    {
        ArgumentException.ThrowIfNullOrEmpty(passwordHash);

        var trimmed = userName?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : new User(trimmed, passwordHash);
    }

    public bool HasUserName(string? candidate)
    {
        return string.Equals(UserName, candidate?.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    public bool IsLockedOut(DateTimeOffset now)
    {
        return LockoutEnd > now;
    }

    /// <summary>Conta uma tentativa errada; na quinta seguida, bloqueia por <see cref="LockoutDuration"/>.</summary>
    public void RegisterFailedSignIn(DateTimeOffset now)
    {
        if (LockoutEnd <= now)
        {
            LockoutEnd = null;
            FailedSignInCount = 0;
        }

        FailedSignInCount++;
        if (FailedSignInCount >= MaxFailedSignIns)
        {
            LockoutEnd = now + LockoutDuration;
            FailedSignInCount = 0;
        }
    }

    public void RegisterSuccessfulSignIn()
    {
        FailedSignInCount = 0;
        LockoutEnd = null;
    }

    /// <summary>Troca a senha, encerra as sessões abertas (novo carimbo) e remove qualquer bloqueio.</summary>
    public void ChangePassword(string passwordHash)
    {
        ArgumentException.ThrowIfNullOrEmpty(passwordHash);

        PasswordHash = passwordHash;
        SecurityStamp = NewSecurityStamp();
        RegisterSuccessfulSignIn();
    }

    /// <summary>Atualiza o hash para um algoritmo ou custo mais novo, sem encerrar as sessões.</summary>
    public void Rehash(string passwordHash)
    {
        ArgumentException.ThrowIfNullOrEmpty(passwordHash);

        PasswordHash = passwordHash;
    }

    private static string NewSecurityStamp()
    {
        return Guid.NewGuid().ToString("N");
    }
}
