using MudBlazor;

namespace Ags.Finance.Web.Components.Layout;

/// <summary>
/// Tema do MudBlazor. Repete os valores de wwwroot/css/tokens.css porque a biblioteca calcula variações a partir da cor
/// e não aceita var(); tokens.css continua a fonte de verdade, e AppThemeTests falha se os dois divergirem.
/// </summary>
internal static class AppTheme
{
    public static MudTheme Instance { get; } = new()
    {
        PaletteDark = new PaletteDark
        {
            Primary = "#10B981",
            PrimaryDarken = "#34D399",
            PrimaryLighten = "#34D399",
            PrimaryContrastText = "#121212",
            Secondary = "#34D399",
            SecondaryContrastText = "#121212",
            Success = "#34D399",
            SuccessContrastText = "#121212",
            Info = "#34D399",
            InfoContrastText = "#121212",
            Warning = "#FBBF24",
            WarningContrastText = "#121212",
            Error = "#F87171",
            ErrorContrastText = "#121212",
            Background = "#121212",
            BackgroundGray = "#242424",
            Surface = "#1A1A1A",
            DrawerBackground = "#1A1A1A",
            DrawerText = "#EDEDED",
            DrawerIcon = "#A3A3A3",
            AppbarBackground = "#1A1A1A",
            AppbarText = "#EDEDED",
            TextPrimary = "#EDEDED",
            TextSecondary = "#A3A3A3",
            TextDisabled = "#A3A3A3",
            ActionDefault = "#A3A3A3",
            ActionDisabled = "#A3A3A3",
            ActionDisabledBackground = "#242424",
            LinesDefault = "#2E2E2E",
            LinesInputs = "#2E2E2E",
            TableLines = "#2E2E2E",
            TableStriped = "#242424",
            TableHover = "#242424",
            Divider = "#2E2E2E",
            DividerLight = "#2E2E2E",
            Skeleton = "#242424",
            GrayLight = "#2E2E2E",
            GrayLighter = "#242424",
        },
        PaletteLight = new PaletteLight
        {
            Primary = "#047857",
            PrimaryDarken = "#065F46",
            PrimaryLighten = "#065F46",
            PrimaryContrastText = "#F4F7F5",
            Secondary = "#065F46",
            SecondaryContrastText = "#F4F7F5",
            Success = "#047857",
            SuccessContrastText = "#F4F7F5",
            Info = "#047857",
            InfoContrastText = "#F4F7F5",
            Warning = "#92400E",
            WarningContrastText = "#F4F7F5",
            Error = "#B91C1C",
            ErrorContrastText = "#F4F7F5",
            Background = "#F4F7F5",
            BackgroundGray = "#E8EFEB",
            Surface = "#FFFFFF",
            DrawerBackground = "#FFFFFF",
            DrawerText = "#13201A",
            DrawerIcon = "#4B5C54",
            AppbarBackground = "#FFFFFF",
            AppbarText = "#13201A",
            TextPrimary = "#13201A",
            TextSecondary = "#4B5C54",
            TextDisabled = "#4B5C54",
            ActionDefault = "#4B5C54",
            ActionDisabled = "#4B5C54",
            ActionDisabledBackground = "#E8EFEB",
            LinesDefault = "#CBD7D0",
            LinesInputs = "#CBD7D0",
            TableLines = "#CBD7D0",
            TableStriped = "#E8EFEB",
            TableHover = "#E8EFEB",
            Divider = "#CBD7D0",
            DividerLight = "#CBD7D0",
            Skeleton = "#E8EFEB",
            GrayLight = "#CBD7D0",
            GrayLighter = "#E8EFEB",
        },
        LayoutProperties = new LayoutProperties
        {
            DefaultBorderRadius = "12px",
            DrawerWidthLeft = "256px",
            DrawerMiniWidthLeft = "76px",
        },
        Typography = new Typography
        {
            Default = new DefaultTypography
            {
                FontFamily = ["Inter", "system-ui", "-apple-system", "Segoe UI", "Roboto", "sans-serif"],
            },
        },
    };
}
