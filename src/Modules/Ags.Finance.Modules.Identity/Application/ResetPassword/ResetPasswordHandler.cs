using Ags.Finance.Modules.Identity.Contracts;
using Ags.Finance.Modules.Identity.Domain;

namespace Ags.Finance.Modules.Identity.Application.ResetPassword;

internal sealed class ResetPasswordHandler : IResetPassword
{
    private readonly IUserStore _store;
    private readonly IPasswordHasher _hasher;

    public ResetPasswordHandler(IUserStore store, IPasswordHasher hasher)
    {
        _store = store;
        _hasher = hasher;
    }

    public async Task<ResetPasswordStatus> ExecuteAsync(string? newPassword, CancellationToken cancellationToken = default)
    {
        var user = await _store.GetAsync(cancellationToken);
        if (user is null)
        {
            return ResetPasswordStatus.NoUser;
        }

        if (!PasswordPolicy.IsValid(newPassword))
        {
            return ResetPasswordStatus.PasswordTooShort;
        }

        user.ChangePassword(_hasher.Hash(newPassword!));
        await _store.SaveChangesAsync(cancellationToken);
        return ResetPasswordStatus.Reset;
    }
}
