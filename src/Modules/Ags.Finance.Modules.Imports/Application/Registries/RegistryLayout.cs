using Ags.Finance.Modules.Imports.Contracts;

namespace Ags.Finance.Modules.Imports.Application.Registries;

/// <summary>Layout oficial e versionado de um tipo de cadastro. Exportador (#61) e importador (#28) usam esta mesma definição.</summary>
internal sealed record RegistryLayout(RegistryKind Kind, string Type, int Version, IReadOnlyList<string> Columns, IReadOnlyList<IReadOnlyList<string>> Examples)
{
    public string FileName => $"cadastro-{Type.Replace('_', '-')}-v{Version}.csv";

    public static RegistryLayout For(RegistryKind kind) => All.Single(x => x.Kind == kind);

    // Identificadores fictícios fixos, só para os exemplos.
    private const string Bank = "0190a000-0000-7000-8000-000000000001";
    private const string Account = "0190a000-0000-7000-8000-000000000002";
    private const string Card = "0190a000-0000-7000-8000-000000000003";
    private const string Category = "0190a000-0000-7000-8000-000000000004";
    private const string Subcategory = "0190a000-0000-7000-8000-000000000005";

    public static IReadOnlyList<RegistryLayout> All { get; } =
    [
        new(RegistryKind.Banks, "banks", 1, ["external_id", "name"],
            [[Bank, "Banco Exemplo"]]),
        new(RegistryKind.Accounts, "accounts", 1, ["external_id", "bank_external_id", "name", "kind", "color", "is_active", "opening_balance_cents", "opening_date"],
            [[Account, Bank, "Conta Exemplo", "Checking", "green", "true", "100000", "2026-01-01"]]),
        new(RegistryKind.Cards, "cards", 1, ["external_id", "account_external_id", "name", "brand", "credit_limit_cents", "closing_day", "due_day", "includes_closing_day", "color", "is_active"],
            [[Card, Account, "Cartão Exemplo", "Visa", "500000", "3", "10", "true", "violet", "true"]]),
        new(RegistryKind.CardDigits, "card_digits", 1, ["external_id", "card_external_id", "digits", "usage", "label", "is_active"],
            [["0190a000-0000-7000-8000-000000000006", Card, "1234", "Own", "Próprio", "true"]]),
        new(RegistryKind.Categories, "categories", 1, ["external_id", "name", "kind", "is_active"],
            [[Category, "Mercado", "Expense", "true"]]),
        new(RegistryKind.Subcategories, "subcategories", 1, ["external_id", "category_external_id", "name", "is_active"],
            [[Subcategory, Category, "Feira", "true"]]),
        new(RegistryKind.Merchants, "merchants", 1, ["external_id", "name", "default_subcategory_external_id", "is_active"],
            [["0190a000-0000-7000-8000-000000000007", "Loja Exemplo", Subcategory, "true"]]),
        new(RegistryKind.People, "people", 1, ["external_id", "name", "payer_description"],
            [["0190a000-0000-7000-8000-000000000008", "Pessoa Exemplo", "Pessoa Exemplo (pagador)"]]),
    ];
}
