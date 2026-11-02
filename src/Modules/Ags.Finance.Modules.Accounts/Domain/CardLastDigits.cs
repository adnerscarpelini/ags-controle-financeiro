namespace Ags.Finance.Modules.Accounts.Domain;

internal enum CardUsage
{
    Own,
    Shared,
    Subscription
}

internal sealed class CardLastDigits : Entity
{
    private CardLastDigits()
    {
    }

    public Guid CardExternalId { get; private set; }
    public string Digits { get; private set; } = string.Empty;
    public CardUsage Usage { get; private set; }
    public string Label { get; private set; } = string.Empty;
    public bool IsActive { get; private set; } = true;

    public static CardLastDigits Create() => new();

    public string? Update(Guid cardExternalId, string digits, CardUsage usage, string label)
    {
        if (cardExternalId == Guid.Empty)
        {
            return "Selecione um cartão cadastrado.";
        }
        if (digits is null || digits.Length != 4 || digits.Any(character => character is < '0' or > '9'))
        {
            return "O final deve conter exatamente 4 dígitos numéricos.";
        }
        if (!Enum.IsDefined(usage))
        {
            return "Selecione um tipo de uso válido.";
        }

        CardExternalId = cardExternalId;
        Digits = digits;
        Usage = usage;
        Label = label?.Trim() ?? string.Empty;
        return null;
    }

    public void Deactivate() => IsActive = false;
}
