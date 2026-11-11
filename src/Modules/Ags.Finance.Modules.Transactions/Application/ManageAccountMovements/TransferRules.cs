using Ags.Finance.Modules.Accounts.Contracts;
using Ags.Finance.SharedKernel;

namespace Ags.Finance.Modules.Transactions.Application.ManageAccountMovements;

/// <summary>Regras comuns às transferências avulsas e recorrentes: origem e destino são contas ativas distintas, de qualquer tipo.</summary>
internal static class TransferRules
{
    public static async Task<string?> CheckAsync(IAccountCatalog accounts, Guid source, Guid destination, DateOnly date, Money amount, CancellationToken cancellationToken)
    {
        if (source == Guid.Empty || destination == Guid.Empty || source == destination || date == DateOnly.MinValue || amount.Amount <= 0 || amount.Amount > 92233720368547758.07m)
        {
            return "Informe contas distintas, data e valor positivo válidos.";
        }

        var available = await accounts.GetAccountsAsync(cancellationToken: cancellationToken);
        return available.All(x => x.ExternalId != source) || available.All(x => x.ExternalId != destination)
            ? "Selecione contas ativas na origem e no destino."
            : null;
    }
}
