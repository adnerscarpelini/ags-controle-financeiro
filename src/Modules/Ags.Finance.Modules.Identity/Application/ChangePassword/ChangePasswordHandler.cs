using Ags.Finance.Modules.Identity.Contracts;
using Ags.Finance.Modules.Identity.Domain;

namespace Ags.Finance.Modules.Identity.Application.ChangePassword;

internal sealed class ChangePasswordHandler : IChangePassword
{
    private readonly IUserStore _store;
    private readonly IPasswordHasher _hasher;
    private readonly TimeProvider _timeProvider;

    public ChangePasswordHandler(IUserStore store, IPasswordHasher hasher, TimeProvider timeProvider)
    {
        _store = store;
        _hasher = hasher;
        _timeProvider = timeProvider;
    }

    public async Task<ChangePasswordResult> ExecuteAsync(
        Guid userId,
        string? currentPassword,
        string? newPassword,
        CancellationToken cancellationToken = default)
    {
        var user = await _store.GetAsync(cancellationToken);
        if (user is null || user.ExternalId != userId)
        {
            return new ChangePasswordResult(ChangePasswordStatus.UserNotFound);
        }

        if (!PasswordPolicy.IsValid(newPassword))
        {
            return new ChangePasswordResult(ChangePasswordStatus.PasswordTooShort);
        }

        // Quem tem uma sessão aberta também não testa senhas sem limite: errar a atual conta como no login.
        var now = _timeProvider.GetUtcNow();
        if (user.IsLockedOut(now))
        {
            return new ChangePasswordResult(ChangePasswordStatus.LockedOut);
        }

        if (string.IsNullOrEmpty(currentPassword) || _hasher.Verify(user.PasswordHash, currentPassword) == PasswordVerification.Failed)
        {
            user.RegisterFailedSignIn(now);
            await _store.SaveChangesAsync(cancellationToken);
            return new ChangePasswordResult(user.IsLockedOut(now)
                ? ChangePasswordStatus.LockedOut
                : ChangePasswordStatus.WrongCurrentPassword);
        }

        user.ChangePassword(_hasher.Hash(newPassword!));
        await _store.SaveChangesAsync(cancellationToken);
        return new ChangePasswordResult(
            ChangePasswordStatus.Changed,
            new AuthenticatedUser(user.ExternalId, user.UserName, user.SecurityStamp));
    }
}
