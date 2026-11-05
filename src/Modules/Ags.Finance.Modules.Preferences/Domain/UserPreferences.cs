namespace Ags.Finance.Modules.Preferences.Domain;

internal enum ThemeMode
{
    Dark = 0,
    Light = 1,
    Auto = 2,
}

internal enum StartScreen
{
    Dashboard = 0,
    Transactions = 1,
    Invoices = 2,
}

internal enum RecurringMode
{
    SumOnDate = 0,
    SumUpfront = 1,
}

internal readonly record struct PreferenceValues(
    ThemeMode Theme,
    StartScreen StartScreen,
    RecurringMode RecurringMode,
    int PendingWindowDays,
    int TopCategories,
    bool ShowSummary,
    bool ShowPending,
    bool ShowCategories,
    bool ShowInvoices,
    int PageSize,
    int SessionHours,
    bool GroupCardEntries);

/// <summary>As preferências do único usuário. Existe no máximo um registro.</summary>
internal sealed class UserPreferences : Entity
{
    private static readonly int[] s_pendingWindowDays = [7, 15, 30];
    private static readonly int[] s_topCategories = [5, 10, 20];
    private static readonly int[] s_pageSizes = [25, 50, 100, 200];
    private static readonly int[] s_sessionHours = [12, 24, 168];

    public ThemeMode Theme { get; private set; }

    public StartScreen StartScreen { get; private set; }

    public RecurringMode RecurringMode { get; private set; }

    public int PendingWindowDays { get; private set; }

    public int TopCategories { get; private set; }

    public bool ShowSummary { get; private set; }

    public bool ShowPending { get; private set; }

    public bool ShowCategories { get; private set; }

    public bool ShowInvoices { get; private set; }

    public int PageSize { get; private set; }

    public int SessionHours { get; private set; }

    public bool GroupCardEntries { get; private set; }

    /// <summary>Valida todos os campos antes de aplicar qualquer um: em caso de erro nada muda.</summary>
    public string? Apply(PreferenceValues values)
    {
        if (!Enum.IsDefined(values.Theme)) { return "Escolha um tema válido."; }
        if (!Enum.IsDefined(values.StartScreen)) { return "Escolha uma tela inicial válida."; }
        if (!Enum.IsDefined(values.RecurringMode)) { return "Escolha como somar as recorrentes do cartão."; }
        if (!s_pendingWindowDays.Contains(values.PendingWindowDays)) { return "Escolha uma janela de pendências de 7, 15 ou 30 dias."; }
        if (!s_topCategories.Contains(values.TopCategories)) { return "Escolha 5, 10 ou 20 categorias no gráfico."; }
        if (!s_pageSizes.Contains(values.PageSize)) { return "Escolha 25, 50, 100 ou 200 itens por página."; }
        if (!s_sessionHours.Contains(values.SessionHours)) { return "Escolha uma duração de sessão de 12 horas, 24 horas ou 7 dias."; }

        Theme = values.Theme;
        StartScreen = values.StartScreen;
        RecurringMode = values.RecurringMode;
        PendingWindowDays = values.PendingWindowDays;
        TopCategories = values.TopCategories;
        ShowSummary = values.ShowSummary;
        ShowPending = values.ShowPending;
        ShowCategories = values.ShowCategories;
        ShowInvoices = values.ShowInvoices;
        PageSize = values.PageSize;
        SessionHours = values.SessionHours;
        GroupCardEntries = values.GroupCardEntries;
        return null;
    }
}
