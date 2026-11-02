using Ags.Finance.Modules.Identity.Contracts;

namespace Ags.Finance.Modules.Identity.Application.ValidateSession;

internal sealed class ValidateSessionHandler : IValidateSession
{
    private readonly IUserStore _store;

    public ValidateSessionHandler(IUserStore store)
    {
        _store = store;
    }

    public async Task<bool> ExecuteAsync(Guid userId, string securityStamp, CancellationToken cancellationToken = default)
    {
        var user = await _store.GetAsync(cancellationToken);
        return user is not null && user.ExternalId == userId && user.SecurityStamp == securityStamp;
    }
}
