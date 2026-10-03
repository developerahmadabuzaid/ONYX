using System.Text.RegularExpressions;
using Onyx.Contracts.Authorization;

namespace Onyx.Application.Tests.Authorization;

public sealed partial class PermissionCatalogueTests
{
    [Fact]
    public void Catalogue_is_discovered_and_non_empty()
    {
        Assert.NotEmpty(Permissions.All);
        Assert.Contains(Permissions.Users.Manage, Permissions.All);
        Assert.Contains(Permissions.Leads.ViewAll, Permissions.All);
        Assert.Contains(Permissions.SystemJobs.View, Permissions.All);
    }

    [Fact]
    public void Permission_values_are_unique()
    {
        var duplicates = Permissions.All.GroupBy(value => value, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToList();

        Assert.True(duplicates.Count == 0, $"Duplicate permission values: {string.Join(", ", duplicates)}");
    }

    [Fact]
    public void Permission_values_follow_the_dotted_pascal_case_format()
    {
        var invalid = Permissions.All.Where(value => !DottedPascalCase().IsMatch(value)).ToList();

        Assert.True(invalid.Count == 0, $"Invalid permission values: {string.Join(", ", invalid)}");
    }

    [GeneratedRegex(@"^[A-Z][A-Za-z]*(\.[A-Z][A-Za-z]*)+$", RegexOptions.CultureInvariant)]
    private static partial Regex DottedPascalCase();
}
