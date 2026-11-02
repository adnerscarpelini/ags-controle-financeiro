using System.Globalization;

namespace Ags.Finance.SharedKernel;

/// <summary>
/// Valor monetário em BRL. O valor é sempre arredondado para 2 casas decimais,
/// com <see cref="MidpointRounding.AwayFromZero"/>, ao ser criado ou resultar de uma operação.
/// </summary>
public readonly record struct Money : IComparable<Money>
{
    private const int Decimals = 2;
    private const decimal CentsPerUnit = 100m;

    private static readonly NumberFormatInfo s_format = new()
    {
        NumberDecimalSeparator = ",",
        NumberGroupSeparator = ".",
        NumberGroupSizes = [3],
        NegativeSign = "-",
    };

    public Money(decimal amount)
    {
        Amount = Math.Round(amount, Decimals, MidpointRounding.AwayFromZero);
    }

    public static Money Zero => default;

    public decimal Amount { get; }

    public bool IsNegative => Amount < 0;

    public bool IsZero => Amount == 0;

    public static Money FromCents(long cents)
    {
        return new Money(cents / CentsPerUnit);
    }

    public long ToCents()
    {
        return decimal.ToInt64(Amount * CentsPerUnit);
    }

    /// <summary>
    /// Divide o valor em <paramref name="parts"/> partes cuja soma é exatamente o valor original.
    /// As partes são o valor dividido e arredondado; a diferença de centavos fica na primeira.
    /// </summary>
    public IReadOnlyList<Money> Split(int parts)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(parts, 0);

        var regular = new Money(Amount / parts);
        var first = this - (regular * (parts - 1));

        var result = new Money[parts];
        result[0] = first;
        Array.Fill(result, regular, 1, parts - 1);
        return result;
    }

    public static Money Add(Money left, Money right)
    {
        return new Money(left.Amount + right.Amount);
    }

    public static Money Subtract(Money left, Money right)
    {
        return new Money(left.Amount - right.Amount);
    }

    public static Money Negate(Money value)
    {
        return new Money(-value.Amount);
    }

    public static Money Multiply(Money value, decimal factor)
    {
        return new Money(value.Amount * factor);
    }

    public int CompareTo(Money other)
    {
        return Amount.CompareTo(other.Amount);
    }

    public static Money operator +(Money left, Money right)
    {
        return Add(left, right);
    }

    public static Money operator -(Money left, Money right)
    {
        return Subtract(left, right);
    }

    public static Money operator -(Money value)
    {
        return Negate(value);
    }

    public static Money operator *(Money value, decimal factor)
    {
        return Multiply(value, factor);
    }

    public static Money operator *(decimal factor, Money value)
    {
        return Multiply(value, factor);
    }

    public static bool operator <(Money left, Money right)
    {
        return left.CompareTo(right) < 0;
    }

    public static bool operator <=(Money left, Money right)
    {
        return left.CompareTo(right) <= 0;
    }

    public static bool operator >(Money left, Money right)
    {
        return left.CompareTo(right) > 0;
    }

    public static bool operator >=(Money left, Money right)
    {
        return left.CompareTo(right) >= 0;
    }

    /// <summary>Formata no padrão pt-BR, por exemplo <c>R$ 1.234,56</c> ou <c>-R$ 1.234,56</c>.</summary>
    public override string ToString()
    {
        var sign = IsNegative ? "-" : string.Empty;
        return $"{sign}R$ {Math.Abs(Amount).ToString("N2", s_format)}";
    }
}
