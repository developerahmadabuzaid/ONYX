using System.Net;
using System.Net.Http.Json;
using Onyx.API.IntegrationTests.Infrastructure;
using Onyx.API.IntegrationTests.TestOnly;
using Onyx.Contracts.Common;

namespace Onyx.API.IntegrationTests.Http;

public sealed class ErrorHandlingTests
{
    [Theory]
    [InlineData("Production")]
    [InlineData("Testing")]
    public async Task Unhandled_exceptions_return_generic_problem_details_without_leaking_internals(string environment)
    {
        await using var factory = new OnyxApiFactory(environment, OnyxApiFactory.UnreachableDatabase);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/api/v1/test-only/throw", UriKind.Relative), TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.DoesNotContain(TestOnlyController.SecretExceptionMessage, body, StringComparison.Ordinal);
        Assert.DoesNotContain("hunter2", body, StringComparison.Ordinal);
        Assert.DoesNotContain(nameof(InvalidOperationException), body, StringComparison.Ordinal);
        Assert.DoesNotContain("   at ", body, StringComparison.Ordinal);
        Assert.DoesNotContain("Onyx.API", body, StringComparison.Ordinal);

        var problem = await response.Content.ReadFromJsonAsync<ApiProblem>(TestContext.Current.CancellationToken);
        Assert.Equal(500, problem!.Status);
        Assert.Equal("An unexpected error occurred.", problem.Title);
        Assert.False(string.IsNullOrEmpty(problem.TraceId));
        Assert.False(string.IsNullOrEmpty(problem.CorrelationId));
    }

    [Fact]
    public async Task Unknown_routes_return_problem_details()
    {
        await using var factory = new OnyxApiFactory("Production", OnyxApiFactory.UnreachableDatabase);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/api/v1/does-not-exist", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Invalid_json_body_returns_validation_problem_without_internals()
    {
        await using var factory = new OnyxApiFactory("Production", OnyxApiFactory.UnreachableDatabase);
        using var client = factory.CreateClient();
        using var content = new StringContent("{ not json", System.Text.Encoding.UTF8, "application/json");

        using var response = await client.PostAsync(new Uri("/api/v1/test-only/echo", UriKind.Relative), content, TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.DoesNotContain("   at ", body, StringComparison.Ordinal);
    }
}
