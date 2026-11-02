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
    public void TheTokensFileDefinesEveryPaletteColor()
    {
        var tokens = File.ReadAllText(Path.Combine(
            ProjectFiles.FindRepositoryRoot(), "src", "Ags.Finance.Web", "wwwroot", "css", TokensFile));

        string[] expected =
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
        ];

        Assert.All(expected, token => Assert.Contains(token, tokens, StringComparison.Ordinal));
    }

    private static IEnumerable<string> UiSourceFiles(string webRoot)
    {
        var directories = new[] { Path.Combine(webRoot, "Components"), Path.Combine(webRoot, "wwwroot") };
        return directories
            .Where(Directory.Exists)
            .SelectMany(directory => Directory.EnumerateFiles(directory, "*.*", SearchOption.AllDirectories))
            .Where(file => file.EndsWith(".razor", StringComparison.OrdinalIgnoreCase)
                || file.EndsWith(".css", StringComparison.OrdinalIgnoreCase));
    }

    [GeneratedRegex(@"(?<![\w&/])#(?:[0-9a-fA-F]{8}|[0-9a-fA-F]{6}|[0-9a-fA-F]{3,4})\b|\b(?:rgb|rgba|hsl|hsla|oklch|oklab|hwb)\(", RegexOptions.IgnoreCase)]
    private static partial Regex LiteralColorRegex();
}
