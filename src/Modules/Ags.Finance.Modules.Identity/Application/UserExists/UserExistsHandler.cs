using Ags.Finance.Modules.Identity.Contracts;

namespace Ags.Finance.Modules.Identity.Application.UserExists;

internal sealed class UserExistsHandler : IUserExists
{
    private readonly IUserStore _store;

    public UserExistsHandler(IUserStore store)
    {
        _store = store;
    }

    public Task<bool> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        return _store.AnyAsync(cancellationToken);
    }
}
