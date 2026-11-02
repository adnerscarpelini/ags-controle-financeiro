using Ags.Finance.SharedKernel;

namespace Ags.Finance.Modules.Accounts.Domain;

internal sealed class Account : Entity
{
    private Account()
    {
    }
    public Guid BankExternalId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string NormalizedName { get; private set; } = string.Empty;
    public AccountKind Kind { get; private set; }
    public string Color { get; private set; } = string.Empty;
    public bool IsActive { get; private set; } = true;
    public Money OpeningBalance { get; private set; }
    public DateOnly? OpeningDate { get; private set; }

    public string? SetOpeningBalance(Money balance, DateOnly? referenceDate)
    {
        if (referenceDate is null || referenceDate == DateOnly.MinValue)
        {
            return "Informe a data de referência do saldo inicial.";
        }
        if (OpeningDate is not null && (OpeningBalance != balance || OpeningDate != referenceDate))
        {
            return "O saldo inicial já foi registrado. Use Ajustar saldo para corrigir o saldo da conta.";
        }
        OpeningBalance = balance;
        OpeningDate = referenceDate;
        return null;
    }

    public static Account Create() => new();
    public string? Update(Guid bankExternalId, string name, AccountKind kind, string color)
    {
        if (bankExternalId == Guid.Empty)
        {
            return "Selecione um banco cadastrado.";
        }
        if (!CatalogRules.IsValidName(name))
        {
            return "Informe um nome com até 100 caracteres.";
        }
        if (!Enum.IsDefined(kind))
        {
            return "Selecione um tipo de conta válido.";
        }
        if (!CatalogRules.IsValidColor(color))
        {
            return "Selecione uma cor da paleta.";
        }
        BankExternalId = bankExternalId;
        Name = name.Trim();
        NormalizedName = CatalogRules.NormalizeName(name);
        Kind = kind;
        Color = color;
        return null;
    }
    public void Deactivate() => IsActive = false;
}
