using Ags.Finance.SharedKernel;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Ags.Finance.Modules.Reimbursements.Infrastructure;

/// <summary>Grava <see cref="Money"/> como centavos em <c>BIGINT</c>.</summary>
internal sealed class MoneyCentsConverter : ValueConverter<Money, long>
{
    public MoneyCentsConverter()
        : base(money => money.ToCents(), cents => Money.FromCents(cents))
    {
    }
}

/// <summary>Grava <see cref="ReferenceMonth"/> como inteiro <c>yyyyMM</c> (janeiro de 2027 vira 202701).</summary>
internal sealed class ReferenceMonthConverter : ValueConverter<ReferenceMonth, int>
{
    public ReferenceMonthConverter()
        : base(month => (month.Year * 100) + month.Month, value => new ReferenceMonth(value / 100, value % 100))
    {
    }
}
