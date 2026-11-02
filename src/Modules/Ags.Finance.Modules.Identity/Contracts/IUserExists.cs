namespace Ags.Finance.Modules.Identity.Contracts;

public interface IUserExists
{
    Task<bool> ExecuteAsync(CancellationToken cancellationToken = default);
}
