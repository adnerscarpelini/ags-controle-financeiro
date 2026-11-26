using Ags.Finance.SharedKernel;

namespace Ags.Finance.Modules.Transactions.Domain;

/// <summary>Cashback previsto de uma compra: valor absoluto vezes o percentual gravado, arredondado em centavos pela regra de <see cref="Money"/>.</summary>
internal static class CashbackCalculation
{
    public static Money Expected(Money amount, decimal percent)
        => percent <= 0 ? Money.Zero : new Money(Math.Abs(amount.Amount) * percent / 100m);
}
