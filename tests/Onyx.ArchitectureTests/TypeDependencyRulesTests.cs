using System.Reflection;
using NetArchTest.Rules;
using ArchResult = NetArchTest.Rules.TestResult;

namespace Onyx.ArchitectureTests;

/// <summary>Enforces CLAUDE.md §5 at the type level, so a transitive reference cannot be used to sneak around the project rules.</summary>
public sealed class TypeDependencyRulesTests
{
    private static readonly string[] PersistenceAndWeb =
        ["Microsoft.EntityFrameworkCore", "Npgsql", "Microsoft.AspNetCore"];

    [Fact]
    public void Domain_depends_on_nothing_outside_itself()
    {
        AssertNoDependency("Onyx.Domain",
            [.. PersistenceAndWeb, "Onyx.Contracts", "Onyx.Application", "Onyx.Infrastructure", "Onyx.API", "Onyx.Web", "Onyx.Worker"]);
    }

    [Fact]
    public void Contracts_depend_on_no_other_layer()
    {
        AssertNoDependency("Onyx.Contracts",
            [.. PersistenceAndWeb, "Onyx.Domain", "Onyx.Application", "Onyx.Infrastructure", "Onyx.API", "Onyx.Web", "Onyx.Worker"]);
    }

    [Fact]
    public void Application_does_not_depend_on_infrastructure_hosts_or_frameworks()
    {
        AssertNoDependency("Onyx.Application",
            [.. PersistenceAndWeb, "Onyx.Infrastructure", "Onyx.API", "Onyx.Web", "Onyx.Worker"]);
    }

    [Fact]
    public void Web_depends_only_on_contracts()
    {
        AssertNoDependency("Onyx.Web",
            ["Microsoft.EntityFrameworkCore", "Npgsql", "Onyx.Domain", "Onyx.Application", "Onyx.Infrastructure", "Onyx.API", "Onyx.Worker"]);
    }

    [Theory]
    [InlineData("Onyx.API")]
    [InlineData("Onyx.Worker")]
    public void Hosts_use_infrastructure_only_from_the_composition_root(string host)
    {
        // Program (top-level statements) is the composition root; everything else must go through Application abstractions.
        var result = Types.InAssembly(Load(host))
            .That().DoNotHaveName("Program")
            .And().DoNotHaveNameStartingWith("<")
            .ShouldNot().HaveDependencyOn("Onyx.Infrastructure")
            .GetResult();

        Assert.True(result.IsSuccessful, Describe(host, "Onyx.Infrastructure", result));
    }

    private static void AssertNoDependency(string assemblyName, string[] forbidden)
    {
        var result = Types.InAssembly(Load(assemblyName))
            .ShouldNot().HaveDependencyOnAny(forbidden)
            .GetResult();

        Assert.True(result.IsSuccessful, Describe(assemblyName, string.Join(", ", forbidden), result));
    }

    private static Assembly Load(string assemblyName) => Assembly.Load(assemblyName);

    private static string Describe(string assemblyName, string forbidden, ArchResult result) =>
        $"{assemblyName} has forbidden dependencies ({forbidden}) in: {string.Join(", ", result.FailingTypeNames ?? [])}";
}
