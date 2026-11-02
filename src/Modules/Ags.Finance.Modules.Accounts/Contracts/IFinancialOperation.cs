namespace Ags.Finance.Modules.Accounts.Contracts;

public interface IFinancialOperation
{
    Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> action, Func<T, bool> succeeded, CancellationToken cancellationToken = default);
}
