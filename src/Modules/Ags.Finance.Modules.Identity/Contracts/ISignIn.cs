namespace Ags.Finance.Modules.Identity.Contracts;

public enum SignInStatus
{
    Succeeded,
    InvalidCredentials,
    LockedOut,
}

/// <param name="Status">Resultado da tentativa. Usuário inexistente e senha errada voltam como <see cref="SignInStatus.InvalidCredentials"/>.</param>
/// <param name="User">Preenchido só quando <see cref="SignInStatus.Succeeded"/>.</param>
public sealed record SignInResult(SignInStatus Status, AuthenticatedUser? User = null);

public interface ISignIn
{
    Task<SignInResult> ExecuteAsync(string? userName, string? password, CancellationToken cancellationToken = default);
}
