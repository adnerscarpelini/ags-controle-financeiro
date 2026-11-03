using Ags.Finance.SharedKernel;

namespace Ags.Finance.Modules.Reimbursements.Domain;

/// <summary>Uma pessoa deve <see cref="Percent"/> dos lançamentos do filtro salvo, a partir de <see cref="StartMonth"/>. O filtro nunca é copiado.</summary>
internal sealed class SharingRule : Entity
{
    public Guid PersonExternalId { get; private set; }
    public Guid SavedFilterExternalId { get; private set; }
    public decimal Percent { get; private set; }
    public AccountingMonth StartMonth { get; private set; }

    public string? Update(Guid personExternalId, Guid savedFilterExternalId, decimal percent, AccountingMonth startMonth)
    {
        if (personExternalId == Guid.Empty || savedFilterExternalId == Guid.Empty || startMonth.Year == 0)
        {
            return "Selecione a pessoa, o filtro salvo e o mês inicial.";
        }
        if (percent is < 0 or > 100 || decimal.Round(percent, 2) != percent)
        {
            return "Informe um percentual de 0 a 100, com até duas casas decimais.";
        }
        PersonExternalId = personExternalId;
        SavedFilterExternalId = savedFilterExternalId;
        Percent = percent;
        StartMonth = startMonth;
        return null;
    }
}
