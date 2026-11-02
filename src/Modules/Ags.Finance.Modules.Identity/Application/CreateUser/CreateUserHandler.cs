using Ags.Finance.Modules.Identity.Contracts;
using Ags.Finance.Modules.Identity.Domain;

namespace Ags.Finance.Modules.Identity.Application.CreateUser;

internal sealed class CreateUserHandler : ICreateUser
{
    private readonly IUserStore _store;
    private readonly IPasswordHasher _hasher;

    public CreateUserHandler(IUserStore store, IPasswordHasher hasher)
    {
        _store = store;
        _hasher = hasher;
    }

    public async Task<CreateUserStatus> ExecuteAsync(string? userName, string? password, CancellationToken cancellationToken = default)
    {
        if (await _store.AnyAsync(cancellationToken))
        {
            return CreateUserStatus.UserAlreadyExists;
        }

        if (string.IsNullOrWhiteSpace(userName))
        {
            return CreateUserStatus.InvalidUserName;
        }

        if (!PasswordPolicy.IsValid(password))
        {
            return CreateUserStatus.PasswordTooShort;
        }

        var user = User.Create(userName, _hasher.Hash(password!));
        if (user is null)
        {
            return CreateUserStatus.InvalidUserName;
        }

        _store.Add(user);
        await _store.SaveChangesAsync(cancellationToken);
        return CreateUserStatus.Created;
    }
}
