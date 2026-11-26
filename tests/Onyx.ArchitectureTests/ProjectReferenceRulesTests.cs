namespace Onyx.ArchitectureTests;

/// <summary>Enforces the dependency table in CLAUDE.md §5 at the project-file level.</summary>
public sealed class ProjectReferenceRulesTests
{
    public static TheoryData<string, string[]> AllowedProjectReferences => new()
    {
        { "Onyx.Domain", [] },
        { "Onyx.Contracts", [] },
        { "Onyx.Application", ["Onyx.Domain", "Onyx.Contracts"] },
        { "Onyx.Infrastructure", ["Onyx.Application", "Onyx.Domain"] },
        { "Onyx.API", ["Onyx.Application", "Onyx.Contracts", "Onyx.Infrastructure"] },
        { "Onyx.Worker", ["Onyx.Application", "Onyx.Infrastructure"] },
        { "Onyx.Web", ["Onyx.Contracts"] },

        // The single deployable application: it only starts the website and the internal API side by side.
        { "Onyx.Host", ["Onyx.API", "Onyx.Web"] },
    };

    [Theory]
    [InlineData("Onyx.Domain")]
    [InlineData("Onyx.Contracts")]
    [InlineData("Onyx.Application")]
    [InlineData("Onyx.Infrastructure")]
    [InlineData("Onyx.API")]
    [InlineData("Onyx.Web")]
    [InlineData("Onyx.Worker")]
    public void Nothing_references_the_host(string project) =>
        Assert.DoesNotContain("Onyx.Host", SolutionLayout.ProjectReferences(project));

    [Theory]
    [MemberData(nameof(AllowedProjectReferences))]
    public void Project_references_only_allowed_projects(string project, string[] allowed)
    {
        var forbidden = SolutionLayout.ProjectReferences(project).Except(allowed).ToList();

        Assert.True(forbidden.Count == 0, $"{project} references forbidden project(s): {string.Join(", ", forbidden)}");
    }

    [Theory]
    [InlineData("Onyx.Domain")]
    [InlineData("Onyx.Contracts")]
    public void Core_projects_have_no_package_or_framework_references(string project)
    {
        Assert.Empty(SolutionLayout.PackageReferences(project));
        Assert.Empty(SolutionLayout.FrameworkReferences(project));
    }

    [Fact]
    public void Application_does_not_reference_aspnetcore_framework()
    {
        Assert.DoesNotContain("Microsoft.AspNetCore.App", SolutionLayout.FrameworkReferences("Onyx.Application"));
    }

    [Theory]
    [InlineData("Onyx.Application")]
    [InlineData("Onyx.Web")]
    [InlineData("Onyx.Domain")]
    [InlineData("Onyx.Contracts")]
    public void Project_has_no_persistence_or_provider_packages(string project)
    {
        string[] forbiddenPrefixes = ["Microsoft.EntityFrameworkCore", "Npgsql", "Microsoft.AspNetCore.Identity", "Google.", "MailKit"];

        var offending = SolutionLayout.PackageReferences(project)
            .Where(package => forbiddenPrefixes.Any(prefix => package.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        Assert.True(offending.Count == 0, $"{project} references forbidden package(s): {string.Join(", ", offending)}");
    }
}
