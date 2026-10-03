using System.Xml.Linq;

namespace Onyx.ArchitectureTests;

/// <summary>Reads project files from the repository so rules apply to declared references, not just used ones.</summary>
internal static class SolutionLayout
{
    private static readonly Lazy<string> RepositoryRoot = new(FindRepositoryRoot);

    public static string ProjectFile(string projectName)
    {
        var folder = projectName.EndsWith("Tests", StringComparison.Ordinal) ? "tests" : "src";
        return Path.Combine(RepositoryRoot.Value, folder, projectName, $"{projectName}.csproj");
    }

    public static IReadOnlyList<string> ProjectReferences(string projectName) =>
        Items(projectName, "ProjectReference")
            .Select(include => Path.GetFileNameWithoutExtension(include.Replace('\\', Path.DirectorySeparatorChar)))
            .ToList();

    public static IReadOnlyList<string> PackageReferences(string projectName) =>
        Items(projectName, "PackageReference").ToList();

    public static IReadOnlyList<string> FrameworkReferences(string projectName) =>
        Items(projectName, "FrameworkReference").ToList();

    private static IEnumerable<string> Items(string projectName, string itemType)
    {
        var document = XDocument.Load(ProjectFile(projectName));
        return document.Descendants(itemType)
            .Select(element => element.Attribute("Include")?.Value)
            .OfType<string>();
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Onyx.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("Could not locate Onyx.slnx above the test output directory.");
    }
}
