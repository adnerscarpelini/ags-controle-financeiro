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
            Primary = "#30C48D",
            PrimaryDarken = "#4AD6A1",
            PrimaryLighten = "#4AD6A1",
            PrimaryContrastText = "#141415",
            Secondary = "#4AD6A1",
            SecondaryContrastText = "#141415",
            Success = "#4AD6A2",
            SuccessContrastText = "#141415",
            Info = "#A1A1A6",
            InfoContrastText = "#141415",
            Warning = "#E5B13A",
            WarningContrastText = "#141415",
            Error = "#FF8177",
            ErrorContrastText = "#141415",
            Background = "#141415",
            BackgroundGray = "#2C2C2F",
            Surface = "#222224",
            DrawerBackground = "#1A1A1C",
            DrawerText = "#F5F5F7",
            DrawerIcon = "#A1A1A6",
            AppbarBackground = "#141415",
            AppbarText = "#F5F5F7",
            TextPrimary = "#F5F5F7",
            TextSecondary = "#A1A1A6",
            TextDisabled = "#A1A1A6",
            ActionDefault = "#A1A1A6",
            ActionDisabled = "#A1A1A6",
            ActionDisabledBackground = "#2C2C2F",
            LinesDefault = "#2C2C2E",
            LinesInputs = "#2C2C2E",
            TableLines = "#2C2C2E",
            TableStriped = "#2C2C2F",
            TableHover = "#2C2C2F",
            Divider = "#2C2C2E",
            DividerLight = "#2C2C2E",
            Skeleton = "#2C2C2F",
            GrayLight = "#2C2C2E",
            GrayLighter = "#2C2C2F",
        },
        PaletteLight = new PaletteLight
        {
            Primary = "#0B7A55",
            PrimaryDarken = "#09684A",
            PrimaryLighten = "#09684A",
            PrimaryContrastText = "#FFFFFF",
            Secondary = "#09684A",
            SecondaryContrastText = "#FFFFFF",
            Success = "#0A7450",
            SuccessContrastText = "#FFFFFF",
            Info = "#6A6A6F",
            InfoContrastText = "#FFFFFF",
            Warning = "#946200",
            WarningContrastText = "#FFFFFF",
            Error = "#C4372E",
            ErrorContrastText = "#FFFFFF",
            Background = "#FFFFFF",
            BackgroundGray = "#F2F2F1",
            Surface = "#FEFEFE",
            DrawerBackground = "#F7F7F6",
            DrawerText = "#1D1D1F",
            DrawerIcon = "#6A6A6F",
            AppbarBackground = "#FFFFFF",
            AppbarText = "#1D1D1F",
            TextPrimary = "#1D1D1F",
            TextSecondary = "#6A6A6F",
            TextDisabled = "#6A6A6F",
            ActionDefault = "#6A6A6F",
            ActionDisabled = "#6A6A6F",
            ActionDisabledBackground = "#F2F2F1",
            LinesDefault = "#E8E8E7",
            LinesInputs = "#E8E8E7",
            TableLines = "#E8E8E7",
            TableStriped = "#F2F2F1",
            TableHover = "#F2F2F1",
            Divider = "#E8E8E7",
            DividerLight = "#E8E8E7",
            Skeleton = "#F2F2F1",
            GrayLight = "#E8E8E7",
            GrayLighter = "#F2F2F1",
        },
        LayoutProperties = new LayoutProperties
        {
            DefaultBorderRadius = "8px",
            DrawerWidthLeft = "232px",
            DrawerMiniWidthLeft = "60px",
        },
        Typography = new Typography
        {
            Default = new DefaultTypography
            {
                FontFamily = ["Inter", "system-ui", "-apple-system", "Segoe UI", "Roboto", "sans-serif"],
                FontSize = ".875rem",
                LetterSpacing = "-.006em",
            },
            Button = new ButtonTypography
            {
                FontSize = ".875rem",
                FontWeight = "500",
                LetterSpacing = "normal",
                TextTransform = "none",
            },
        },
    };
}
