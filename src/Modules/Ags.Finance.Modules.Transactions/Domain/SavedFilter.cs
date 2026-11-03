namespace Ags.Finance.Modules.Transactions.Domain;

internal sealed class SavedFilter : Entity
{
    private const int MaxItems = 200;

    public string Name { get; private set; } = string.Empty;
    public string NormalizedName { get; private set; } = string.Empty;
    public Guid[] CategoryExternalIds { get; private set; } = [];
    public Guid[] SubcategoryExternalIds { get; private set; } = [];
    public Guid[] MerchantExternalIds { get; private set; } = [];
    public Guid[] AccountExternalIds { get; private set; } = [];
    public Guid[] CardExternalIds { get; private set; } = [];
    public Guid[] CardLastDigitsExternalIds { get; private set; } = [];
    public string Text { get; private set; } = string.Empty;
    public bool? IsEffective { get; private set; }

    public string? Update(string name, IEnumerable<Guid> categories, IEnumerable<Guid> subcategories, IEnumerable<Guid> merchants, IEnumerable<Guid> accounts,
        IEnumerable<Guid> cards, IEnumerable<Guid> finals, string text, bool? isEffective)
    {
        var normalized = Merchant.Normalize(name ?? string.Empty);
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 100 || normalized.Length == 0)
        {
            return "Informe um nome com letras ou números e até 100 caracteres.";
        }
        var lists = new[] { categories, subcategories, merchants, accounts, cards, finals }.Select(x => x.Distinct().ToArray()).ToArray();
        text = (text ?? string.Empty).Trim();
        if (text.Length > 200 || lists.Any(x => x.Length > MaxItems || x.Contains(Guid.Empty)))
        {
            return "Informe critérios válidos: texto de até 200 caracteres e até 200 itens por lista.";
        }
        if (lists.All(x => x.Length == 0) && text.Length == 0 && isEffective is null)
        {
            return "Informe ao menos um critério para salvar o filtro.";
        }
        Name = name.Trim();
        NormalizedName = normalized;
        (CategoryExternalIds, SubcategoryExternalIds, MerchantExternalIds, AccountExternalIds, CardExternalIds, CardLastDigitsExternalIds) = (lists[0], lists[1], lists[2], lists[3], lists[4], lists[5]);
        Text = text;
        IsEffective = isEffective;
        return null;
    }
}
