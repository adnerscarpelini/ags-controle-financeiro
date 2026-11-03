namespace Ags.Finance.Modules.Preferences.Contracts;

public enum ThemePreference
{
    Dark = 0,
    Light = 1,

    /// <summary>Segue a preferência de tema do sistema operacional.</summary>
    Auto = 2,
}

public enum StartPage
{
    Dashboard = 0,
    Transactions = 1,
    Invoices = 2,
}

/// <summary>Quando uma ocorrência recorrente futura do cartão passa a somar no total da fatura Aberta.</summary>
public enum RecurringInvoiceMode
{
    /// <summary>Soma só a partir da data da ocorrência; antes disso ela aparece como valor previsto.</summary>
    SumOnDate = 0,

    /// <summary>Soma desde que a ocorrência é gerada, mesmo com data futura.</summary>
    SumUpfront = 1,
}

/// <summary>Quais seções o dashboard exibe.</summary>
public sealed record DashboardSections(bool Summary, bool Pending, bool Categories, bool Invoices, bool HeatMap)
{
    public static DashboardSections All { get; } = new(true, true, true, true, true);

    public bool AnyVisible => Summary || Pending || Categories || Invoices || HeatMap;
}

/// <summary>As parametrizações do aplicativo. Os valores permitidos de cada campo numérico estão em <see cref="PreferenceOptions"/>.</summary>
public sealed record AppPreferences(
    ThemePreference Theme,
    StartPage StartPage,
    RecurringInvoiceMode RecurringInvoices,
    int PendingWindowDays,
    int TopCategories,
    DashboardSections Sections,
    int PageSize,
    int SessionHours)
{
    public static AppPreferences Default { get; } = new(
        ThemePreference.Dark,
        StartPage.Dashboard,
        RecurringInvoiceMode.SumOnDate,
        PendingWindowDays: 7,
        TopCategories: 10,
        DashboardSections.All,
        PageSize: 100,
        SessionHours: 12);

    /// <summary>Duração do login sem "lembrar", a partir do momento do login.</summary>
    public TimeSpan SessionLifetime => TimeSpan.FromHours(SessionHours);
}

/// <summary>Opções permitidas para as preferências numéricas.</summary>
public static class PreferenceOptions
{
    public static IReadOnlyList<int> PendingWindowDays { get; } = [7, 15, 30];

    public static IReadOnlyList<int> TopCategories { get; } = [5, 10, 20];

    public static IReadOnlyList<int> PageSizes { get; } = [25, 50, 100, 200];

    /// <summary>12 horas, 24 horas ou 7 dias.</summary>
    public static IReadOnlyList<int> SessionHours { get; } = [12, 24, 168];
}
