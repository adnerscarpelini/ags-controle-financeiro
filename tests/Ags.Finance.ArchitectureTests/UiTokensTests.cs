using System.Text.RegularExpressions;

namespace Ags.Finance.ArchitectureTests;

/// <summary>
/// As cores da interface vêm dos tokens (variáveis CSS) de docs/architecture/ui-design.md.
/// Só o arquivo de tokens pode ter valores literais de cor.
/// </summary>
public sealed partial class UiTokensTests
{
    private const string TokensFile = "tokens.css";

    [Fact]
    public void OnlyTheTokensFileDeclaresLiteralColors()
    {
        var webRoot = Path.Combine(ProjectFiles.FindRepositoryRoot(), "src", "Ags.Finance.Web");

        var violations = UiSourceFiles(webRoot)
            .Where(file => !string.Equals(Path.GetFileName(file), TokensFile, StringComparison.OrdinalIgnoreCase))
            .SelectMany(file => LiteralColorRegex().Matches(File.ReadAllText(file))
                .Select(match => $"{Path.GetRelativePath(webRoot, file)}: {match.Value}"))
            .ToList();

        Assert.True(
            violations.Count == 0,
            $"Use as variáveis de tokens em vez de cores literais: {string.Join("; ", violations)}");
    }

    [Fact]
    public void TheTokensFileDefinesEveryPaletteColorWithTheDocumentedDarkValue()
    {
        var palette = Palette();

        string[] dark =
        [
            "--color-background: #0E1311",
            "--color-surface: #151C19",
            "--color-surface-raised: #1C2521",
            "--color-border: #27332E",
            "--color-text-primary: #E6EDE9",
            "--color-text-secondary: #93A39B",
            "--color-accent: #10B981",
            "--color-accent-hover: #34D399",
            "--color-income: #34D399",
            "--color-expense: #F87171",
            "--color-warning: #FBBF24",
            "--color-bank-itau: #FB923C",
            "--color-bank-nubank: #A78BFA",
            "--color-bank-bradesco: #F87171",
        ];

        Assert.All(dark, expected =>
        {
            var parts = expected.Split(": ");
            Assert.True(palette.TryGetValue(parts[0], out var colors), $"Token ausente: {parts[0]}");
            Assert.Equal(parts[1], colors.Dark, ignoreCase: true);
        });
    }

    [Fact]
    public void EveryTextColorMeetsWcagAaContrastInBothThemes()
    {
        var palette = Palette();
        string[] surfaces = ["--color-background", "--color-surface", "--color-surface-raised"];
        string[] text =
        [
            "--color-text-primary", "--color-text-secondary", "--color-accent", "--color-accent-hover", "--color-income",
            "--color-expense", "--color-warning", "--color-bank-itau", "--color-bank-nubank", "--color-bank-bradesco",
        ];

        var failures = new List<string>();
        foreach (var theme in new[] { "light", "dark" })
        {
            string Pick(string token) => theme == "light" ? palette[token].Light : palette[token].Dark;

            foreach (var foreground in text)
            {
                foreach (var surface in surfaces)
                {
                    Check(failures, theme, foreground, surface, Pick(foreground), Pick(surface));
                }
            }

            // Rótulo do botão: a cor de fundo da página sobre o accent (e o accent em hover).
            Check(failures, theme, "--color-background", "--color-accent", Pick("--color-background"), Pick("--color-accent"));
            Check(failures, theme, "--color-background", "--color-accent-hover", Pick("--color-background"), Pick("--color-accent-hover"));
        }

        Assert.True(failures.Count == 0, "Contraste abaixo de 4,5:1: " + string.Join("; ", failures));
    }

    [Fact]
    public void BothThemesAreDifferentAndEveryTokenHasBothSides()
    {
        var palette = Palette();

        Assert.NotEmpty(palette);
        Assert.All(palette, token => Assert.False(string.Equals(token.Value.Light, token.Value.Dark, StringComparison.OrdinalIgnoreCase), token.Key));
    }

    private static void Check(List<string> failures, string theme, string foreground, string background, string foregroundHex, string backgroundHex)
    {
        var ratio = ContrastRatio(foregroundHex, backgroundHex);
        if (ratio < 4.5)
        {
            failures.Add($"{theme}: {foreground} {foregroundHex} sobre {background} {backgroundHex} = {ratio:0.00}");
        }
    }

    private static Dictionary<string, (string Light, string Dark)> Palette()
    {
        var tokens = File.ReadAllText(Path.Combine(
            ProjectFiles.FindRepositoryRoot(), "src", "Ags.Finance.Web", "wwwroot", "css", TokensFile));
        return PaletteRegex().Matches(tokens).ToDictionary(match => match.Groups["name"].Value, match => (match.Groups["light"].Value, match.Groups["dark"].Value));
    }

    private static double ContrastRatio(string first, string second)
    {
        var a = Luminance(first);
        var b = Luminance(second);
        var (lighter, darker) = a > b ? (a, b) : (b, a);
        return (lighter + 0.05) / (darker + 0.05);
    }

    private static double Luminance(string hex)
    {
        double Channel(int offset)
        {
            var value = int.Parse(hex.AsSpan(1 + offset, 2), System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture) / 255.0;
            return value <= 0.03928 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
        }

        return (0.2126 * Channel(0)) + (0.7152 * Channel(2)) + (0.0722 * Channel(4));
    }

    [GeneratedRegex(@"(?<name>--color-[a-z-]+):\s*light-dark\((?<light>#[0-9A-Fa-f]{6}),\s*(?<dark>#[0-9A-Fa-f]{6})\)")]
    private static partial Regex PaletteRegex();
    private static IEnumerable<string> UiSourceFiles(string webRoot)
    {
        var directories = new[] { Path.Combine(webRoot, "Components"), Path.Combine(webRoot, "wwwroot") };
        return directories
            .Where(Directory.Exists)
            .SelectMany(directory => Directory.EnumerateFiles(directory, "*.*", SearchOption.AllDirectories))
            .Where(file => file.EndsWith(".razor", StringComparison.OrdinalIgnoreCase)
                || file.EndsWith(".css", StringComparison.OrdinalIgnoreCase)
                || file.EndsWith(".js", StringComparison.OrdinalIgnoreCase));
    }

    [GeneratedRegex(@"(?<![\w&/])#(?:[0-9a-fA-F]{8}|[0-9a-fA-F]{6}|[0-9a-fA-F]{3,4})\b|\b(?:rgb|rgba|hsl|hsla|oklch|oklab|hwb)\(", RegexOptions.IgnoreCase)]
    private static partial Regex LiteralColorRegex();
}
