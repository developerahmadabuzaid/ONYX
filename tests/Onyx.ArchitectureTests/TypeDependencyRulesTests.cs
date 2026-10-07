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

    [Fact]
    public void Public_api_controllers_never_reach_competitor_or_intelligence_data()
    {
        // Competitor research and content intelligence are internal: only Admin controllers may use them.
        var result = Types.InAssembly(Load("Onyx.API"))
            .That().ResideInNamespace("Onyx.API.Controllers")
            .And().DoNotResideInNamespace("Onyx.API.Controllers.Admin")
            .ShouldNot().HaveDependencyOnAny("Onyx.Application.Intelligence", "Onyx.Contracts.Intelligence", "Onyx.Domain.Intelligence")
            .GetResult();

        Assert.True(result.IsSuccessful, Describe("Onyx.API (public controllers)", "intelligence", result));
    }

    [Fact]
    public void The_public_site_never_reaches_competitor_or_intelligence_data()
    {
        var result = Types.InAssembly(Load("Onyx.Web"))
            .That().ResideInNamespace("Onyx.Web.Controllers")
            .Or().ResideInNamespace("Onyx.Web.Content")
            .ShouldNot().HaveDependencyOnAny("Onyx.Contracts.Intelligence", "Onyx.Web.ApiClients.IAdminCmsApiClient")
            .GetResult();

        Assert.True(result.IsSuccessful, Describe("Onyx.Web (public site)", "intelligence / admin CMS client", result));
    }

    [Theory]
    [InlineData("Onyx.Domain")]
    [InlineData("Onyx.Contracts")]
    [InlineData("Onyx.Application")]
    [InlineData("Onyx.API")]
    [InlineData("Onyx.Web")]
    [InlineData("Onyx.Worker")]
    public void The_feed_parsing_package_is_used_only_by_infrastructure(string assemblyName) =>
        AssertNoDependency(assemblyName, ["System.ServiceModel.Syndication"]);

    [Fact]
    public void Inside_infrastructure_only_the_news_feed_reader_parses_feeds()
    {
        // Owner approval (Stage 11): System.ServiceModel.Syndication stays behind INewsFeedReader.
        var result = Types.InAssembly(Load("Onyx.Infrastructure"))
            .That().DoNotResideInNamespace("Onyx.Infrastructure.News")
            .ShouldNot().HaveDependencyOn("System.ServiceModel.Syndication")
            .GetResult();

        Assert.True(result.IsSuccessful, Describe("Onyx.Infrastructure (outside News)", "System.ServiceModel.Syndication", result));

        // The rule is not vacuous: the reader itself is seen depending on the package.
        var users = Types.InAssembly(Load("Onyx.Infrastructure")).That().HaveDependencyOn("System.ServiceModel.Syndication").GetTypes();
        Assert.Contains(users, type => type.Name == "SyndicationNewsFeedReader");
    }

    [Fact]
    public void Public_api_controllers_never_reach_news_administration()
    {
        var result = Types.InAssembly(Load("Onyx.API"))
            .That().ResideInNamespace("Onyx.API.Controllers")
            .And().DoNotResideInNamespace("Onyx.API.Controllers.Admin")
            .ShouldNot().HaveDependencyOnAny(
                "Onyx.Application.News.NewsAdministrationService", "Onyx.Application.News.NewsSourceAdministrationService",
                "Onyx.Application.News.NewsCategoryAdministrationService", "Onyx.Application.News.NewsIngestionService")
            .GetResult();

        Assert.True(result.IsSuccessful, Describe("Onyx.API (public controllers)", "news administration", result));
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
