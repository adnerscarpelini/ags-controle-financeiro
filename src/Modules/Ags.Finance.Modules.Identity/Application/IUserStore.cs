using Ags.Finance.Modules.Identity.Domain;

namespace Ags.Finance.Modules.Identity.Application;

/// <summary>Fronteira de persistência do usuário único.</summary>
internal interface IUserStore
{
    Task<bool> AnyAsync(CancellationToken cancellationToken);

    /// <summary>O usuário cadastrado, rastreado para alteração, ou <c>null</c> quando não há nenhum.</summary>
    Task<User?> GetAsync(CancellationToken cancellationToken);

    void Add(User user);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
