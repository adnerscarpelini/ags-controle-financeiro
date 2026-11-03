using Ags.Finance.Modules.Imports.Contracts;

namespace Ags.Finance.Modules.Imports.Application.Transactions;

/// <summary>Layout oficial e versionado dos arquivos de lançamentos. Exportador (#62) e importador (#29) usam esta mesma definição.</summary>
internal sealed record TransactionLayout(TransactionFileKind Kind, string Type, int Version, IReadOnlyList<string> Columns, IReadOnlyList<IReadOnlyList<string>> Examples)
{
    public string FileName => $"lancamentos-{Type.Replace('_', '-')}-v{Version}.csv";

    public static TransactionLayout For(TransactionFileKind kind) => All.Single(x => x.Kind == kind);

    // Identificadores fictícios fixos, só para os exemplos (os mesmos do exemplo de cadastros).
    private const string Merchant = "0190a000-0000-7000-8000-000000000007";
    private const string Subcategory = "0190a000-0000-7000-8000-000000000005";
    private const string Account = "0190a000-0000-7000-8000-000000000002";
    private const string Card = "0190a000-0000-7000-8000-000000000003";
    private const string Final = "0190a000-0000-7000-8000-000000000006";
    private const string Series = "0190a000-0000-7000-8000-000000000011";
    private const string Purchase = "0190a000-0000-7000-8000-000000000012";
    private const string Person = "0190a000-0000-7000-8000-000000000008";

    public static IReadOnlyList<TransactionLayout> All { get; } =
    [
        new(TransactionFileKind.Transactions, "transactions", 3,
            ["external_id", "date", "reference_month", "kind", "amount_cents", "merchant_external_id", "subcategory_external_id", "account_external_id", "card_external_id",
                "card_digits_external_id", "details", "is_effective", "effective_date", "installment_purchase_external_id", "installment_number", "installment_count",
                "recurring_entry_external_id", "occurrence_month", "reimbursement_person_external_id", "reimbursement_percent"],
            [
                ["0190a000-0000-7000-8000-000000000021", "2026-10-05", "2026-10", "Expense", "14290", Merchant, Subcategory, Account, "", "", "Compra avulsa fictícia", "true", "2026-10-05", "", "", "", "", "", Person, "50"],
                ["0190a000-0000-7000-8000-000000000022", "2026-10-06", "2026-11", "Expense", "10000", Merchant, Subcategory, Account, Card, Final, "Parcela fictícia", "true", "2026-10-06", Purchase, "1", "3", "", "", "", ""],
                ["0190a000-0000-7000-8000-000000000023", "2026-11-05", "2026-11", "Expense", "9990", Merchant, Subcategory, Account, "", "", "Assinatura fictícia", "false", "", "", "", "", Series, "2026-11-01", "", ""],
            ]),
        new(TransactionFileKind.RecurringSeries, "recurring_series", 3,
            ["external_id", "start_date", "day", "end_month", "merchant_external_id", "subcategory_external_id", "account_external_id", "card_external_id", "card_digits_external_id",
                "amount_cents", "details", "reference_month", "is_active", "reimbursement_person_external_id", "reimbursement_percent"],
            [[Series, "2026-10-05", "5", "", Merchant, Subcategory, Account, "", "", "9990", "Assinatura fictícia", "", "true", "", ""]]),
    ];
}
