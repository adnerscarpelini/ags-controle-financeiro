using Ags.Finance.SharedKernel;

namespace Ags.Finance.Modules.Reports.Contracts;

/// <summary>Uma pendência pela data prevista. <see cref="Amount"/> é positivo; <see cref="IsIncome"/> distingue a receber de a pagar.</summary>
public sealed record PendingItem(Guid EntryExternalId, DateOnly Date, string Description, Money Amount, bool IsIncome, Guid AccountExternalId, string AccountName);

/// <summary>Totais separados de receitas a receber e despesas a pagar. É previsão: nunca altera o saldo efetivo da conta.</summary>
public sealed record PendingGroup(IReadOnlyList<PendingItem> Items, Money ToReceive, Money ToPay);

/// <summary>
/// Janela fechada de <see cref="Today"/> até <see cref="WindowEnd"/> (hoje + 6) e, à parte, os atrasados (data prevista anterior a hoje).
/// <see cref="Today"/> é a data no fuso de negócio; o mês contábil selecionado no dashboard não a desloca.
/// </summary>
public sealed record PendingAgenda(DateOnly Today, DateOnly WindowEnd, PendingGroup Upcoming, PendingGroup Overdue);

public interface IPendingAgenda
{
    Task<PendingAgenda> GetAsync(CancellationToken cancellationToken = default);
}
