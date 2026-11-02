using Ags.Finance.Modules.Identity.Contracts;

namespace Ags.Finance.Modules.Identity.Application.SignIn;

internal sealed class SignInHandler : ISignIn
{
    private readonly IUserStore _store;
    private readonly IPasswordHasher _hasher;
    private readonly TimeProvider _timeProvider;

    public SignInHandler(IUserStore store, IPasswordHasher hasher, TimeProvider timeProvider)
    {
        _store = store;
        _hasher = hasher;
        _timeProvider = timeProvider;
    }

    public async Task<SignInResult> ExecuteAsync(string? userName, string? password, CancellationToken cancellationToken = default)
    {
        var user = await _store.GetAsync(cancellationToken);
        if (user is null || !user.HasUserName(userName))
        {
            // Gasta o tempo de um hash, para o tempo de resposta não revelar se o usuário existe.
            _hasher.Hash(password ?? string.Empty);
            return new SignInResult(SignInStatus.InvalidCredentials);
        }

        var now = _timeProvider.GetUtcNow();
        if (user.IsLockedOut(now))
        {
            return new SignInResult(SignInStatus.LockedOut);
        }

        var verification = string.IsNullOrEmpty(password)
            ? PasswordVerification.Failed
            : _hasher.Verify(user.PasswordHash, password);

        if (verification == PasswordVerification.Failed)
        {
            user.RegisterFailedSignIn(now);
            await _store.SaveChangesAsync(cancellationToken);
            return new SignInResult(user.IsLockedOut(now) ? SignInStatus.LockedOut : SignInStatus.InvalidCredentials);
        }

        user.RegisterSuccessfulSignIn();
        if (verification == PasswordVerification.SuccessRehashNeeded)
        {
            user.Rehash(_hasher.Hash(password!));
        }

        await _store.SaveChangesAsync(cancellationToken);
        return new SignInResult(
            SignInStatus.Succeeded,
            new AuthenticatedUser(user.ExternalId, user.UserName, user.SecurityStamp));
    }
}
