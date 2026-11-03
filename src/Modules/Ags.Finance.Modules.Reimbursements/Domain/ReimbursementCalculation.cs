using Ags.Finance.SharedKernel;

namespace Ags.Finance.Modules.Reimbursements.Domain;

internal static class ReimbursementCalculation
{
    /// <summary>Valor da despesa × percentual, arredondado ao centavo item a item pela regra de <see cref="Money"/>.</summary>
    public static Money Expected(Money expense, decimal percent) => new Money(Math.Abs(expense.Amount)) * (percent / 100m);
}
