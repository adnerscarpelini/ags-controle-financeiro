using System.Text.RegularExpressions;
using Ags.Finance.Web.Components.Layout;
using MudBlazor;
using MudBlazor.Utilities;

namespace Ags.Finance.Web.Tests;

/// <summary>O tema do MudBlazor repete os valores de tokens.css, que é a fonte de verdade: os dois não podem divergir.</summary>
public sealed class AppThemeTests
{
    public static TheoryData<string, Func<Palette, MudColor>> Tokens => new()
    {
        { "--color-background", palette => palette.Background },
        { "--color-surface", palette => palette.Surface },
        { "--color-surface-raised", palette => palette.BackgroundGray },
        { "--color-border", palette => palette.LinesDefault },
        { "--color-text-primary", palette => palette.TextPrimary },
        { "--color-text-secondary", palette => palette.TextSecondary },
        { "--color-accent", palette => palette.Primary },
        { "--color-accent-hover", palette => palette.PrimaryDarken },
        { "--color-expense", palette => palette.Error },
        { "--color-warning", palette => palette.Warning },
    };

    [Theory]
    [MemberData(nameof(Tokens))]
    public void EveryPaletteColorMatchesTheTokenInBothThemes(string token, Func<Palette, MudColor> read)
    {
        var match = Regex.Match(ReadTokens(), Regex.Escape(token) + @":\s*light-dark\((?<light>#[0-9A-Fa-f]{6}),\s*(?<dark>#[0-9A-Fa-f]{6})\)");

        Assert.True(match.Success, $"Token ausente: {token}");
        Assert.Equal(match.Groups["light"].Value, Hex(read(AppTheme.Instance.PaletteLight)), ignoreCase: true);
        Assert.Equal(match.Groups["dark"].Value, Hex(read(AppTheme.Instance.PaletteDark)), ignoreCase: true);
    }

    [Fact]
    public void TheThemeUsesTheLocalInterFont()
    {
        Assert.Equal("Inter", AppTheme.Instance.Typography.Default.FontFamily![0]);
    }

    private static string Hex(MudColor color) => color.Value[..7];

    private static string ReadTokens()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Ags.Finance.slnx")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return File.ReadAllText(Path.Combine(directory.FullName, "src", "Ags.Finance.Web", "wwwroot", "css", "tokens.css"));
    }
}
