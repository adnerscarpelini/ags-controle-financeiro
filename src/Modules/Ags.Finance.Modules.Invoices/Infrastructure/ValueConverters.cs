using Ags.Finance.SharedKernel;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Ags.Finance.Modules.Invoices.Infrastructure;

/// <summary>Grava <see cref="Money"/> como centavos em <c>INTEGER</c>.</summary>
internal sealed class MoneyCentsConverter : ValueConverter<Money, long>
{
    public MoneyCentsConverter()
        : base(money => money.ToCents(), cents => Money.FromCents(cents))
    {
    }
}

/// <summary>Grava <see cref="AccountingMonth"/> como inteiro <c>yyyyMM</c> (janeiro de 2027 vira 202701).</summary>
internal sealed class AccountingMonthConverter : ValueConverter<AccountingMonth, int>
{
    public AccountingMonthConverter()
        : base(month => (month.Year * 100) + month.Month, value => new AccountingMonth(value / 100, value % 100))
    {
    }
}
