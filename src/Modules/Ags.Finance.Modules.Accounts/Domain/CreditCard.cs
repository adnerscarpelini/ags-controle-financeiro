using Ags.Finance.SharedKernel;

namespace Ags.Finance.Modules.Accounts.Domain;

internal sealed class CreditCard : Entity
{
    private CreditCard()
    {
    }
    public Guid AccountExternalId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public CardBrand Brand { get; private set; }
    public Money CreditLimit { get; private set; }
    public int ClosingDay { get; private set; }
    public int DueDay { get; private set; }
    public bool IncludesClosingDay { get; private set; }
    public string Color { get; private set; } = string.Empty;
    public bool IsActive { get; private set; } = true;

    public static CreditCard Create() => new();
    public string? Update(Guid accountExternalId, string name, CardBrand brand, Money creditLimit, int closingDay, int dueDay, bool includesClosingDay, string color)
    {
        if (accountExternalId == Guid.Empty)
        {
            return "Selecione uma conta corrente cadastrada.";
        }
        if (!CatalogRules.IsValidName(name))
        {
            return "Informe um nome com até 100 caracteres.";
        }
        if (!Enum.IsDefined(brand))
        {
            return "Selecione Visa, Mastercard ou Elo.";
        }
        if (creditLimit.IsNegative)
        {
            return "O limite não pode ser negativo.";
        }
        if (closingDay is < 1 or > 31)
        {
            return "O dia de fechamento deve estar entre 1 e 31.";
        }
        if (dueDay is < 1 or > 31)
        {
            return "O dia de vencimento deve estar entre 1 e 31.";
        }
        if (!CatalogRules.IsValidColor(color))
        {
            return "Selecione uma cor da paleta.";
        }
        AccountExternalId = accountExternalId;
        Name = name.Trim();
        Brand = brand;
        CreditLimit = creditLimit;
        ClosingDay = closingDay;
        DueDay = dueDay;
        IncludesClosingDay = includesClosingDay;
        Color = color;
        return null;
    }
    public void Deactivate() => IsActive = false;
}
