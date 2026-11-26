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
            Primary = "#9A8AFF",
            PrimaryDarken = "#B4A8FF",
            PrimaryLighten = "#B4A8FF",
            PrimaryContrastText = "#0B0B10",
            Secondary = "#B4A8FF",
            SecondaryContrastText = "#0B0B10",
            Success = "#3DDC97",
            SuccessContrastText = "#0B0B10",
            Info = "#9C9CB0",
            InfoContrastText = "#0B0B10",
            Warning = "#F4B740",
            WarningContrastText = "#0B0B10",
            Error = "#FF6B81",
            ErrorContrastText = "#0B0B10",
            Background = "#0B0B10",
            BackgroundGray = "#22222E",
            Surface = "#1B1B25",
            DrawerBackground = "#0F0F16",
            DrawerText = "#F3F3F8",
            DrawerIcon = "#9C9CB0",
            AppbarBackground = "#0B0B10",
            AppbarText = "#F3F3F8",
            TextPrimary = "#F3F3F8",
            TextSecondary = "#9C9CB0",
            TextDisabled = "#9C9CB0",
            ActionDefault = "#9C9CB0",
            ActionDisabled = "#9C9CB0",
            ActionDisabledBackground = "#22222E",
            LinesDefault = "#24242E",
            LinesInputs = "#24242E",
            TableLines = "#24242E",
            TableStriped = "#22222E",
            TableHover = "#22222E",
            Divider = "#24242E",
            DividerLight = "#24242E",
            Skeleton = "#22222E",
            GrayLight = "#24242E",
            GrayLighter = "#22222E",
        },
        PaletteLight = new PaletteLight
        {
            Primary = "#6544E0",
            PrimaryDarken = "#5534D0",
            PrimaryLighten = "#5534D0",
            PrimaryContrastText = "#FFFFFF",
            Secondary = "#5534D0",
            SecondaryContrastText = "#FFFFFF",
            Success = "#0A7A4B",
            SuccessContrastText = "#FFFFFF",
            Info = "#5D5D70",
            InfoContrastText = "#FFFFFF",
            Warning = "#8F5E00",
            WarningContrastText = "#FFFFFF",
            Error = "#C82A43",
            ErrorContrastText = "#FFFFFF",
            Background = "#F7F7FA",
            BackgroundGray = "#F1F1F6",
            Surface = "#FFFFFF",
            DrawerBackground = "#FFFFFF",
            DrawerText = "#16161E",
            DrawerIcon = "#5D5D70",
            AppbarBackground = "#F7F7FA",
            AppbarText = "#16161E",
            TextPrimary = "#16161E",
            TextSecondary = "#5D5D70",
            TextDisabled = "#5D5D70",
            ActionDefault = "#5D5D70",
            ActionDisabled = "#5D5D70",
            ActionDisabledBackground = "#F1F1F6",
            LinesDefault = "#ECECF2",
            LinesInputs = "#ECECF2",
            TableLines = "#ECECF2",
            TableStriped = "#F1F1F6",
            TableHover = "#F1F1F6",
            Divider = "#ECECF2",
            DividerLight = "#ECECF2",
            Skeleton = "#F1F1F6",
            GrayLight = "#ECECF2",
            GrayLighter = "#F1F1F6",
        },
        LayoutProperties = new LayoutProperties
        {
            DefaultBorderRadius = "10px",
            DrawerWidthLeft = "248px",
            DrawerMiniWidthLeft = "60px",
        },
        Typography = new Typography
        {
            Default = new DefaultTypography
            {
                FontFamily = ["Geist", "system-ui", "-apple-system", "Segoe UI", "Roboto", "sans-serif"],
                FontSize = ".875rem",
                LetterSpacing = "-.006em",
            },
            Button = new ButtonTypography
            {
                FontSize = ".875rem",
                FontWeight = "550",
                LetterSpacing = "-.006em",
                TextTransform = "none",
            },
        },
    };
}
