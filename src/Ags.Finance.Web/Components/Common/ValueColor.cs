using Ags.Finance.SharedKernel;

namespace Ags.Finance.Web.Components.Common;

/// <summary>
/// Cor do texto de um valor em toda a aplicação (#112): despesa em vermelho, receita em verde,
/// saldo pelo sinal e zero em cor neutra. A cor reforça, mas o sinal de menos continua vindo do próprio valor.
/// </summary>
internal static class ValueColor
{
    public const string Neutral = "value-neutral";
    public const string Positive = "value-positive";
    public const string Negative = "value-negative";

    public static string Expense(Money value) => value.IsZero ? Neutral : Negative;

    public static string Income(Money value) => value.IsZero ? Neutral : Positive;

    public static string Balance(Money value) => value.IsZero ? Neutral : value.IsNegative ? Negative : Positive;

    /// <summary>Saldo devido, como o restante de uma fatura: a dever em vermelho, crédito em verde.</summary>
    public static string Owed(Money value) => value.IsZero ? Neutral : value.IsNegative ? Positive : Negative;

    public static string OfKind(Money value, bool isExpense) => isExpense ? Expense(value) : Income(value);

    public static string Percent(decimal? percent) => percent switch
    {
        > 0 => Positive,
        < 0 => Negative,
        _ => Neutral,
    };
}
