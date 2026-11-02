using System.Xml.Linq;

namespace Ags.Finance.ArchitectureTests;

internal static class ProjectFiles
{
    private static readonly char[] s_pathSeparators = ['\\', '/'];

    /// <summary>Nomes dos projetos referenciados (ProjectReference) pelo .csproj informado.</summary>
    public static IReadOnlyList<string> ProjectReferencesOf(string projectName, string folder)
    {
        var path = Path.Combine(FindRepositoryRoot(), folder, projectName, $"{projectName}.csproj");
        Assert.True(File.Exists(path), $"Projeto não encontrado: {path}");

        return XDocument.Load(path)
            .Descendants("ProjectReference")
            .Select(reference => reference.Attribute("Include")?.Value ?? string.Empty)
            .Select(include => include.Split(s_pathSeparators)[^1])
            .Select(fileName => Path.GetFileNameWithoutExtension(fileName))
            .ToList();
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Ags.Finance.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("Ags.Finance.slnx não encontrado acima de " + AppContext.BaseDirectory);
    }
}
