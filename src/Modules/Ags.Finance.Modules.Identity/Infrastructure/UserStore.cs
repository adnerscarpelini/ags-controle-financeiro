using Ags.Finance.Modules.Identity.Application;
using Ags.Finance.Modules.Identity.Domain;
using Microsoft.EntityFrameworkCore;

namespace Ags.Finance.Modules.Identity.Infrastructure;

internal sealed class UserStore : IUserStore
{
    private readonly IdentityDbContext _context;

    public UserStore(IdentityDbContext context)
    {
        _context = context;
    }

    public Task<bool> AnyAsync(CancellationToken cancellationToken)
    {
        return _context.Users.AnyAsync(cancellationToken);
    }

    public Task<User?> GetAsync(CancellationToken cancellationToken)
    {
        return _context.Users.OrderBy(user => user.Id).FirstOrDefaultAsync(cancellationToken);
    }

    public void Add(User user)
    {
        _context.Users.Add(user);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        return _context.SaveChangesAsync(cancellationToken);
    }
}
